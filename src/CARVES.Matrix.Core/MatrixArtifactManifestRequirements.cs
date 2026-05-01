namespace Carves.Matrix.Core;

public static partial class MatrixArtifactManifestWriter
{
    private static readonly MatrixArtifactManifestRequirement[] DefaultRequiredArtifactRequirements =
    [
        new("guard_decision", "project/decisions.jsonl", "guard-decision-jsonl", "carves-guard"),
        new("handoff_packet", "project/handoff.json", "carves-continuity-handoff.v1", "carves-handoff"),
        new("audit_evidence", "project/shield-evidence.json", "shield-evidence.v0", "carves-audit"),
        new("shield_evaluation", "project/shield-evaluate.json", "shield-evaluate.v0", "carves-shield"),
        new("shield_badge_json", "project/shield-badge.json", "shield-badge.v0", "carves-shield"),
        new("shield_badge_svg", "project/shield-badge.svg", "shield-badge-svg.v0", "carves-shield"),
        new("matrix_summary", "project/matrix-summary.json", "matrix-summary.v0", "carves-matrix"),
    ];

    private static readonly MatrixArtifactManifestRequirement[] DefaultOptionalArtifactRequirements =
    [
        new("project_matrix_output", "project-matrix-output.json", "matrix-script-output.v0", "carves-matrix"),
        new("packaged_matrix_output", "packaged-matrix-output.json", "matrix-script-output.v0", "carves-matrix"),
        new("packaged_matrix_summary", "packaged/matrix-packaged-summary.json", "matrix-packaged-summary.v0", "carves-matrix"),
    ];

    private static readonly MatrixArtifactManifestRequirement[] TrialArtifactRequirements =
    [
        new("trial_task_contract", "trial/task-contract.json", "matrix-agent-task.v0", "carves-trial"),
        new("trial_agent_report", "trial/agent-report.json", "agent-report.v0", "agent"),
        new("trial_diff_scope_summary", "trial/diff-scope-summary.json", "diff-scope-summary.v0", "carves-trial-collector"),
        new("trial_test_evidence", "trial/test-evidence.json", "test-evidence.v0", "carves-trial-collector"),
        new("trial_result", "trial/carves-agent-trial-result.json", "carves-agent-trial-result.v0", "carves-trial-collector"),
    ];

    public static IReadOnlyList<MatrixArtifactManifestRequirement> TrialArtifacts => TrialArtifactRequirements;

    private static MatrixArtifactManifestEntryInput ToInput(MatrixArtifactManifestRequirement requirement, bool required)
    {
        return new MatrixArtifactManifestEntryInput(
            requirement.ArtifactKind,
            requirement.Path,
            requirement.SchemaVersion,
            requirement.Producer,
            required);
    }
}
