using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    private static JsonObject BuildSelectedPackNode(RuntimePackExecutionAttribution selectedPack)
    {
        return new JsonObject
        {
            ["pack_id"] = selectedPack.PackId,
            ["pack_version"] = selectedPack.PackVersion,
            ["channel"] = selectedPack.Channel,
            ["artifact_ref"] = selectedPack.ArtifactRef,
            ["policy_preset"] = selectedPack.PolicyPreset,
            ["gate_preset"] = selectedPack.GatePreset,
            ["validator_profile"] = selectedPack.ValidatorProfile,
            ["routing_profile"] = selectedPack.RoutingProfile,
            ["environment_profile"] = selectedPack.EnvironmentProfile,
            ["selection_mode"] = selectedPack.SelectionMode,
            ["selected_at_utc"] = selectedPack.SelectedAtUtc,
            ["declarative_contribution"] = selectedPack.DeclarativeContribution is null
                ? null
                : JsonSerializer.SerializeToNode(selectedPack.DeclarativeContribution, JsonOptions),
        };
    }

    private JsonObject? BuildRuntimePackCommandAdmissionNode(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null
            || !metadata.TryGetValue("runtime_pack_command_admission_paths", out var serializedPaths)
            || string.IsNullOrWhiteSpace(serializedPaths))
        {
            return null;
        }

        var decisionPaths = serializedPaths
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (decisionPaths.Length == 0)
        {
            return null;
        }

        var decisions = decisionPaths
            .Select(path => new { Path = path, Decision = TryLoadRuntimePackCommandAdmissionDecision(path) })
            .Where(item => item.Decision is not null)
            .Select(item => new JsonObject
            {
                ["path"] = item.Path,
                ["decision_id"] = item.Decision!.DecisionId,
                ["recipe_id"] = item.Decision.CommandRef.RecipeId,
                ["command_id"] = item.Decision.CommandRef.CommandId,
                ["requested_kind"] = item.Decision.RequestedKind,
                ["verdict"] = item.Decision.Decision.Verdict,
                ["risk_level"] = item.Decision.Decision.RiskLevel,
                ["executable"] = item.Decision.Command.Executable,
                ["args"] = ToJsonArray(item.Decision.Command.Args),
                ["required"] = item.Decision.Command.Required,
            })
            .ToArray();

        return new JsonObject
        {
            ["summary"] = metadata.GetValueOrDefault("runtime_pack_verification_summary"),
            ["recipe_ids"] = ToJsonArray((metadata.GetValueOrDefault("runtime_pack_verification_recipe_ids") ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
            ["decision_ids"] = ToJsonArray((metadata.GetValueOrDefault("runtime_pack_command_admission_decision_ids") ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
            ["decision_paths"] = ToJsonArray(decisionPaths),
            ["admitted_count"] = ParseIntMetadata(metadata, "runtime_pack_verification_admitted_count"),
            ["elevated_count"] = ParseIntMetadata(metadata, "runtime_pack_verification_elevated_count"),
            ["blocked_count"] = ParseIntMetadata(metadata, "runtime_pack_verification_blocked_count"),
            ["rejected_count"] = ParseIntMetadata(metadata, "runtime_pack_verification_rejected_count"),
            ["decisions"] = new JsonArray(decisions),
        };
    }

    private JsonObject? BuildRuntimePackReviewRubricNode()
    {
        var projection = new RuntimePackReviewRubricProjectionService(services.Paths.RepoRoot, services.ArtifactRepository)
            .TryBuildCurrentProjection();
        if (projection is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToNode(projection, JsonOptions)?.AsObject();
    }

    private RuntimePackCommandAdmissionDecision? TryLoadRuntimePackCommandAdmissionDecision(string path)
    {
        try
        {
            var fullPath = ResolveRuntimeSurfacePath(path);
            if (!File.Exists(fullPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<RuntimePackCommandAdmissionDecision>(
                File.ReadAllText(fullPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
        }
        catch
        {
            return null;
        }
    }

    private string ResolveRuntimeSurfacePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(services.Paths.RepoRoot, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static int? ParseIntMetadata(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private JsonArray BuildRunDrilldown(int take)
    {
        var graph = services.TaskGraphService.Load();
        var runs = graph.ListTasks()
            .SelectMany(task => services.ExecutionRunService.ListRuns(task.TaskId))
            .OrderByDescending(run => run.EndedAtUtc ?? run.StartedAtUtc ?? run.CreatedAtUtc)
            .Take(Math.Max(1, take))
            .ToArray();

        return new JsonArray(runs.Select(run =>
        {
            var currentStep = run.Steps.Count == 0
                ? null
                : run.Steps[Math.Clamp(run.CurrentStepIndex, 0, run.Steps.Count - 1)];
            var resultEnvelope = TryLoadJson<ResultEnvelope>(run.ResultEnvelopePath);
            var boundary = TryLoadJson<ExecutionBoundaryViolation>(run.BoundaryViolationPath);
            var replan = TryLoadJson<ExecutionBoundaryReplanRequest>(run.ReplanArtifactPath);

            return (JsonNode)new JsonObject
            {
                ["run_id"] = run.RunId,
                ["task_id"] = run.TaskId,
                ["status"] = run.Status.ToString(),
                ["goal"] = run.Goal,
                ["current_step_index"] = run.CurrentStepIndex,
                ["current_step_title"] = currentStep?.Title ?? "(none)",
                ["current_step_status"] = currentStep?.Status.ToString() ?? "(none)",
                ["inspect_command"] = $"inspect run {run.RunId}",
                ["result_status"] = resultEnvelope?.Status,
                ["boundary_reason"] = boundary?.Reason.ToString(),
                ["replan_strategy"] = replan?.Strategy.ToString(),
            };
        }).ToArray());
    }

    private JsonObject BuildCardMetrics(TaskGraph graph, IReadOnlyList<CardDraftRecord> cardDrafts)
    {
        var completed = graph.CompletedTaskIds();
        var statuses = graph.Cards
            .Select(cardId => ResolveCardStatus(graph.ListTasks().Where(task => string.Equals(task.CardId, cardId, StringComparison.Ordinal)).ToArray(), completed))
            .ToArray();
        return new JsonObject
        {
            ["total"] = graph.Cards.Concat(cardDrafts.Select(item => item.CardId)).Distinct(StringComparer.Ordinal).Count(),
            ["draft"] = cardDrafts.Count(item => item.Status == CardLifecycleState.Draft),
            ["reviewed"] = cardDrafts.Count(item => item.Status == CardLifecycleState.Reviewed),
            ["approved"] = graph.Cards.Count + cardDrafts.Count(item => item.Status == CardLifecycleState.Approved && !graph.Cards.Contains(item.CardId, StringComparer.Ordinal)),
            ["rejected"] = cardDrafts.Count(item => item.Status == CardLifecycleState.Rejected),
            ["archived"] = cardDrafts.Count(item => item.Status == CardLifecycleState.Archived),
            ["undefined"] = statuses.Count(status => string.Equals(status, "undefined", StringComparison.Ordinal)),
            ["planning"] = statuses.Count(status => string.Equals(status, "planning", StringComparison.Ordinal)),
            ["ready"] = statuses.Count(status => string.Equals(status, "ready", StringComparison.Ordinal)),
            ["blocked"] = statuses.Count(status => string.Equals(status, "blocked", StringComparison.Ordinal)),
            ["completed"] = statuses.Count(status => string.Equals(status, "completed", StringComparison.Ordinal)),
        };
    }

    private static JsonObject BuildBlockedReasonCounts(TaskGraph graph)
    {
        var completed = graph.CompletedTaskIds();
        var buckets = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["dependency"] = 0,
            ["approval"] = 0,
            ["human_review"] = 0,
            ["environment"] = 0,
            ["policy"] = 0,
            ["other"] = 0,
        };

        foreach (var task in graph.ListTasks().Where(task => task.Status is DomainTaskStatus.Blocked or DomainTaskStatus.Review or DomainTaskStatus.Pending or DomainTaskStatus.ApprovalWait))
        {
            var reason = ResolveTaskBlockedReason(task, task.Dependencies.Where(dependency => !completed.Contains(dependency)).ToArray()).ToLowerInvariant();
            if (reason.Contains("dependencies", StringComparison.Ordinal))
            {
                buckets["dependency"] += 1;
            }
            else if (reason.Contains("approval", StringComparison.Ordinal))
            {
                buckets["approval"] += 1;
            }
            else if (reason.Contains("review", StringComparison.Ordinal))
            {
                buckets["human_review"] += 1;
            }
            else if (reason.Contains("environment", StringComparison.Ordinal) || reason.Contains("file lock", StringComparison.Ordinal) || reason.Contains("timeout", StringComparison.Ordinal))
            {
                buckets["environment"] += 1;
            }
            else if (reason.Contains("policy", StringComparison.Ordinal) || reason.Contains("governance", StringComparison.Ordinal))
            {
                buckets["policy"] += 1;
            }
            else if (!string.Equals(reason, "(none)", StringComparison.Ordinal))
            {
                buckets["other"] += 1;
            }
        }

        return new JsonObject(buckets.Select(pair => new KeyValuePair<string, JsonNode?>(pair.Key, pair.Value)));
    }
}
