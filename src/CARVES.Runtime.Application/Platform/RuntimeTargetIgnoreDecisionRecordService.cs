using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetIgnoreDecisionRecordService
{
    public const string PhaseDocumentPath = RuntimeProductClosureMetadata.CurrentDocumentPath;
    public const string PreviousPhaseDocumentPath = RuntimeProductClosureMetadata.PreviousDocumentPath;
    public const string CommitReadbackPhaseDocumentPath = "docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md";
    public const string AuditPhaseDocumentPath = "docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md";
    public const string DecisionRecordPhaseDocumentPath = "docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md";
    public const string IgnoreDecisionPlanDocumentPath = RuntimeTargetIgnoreDecisionPlanService.PhaseDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md";
    public const string RecordRootPath = ".ai/runtime/target-ignore-decisions";

    private static readonly string[] AllowedDecisions =
    [
        "keep_local",
        "add_to_gitignore_after_review",
        "manual_cleanup_after_review",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetIgnoreDecisionRecordService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetIgnoreDecisionRecordSurface Build()
    {
        return BuildCore([]);
    }

    public RuntimeTargetIgnoreDecisionRecordEntrySurface Record(TargetIgnoreDecisionRecordRequest request)
    {
        var plan = new RuntimeTargetIgnoreDecisionPlanService(repoRoot).Build();
        var decision = NormalizeDecision(request.Decision);
        var errors = new List<string>();

        if (!AllowedDecisions.Contains(decision, StringComparer.Ordinal))
        {
            errors.Add($"decision must be one of: {string.Join(", ", AllowedDecisions)}.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors.Add("reason is required.");
        }

        if (!plan.IgnoreDecisionPlanReady)
        {
            errors.Add("ignore decision plan is not ready; run carves pilot ignore-plan --json and resolve its gaps first.");
        }

        if (!string.IsNullOrWhiteSpace(request.PlanId)
            && !string.Equals(request.PlanId.Trim(), plan.IgnoreDecisionPlanId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"request plan id '{request.PlanId}' does not match current plan id '{plan.IgnoreDecisionPlanId}'.");
        }

        var entries = ResolveRequestedEntries(request, plan).ToArray();
        if (entries.Length == 0)
        {
            errors.Add("at least one ignore decision entry is required; use --all or one or more --entry values.");
        }

        var knownEntries = plan.SuggestedIgnoreEntries
            .Concat(plan.MissingIgnoreEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => !knownEntries.Contains(entry)))
        {
            errors.Add($"entry '{entry}' is not present in the current ignore decision plan.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        var now = DateTimeOffset.UtcNow;
        var recordId = $"target-ignore-decision-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..46];
        var recordPath = $"{RecordRootPath}/{recordId}.json";
        var record = new RuntimeTargetIgnoreDecisionRecordEntrySurface
        {
            DecisionRecordId = recordId,
            RecordPath = recordPath,
            ProductClosurePhase = RuntimeProductClosureMetadata.CurrentPhase,
            PhaseDocumentPath = PhaseDocumentPath,
            SourceSurfaceId = plan.SurfaceId,
            IgnoreDecisionPlanId = plan.IgnoreDecisionPlanId,
            Decision = decision,
            Entries = entries,
            Reason = request.Reason.Trim(),
            Operator = string.IsNullOrWhiteSpace(request.Operator) ? "operator" : request.Operator.Trim(),
            RecordedAtUtc = now,
        };

        var fullRecordPath = Path.Combine(repoRoot, recordPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullRecordPath)!);
        File.WriteAllText(fullRecordPath, JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    private RuntimeTargetIgnoreDecisionRecordSurface BuildCore(IReadOnlyList<string> extraErrors)
    {
        var errors = new List<string>(extraErrors);
        ValidateRuntimeDocument(PhaseDocumentPath, RuntimeProductClosureMetadata.CurrentDocumentLabel, errors);
        ValidateRuntimeDocument(CommitReadbackPhaseDocumentPath, "Product closure Phase 31 target ignore decision record commit readback document", errors);
        ValidateRuntimeDocument(AuditPhaseDocumentPath, "Product closure Phase 30 target ignore decision record audit document", errors);
        ValidateRuntimeDocument(DecisionRecordPhaseDocumentPath, "Product closure Phase 29 target ignore decision record document", errors);
        ValidateRuntimeDocument(IgnoreDecisionPlanDocumentPath, "Product closure Phase 28 target ignore decision plan document", errors);
        ValidateRuntimeDocument(RuntimeTargetResiduePolicyService.PhaseDocumentPath, "Product closure Phase 27 external target residue policy document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Productized pilot status guide document", errors);

        var plan = new RuntimeTargetIgnoreDecisionPlanService(repoRoot).Build();
        errors.AddRange(plan.Errors.Select(static error => $"runtime-target-ignore-decision-plan: {error}"));

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
                           && string.Equals(read.Record.IgnoreDecisionPlanId, plan.IgnoreDecisionPlanId, StringComparison.OrdinalIgnoreCase))
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
            .Where(record => !string.Equals(record.IgnoreDecisionPlanId, plan.IgnoreDecisionPlanId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenBy(static record => record.DecisionRecordId, StringComparer.Ordinal)
            .ToArray();
        var invalidRecordPaths = currentPlanReads
            .Where(static read => read.Gaps.Count > 0)
            .Select(static read => read.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var conflicts = BuildConflictingDecisionEntries(validCurrentPlanRecords).ToArray();
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
        var requiredEntries = plan.IgnoreDecisionRequired
            ? plan.MissingIgnoreEntries
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];
        var recordedEntries = validCurrentPlanRecords
            .SelectMany(static record => record.Entries)
            .Select(NormalizeEntry)
            .Where(static entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var recordedSet = recordedEntries.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingEntries = requiredEntries
            .Where(entry => !recordedSet.Contains(entry))
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var decisionRecordReady = errors.Count == 0
                                  && recordAuditReady
                                  && decisionRecordCommitReady
                                  && plan.IgnoreDecisionPlanReady
                                  && missingEntries.Length == 0;

        return new RuntimeTargetIgnoreDecisionRecordSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            IgnoreDecisionPlanDocumentPath = IgnoreDecisionPlanDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = documentRoot.DocumentRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(plan, recordAuditReady, decisionRecordCommitReady, decisionRecordReady, requiredEntries.Length, missingEntries.Length, errors.Count),
            IgnoreDecisionPlanId = plan.IgnoreDecisionPlanId,
            IgnoreDecisionPlanPosture = plan.OverallPosture,
            IgnoreDecisionPlanReady = plan.IgnoreDecisionPlanReady,
            IgnoreDecisionRequired = plan.IgnoreDecisionRequired,
            DecisionRecordReady = decisionRecordReady,
            RecordAuditReady = recordAuditReady,
            DecisionRecordCommitReady = decisionRecordCommitReady,
            ProductProofCanRemainComplete = plan.ProductProofCanRemainComplete && decisionRecordReady,
            RequiredDecisionEntryCount = requiredEntries.Length,
            RecordedDecisionEntryCount = recordedEntries.Length,
            MissingDecisionEntryCount = missingEntries.Length,
            RecordCount = records.Length,
            CurrentPlanRecordCount = currentPlanRecords.Length,
            ValidCurrentPlanRecordCount = validCurrentPlanRecords.Length,
            StaleRecordCount = staleRecords.Length,
            InvalidRecordCount = invalidRecordPaths.Length,
            MalformedRecordCount = malformedRecordPaths.Length,
            ConflictingDecisionEntryCount = conflicts.Length,
            DirtyDecisionRecordCount = recordCommitRead.DirtyRecordPaths.Count,
            UntrackedDecisionRecordCount = recordCommitRead.UntrackedRecordPaths.Count,
            UncommittedDecisionRecordCount = uncommittedRecordPaths.Length,
            RequiredDecisionEntries = requiredEntries,
            RecordedDecisionEntries = recordedEntries,
            MissingDecisionEntries = missingEntries,
            DecisionRecordIds = validCurrentPlanRecords.Select(static record => record.DecisionRecordId).ToArray(),
            StaleDecisionRecordIds = staleRecords.Select(static record => record.DecisionRecordId).ToArray(),
            InvalidDecisionRecordPaths = invalidRecordPaths,
            MalformedDecisionRecordPaths = malformedRecordPaths,
            ConflictingDecisionEntries = conflicts,
            DirtyDecisionRecordPaths = recordCommitRead.DirtyRecordPaths,
            UntrackedDecisionRecordPaths = recordCommitRead.UntrackedRecordPaths,
            UncommittedDecisionRecordPaths = uncommittedRecordPaths,
            Records = validCurrentPlanRecords,
            BoundaryRules =
            [
                "The decision record is target truth under .ai/runtime/target-ignore-decisions/ and should be committed through commit-plan after it is written.",
                "The record can capture keep_local, add_to_gitignore_after_review, or manual_cleanup_after_review; CARVES does not automatically mutate .gitignore.",
                "A stale record for a previous ignore_decision_plan_id does not satisfy the current plan.",
                "Only well-formed, current-plan, non-conflicting decision records satisfy product proof.",
                "Decision records do not satisfy product proof until their record paths are tracked and clean in the target git work tree.",
                "If the operator chooses add_to_gitignore_after_review, the .gitignore edit remains a separate operator-reviewed target change.",
            ],
            Gaps = BuildGaps(plan, missingEntries, malformedRecordPaths, invalidRecordPaths, conflicts, recordCommitRead, uncommittedRecordPaths, decisionRecordReady).ToArray(),
            Summary = BuildSummary(plan, recordAuditReady, decisionRecordCommitReady, decisionRecordReady, requiredEntries.Length, missingEntries.Length, malformedRecordPaths.Length, invalidRecordPaths.Length, conflicts.Length, uncommittedRecordPaths.Length, errors.Count),
            RecommendedNextAction = BuildRecommendedNextAction(plan, recordAuditReady, decisionRecordCommitReady, decisionRecordReady, requiredEntries.Length, missingEntries.Length, malformedRecordPaths.Length, invalidRecordPaths.Length, conflicts.Length, uncommittedRecordPaths.Length, errors.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not write, append, sort, or rewrite .gitignore.",
                "This surface does not stage, commit, clean, delete, push, release, or retarget anything.",
                "This surface does not treat unreviewed suggested ignore entries, malformed records, stale records, or conflicting records as approved target policy.",
                "This surface does not replace target commit closure, residue policy, ignore-plan readback, product pilot proof, or downstream release policy.",
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
            var record = JsonSerializer.Deserialize<RuntimeTargetIgnoreDecisionRecordEntrySurface>(File.ReadAllText(path), JsonOptions);
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

    private RecordRead ValidateRecordRead(RecordRead read, RuntimeTargetIgnoreDecisionPlanSurface plan)
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

        var knownEntries = plan.SuggestedIgnoreEntries
            .Concat(plan.MissingIgnoreEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedEntries = record.Entries
            .Select(NormalizeEntry)
            .Where(static entry => entry.Length > 0)
            .ToArray();
        if (normalizedEntries.Length == 0)
        {
            gaps.Add("entries_missing");
        }

        foreach (var entry in normalizedEntries.Where(entry => !knownEntries.Contains(entry)))
        {
            gaps.Add($"entry_not_in_current_plan:{entry}");
        }

        return read with { Gaps = gaps };
    }

    private static IEnumerable<string> BuildConflictingDecisionEntries(IReadOnlyList<RuntimeTargetIgnoreDecisionRecordEntrySurface> records)
    {
        return records
            .SelectMany(record => record.Entries.Select(entry => new { Entry = NormalizeEntry(entry), record.Decision }))
            .Where(item => item.Entry.Length > 0)
            .GroupBy(item => item.Entry, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => item.Decision).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(group => group.Key)
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ResolveRequestedEntries(TargetIgnoreDecisionRecordRequest request, RuntimeTargetIgnoreDecisionPlanSurface plan)
    {
        var entries = request.AllEntries
            ? plan.MissingIgnoreEntries.Count > 0
                ? plan.MissingIgnoreEntries
                : plan.SuggestedIgnoreEntries
            : request.Entries;

        return entries
            .Select(NormalizeEntry)
            .Where(static entry => entry.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static entry => entry, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> BuildGaps(
        RuntimeTargetIgnoreDecisionPlanSurface plan,
        IReadOnlyList<string> missingEntries,
        IReadOnlyList<string> malformedRecordPaths,
        IReadOnlyList<string> invalidRecordPaths,
        IReadOnlyList<string> conflictingDecisionEntries,
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
            yield return $"target_ignore_decision_plan:{gap}";
        }

        if (!plan.IgnoreDecisionPlanReady)
        {
            yield return "target_ignore_decision_plan_not_ready";
        }

        foreach (var path in malformedRecordPaths)
        {
            yield return $"target_ignore_decision_record_malformed:{path}";
        }

        foreach (var path in invalidRecordPaths)
        {
            yield return $"target_ignore_decision_record_invalid:{path}";
        }

        foreach (var entry in conflictingDecisionEntries)
        {
            yield return $"target_ignore_decision_record_conflict:{entry}";
        }

        if (!string.IsNullOrWhiteSpace(recordCommitRead.Error))
        {
            yield return $"target_ignore_decision_record_commit_readback:{recordCommitRead.Error}";
        }

        if (!recordCommitRead.GitRepositoryDetected && uncommittedRecordPaths.Count > 0)
        {
            yield return "target_ignore_decision_record_commit_readback_git_unavailable";
        }

        foreach (var path in uncommittedRecordPaths)
        {
            yield return $"target_ignore_decision_record_uncommitted:{path}";
        }

        foreach (var entry in missingEntries)
        {
            yield return $"target_ignore_decision_record_missing:{entry}";
        }
    }

    private static string ResolvePosture(
        RuntimeTargetIgnoreDecisionPlanSurface plan,
        bool recordAuditReady,
        bool decisionRecordCommitReady,
        bool decisionRecordReady,
        int requiredEntryCount,
        int missingEntryCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "target_ignore_decision_record_blocked_by_surface_gaps";
        }

        if (!decisionRecordCommitReady)
        {
            return "target_ignore_decision_record_waiting_for_record_commit";
        }

        if (!plan.IgnoreDecisionPlanReady)
        {
            return "target_ignore_decision_record_waiting_for_ignore_decision_plan";
        }

        if (!recordAuditReady)
        {
            return "target_ignore_decision_record_blocked_by_record_audit";
        }

        if (!plan.IgnoreDecisionRequired || requiredEntryCount == 0)
        {
            return "target_ignore_decision_record_no_decision_required";
        }

        return decisionRecordReady && missingEntryCount == 0
            ? "target_ignore_decision_record_ready"
            : "target_ignore_decision_record_waiting_for_operator_decision";
    }

    private static string BuildSummary(
        RuntimeTargetIgnoreDecisionPlanSurface plan,
        bool recordAuditReady,
        bool decisionRecordCommitReady,
        bool decisionRecordReady,
        int requiredEntryCount,
        int missingEntryCount,
        int malformedRecordCount,
        int invalidRecordCount,
        int conflictingDecisionEntryCount,
        int uncommittedRecordCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Target ignore decision record cannot be trusted until required documents and ignore-plan gaps are resolved.";
        }

        if (!plan.IgnoreDecisionPlanReady)
        {
            return plan.Summary;
        }

        if (!recordAuditReady)
        {
            return $"Target ignore decision record audit is blocked by malformed={malformedRecordCount}, invalid={invalidRecordCount}, conflicting_entries={conflictingDecisionEntryCount}.";
        }

        if (!decisionRecordCommitReady)
        {
            return $"Target ignore decision record commit readback is blocked by uncommitted_record_paths={uncommittedRecordCount}.";
        }

        if (!plan.IgnoreDecisionRequired || requiredEntryCount == 0)
        {
            return "No missing ignore entries require a durable operator decision record.";
        }

        return decisionRecordReady
            ? "Operator ignore decisions are durably recorded for every missing ignore entry in the current plan."
            : $"Operator ignore decisions are missing for {missingEntryCount} current-plan ignore entr{(missingEntryCount == 1 ? "y" : "ies")}.";
    }

    private static string BuildRecommendedNextAction(
        RuntimeTargetIgnoreDecisionPlanSurface plan,
        bool recordAuditReady,
        bool decisionRecordCommitReady,
        bool decisionRecordReady,
        int requiredEntryCount,
        int missingEntryCount,
        int malformedRecordCount,
        int invalidRecordCount,
        int conflictingDecisionEntryCount,
        int uncommittedRecordCount,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Restore target ignore decision record docs and ignore-plan inputs, then rerun carves pilot ignore-record --json.";
        }

        if (!plan.IgnoreDecisionPlanReady)
        {
            return "Run carves pilot ignore-plan --json and resolve its gaps before recording a durable operator decision.";
        }

        if (!recordAuditReady)
        {
            return $"Review .ai/runtime/target-ignore-decisions/ records; resolve malformed={malformedRecordCount}, invalid={invalidRecordCount}, conflicting_entries={conflictingDecisionEntryCount}, then rerun carves pilot ignore-record --json.";
        }

        if (!decisionRecordCommitReady)
        {
            return $"Run carves pilot commit-plan --json; stage and commit .ai/runtime/target-ignore-decisions/ records, then rerun closure, residue, ignore-plan, ignore-record, and proof. Uncommitted records={uncommittedRecordCount}.";
        }

        if (!plan.IgnoreDecisionRequired || requiredEntryCount == 0)
        {
            return "target_ignore_decision_record_readback_clean";
        }

        if (decisionRecordReady)
        {
            return "Run carves pilot commit-plan --json; commit the decision record through an operator-reviewed target commit, then rerun closure, residue, ignore-plan, ignore-record, and proof.";
        }

        return missingEntryCount > 0
            ? "Run carves pilot record-ignore-decision keep_local --all --reason <operator-reviewed-reason>, or record add_to_gitignore_after_review/manual_cleanup_after_review after review."
            : "Rerun carves pilot ignore-record --json.";
    }

    private static string NormalizeDecision(string decision)
    {
        return decision.Trim().Replace('-', '_').ToLowerInvariant();
    }

    private static string NormalizeEntry(string entry)
    {
        var normalized = entry.Replace('\\', '/').Trim();
        return normalized.StartsWith("./", StringComparison.Ordinal)
            ? normalized[2..]
            : normalized;
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
        RuntimeTargetIgnoreDecisionRecordEntrySurface? Record,
        IReadOnlyList<string> Gaps);

    private sealed record RecordCommitRead(
        bool GitRepositoryDetected,
        IReadOnlyList<string> DirtyRecordPaths,
        IReadOnlyList<string> UntrackedRecordPaths,
        string Error);

    private sealed record GitCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
