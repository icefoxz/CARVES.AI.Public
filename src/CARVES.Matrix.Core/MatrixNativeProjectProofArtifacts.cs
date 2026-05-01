using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void ResetNativeFullReleaseProjectArtifacts(string artifactRoot)
    {
        Directory.CreateDirectory(ResolveNativeRelativePath(artifactRoot, "project"));
        foreach (var relativePath in new[]
        {
            "project/guard-init.json",
            "project/guard-check.json",
            "project/decisions.jsonl",
            "project/handoff-draft.json",
            "project/handoff.json",
            "project/handoff-inspect.json",
            "project/audit-summary.json",
            "project/audit-timeline.json",
            "project/audit-explain.json",
            "project/audit-evidence.json",
            "project/shield-evidence.json",
            "project/shield-evaluate.json",
            "project/shield-badge.json",
            "project/shield-badge.svg",
            "project/matrix-summary.json",
            "project-matrix-output.json",
        })
        {
            var path = ResolveNativeRelativePath(artifactRoot, relativePath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string WriteNativeFullReleaseProjectArtifacts(
        string artifactRoot,
        MatrixNativeProjectProofChainResult chain)
    {
        var guardCheckRoot = ParseNativeFullReleaseProjectJson(chain.GuardCheckJson, "guard check");
        var handoffInspectRoot = ParseNativeFullReleaseProjectJson(chain.HandoffInspectJson, "handoff inspect");
        var auditSummaryRoot = ParseNativeFullReleaseProjectJson(chain.AuditSummaryJson, "audit summary");
        var auditEvidenceRoot = ParseNativeFullReleaseProjectJson(chain.AuditEvidenceJson, "audit evidence");
        var shieldEvaluateRoot = ParseNativeFullReleaseProjectJson(chain.ShieldEvaluateJson, "shield evaluate");
        var shieldBadgeRoot = ParseNativeFullReleaseProjectJson(chain.ShieldBadgeJson, "shield badge");

        var summary = BuildNativeFullReleaseProjectSummary(
            chain.GuardRunId,
            guardCheckRoot,
            handoffInspectRoot,
            auditSummaryRoot,
            auditEvidenceRoot,
            shieldEvaluateRoot,
            shieldBadgeRoot);
        var summaryJson = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(ResolveNativeRelativePath(artifactRoot, "project/matrix-summary.json"), summaryJson);
        return summaryJson;
    }

    private static object BuildNativeFullReleaseProjectSummary(
        string guardRunId,
        JsonElement guardCheckRoot,
        JsonElement handoffInspectRoot,
        JsonElement auditSummaryRoot,
        JsonElement auditEvidenceRoot,
        JsonElement shieldEvaluateRoot,
        JsonElement shieldBadgeRoot,
        string toolMode = "project")
    {
        return new
        {
            smoke = "matrix_e2e",
            producer = "native_full_release_project",
            tool_mode = toolMode,
            target_repository = "<redacted-target-repository>",
            artifact_root = ToPublicArtifactRootMarker(),
            guard_run_id = guardRunId,
            artifacts = BuildNativeFullReleaseProjectArtifactIndex(),
            matrix = new
            {
                proof_role = "composition_orchestrator",
                scoring_owner = "shield",
                alters_shield_score = false,
                consumed_shield_evidence_artifact = "shield-evidence.json",
                shield_evaluation_artifact = "shield-evaluate.json",
                shield_badge_json_artifact = "shield-badge.json",
                shield_badge_svg_artifact = "shield-badge.svg",
                trust_chain_hardening = BuildNativeFullReleaseTrustChainEvidence(),
            },
            guard = new
            {
                decision = GetString(guardCheckRoot, "decision"),
                changed_file_count = GetInt(guardCheckRoot, "patch_stats", "changed_file_count"),
                requires_runtime_task_truth = GetBool(guardCheckRoot, "requires_runtime_task_truth"),
            },
            handoff = new
            {
                packet_path = ".ai/handoff/handoff.json",
                readiness = GetString(handoffInspectRoot, "readiness", "decision"),
                linked_guard_run = $"guard-run:{guardRunId}",
            },
            audit = new
            {
                event_count = GetInt(auditSummaryRoot, "event_count"),
                confidence_posture = GetString(auditSummaryRoot, "confidence_posture"),
                evidence_schema = GetString(auditEvidenceRoot, "schema_version"),
            },
            shield = new
            {
                status = GetString(shieldEvaluateRoot, "status"),
                standard_label = GetString(shieldEvaluateRoot, "standard", "label"),
                lite_score = GetInt(shieldEvaluateRoot, "lite", "score"),
                lite_band = GetString(shieldEvaluateRoot, "lite", "band"),
                consumed_evidence_sha256 = GetString(shieldEvaluateRoot, "consumed_evidence_sha256"),
                badge_message = GetString(shieldBadgeRoot, "badge", "message"),
            },
            privacy = new
            {
                source_included = GetBool(auditEvidenceRoot, "privacy", "source_included"),
                raw_diff_included = GetBool(auditEvidenceRoot, "privacy", "raw_diff_included"),
                prompt_included = GetBool(auditEvidenceRoot, "privacy", "prompt_included"),
                secrets_included = GetBool(auditEvidenceRoot, "privacy", "secrets_included"),
                upload_intent = GetString(auditEvidenceRoot, "privacy", "upload_intent"),
                hosted_api_required = false,
                provider_secrets_required = false,
                source_upload_required = false,
                raw_diff_upload_required = false,
                prompt_upload_required = false,
                model_response_upload_required = false,
            },
            public_claims = new
            {
                certification = false,
                public_leaderboard = false,
                hosted_verification = false,
                os_sandbox_claim = false,
            },
        };
    }

    private static object BuildNativeFullReleaseProjectArtifactIndex()
    {
        return new
        {
            guard_init = "guard-init.json",
            guard_check = "guard-check.json",
            guard_decisions = "decisions.jsonl",
            handoff_packet = "handoff.json",
            handoff_inspect = "handoff-inspect.json",
            audit_summary = "audit-summary.json",
            audit_timeline = "audit-timeline.json",
            audit_explain = "audit-explain.json",
            shield_evidence = "shield-evidence.json",
            shield_evaluate = "shield-evaluate.json",
            shield_badge_json = "shield-badge.json",
            shield_badge_svg = "shield-badge.svg",
        };
    }
}
