using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentProblemTriageLedgerSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-triage-ledger.v1";

    public string SurfaceId { get; init; } = "runtime-agent-problem-triage-ledger";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string GuideDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentGuideDocumentPath;

    public string ProblemIntakeGuideDocumentPath { get; init; } = "docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md";

    public string TriageLedgerGuideDocumentPath { get; init; } = "docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md";

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot triage";

    public string JsonCommandEntry { get; init; } = "carves pilot triage --json";

    public string AliasCommandEntry { get; init; } = "carves pilot problem-triage --json";

    public string FrictionLedgerCommandEntry { get; init; } = "carves pilot friction-ledger --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-problem-triage-ledger";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-problem-triage-ledger";

    public string ProblemStorageRoot { get; init; } = ".ai/runtime/pilot-problems";

    public string EvidenceLedgerRoot { get; init; } = ".ai/runtime/pilot-evidence";

    public bool TriageLedgerReady { get; init; }

    public int RecordedProblemCount { get; init; }

    public int BlockingProblemCount { get; init; }

    public int RepoCount { get; init; }

    public int DistinctProblemKindCount { get; init; }

    public int ReviewQueueCount { get; init; }

    public IReadOnlyList<RuntimeAgentProblemKindLedgerSurface> ProblemKindLedger { get; init; } = [];

    public IReadOnlyList<RuntimeAgentProblemSeverityLedgerSurface> SeverityLedger { get; init; } = [];

    public IReadOnlyList<RuntimeAgentProblemStageLedgerSurface> StageLedger { get; init; } = [];

    public IReadOnlyList<RuntimeAgentProblemTriageQueueItemSurface> ReviewQueue { get; init; } = [];

    public IReadOnlyList<string> TriageRules { get; init; } = [];

    public IReadOnlyList<string> RecommendedOperatorActions { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentProblemKindLedgerSurface
{
    public string ProblemKind { get; init; } = string.Empty;

    public int Count { get; init; }

    public int BlockingCount { get; init; }

    public string RecommendedTriageLane { get; init; } = string.Empty;

    public DateTimeOffset? LatestRecordedAtUtc { get; init; }
}

public sealed class RuntimeAgentProblemSeverityLedgerSurface
{
    public string Severity { get; init; } = string.Empty;

    public int Count { get; init; }
}

public sealed class RuntimeAgentProblemStageLedgerSurface
{
    public string CurrentStageId { get; init; } = string.Empty;

    public int Count { get; init; }

    public DateTimeOffset? LatestRecordedAtUtc { get; init; }
}

public sealed class RuntimeAgentProblemTriageQueueItemSurface
{
    public string ProblemId { get; init; } = string.Empty;

    public string EvidenceId { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string CurrentStageId { get; init; } = string.Empty;

    public string ProblemKind { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string BlockedCommand { get; init; } = string.Empty;

    public string NextGovernedCommand { get; init; } = string.Empty;

    public string RecommendedTriageLane { get; init; } = string.Empty;

    public string RecommendedFollowUp { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; }

    public static RuntimeAgentProblemTriageQueueItemSurface FromRecord(PilotProblemIntakeRecord record)
    {
        return new RuntimeAgentProblemTriageQueueItemSurface
        {
            ProblemId = record.ProblemId,
            EvidenceId = record.EvidenceId,
            RepoId = record.RepoId ?? string.Empty,
            TaskId = record.TaskId ?? string.Empty,
            CardId = record.CardId ?? string.Empty,
            CurrentStageId = NormalizeStage(record.CurrentStageId),
            ProblemKind = record.ProblemKind,
            Severity = NormalizeSeverity(record.Severity),
            Summary = record.Summary,
            BlockedCommand = record.BlockedCommand,
            NextGovernedCommand = record.NextGovernedCommand,
            RecommendedTriageLane = ResolveTriageLane(record.ProblemKind),
            RecommendedFollowUp = record.RecommendedFollowUp,
            RecordedAtUtc = record.RecordedAtUtc,
        };
    }

    internal static string NormalizeStage(string value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();

    internal static string NormalizeSeverity(string value) => string.IsNullOrWhiteSpace(value) ? "blocking" : value.Trim();

    private static string ResolveTriageLane(string problemKind)
    {
        return problemKind.Trim() switch
        {
            "command_failed" => "command_contract_or_runtime_surface_review",
            "blocked_posture" => "command_contract_or_runtime_surface_review",
            "protected_truth_root_requested" => "protected_truth_root_policy_review",
            "missing_acceptance_contract" => "acceptance_contract_ingress_review",
            "workspace_scope_ambiguous" => "managed_workspace_or_path_lease_review",
            "next_command_ambiguous" => "pilot_next_or_stage_status_review",
            "runtime_binding_ambiguous" => "dist_binding_or_attach_review",
            "agent_policy_conflict" => "agent_bootstrap_or_constraint_ladder_review",
            "other" => "operator_triage_required",
            _ => "operator_triage_required",
        };
    }
}
