using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentProblemIntakeSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-intake.v1";

    public string SurfaceId { get; init; } = "runtime-agent-problem-intake";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string GuideDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentGuideDocumentPath;

    public string ProblemIntakeGuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot problem-intake";

    public string JsonCommandEntry { get; init; } = "carves pilot problem-intake --json";

    public string ReportProblemCommandEntry { get; init; } = "carves pilot report-problem <json-path>";

    public string ReportProblemJsonCommandEntry { get; init; } = "carves pilot report-problem <json-path> --json";

    public string ListProblemsCommandEntry { get; init; } = "carves pilot list-problems";

    public string InspectProblemCommandEntry { get; init; } = "carves pilot inspect-problem <problem-id>";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-problem-intake";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-problem-intake";

    public bool ProblemIntakeReady { get; init; }

    public bool PilotStartBundleReady { get; init; }

    public bool ReadyToRunNextCommand { get; init; }

    public string CurrentStageId { get; init; } = string.Empty;

    public int CurrentStageOrder { get; init; }

    public string CurrentStageStatus { get; init; } = string.Empty;

    public string NextGovernedCommand { get; init; } = string.Empty;

    public string ProblemStorageRoot { get; init; } = ".ai/runtime/pilot-problems";

    public string EvidenceLedgerRoot { get; init; } = ".ai/runtime/pilot-evidence";

    public IReadOnlyList<string> AcceptedProblemKinds { get; init; } = [];

    public IReadOnlyList<string> RequiredPayloadFields { get; init; } = [];

    public IReadOnlyList<string> OptionalPayloadFields { get; init; } = [];

    public IReadOnlyList<string> PayloadRules { get; init; } = [];

    public IReadOnlyList<string> StopAndReportTriggers { get; init; } = [];

    public IReadOnlyList<string> CommandExamples { get; init; } = [];

    public int RecentProblemCount { get; init; }

    public IReadOnlyList<RuntimeAgentProblemIntakeRecentProblemSurface> RecentProblems { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentProblemIntakeRecentProblemSurface
{
    public string ProblemId { get; init; } = string.Empty;

    public string EvidenceId { get; init; } = string.Empty;

    public string ProblemKind { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string CurrentStageId { get; init; } = string.Empty;

    public string BlockedCommand { get; init; } = string.Empty;

    public DateTimeOffset RecordedAtUtc { get; init; }

    public static RuntimeAgentProblemIntakeRecentProblemSurface FromRecord(PilotProblemIntakeRecord record)
    {
        return new RuntimeAgentProblemIntakeRecentProblemSurface
        {
            ProblemId = record.ProblemId,
            EvidenceId = record.EvidenceId,
            ProblemKind = record.ProblemKind,
            Severity = record.Severity,
            Summary = record.Summary,
            CurrentStageId = record.CurrentStageId,
            BlockedCommand = record.BlockedCommand,
            RecordedAtUtc = record.RecordedAtUtc,
        };
    }
}
