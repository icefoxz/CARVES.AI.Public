using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentProblemFollowUpCandidatesSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-problem-follow-up-candidates.v1";

    public string SurfaceId { get; init; } = "runtime-agent-problem-follow-up-candidates";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentDocumentPath;

    public string PreviousPhaseDocumentPath { get; init; } = RuntimeProductClosureMetadata.PreviousDocumentPath;

    public string GuideDocumentPath { get; init; } = RuntimeProductClosureMetadata.CurrentGuideDocumentPath;

    public string ProblemIntakeGuideDocumentPath { get; init; } = "docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md";

    public string TriageLedgerGuideDocumentPath { get; init; } = "docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md";

    public string FollowUpCandidatesGuideDocumentPath { get; init; } = "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md";

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot follow-up";

    public string JsonCommandEntry { get; init; } = "carves pilot follow-up --json";

    public string AliasCommandEntry { get; init; } = "carves pilot problem-follow-up --json";

    public string TriageAliasCommandEntry { get; init; } = "carves pilot triage-follow-up --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-agent-problem-follow-up-candidates";

    public string ApiCommandEntry { get; init; } = "carves api runtime-agent-problem-follow-up-candidates";

    public bool FollowUpCandidatesReady { get; init; }

    public int RecordedProblemCount { get; init; }

    public int CandidateCount { get; init; }

    public int GovernedCandidateCount { get; init; }

    public int WatchlistCandidateCount { get; init; }

    public int RepeatedPatternCount { get; init; }

    public int BlockingCandidateCount { get; init; }

    public IReadOnlyList<RuntimeAgentProblemFollowUpCandidateSurface> Candidates { get; init; } = [];

    public IReadOnlyList<string> CandidateRules { get; init; } = [];

    public IReadOnlyList<string> OperatorReviewQuestions { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentProblemFollowUpCandidateSurface
{
    public string CandidateId { get; init; } = string.Empty;

    public string CandidateStatus { get; init; } = string.Empty;

    public string ProblemKind { get; init; } = string.Empty;

    public string RecommendedTriageLane { get; init; } = string.Empty;

    public int ProblemCount { get; init; }

    public int BlockingCount { get; init; }

    public int RepoCount { get; init; }

    public int StageCount { get; init; }

    public DateTimeOffset? LatestRecordedAtUtc { get; init; }

    public string SuggestedTitle { get; init; } = string.Empty;

    public string SuggestedIntent { get; init; } = string.Empty;

    public string RequiredOperatorDecision { get; init; } = string.Empty;

    public IReadOnlyList<string> RelatedProblemIds { get; init; } = [];

    public IReadOnlyList<string> RelatedEvidenceIds { get; init; } = [];

    public IReadOnlyList<string> AffectedStages { get; init; } = [];

    public IReadOnlyList<string> AffectedRepos { get; init; } = [];

    public string ExampleSummary { get; init; } = string.Empty;

    public static RuntimeAgentProblemFollowUpCandidateSurface FromRecords(
        string problemKind,
        IReadOnlyList<PilotProblemIntakeRecord> records)
    {
        var orderedRecords = records
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenByDescending(static record => record.ProblemId, StringComparer.Ordinal)
            .ToArray();
        var lane = RuntimeAgentProblemTriageLedgerService.ResolveTriageLane(problemKind);
        var blockingCount = orderedRecords.Count(IsBlocking);
        var affectedStages = orderedRecords
            .Select(static record => RuntimeAgentProblemTriageQueueItemSurface.NormalizeStage(record.CurrentStageId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static stage => stage, StringComparer.Ordinal)
            .ToArray();
        var affectedRepos = orderedRecords
            .Select(static record => string.IsNullOrWhiteSpace(record.RepoId) ? "(none)" : record.RepoId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static repo => repo, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new RuntimeAgentProblemFollowUpCandidateSurface
        {
            CandidateId = $"FOLLOW-UP-{NormalizeIdentifier(lane)}-{NormalizeIdentifier(problemKind)}",
            CandidateStatus = ResolveCandidateStatus(orderedRecords.Length, blockingCount),
            ProblemKind = problemKind,
            RecommendedTriageLane = lane,
            ProblemCount = orderedRecords.Length,
            BlockingCount = blockingCount,
            RepoCount = affectedRepos.Length,
            StageCount = affectedStages.Length,
            LatestRecordedAtUtc = orderedRecords.FirstOrDefault()?.RecordedAtUtc,
            SuggestedTitle = BuildSuggestedTitle(problemKind, lane),
            SuggestedIntent = BuildSuggestedIntent(problemKind, lane),
            RequiredOperatorDecision = "operator_accept_pattern_before_governed_card_or_task",
            RelatedProblemIds = orderedRecords.Select(static record => record.ProblemId).ToArray(),
            RelatedEvidenceIds = orderedRecords.Select(static record => record.EvidenceId).ToArray(),
            AffectedStages = affectedStages,
            AffectedRepos = affectedRepos,
            ExampleSummary = orderedRecords.FirstOrDefault()?.Summary ?? string.Empty,
        };
    }

    private static bool IsBlocking(PilotProblemIntakeRecord record)
    {
        var severity = RuntimeAgentProblemTriageQueueItemSurface.NormalizeSeverity(record.Severity);
        return string.Equals(severity, "blocking", StringComparison.OrdinalIgnoreCase)
               || string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase)
               || string.Equals(severity, "high", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCandidateStatus(int problemCount, int blockingCount)
    {
        return problemCount >= 2 || blockingCount > 0
            ? "governed_follow_up_candidate"
            : "watchlist_only";
    }

    private static string BuildSuggestedTitle(string problemKind, string lane)
    {
        return $"Review {problemKind} friction in {lane}";
    }

    private static string BuildSuggestedIntent(string problemKind, string lane)
    {
        return $"Assess whether recorded agent problem intake for `{problemKind}` should become governed follow-up work in `{lane}`.";
    }

    private static string NormalizeIdentifier(string value)
    {
        var normalized = new string(value
            .Trim()
            .Select(static character => char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '-')
            .ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }
}
