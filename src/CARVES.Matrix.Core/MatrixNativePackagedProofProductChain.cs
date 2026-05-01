namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool TryRunNativePackagedProductChain(
        string packagedRoot,
        string workRepoRoot,
        MatrixNativeInstalledCommands commands,
        List<MatrixNativeProofStep> steps,
        out MatrixNativeProjectProofChainResult chain,
        out MatrixNativePackagedFailure failure)
    {
        chain = null!;
        if (!TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "guard_check", "carves-guard check --json", commands.CarvesGuard, ["--repo-root", workRepoRoot, "check", "--json"], "guard-check.json", "Guard check failed in the native packaged proof chain.", out var guardCheckJson, out failure))
        {
            return false;
        }

        var guardCheckRoot = ParseNativeFullReleaseProjectJson(guardCheckJson, "guard check");
        if (!string.Equals(GetString(guardCheckRoot, "decision"), "allow", StringComparison.Ordinal))
        {
            failure = new MatrixNativePackagedFailure("guard_check", ["native_packaged_guard_check_not_allow"], "Expected Guard check to allow the native packaged proof patch.");
            return false;
        }

        var guardRunId = GetString(guardCheckRoot, "run_id");
        if (string.IsNullOrWhiteSpace(guardRunId))
        {
            failure = new MatrixNativePackagedFailure("guard_check", ["native_packaged_guard_run_id_missing"], "Guard check did not emit run_id.");
            return false;
        }

        CopyNativeArtifact(workRepoRoot, ".ai/runtime/guard/decisions.jsonl", packagedRoot, "decisions.jsonl");
        WriteNativeFullReleaseGuardWorkflowFixture(workRepoRoot);

        if (!TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "handoff_draft", "carves-handoff draft --json", commands.CarvesHandoff, ["--repo-root", workRepoRoot, "draft", "--json"], "handoff-draft.json", "Handoff draft failed in the native packaged proof chain.", out _, out failure))
        {
            return false;
        }

        var handoffPacketPath = ResolveNativeRelativePath(workRepoRoot, ".ai/handoff/handoff.json");
        WriteNativeFullReleaseReadyHandoffPacket(handoffPacketPath, guardRunId);
        CopyNativeArtifact(workRepoRoot, ".ai/handoff/handoff.json", packagedRoot, "handoff.json");

        if (!TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "handoff_inspect", "carves-handoff inspect --json", commands.CarvesHandoff, ["--repo-root", workRepoRoot, "inspect", "--json"], "handoff-inspect.json", "Handoff inspect failed in the native packaged proof chain.", out var handoffInspectJson, out failure))
        {
            return false;
        }

        if (!string.Equals(GetString(ParseNativeFullReleaseProjectJson(handoffInspectJson, "handoff inspect"), "readiness", "decision"), "ready", StringComparison.Ordinal))
        {
            failure = new MatrixNativePackagedFailure("handoff_inspect", ["native_packaged_handoff_not_ready"], "Expected Handoff inspect readiness to be ready.");
            return false;
        }

        return TryRunNativePackagedAuditAndShield(packagedRoot, workRepoRoot, commands, steps, guardRunId, handoffInspectJson, out chain, out failure);
    }

    private static bool TryRunNativePackagedAuditAndShield(
        string packagedRoot,
        string workRepoRoot,
        MatrixNativeInstalledCommands commands,
        List<MatrixNativeProofStep> steps,
        string guardRunId,
        string handoffInspectJson,
        out MatrixNativeProjectProofChainResult chain,
        out MatrixNativePackagedFailure failure)
    {
        chain = null!;
        if (!TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "audit_summary", "carves-audit summary --json", commands.CarvesAudit, ["summary", "--json"], "audit-summary.json", "Audit summary failed in the native packaged proof chain.", out var auditSummaryJson, out failure)
            || !TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "audit_timeline", "carves-audit timeline --json", commands.CarvesAudit, ["timeline", "--json"], "audit-timeline.json", "Audit timeline failed in the native packaged proof chain.", out _, out failure)
            || !TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "audit_explain", "carves-audit explain <guard-run> --json", commands.CarvesAudit, ["explain", guardRunId, "--json"], "audit-explain.json", "Audit explain failed in the native packaged proof chain.", out _, out failure)
            || !TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "audit_evidence", "carves-audit evidence --json --output .carves/shield-evidence.json", commands.CarvesAudit, ["evidence", "--json", "--output", ".carves/shield-evidence.json"], "audit-evidence.json", "Audit evidence failed in the native packaged proof chain.", out var auditEvidenceJson, out failure))
        {
            return false;
        }

        if ((GetInt(ParseNativeFullReleaseProjectJson(auditSummaryJson, "audit summary"), "event_count") ?? 0) < 2)
        {
            failure = new MatrixNativePackagedFailure("audit_summary", ["native_packaged_audit_summary_incomplete"], "Expected Audit summary to discover Guard and Handoff evidence.");
            return false;
        }

        if (!string.Equals(GetString(ParseNativeFullReleaseProjectJson(auditEvidenceJson, "audit evidence"), "schema_version"), "shield-evidence.v0", StringComparison.Ordinal))
        {
            failure = new MatrixNativePackagedFailure("audit_evidence", ["native_packaged_audit_evidence_schema_mismatch"], "Audit evidence emitted an unexpected schema.");
            return false;
        }

        CopyNativeArtifact(workRepoRoot, ".carves/shield-evidence.json", packagedRoot, "shield-evidence.json");
        var badgeSvgPath = ResolveNativeRelativePath(packagedRoot, "shield-badge.svg");
        if (!TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "shield_evaluate", "carves-shield evaluate .carves/shield-evidence.json --json --output combined", commands.CarvesShield, ["--repo-root", workRepoRoot, "evaluate", ".carves/shield-evidence.json", "--json", "--output", "combined"], "shield-evaluate.json", "Shield evaluate failed in the native packaged proof chain.", out var shieldEvaluateJson, out failure)
            || !TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "shield_badge", "carves-shield badge .carves/shield-evidence.json --json --output <badge>", commands.CarvesShield, ["--repo-root", workRepoRoot, "badge", ".carves/shield-evidence.json", "--json", "--output", badgeSvgPath], "shield-badge.json", "Shield badge failed in the native packaged proof chain.", out var shieldBadgeJson, out failure))
        {
            return false;
        }

        if (!string.Equals(GetString(ParseNativeFullReleaseProjectJson(shieldEvaluateJson, "shield evaluate"), "status"), "ok", StringComparison.Ordinal)
            || !File.Exists(badgeSvgPath))
        {
            failure = new MatrixNativePackagedFailure("shield_evaluate", ["native_packaged_shield_not_ok"], "Shield evaluate or badge failed to produce expected outputs.");
            return false;
        }

        chain = new MatrixNativeProjectProofChainResult(
            guardRunId,
            File.ReadAllText(ResolveNativeRelativePath(packagedRoot, "guard-check.json")),
            handoffInspectJson,
            auditSummaryJson,
            auditEvidenceJson,
            shieldEvaluateJson,
            shieldBadgeJson);
        return true;
    }
}
