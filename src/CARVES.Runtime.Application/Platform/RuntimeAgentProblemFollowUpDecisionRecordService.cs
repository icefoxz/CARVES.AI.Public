using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentProblemFollowUpDecisionRecordService
{
    public const string Phase38DocumentPath = "docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md";
    public const string DecisionRecordGuideDocumentPath = "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md";
    public const string RecordRootPath = ".ai/runtime/agent-problem-follow-up-decisions";

    private static readonly string[] AllowedDecisions =
    [
        "accept_as_governed_planning_input",
        "accept_as_governed_planning_input_after_operator_override",
        "reject_as_target_project_only",
        "reject_as_noise_or_target_only",
        "wait_for_more_evidence",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;
    private readonly Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory;

    public RuntimeAgentProblemFollowUpDecisionRecordService(
        string repoRoot,
        Func<IReadOnlyList<PilotProblemIntakeRecord>> problemRecordsFactory)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
        this.problemRecordsFactory = problemRecordsFactory;
    }

    public RuntimeAgentProblemFollowUpDecisionRecordSurface Build()
    {
        return BuildCore([]);
    }

    public RuntimeAgentProblemFollowUpDecisionRecordEntrySurface Record(AgentProblemFollowUpDecisionRecordRequest request)
    {
        var plan = new RuntimeAgentProblemFollowUpDecisionPlanService(repoRoot, problemRecordsFactory).Build();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("reason is required.");
        }

        if (!plan.DecisionPlanReady)
        {
            errors.Add("agent problem follow-up decision plan is not ready; run carves pilot follow-up-plan --json and resolve its gaps first.");
        }

        if (!string.IsNullOrWhiteSpace(request.PlanId)
            && !string.Equals(request.PlanId.Trim(), plan.DecisionPlanId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"request plan id '{request.PlanId}' does not match current decision plan id '{plan.DecisionPlanId}'.");
        }

        var candidates = ResolveRequestedCandidates(request, plan).ToArray();
        if (candidates.Length == 0)
        {
            errors.Add("at least one follow-up candidate id is required; use --all or one or more --candidate values.");
        }

        var knownCandidates = plan.DecisionItems
            .ToDictionary(static item => item.CandidateId, StringComparer.OrdinalIgnoreCase);
        var selectedItems = new List<RuntimeAgentProblemFollowUpDecisionItemSurface>();
        foreach (var candidateId in candidates)
        {
            if (!knownCandidates.TryGetValue(candidateId, out var item))
            {
                errors.Add($"candidate '{candidateId}' is not present in the current follow-up decision plan.");
                continue;
            }

            selectedItems.Add(item);
        }

        var decision = NormalizeDecisionAlias(request.Decision, selectedItems, errors);
        if (!AllowedDecisions.Contains(decision, StringComparer.Ordinal))
        {
            errors.Add($"decision must be one of: {string.Join(", ", AllowedDecisions)}.");
        }

        foreach (var item in selectedItems)
        {
            if (!item.DecisionOptions.Contains(decision, StringComparer.Ordinal))
            {
                errors.Add($"decision '{decision}' is not allowed for candidate '{item.CandidateId}'.");
            }
        }

        if (IsAcceptDecision(decision))
        {
            if (string.IsNullOrWhiteSpace(request.AcceptanceEvidence))
            {
                errors.Add("acceptance evidence is required when accepting a follow-up candidate.");
            }

            if (string.IsNullOrWhiteSpace(request.ReadbackCommand))
            {
                errors.Add("readback command is required when accepting a follow-up candidate.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        var now = DateTimeOffset.UtcNow;
        var recordId = $"agent-problem-follow-up-decision-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..61];
        var recordPath = $"{RecordRootPath}/{recordId}.json";
        var relatedProblemIds = selectedItems
            .SelectMany(static item => item.RelatedProblemIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        var relatedEvidenceIds = selectedItems
            .SelectMany(static item => item.RelatedEvidenceIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        var record = new RuntimeAgentProblemFollowUpDecisionRecordEntrySurface
        {
            DecisionRecordId = recordId,
            RecordPath = recordPath,
            ProductClosurePhase = RuntimeProductClosureMetadata.CurrentPhase,
            PhaseDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath,
            SourceSurfaceId = plan.SurfaceId,
            DecisionPlanId = plan.DecisionPlanId,
            Decision = decision,
            CandidateIds = candidates,
            RelatedProblemIds = relatedProblemIds,
            RelatedEvidenceIds = relatedEvidenceIds,
            Reason = request.Reason.Trim(),
            Operator = string.IsNullOrWhiteSpace(request.Operator) ? "operator" : request.Operator.Trim(),
            AcceptanceEvidence = request.AcceptanceEvidence.Trim(),
            ReadbackCommand = request.ReadbackCommand.Trim(),
            RecordedAtUtc = now,
        };

        var fullRecordPath = Path.Combine(repoRoot, recordPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullRecordPath)!);
        File.WriteAllText(fullRecordPath, JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    private RuntimeAgentProblemFollowUpDecisionRecordSurface BuildCore(IReadOnlyList<string> extraErrors)
    {
        var errors = new List<string>(extraErrors);
        ValidateRuntimeDocument(RuntimeProductClosureMetadata.CurrentDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(Phase38DocumentPath, "Product closure Phase 38 agent problem follow-up decision record document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.Phase37DocumentPath, "Product closure Phase 37 agent problem follow-up decision plan document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "Product closure Phase 36 agent problem follow-up candidates document", errors);
        ValidateRuntimeDocument("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "Product closure Phase 35 agent problem triage ledger document", errors);
        ValidateRuntimeDocument(DecisionRecordGuideDocumentPath, "Agent problem follow-up decision record guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath, "Agent problem follow-up decision plan guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath, "Agent problem follow-up candidates guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemTriageLedgerService.TriageLedgerGuideDocumentPath, "Agent problem triage ledger guide document", errors);
        ValidateRuntimeDocument(RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "Agent problem intake guide document", errors);
        ValidateRuntimeDocument(RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "External agent quickstart guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotGuideService.GuideDocumentPath, "Productized pilot guide document", errors);
        ValidateRuntimeDocument(RuntimeProductClosurePilotStatusService.GuideDocumentPath, "Productized pilot status document", errors);

        var plan = new RuntimeAgentProblemFollowUpDecisionPlanService(repoRoot, problemRecordsFactory).Build();
        errors.AddRange(plan.Errors.Select(static error => $"runtime-agent-problem-follow-up-decision-plan: {error}"));

        var recordReads = ListRecordReads().ToArray();
        var records = recordReads
            .Where(static read => read.Record is not null)
            .Select(static read => read.Record!)
            .ToArray();
        var malformedRecordPaths = recordReads
            .Where(static read => read.Record is null)
            .Select(static read => read.Path)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var currentPlanReads = recordReads
            .Where(read => read.Record is not null
                           && string.Equals(read.Record.DecisionPlanId, plan.DecisionPlanId, StringComparison.OrdinalIgnoreCase))
            .Select(read => ValidateRecordRead(read, plan))
            .ToArray();
        var validCurrentPlanRecords = currentPlanReads
            .Where(static read => read.Gaps.Count == 0 && read.Record is not null)
            .Select(static read => read.Record!)
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenBy(static record => record.DecisionRecordId, StringComparer.Ordinal)
            .ToArray();
        var currentPlanRecords = currentPlanReads
            .Where(static read => read.Record is not null)
            .Select(static read => read.Record!)
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenBy(static record => record.DecisionRecordId, StringComparer.Ordinal)
            .ToArray();
        var staleRecords = records
            .Where(record => !string.Equals(record.DecisionPlanId, plan.DecisionPlanId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenBy(static record => record.DecisionRecordId, StringComparer.Ordinal)
            .ToArray();
        var invalidRecordPaths = currentPlanReads
            .Where(static read => read.Gaps.Count > 0)
            .Select(static read => read.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var conflicts = BuildConflictingDecisionCandidates(validCurrentPlanRecords).ToArray();
        var recordCommitRead = ReadRecordCommitStatus(recordReads.Select(static read => read.Path).ToArray());
        var uncommittedRecordPaths = recordCommitRead.DirtyRecordPaths
            .Concat(recordCommitRead.UntrackedRecordPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var decisionRecordCommitReady = recordReads.Length == 0
                                        || (recordCommitRead.GitRepositoryDetected
                                            && string.IsNullOrWhiteSpace(recordCommitRead.Error)
                                            && uncommittedRecordPaths.Length == 0);
        var recordAuditReady = errors.Count == 0
                               && malformedRecordPaths.Length == 0
                               && invalidRecordPaths.Length == 0
                               && conflicts.Length == 0;
        var requiredCandidates = plan.DecisionRequired
            ? plan.DecisionItems
                .Where(static item => item.RecommendedDecision == "operator_review_required")
                .Select(static item => item.CandidateId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        var recordedCandidates = validCurrentPlanRecords
            .SelectMany(static record => record.CandidateIds)
            .Select(NormalizeCandidateId)
            .Where(static candidateId => candidateId.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static candidateId => candidateId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var recordedSet = recordedCandidates.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingCandidates = requiredCandidates
            .Where(candidateId => !recordedSet.Contains(candidateId))
            .OrderBy(static candidateId => candidateId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var decisionRecordReady = errors.Count == 0
                                  && recordAuditReady
                                  && decisionRecordCommitReady
                                  && plan.DecisionPlanReady
                                  && missingCandidates.Length == 0;

        return new RuntimeAgentProblemFollowUpDecisionRecordSurface
        {
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(plan, recordAuditReady, decisionRecordCommitReady, decisionRecordReady, requiredCandidates.Length, missingCandidates.Length, errors.Count),
            DecisionPlanId = plan.DecisionPlanId,
            DecisionPlanPosture = plan.OverallPosture,
            DecisionPlanReady = plan.DecisionPlanReady,
            DecisionRequired = plan.DecisionRequired,
            DecisionRecordReady = decisionRecordReady,
            RecordAuditReady = recordAuditReady,
            DecisionRecordCommitReady = decisionRecordCommitReady,
            RequiredDecisionCandidateCount = requiredCandidates.Length,
            RecordedDecisionCandidateCount = recordedCandidates.Length,
            MissingDecisionCandidateCount = missingCandidates.Length,
            RecordCount = records.Length,
            CurrentPlanRecordCount = currentPlanRecords.Length,
            ValidCurrentPlanRecordCount = validCurrentPlanRecords.Length,
            StaleRecordCount = staleRecords.Length,
            InvalidRecordCount = invalidRecordPaths.Length,
            MalformedRecordCount = malformedRecordPaths.Length,
            ConflictingDecisionCandidateCount = conflicts.Length,
            DirtyDecisionRecordCount = recordCommitRead.DirtyRecordPaths.Count,
            UntrackedDecisionRecordCount = recordCommitRead.UntrackedRecordPaths.Count,
            UncommittedDecisionRecordCount = uncommittedRecordPaths.Length,
            RequiredDecisionCandidateIds = requiredCandidates,
            RecordedDecisionCandidateIds = recordedCandidates,
            MissingDecisionCandidateIds = missingCandidates,
            DecisionRecordIds = validCurrentPlanRecords.Select(static record => record.DecisionRecordId).ToArray(),
            StaleDecisionRecordIds = staleRecords.Select(static record => record.DecisionRecordId).ToArray(),
            InvalidDecisionRecordPaths = invalidRecordPaths,
            MalformedDecisionRecordPaths = malformedRecordPaths,
            ConflictingDecisionCandidateIds = conflicts,
            DirtyDecisionRecordPaths = recordCommitRead.DirtyRecordPaths,
            UntrackedDecisionRecordPaths = recordCommitRead.UntrackedRecordPaths,
            UncommittedDecisionRecordPaths = uncommittedRecordPaths,
            Records = validCurrentPlanRecords,
            BoundaryRules =
            [
                "The decision record is target runtime evidence under .ai/runtime/agent-problem-follow-up-decisions/ and should be committed through target commit closure after it is written.",
                "The record can capture accept, reject, or wait decisions for follow-up candidates; it does not create cards, tasks, acceptance contracts, reviews, or writebacks.",
                "Accepted candidates require acceptance evidence and a readback command, and must still enter the existing intent/plan lane.",
                "A stale record for a previous decision_plan_id does not satisfy the current follow-up decision plan.",
                "Only well-formed, current-plan, non-conflicting decision records can satisfy the decision-record readback.",
                "Decision records do not satisfy closure until their record paths are tracked and clean in the target git work tree.",
            ],
            Gaps = BuildGaps(plan, missingCandidates, malformedRecordPaths, invalidRecordPaths, conflicts, recordCommitRead, uncommittedRecordPaths, decisionRecordReady).ToArray(),
            Summary = BuildSummary(plan, recordAuditReady, decisionRecordCommitReady, decisionRecordReady, requiredCandidates.Length, missingCandidates.Length, malformedRecordPaths.Length, invalidRecordPaths.Length, conflicts.Length, uncommittedRecordPaths.Length, errors.Count),
            RecommendedNextAction = BuildRecommendedNextAction(plan, recordAuditReady, decisionRecordCommitReady, decisionRecordReady, requiredCandidates.Length, missingCandidates.Length, malformedRecordPaths.Length, invalidRecordPaths.Length, conflicts.Length, uncommittedRecordPaths.Length, errors.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create, approve, or mutate cards, tasks, acceptance contracts, reviews, writebacks, commits, releases, packs, or problem intake records.",
                "This surface does not authorize an external agent to continue past a CARVES blocker.",
                "This surface does not close the triage ledger or delete problem reports.",
                "This surface does not replace the existing planning lane or operator review.",
            ],
        };
    }

    private IEnumerable<RecordRead> ListRecordReads()
    {
        var root = Path.Combine(repoRoot, RecordRootPath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly))
        {
            yield return ReadRecord(path);
        }
    }

    private RecordRead ReadRecord(string path)
    {
        var relativePath = ToRepoRelativePath(path);
        try
        {
            var record = JsonSerializer.Deserialize<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface>(File.ReadAllText(path), JsonOptions);
            return record is null
                ? new RecordRead(relativePath, null, ["empty_record"])
                : new RecordRead(relativePath, record, []);
        }
        catch (JsonException)
        {
            return new RecordRead(relativePath, null, ["malformed_json"]);
        }
        catch (IOException)
        {
            return new RecordRead(relativePath, null, ["unreadable_record"]);
        }
    }

    private RecordRead ValidateRecordRead(RecordRead read, RuntimeAgentProblemFollowUpDecisionPlanSurface plan)
    {
        if (read.Record is null)
        {
            return read;
        }

        var record = read.Record;
        var gaps = new List<string>(read.Gaps);
        if (string.IsNullOrWhiteSpace(record.DecisionRecordId))
        {
            gaps.Add("decision_record_id_missing");
        }

        if (!string.Equals(record.RecordPath, read.Path, StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add("record_path_mismatch");
        }

        if (!string.Equals(record.SourceSurfaceId, plan.SurfaceId, StringComparison.Ordinal))
        {
            gaps.Add("source_surface_mismatch");
        }

        if (!AllowedDecisions.Contains(record.Decision, StringComparer.Ordinal))
        {
            gaps.Add("decision_invalid");
        }

        if (string.IsNullOrWhiteSpace(record.Reason))
        {
            gaps.Add("reason_missing");
        }

        if (string.IsNullOrWhiteSpace(record.Operator))
        {
            gaps.Add("operator_missing");
        }

        if (IsAcceptDecision(record.Decision))
        {
            if (string.IsNullOrWhiteSpace(record.AcceptanceEvidence))
            {
                gaps.Add("acceptance_evidence_missing");
            }

            if (string.IsNullOrWhiteSpace(record.ReadbackCommand))
            {
                gaps.Add("readback_command_missing");
            }
        }

        var knownCandidates = plan.DecisionItems
            .ToDictionary(static item => item.CandidateId, StringComparer.OrdinalIgnoreCase);
        var normalizedCandidates = record.CandidateIds
            .Select(NormalizeCandidateId)
            .Where(static candidateId => candidateId.Length > 0)
            .ToArray();
        if (normalizedCandidates.Length == 0)
        {
            gaps.Add("candidate_ids_missing");
        }

        foreach (var candidateId in normalizedCandidates)
        {
            if (!knownCandidates.TryGetValue(candidateId, out var item))
            {
                gaps.Add($"candidate_not_in_current_plan:{candidateId}");
                continue;
            }

            if (!item.DecisionOptions.Contains(record.Decision, StringComparer.Ordinal))
            {
                gaps.Add($"decision_not_allowed_for_candidate:{candidateId}");
            }
        }

        return read with { Gaps = gaps };
    }

    private static IEnumerable<string> BuildConflictingDecisionCandidates(IReadOnlyList<RuntimeAgentProblemFollowUpDecisionRecordEntrySurface> records)
    {
        return records
            .SelectMany(record => record.CandidateIds.Select(candidateId => new { CandidateId = NormalizeCandidateId(candidateId), record.Decision }))
            .Where(item => item.CandidateId.Length > 0)
            .GroupBy(item => item.CandidateId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Decision).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .OrderBy(static candidateId => candidateId, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ResolveRequestedCandidates(
        AgentProblemFollowUpDecisionRecordRequest request,
        RuntimeAgentProblemFollowUpDecisionPlanSurface plan)
    {
        var candidates = request.AllCandidates
            ? plan.DecisionRequired
                ? plan.DecisionItems
                    .Where(static item => item.RecommendedDecision == "operator_review_required")
                    .Select(static item => item.CandidateId)
                : plan.DecisionItems.Select(static item => item.CandidateId)
            : request.CandidateIds;

        return candidates
            .Select(NormalizeCandidateId)
            .Where(static candidateId => candidateId.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static candidateId => candidateId, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeDecisionAlias(
        string decision,
        IReadOnlyList<RuntimeAgentProblemFollowUpDecisionItemSurface> selectedItems,
        List<string> errors)
    {
        var normalized = NormalizeDecision(decision);
        if (normalized is "wait")
        {
            return "wait_for_more_evidence";
        }

        if (normalized is not ("accept" or "reject"))
        {
            return normalized;
        }

        var hasGoverned = selectedItems.Any(static item => item.RecommendedDecision == "operator_review_required");
        var hasWatchlist = selectedItems.Any(static item => item.RecommendedDecision != "operator_review_required");
        if (hasGoverned && hasWatchlist)
        {
            errors.Add($"decision alias '{normalized}' cannot be used for mixed governed/watchlist candidates; use an exact decision label.");
            return normalized;
        }

        if (normalized == "accept")
        {
            return hasWatchlist
                ? "accept_as_governed_planning_input_after_operator_override"
                : "accept_as_governed_planning_input";
        }

        return hasWatchlist
            ? "reject_as_noise_or_target_only"
            : "reject_as_target_project_only";
    }

    private static IEnumerable<string> BuildGaps(
        RuntimeAgentProblemFollowUpDecisionPlanSurface plan,
        IReadOnlyList<string> missingCandidates,
        IReadOnlyList<string> malformedRecordPaths,
        IReadOnlyList<string> invalidRecordPaths,
        IReadOnlyList<string> conflictingDecisionCandidateIds,
        RecordCommitRead recordCommitRead,
        IReadOnlyList<string> uncommittedRecordPaths,
        bool decisionRecordReady)
    {
        if (decisionRecordReady)
        {
            yield break;
        }

        foreach (var gap in plan.Gaps)
        {
            yield return $"agent_problem_follow_up_decision_plan:{gap}";
        }

        if (!plan.DecisionPlanReady)
        {
            yield return "agent_problem_follow_up_decision_plan_not_ready";
        }

        foreach (var path in malformedRecordPaths)
        {
            yield return $"agent_problem_follow_up_decision_record_malformed:{path}";
        }

        foreach (var path in invalidRecordPaths)
        {
            yield return $"agent_problem_follow_up_decision_record_invalid:{path}";
        }

        foreach (var candidateId in conflictingDecisionCandidateIds)
        {
            yield return $"agent_problem_follow_up_decision_record_conflict:{candidateId}";
        }

        if (!string.IsNullOrWhiteSpace(recordCommitRead.Error))
        {
            yield return $"agent_problem_follow_up_decision_record_commit_readback:{recordCommitRead.Error}";
        }

        if (!recordCommitRead.GitRepositoryDetected && uncommittedRecordPaths.Count > 0)
        {
            yield return "agent_problem_follow_up_decision_record_commit_readback_git_unavailable";
        }

        foreach (var path in uncommittedRecordPaths)
        {
            yield return $"agent_problem_follow_up_decision_record_uncommitted:{path}";
        }

        foreach (var candidateId in missingCandidates)
        {
            yield return $"agent_problem_follow_up_decision_record_missing:{candidateId}";
        }
    }

    private static string ResolvePosture(
        RuntimeAgentProblemFollowUpDecisionPlanSurface plan,
        bool recordAuditReady,
        bool decisionRecordCommitReady,
        bool decisionRecordReady,
        int requiredCandidateCount,
        int missingCandidateCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "agent_problem_follow_up_decision_record_blocked_by_surface_gaps";
        }

        if (!decisionRecordCommitReady)
        {
            return "agent_problem_follow_up_decision_record_waiting_for_record_commit";
        }

        if (!plan.DecisionPlanReady)
        {
            return "agent_problem_follow_up_decision_record_waiting_for_decision_plan";
        }

        if (!recordAuditReady)
        {
            return "agent_problem_follow_up_decision_record_blocked_by_record_audit";
        }

        if (!plan.DecisionRequired || requiredCandidateCount == 0)
        {
            return "agent_problem_follow_up_decision_record_no_decision_required";
        }

        return decisionRecordReady && missingCandidateCount == 0
            ? "agent_problem_follow_up_decision_record_ready"
            : "agent_problem_follow_up_decision_record_waiting_for_operator_decision";
    }

    private static string BuildSummary(
        RuntimeAgentProblemFollowUpDecisionPlanSurface plan,
        bool recordAuditReady,
        bool decisionRecordCommitReady,
        bool decisionRecordReady,
        int requiredCandidateCount,
        int missingCandidateCount,
        int malformedRecordCount,
        int invalidRecordCount,
        int conflictingDecisionCandidateCount,
        int uncommittedRecordCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Agent problem follow-up decision record cannot be trusted until required documents and decision-plan gaps are resolved.";
        }

        if (!plan.DecisionPlanReady)
        {
            return plan.Summary;
        }

        if (!recordAuditReady)
        {
            return $"Agent problem follow-up decision record audit is blocked by malformed={malformedRecordCount}, invalid={invalidRecordCount}, conflicting_candidates={conflictingDecisionCandidateCount}.";
        }

        if (!decisionRecordCommitReady)
        {
            return $"Agent problem follow-up decision record commit readback is blocked by uncommitted_record_paths={uncommittedRecordCount}.";
        }

        if (!plan.DecisionRequired || requiredCandidateCount == 0)
        {
            return "No follow-up candidates currently require a durable operator decision record.";
        }

        return decisionRecordReady
            ? "Operator follow-up decisions are durably recorded for every required candidate in the current plan."
            : $"Operator follow-up decisions are missing for {missingCandidateCount} current-plan candidate{(missingCandidateCount == 1 ? string.Empty : "s")}.";
    }

    private static string BuildRecommendedNextAction(
        RuntimeAgentProblemFollowUpDecisionPlanSurface plan,
        bool recordAuditReady,
        bool decisionRecordCommitReady,
        bool decisionRecordReady,
        int requiredCandidateCount,
        int missingCandidateCount,
        int malformedRecordCount,
        int invalidRecordCount,
        int conflictingDecisionCandidateCount,
        int uncommittedRecordCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Restore agent problem follow-up decision record docs and decision-plan inputs, then rerun carves pilot follow-up-record --json.";
        }

        if (!plan.DecisionPlanReady)
        {
            return "Run carves pilot follow-up-plan --json and resolve its gaps before recording a durable operator decision.";
        }

        if (!recordAuditReady)
        {
            return $"Review .ai/runtime/agent-problem-follow-up-decisions/ records; resolve malformed={malformedRecordCount}, invalid={invalidRecordCount}, conflicting_candidates={conflictingDecisionCandidateCount}, then rerun carves pilot follow-up-record --json.";
        }

        if (!decisionRecordCommitReady)
        {
            return $"Run carves pilot commit-plan --json; stage and commit .ai/runtime/agent-problem-follow-up-decisions/ records, then rerun follow-up-plan, follow-up-record, status, and resources. Uncommitted records={uncommittedRecordCount}.";
        }

        if (!plan.DecisionRequired || requiredCandidateCount == 0)
        {
            return "agent_problem_follow_up_decision_record_readback_clean";
        }

        if (decisionRecordReady)
        {
            return "Convert accepted candidates through intent draft --persist and plan init with the recorded acceptance evidence; rejected or waiting candidates remain visible evidence.";
        }

        return missingCandidateCount > 0
            ? "Run carves pilot record-follow-up-decision <decision> --all --reason <operator-reviewed-reason>; accepted decisions also require --acceptance-evidence and --readback."
            : "Rerun carves pilot follow-up-record --json.";
    }

    private static string NormalizeDecision(string decision)
    {
        return decision.Trim().Replace('-', '_').ToLowerInvariant();
    }

    private static string NormalizeCandidateId(string candidateId)
    {
        return candidateId.Trim();
    }

    private static bool IsAcceptDecision(string decision)
    {
        return decision is "accept_as_governed_planning_input" or "accept_as_governed_planning_input_after_operator_override";
    }

    private string ToRepoRelativePath(string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
    }

    private RecordCommitRead ReadRecordCommitStatus(IReadOnlyList<string> recordPaths)
    {
        if (recordPaths.Count == 0)
        {
            return new RecordCommitRead(true, [], [], string.Empty);
        }

        if (!HasGitMetadataAtRepoRoot())
        {
            return new RecordCommitRead(false, [], recordPaths, "git_repository_not_detected");
        }

        try
        {
            var status = RunGit(["status", "--porcelain=v1", "--untracked-files=all", "--", .. recordPaths]);
            if (status.ExitCode != 0)
            {
                return new RecordCommitRead(true, [], recordPaths, BuildGitError(status));
            }

            var dirtyPaths = status.StandardOutput
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseStatusPath)
                .Where(static path => path.Length > 0)
                .Where(path => recordPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var tracked = RunGit(["ls-files", "--", .. recordPaths]);
            if (tracked.ExitCode != 0)
            {
                return new RecordCommitRead(true, dirtyPaths, recordPaths, BuildGitError(tracked));
            }

            var trackedPaths = tracked.StandardOutput
                .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
                .Select(static path => path.Replace('\\', '/').Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var untrackedPaths = recordPaths
                .Where(path => !trackedPaths.Contains(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new RecordCommitRead(true, dirtyPaths, untrackedPaths, string.Empty);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            return new RecordCommitRead(false, [], recordPaths, $"git_status_unavailable:{exception.Message}");
        }
    }

    private bool HasGitMetadataAtRepoRoot()
    {
        var gitPath = Path.Combine(repoRoot, ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    private GitCommandResult RunGit(IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start git process.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitCommandResult(process.ExitCode, standardOutput, standardError);
    }

    private static string ParseStatusPath(string line)
    {
        if (line.Length < 3)
        {
            return string.Empty;
        }

        var path = line[3..].Trim().Replace('\\', '/');
        var renameSeparator = path.LastIndexOf(" -> ", StringComparison.Ordinal);
        return renameSeparator >= 0
            ? path[(renameSeparator + 4)..].Trim()
            : path;
    }

    private static string BuildGitError(GitCommandResult result)
    {
        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput.Trim()
            : result.StandardError.Trim();
        return string.IsNullOrWhiteSpace(detail)
            ? $"git_exit_code:{result.ExitCode}"
            : $"git_exit_code:{result.ExitCode}:{detail}";
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private sealed record RecordRead(
        string Path,
        RuntimeAgentProblemFollowUpDecisionRecordEntrySurface? Record,
        IReadOnlyList<string> Gaps);

    private sealed record RecordCommitRead(
        bool GitRepositoryDetected,
        IReadOnlyList<string> DirtyRecordPaths,
        IReadOnlyList<string> UntrackedRecordPaths,
        string Error);

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
