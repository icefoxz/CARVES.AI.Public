namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentProblemFollowUpCandidates(RuntimeAgentProblemFollowUpCandidatesSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime agent problem follow-up candidates",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Phase document: {surface.PhaseDocumentPath}",
            $"Previous phase document: {surface.PreviousPhaseDocumentPath}",
            $"Guide document: {surface.FollowUpCandidatesGuideDocumentPath}",
            $"Triage ledger guide document: {surface.TriageLedgerGuideDocumentPath}",
            $"Problem intake guide document: {surface.ProblemIntakeGuideDocumentPath}",
            $"Runtime document root: {surface.RuntimeDocumentRoot}",
            $"Runtime document root mode: {surface.RuntimeDocumentRootMode}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Command entry: {surface.CommandEntry}",
            $"JSON command entry: {surface.JsonCommandEntry}",
            $"Alias command entry: {surface.AliasCommandEntry}",
            $"Triage alias command entry: {surface.TriageAliasCommandEntry}",
            $"Inspect command entry: {surface.InspectCommandEntry}",
            $"API command entry: {surface.ApiCommandEntry}",
            $"Follow-up candidates ready: {surface.FollowUpCandidatesReady}",
            $"Recorded problem count: {surface.RecordedProblemCount}",
            $"Candidate count: {surface.CandidateCount}",
            $"Governed candidate count: {surface.GovernedCandidateCount}",
            $"Watchlist candidate count: {surface.WatchlistCandidateCount}",
            $"Repeated pattern count: {surface.RepeatedPatternCount}",
            $"Blocking candidate count: {surface.BlockingCandidateCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Candidates:",
        };

        lines.AddRange(surface.Candidates.Count == 0
            ? ["- none"]
            : surface.Candidates.Select(candidate => $"- {candidate.CandidateId}: status={candidate.CandidateStatus}; kind={candidate.ProblemKind}; lane={candidate.RecommendedTriageLane}; problems={candidate.ProblemCount}; blocking={candidate.BlockingCount}; repos={candidate.RepoCount}; stages={candidate.StageCount}; latest={FormatProblemFollowUpTimestamp(candidate.LatestRecordedAtUtc)}; title={candidate.SuggestedTitle}"));
        lines.Add("Candidate rules:");
        lines.AddRange(surface.CandidateRules.Count == 0
            ? ["- none"]
            : surface.CandidateRules.Select(rule => $"- {rule}"));
        lines.Add("Operator review questions:");
        lines.AddRange(surface.OperatorReviewQuestions.Count == 0
            ? ["- none"]
            : surface.OperatorReviewQuestions.Select(question => $"- {question}"));
        lines.Add("Gaps:");
        lines.AddRange(surface.Gaps.Count == 0
            ? ["- none"]
            : surface.Gaps.Select(gap => $"- {gap}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }

    private static string FormatProblemFollowUpTimestamp(DateTimeOffset? timestamp)
    {
        return timestamp is null ? "(none)" : timestamp.Value.ToString("O");
    }
}
