using Carves.Audit.Core;
using Carves.Guard.Core;
using Carves.Handoff.Core;
using Carves.Shield.Core;
using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool TryRunNativeFullReleaseProjectChain(
        string artifactRoot,
        string outputPath,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        out MatrixNativeProjectProofChainResult chain,
        out MatrixNativeProjectProofResult failure)
    {
        chain = null!;
        failure = null!;

        if (!TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "guard_check", "carves-guard check --json", "project/guard-check.json", () => GuardCliRunner.Run(workRepoRoot, ["check", "--json"], null, "carves-guard", GuardRuntimeTransportPreference.Cold), "Guard check failed in the native project proof chain.", out var guardCheckJson, out failure, [0, 1]))
        {
            return false;
        }

        var guardCheckRoot = ParseNativeFullReleaseProjectJson(guardCheckJson, "guard check");
        if (!string.Equals(GetString(guardCheckRoot, "decision"), "allow", StringComparison.Ordinal))
        {
            failure = WriteNativeFullReleaseProjectFailure(artifactRoot, outputPath, workRepoRoot, steps, "guard_check", ["native_project_guard_check_not_allow"], "Expected Guard check to allow the native project proof patch.");
            return false;
        }

        var guardRunId = GetString(guardCheckRoot, "run_id");
        if (string.IsNullOrWhiteSpace(guardRunId))
        {
            failure = WriteNativeFullReleaseProjectFailure(artifactRoot, outputPath, workRepoRoot, steps, "guard_check", ["native_project_guard_run_id_missing"], "Guard check did not emit run_id.");
            return false;
        }

        CopyNativeArtifact(workRepoRoot, ".ai/runtime/guard/decisions.jsonl", artifactRoot, "project/decisions.jsonl");
        WriteNativeFullReleaseGuardWorkflowFixture(workRepoRoot);

        if (!TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "handoff_draft", "carves-handoff draft --json", "project/handoff-draft.json", () => HandoffCliRunner.Run(workRepoRoot, ["draft", "--json"]), "Handoff draft failed in the native project proof chain.", out _, out failure))
        {
            return false;
        }

        var handoffPacketPath = ResolveNativeRelativePath(workRepoRoot, ".ai/handoff/handoff.json");
        WriteNativeFullReleaseReadyHandoffPacket(handoffPacketPath, guardRunId);
        CopyNativeArtifact(workRepoRoot, ".ai/handoff/handoff.json", artifactRoot, "project/handoff.json");

        if (!TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "handoff_inspect", "carves-handoff inspect --json", "project/handoff-inspect.json", () => HandoffCliRunner.Run(workRepoRoot, ["inspect", "--json"]), "Handoff inspect failed in the native project proof chain.", out var handoffInspectJson, out failure))
        {
            return false;
        }

        var handoffInspectRoot = ParseNativeFullReleaseProjectJson(handoffInspectJson, "handoff inspect");
        if (!string.Equals(GetString(handoffInspectRoot, "readiness", "decision"), "ready", StringComparison.Ordinal))
        {
            failure = WriteNativeFullReleaseProjectFailure(artifactRoot, outputPath, workRepoRoot, steps, "handoff_inspect", ["native_project_handoff_not_ready"], "Expected Handoff inspect readiness to be ready.");
            return false;
        }

        if (!TryRunNativeFullReleaseAuditAndShield(artifactRoot, outputPath, workRepoRoot, steps, guardRunId, handoffInspectJson, out chain, out failure))
        {
            return false;
        }

        return true;
    }

    private static bool TryRunNativeFullReleaseAuditAndShield(
        string artifactRoot,
        string outputPath,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        string guardRunId,
        string handoffInspectJson,
        out MatrixNativeProjectProofChainResult chain,
        out MatrixNativeProjectProofResult failure)
    {
        chain = null!;
        if (!TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "audit_summary", "carves-audit summary --json", "project/audit-summary.json", () => AuditCliRunner.Run(workRepoRoot, ["summary", "--json"]), "Audit summary failed in the native project proof chain.", out var auditSummaryJson, out failure)
            || !TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "audit_timeline", "carves-audit timeline --json", "project/audit-timeline.json", () => AuditCliRunner.Run(workRepoRoot, ["timeline", "--json"]), "Audit timeline failed in the native project proof chain.", out _, out failure)
            || !TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "audit_explain", "carves-audit explain <guard-run> --json", "project/audit-explain.json", () => AuditCliRunner.Run(workRepoRoot, ["explain", guardRunId, "--json"]), "Audit explain failed in the native project proof chain.", out _, out failure)
            || !TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "audit_evidence", "carves-audit evidence --json --output .carves/shield-evidence.json", "project/audit-evidence.json", () => AuditCliRunner.Run(workRepoRoot, ["evidence", "--json", "--output", ".carves/shield-evidence.json"]), "Audit evidence failed in the native project proof chain.", out var auditEvidenceJson, out failure))
        {
            return false;
        }

        if ((GetInt(ParseNativeFullReleaseProjectJson(auditSummaryJson, "audit summary"), "event_count") ?? 0) < 2)
        {
            failure = WriteNativeFullReleaseProjectFailure(artifactRoot, outputPath, workRepoRoot, steps, "audit_summary", ["native_project_audit_summary_incomplete"], "Expected Audit summary to discover Guard and Handoff evidence.");
            return false;
        }

        if (!string.Equals(GetString(ParseNativeFullReleaseProjectJson(auditEvidenceJson, "audit evidence"), "schema_version"), "shield-evidence.v0", StringComparison.Ordinal))
        {
            failure = WriteNativeFullReleaseProjectFailure(artifactRoot, outputPath, workRepoRoot, steps, "audit_evidence", ["native_project_audit_evidence_schema_mismatch"], "Audit evidence emitted an unexpected schema.");
            return false;
        }

        CopyNativeArtifact(workRepoRoot, ".carves/shield-evidence.json", artifactRoot, "project/shield-evidence.json");
        var badgeSvgPath = ResolveNativeRelativePath(artifactRoot, "project/shield-badge.svg");
        if (!TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "shield_evaluate", "carves-shield evaluate .carves/shield-evidence.json --json --output combined", "project/shield-evaluate.json", () => ShieldCliRunner.Run(workRepoRoot, ["evaluate", ".carves/shield-evidence.json", "--json", "--output", "combined"], "carves-shield"), "Shield evaluate failed in the native project proof chain.", out var shieldEvaluateJson, out failure)
            || !TryRunProjectCliJson(artifactRoot, outputPath, workRepoRoot, steps, "shield_badge", "carves-shield badge .carves/shield-evidence.json --json --output <badge>", "project/shield-badge.json", () => ShieldCliRunner.Run(workRepoRoot, ["badge", ".carves/shield-evidence.json", "--json", "--output", badgeSvgPath], "carves-shield"), "Shield badge failed in the native project proof chain.", out var shieldBadgeJson, out failure))
        {
            return false;
        }

        if (!string.Equals(GetString(ParseNativeFullReleaseProjectJson(shieldEvaluateJson, "shield evaluate"), "status"), "ok", StringComparison.Ordinal))
        {
            failure = WriteNativeFullReleaseProjectFailure(artifactRoot, outputPath, workRepoRoot, steps, "shield_evaluate", ["native_project_shield_evaluate_not_ok"], "Shield evaluate did not return ok.");
            return false;
        }

        if (!string.Equals(GetString(ParseNativeFullReleaseProjectJson(shieldBadgeJson, "shield badge"), "status"), "ok", StringComparison.Ordinal)
            || !File.Exists(badgeSvgPath))
        {
            failure = WriteNativeFullReleaseProjectFailure(artifactRoot, outputPath, workRepoRoot, steps, "shield_badge", ["native_project_shield_badge_not_ok"], "Shield badge did not produce an ok JSON result and SVG badge.");
            return false;
        }

        chain = new MatrixNativeProjectProofChainResult(guardRunId, File.ReadAllText(ResolveNativeRelativePath(artifactRoot, "project/guard-check.json")), handoffInspectJson, auditSummaryJson, auditEvidenceJson, shieldEvaluateJson, shieldBadgeJson);
        return true;
    }
}
