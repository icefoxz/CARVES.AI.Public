using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Workers;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeWorkerExecutionAuditService
{
    private const int DefaultRecentEntryLimit = 10;
    private const int MaxQueryLimit = 50;
    private const int FallbackScanLimit = 200;

    private readonly ControlPlanePaths paths;
    private readonly IWorkerExecutionAuditReadModel? readModel;

    public RuntimeWorkerExecutionAuditService(ControlPlanePaths paths, IWorkerExecutionAuditReadModel? readModel)
    {
        this.paths = paths;
        this.readModel = readModel;
    }

    public RuntimeWorkerExecutionAuditSurface Build(string? queryText = null, int recentEntryLimit = DefaultRecentEntryLimit)
    {
        var storagePath = readModel is null
            ? ToRepoRelative(Path.Combine(paths.RuntimeRoot, "audit.db"))
            : ToRepoRelative(readModel.StoragePath);
        var query = ParseQuery(queryText, recentEntryLimit);

        if (readModel is null)
        {
            return BuildUnavailable(storagePath, query, "not_configured", "Worker execution audit read model is not configured.");
        }

        try
        {
            var summary = readModel.GetSummary();
            var queryResult = QueryReadModel(readModel, query);
            var recentEntries = queryResult.Entries
                .Select(MapEntry)
                .ToArray();
            var status = readModel.StorageExists ? "available" : "not_initialized";

            return new RuntimeWorkerExecutionAuditSurface
            {
                Summary = BuildSummary(summary, queryResult, readModel.StorageExists),
                StoragePath = storagePath,
                ReadModelConfigured = true,
                StorageExists = readModel.StorageExists,
                Available = true,
                AvailabilityStatus = status,
                Query = MapQuery(queryResult),
                Counts = MapSummary(summary),
                QueryCounts = MapSummary(queryResult.Summary),
                RecentEntries = recentEntries,
                SupportedQueryFields = BuildSupportedQueryFields(),
                QueryExamples = BuildQueryExamples(),
                Notes = BuildNotes(),
            };
        }
        catch (Exception ex)
        {
            return BuildUnavailable(storagePath, query, "unavailable", ex.Message, configured: true, storageExists: readModel.StorageExists);
        }
    }

    private RuntimeWorkerExecutionAuditSurface BuildUnavailable(
        string storagePath,
        WorkerExecutionAuditQuery query,
        string availabilityStatus,
        string summary,
        bool configured = false,
        bool storageExists = false)
    {
        return new RuntimeWorkerExecutionAuditSurface
        {
            Summary = summary,
            StoragePath = storagePath,
            ReadModelConfigured = configured,
            StorageExists = storageExists,
            Available = false,
            AvailabilityStatus = availabilityStatus,
            Query = MapQuery(new WorkerExecutionAuditQueryResult
            {
                Query = query,
                Summary = new WorkerExecutionAuditSummary(),
                Entries = [],
                QueryMode = "unavailable",
            }),
            Counts = new RuntimeWorkerExecutionAuditSummarySurface(),
            QueryCounts = new RuntimeWorkerExecutionAuditSummarySurface(),
            RecentEntries = [],
            SupportedQueryFields = BuildSupportedQueryFields(),
            QueryExamples = BuildQueryExamples(),
            Notes = BuildNotes(),
        };
    }

    private static WorkerExecutionAuditQueryResult QueryReadModel(
        IWorkerExecutionAuditReadModel readModel,
        WorkerExecutionAuditQuery query)
    {
        if (readModel is IWorkerExecutionAuditQueryReadModel queryReadModel)
        {
            return queryReadModel.Query(query);
        }

        var entries = readModel.QueryRecent(FallbackScanLimit)
            .Where(entry => Matches(entry, query))
            .Take(query.Limit)
            .ToArray();
        return new WorkerExecutionAuditQueryResult
        {
            Query = query,
            Summary = BuildInMemorySummary(readModel.QueryRecent(FallbackScanLimit).Where(entry => Matches(entry, query))),
            Entries = entries,
            QueryMode = "recent_scan_fallback",
        };
    }

    private static RuntimeWorkerExecutionAuditSummarySurface MapSummary(WorkerExecutionAuditSummary summary)
    {
        return new RuntimeWorkerExecutionAuditSummarySurface
        {
            TotalExecutions = summary.TotalExecutions,
            SucceededExecutions = summary.SucceededExecutions,
            FailedExecutions = summary.FailedExecutions,
            BlockedExecutions = summary.BlockedExecutions,
            SkippedExecutions = summary.SkippedExecutions,
            ApprovalWaitExecutions = summary.ApprovalWaitExecutions,
            SafetyBlockedExecutions = summary.SafetyBlockedExecutions,
            PermissionRequestCount = summary.PermissionRequestCount,
            ChangedFilesCount = summary.ChangedFilesCount,
            LatestOccurrenceUtc = summary.LatestOccurrenceUtc,
            LatestTaskId = summary.LatestTaskId,
        };
    }

    private static RuntimeWorkerExecutionAuditQuerySurface MapQuery(WorkerExecutionAuditQueryResult result)
    {
        var query = result.Query;
        return new RuntimeWorkerExecutionAuditQuerySurface
        {
            RequestedQuery = query.RequestedQuery,
            EffectiveQuery = query.EffectiveQuery,
            QueryMode = result.QueryMode,
            Filtered = query.HasFilters,
            Limit = query.Limit,
            TaskId = query.TaskId,
            RunId = query.RunId,
            Status = query.Status,
            EventType = query.EventType,
            BackendId = query.BackendId,
            ProviderId = query.ProviderId,
            SafetyAllowed = query.SafetyAllowed,
            UnsupportedTerms = query.UnsupportedTerms,
            ReasonCodes = query.ReasonCodes,
        };
    }

    private static RuntimeWorkerExecutionAuditEntrySurface MapEntry(WorkerExecutionAuditEntry entry)
    {
        return new RuntimeWorkerExecutionAuditEntrySurface
        {
            SequenceId = entry.SequenceId,
            TaskId = entry.TaskId,
            RunId = entry.RunId,
            EventType = entry.EventType,
            BackendId = entry.BackendId,
            ProviderId = entry.ProviderId,
            AdapterId = entry.AdapterId,
            ProtocolFamily = entry.ProtocolFamily,
            Status = entry.Status,
            FailureKind = entry.FailureKind,
            FailureLayer = entry.FailureLayer,
            ChangedFilesCount = entry.ChangedFilesCount,
            ObservedChangedFilesCount = entry.ObservedChangedFilesCount,
            PermissionRequestCount = entry.PermissionRequestCount,
            InputTokens = entry.InputTokens,
            OutputTokens = entry.OutputTokens,
            ProviderLatencyMs = entry.ProviderLatencyMs,
            SafetyOutcome = entry.SafetyOutcome,
            SafetyAllowed = entry.SafetyAllowed,
            OccurredAtUtc = entry.OccurredAtUtc,
            Summary = entry.Summary,
        };
    }

    private static string BuildSummary(
        WorkerExecutionAuditSummary summary,
        WorkerExecutionAuditQueryResult queryResult,
        bool storageExists)
    {
        if (!storageExists)
        {
            return "Worker execution audit read model is configured but has not been initialized by a worker execution append.";
        }

        var query = queryResult.Query;
        var baseSummary = $"Worker execution audit read model contains {summary.TotalExecutions} append-only entr{(summary.TotalExecutions == 1 ? "y" : "ies")}; succeeded={summary.SucceededExecutions}, failed={summary.FailedExecutions}, blocked={summary.BlockedExecutions}.";
        return query.HasFilters
            ? $"{baseSummary} Query '{query.EffectiveQuery}' matched {queryResult.Summary.TotalExecutions} entr{(queryResult.Summary.TotalExecutions == 1 ? "y" : "ies")}."
            : baseSummary;
    }

    private static WorkerExecutionAuditQuery ParseQuery(string? queryText, int defaultLimit)
    {
        var requested = string.IsNullOrWhiteSpace(queryText) ? "recent" : queryText.Trim();
        var limit = ClampLimit(defaultLimit);
        string? taskId = null;
        string? runId = null;
        string? status = null;
        string? eventType = null;
        string? backendId = null;
        string? providerId = null;
        bool? safetyAllowed = null;
        var unsupported = new List<string>();
        var reasonCodes = new List<string>();

        foreach (var rawTerm in SplitQueryTerms(queryText))
        {
            var term = rawTerm.Trim();
            if (term.Length == 0 || string.Equals(term, "recent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TrySplitTerm(term, out var key, out var value))
            {
                if (term.StartsWith("T-", StringComparison.OrdinalIgnoreCase))
                {
                    taskId = term;
                }
                else if (TryNormalizeStatus(term, out var normalizedStatus))
                {
                    status = normalizedStatus;
                }
                else if (string.Equals(term, "safety-blocked", StringComparison.OrdinalIgnoreCase))
                {
                    safetyAllowed = false;
                }
                else
                {
                    unsupported.Add(term);
                }

                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "task":
                case "task-id":
                case "task_id":
                    taskId = value;
                    break;
                case "run":
                case "run-id":
                case "run_id":
                    runId = value;
                    break;
                case "status":
                    if (TryNormalizeStatus(value, out var normalizedStatus))
                    {
                        status = normalizedStatus;
                    }
                    else
                    {
                        unsupported.Add(term);
                    }

                    break;
                case "event":
                case "event-type":
                case "event_type":
                    eventType = NormalizeEventType(value);
                    break;
                case "backend":
                case "backend-id":
                case "backend_id":
                    backendId = value;
                    break;
                case "provider":
                case "provider-id":
                case "provider_id":
                    providerId = value;
                    break;
                case "safety":
                case "safety-allowed":
                case "safety_allowed":
                    if (TryParseSafety(value, out var parsedSafetyAllowed))
                    {
                        safetyAllowed = parsedSafetyAllowed;
                    }
                    else
                    {
                        unsupported.Add(term);
                    }

                    break;
                case "limit":
                case "take":
                    if (int.TryParse(value, out var parsedLimit))
                    {
                        limit = ClampLimit(parsedLimit);
                        if (parsedLimit != limit)
                        {
                            reasonCodes.Add("limit_clamped");
                        }
                    }
                    else
                    {
                        unsupported.Add(term);
                    }

                    break;
                default:
                    unsupported.Add(term);
                    break;
            }
        }

        if (unsupported.Count > 0)
        {
            reasonCodes.Add("unsupported_terms_ignored");
        }

        reasonCodes.Add("append_only_read_model");
        var query = new WorkerExecutionAuditQuery
        {
            RequestedQuery = requested,
            Limit = limit,
            TaskId = taskId,
            RunId = runId,
            Status = status,
            EventType = eventType,
            BackendId = backendId,
            ProviderId = providerId,
            SafetyAllowed = safetyAllowed,
            UnsupportedTerms = unsupported.Distinct(StringComparer.Ordinal).ToArray(),
            ReasonCodes = reasonCodes.Distinct(StringComparer.Ordinal).ToArray(),
        };

        return query with { EffectiveQuery = BuildEffectiveQuery(query) };
    }

    private static IEnumerable<string> SplitQueryTerms(string? queryText)
    {
        return string.IsNullOrWhiteSpace(queryText)
            ? []
            : queryText.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TrySplitTerm(string term, out string key, out string value)
    {
        var separatorIndex = term.IndexOf(':');
        if (separatorIndex < 0)
        {
            separatorIndex = term.IndexOf('=');
        }

        if (separatorIndex <= 0 || separatorIndex >= term.Length - 1)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = term[..separatorIndex].Trim();
        value = term[(separatorIndex + 1)..].Trim();
        return key.Length > 0 && value.Length > 0;
    }

    private static bool TryNormalizeStatus(string value, out string status)
    {
        status = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant() switch
        {
            "success" or "succeeded" => "Succeeded",
            "fail" or "failed" => "Failed",
            "block" or "blocked" => "Blocked",
            "skip" or "skipped" => "Skipped",
            "approvalwait" => "ApprovalWait",
            _ => string.Empty,
        };
        return status.Length > 0;
    }

    private static string NormalizeEventType(string value)
    {
        return value.Trim().Replace('-', '_').ToLowerInvariant();
    }

    private static bool TryParseSafety(string value, out bool allowed)
    {
        switch (value.Trim().Replace("_", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant())
        {
            case "allow":
            case "allowed":
            case "true":
            case "pass":
            case "passed":
                allowed = true;
                return true;
            case "block":
            case "blocked":
            case "deny":
            case "denied":
            case "false":
            case "fail":
            case "failed":
                allowed = false;
                return true;
            default:
                allowed = false;
                return false;
        }
    }

    private static int ClampLimit(int limit)
    {
        return Math.Clamp(limit, 1, MaxQueryLimit);
    }

    private static string BuildEffectiveQuery(WorkerExecutionAuditQuery query)
    {
        var terms = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.TaskId))
        {
            terms.Add($"task:{query.TaskId}");
        }

        if (!string.IsNullOrWhiteSpace(query.RunId))
        {
            terms.Add($"run:{query.RunId}");
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            terms.Add($"status:{query.Status}");
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            terms.Add($"event:{query.EventType}");
        }

        if (!string.IsNullOrWhiteSpace(query.BackendId))
        {
            terms.Add($"backend:{query.BackendId}");
        }

        if (!string.IsNullOrWhiteSpace(query.ProviderId))
        {
            terms.Add($"provider:{query.ProviderId}");
        }

        if (query.SafetyAllowed is not null)
        {
            terms.Add($"safety:{(query.SafetyAllowed.Value ? "allowed" : "blocked")}");
        }

        if (terms.Count == 0)
        {
            terms.Add("recent");
        }

        terms.Add($"limit:{query.Limit}");
        return string.Join(",", terms);
    }

    private static bool Matches(WorkerExecutionAuditEntry entry, WorkerExecutionAuditQuery query)
    {
        return Matches(entry.TaskId, query.TaskId)
               && Matches(entry.RunId, query.RunId)
               && Matches(entry.Status, query.Status)
               && Matches(entry.EventType, query.EventType)
               && Matches(entry.BackendId, query.BackendId)
               && Matches(entry.ProviderId, query.ProviderId)
               && (query.SafetyAllowed is null || entry.SafetyAllowed == query.SafetyAllowed.Value);
    }

    private static bool Matches(string actual, string? expected)
    {
        return string.IsNullOrWhiteSpace(expected)
               || string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkerExecutionAuditSummary BuildInMemorySummary(IEnumerable<WorkerExecutionAuditEntry> entries)
    {
        var snapshot = entries.ToArray();
        var latest = snapshot.OrderByDescending(static entry => entry.SequenceId ?? 0).FirstOrDefault();
        return new WorkerExecutionAuditSummary
        {
            TotalExecutions = snapshot.Length,
            SucceededExecutions = snapshot.Count(static entry => string.Equals(entry.Status, "Succeeded", StringComparison.Ordinal)),
            FailedExecutions = snapshot.Count(static entry => string.Equals(entry.Status, "Failed", StringComparison.Ordinal)),
            BlockedExecutions = snapshot.Count(static entry => string.Equals(entry.Status, "Blocked", StringComparison.Ordinal)),
            SkippedExecutions = snapshot.Count(static entry => string.Equals(entry.Status, "Skipped", StringComparison.Ordinal)),
            ApprovalWaitExecutions = snapshot.Count(static entry => string.Equals(entry.Status, "ApprovalWait", StringComparison.Ordinal)),
            SafetyBlockedExecutions = snapshot.Count(static entry => !entry.SafetyAllowed),
            PermissionRequestCount = snapshot.Sum(static entry => entry.PermissionRequestCount),
            ChangedFilesCount = snapshot.Sum(static entry => entry.ChangedFilesCount),
            LatestTaskId = latest?.TaskId,
            LatestOccurrenceUtc = latest?.OccurredAtUtc,
        };
    }

    private static IReadOnlyList<string> BuildSupportedQueryFields()
    {
        return
        [
            "task:<task-id>",
            "run:<run-id>",
            "status:<Succeeded|Failed|Blocked|Skipped|ApprovalWait>",
            "event:<completed|failed|skipped|approval_wait>",
            "backend:<backend-id>",
            "provider:<provider-id>",
            "safety:<allowed|blocked>",
            $"limit:<1-{MaxQueryLimit}>",
        ];
    }

    private static IReadOnlyList<string> BuildQueryExamples()
    {
        return
        [
            "api runtime-worker-execution-audit status:Failed",
            "api runtime-worker-execution-audit task:T-CARD-724-001",
            "api runtime-worker-execution-audit backend:codex_cli,limit:20",
            "api runtime-worker-execution-audit safety:blocked",
        ];
    }

    private static IReadOnlyList<string> BuildNotes()
    {
        return
        [
            "SQLite is a non-canonical operational read model; .ai/tasks and .ai/memory/execution remain canonical truth.",
            "Rows are append-only execution audit summaries in this phase; no mutation, deletion, or compaction command is exposed.",
            "Full prompts, full responses, artifacts, task truth, review truth, and policy truth are not stored in this read model.",
            "Query filters are advisory read filters over compact audit rows; unsupported query terms are ignored and reported."
        ];
    }

    private string ToRepoRelative(string path)
    {
        var relative = Path.GetRelativePath(paths.RepoRoot, path);
        return relative.Replace('\\', '/');
    }
}
