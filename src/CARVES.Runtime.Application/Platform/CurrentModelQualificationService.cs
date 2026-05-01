using System.Text.Json;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class CurrentModelQualificationService
{
    private readonly ICurrentModelQualificationRepository repository;
    private readonly IRuntimeRoutingProfileRepository routingProfileRepository;
    private readonly IQualificationLaneExecutor laneExecutor;

    public CurrentModelQualificationService(
        ICurrentModelQualificationRepository repository,
        IRuntimeRoutingProfileRepository routingProfileRepository,
        IQualificationLaneExecutor laneExecutor)
    {
        this.repository = repository;
        this.routingProfileRepository = routingProfileRepository;
        this.laneExecutor = laneExecutor;
    }

    public ModelQualificationMatrix LoadOrCreateMatrix()
    {
        var matrix = repository.LoadMatrix();
        var connectedDefault = CreateDefaultMatrix();
        if (matrix is not null)
        {
            if (ShouldRefreshDynamicConnectedMatrix(matrix, connectedDefault))
            {
                repository.SaveMatrix(connectedDefault);
                return connectedDefault;
            }

            return matrix;
        }

        matrix = connectedDefault;
        repository.SaveMatrix(matrix);
        return matrix;
    }

    public ModelQualificationRunLedger? LoadLatestRun()
    {
        return repository.LoadLatestRun();
    }

    public ModelQualificationCandidateProfile? LoadCandidate()
    {
        return repository.LoadCandidate();
    }

    public ModelQualificationRunLedger Run(int? attemptsOverride = null)
    {
        var matrix = LoadOrCreateMatrix();
        var results = new List<ModelQualificationResult>();
        var generatedAt = DateTimeOffset.UtcNow;
        var runId = $"qual-run-{Guid.NewGuid():N}";

        foreach (var lane in matrix.Lanes.OrderBy(item => item.LaneId, StringComparer.Ordinal))
        {
            foreach (var qualificationCase in matrix.Cases.OrderBy(item => item.CaseId, StringComparer.Ordinal))
            {
                var attempts = attemptsOverride ?? qualificationCase.Attempts ?? matrix.DefaultAttempts;
                for (var attempt = 1; attempt <= Math.Max(1, attempts); attempt++)
                {
                    var execution = laneExecutor.Execute(lane, qualificationCase, attempt);
                    var formatValid = EvaluateFormatValidity(qualificationCase, execution.ResponsePreview ?? execution.Rationale ?? execution.Summary);
                    var qualityScore = EvaluateQualityScore(qualificationCase, execution, formatValid);
                    results.Add(new ModelQualificationResult
                    {
                        RunId = runId,
                        LaneId = lane.LaneId,
                        CaseId = qualificationCase.CaseId,
                        Attempt = attempt,
                        ProviderId = lane.ProviderId,
                        BackendId = lane.BackendId,
                        RequestFamily = lane.RequestFamily,
                        Model = execution.Model,
                        RoutingIntent = qualificationCase.RoutingIntent,
                        ModuleId = qualificationCase.ModuleId,
                        LatencyMs = execution.ProviderLatencyMs ?? (long)Math.Max(0, (execution.CompletedAt - execution.StartedAt).TotalMilliseconds),
                        Success = execution.Succeeded,
                        HttpStatus = execution.ProviderStatusCode,
                        FormatValid = formatValid,
                        QualityScore = qualityScore,
                        TokensInput = execution.InputTokens,
                        TokensOutput = execution.OutputTokens,
                        ErrorType = execution.Succeeded ? null : execution.FailureLayer.ToString(),
                        FailureKind = execution.FailureKind,
                        Notes = BuildNotes(qualificationCase, execution, formatValid),
                        RouteGroup = lane.RouteGroup,
                        ObservedVariance = lane.ObservedVariance,
                        RequestId = execution.RequestId,
                    });
                }
            }
        }

        var ledger = new ModelQualificationRunLedger
        {
            RunId = runId,
            MatrixId = matrix.MatrixId,
            GeneratedAt = generatedAt,
            Summary = $"Qualified {matrix.Lanes.Length} lanes across {matrix.Cases.Length} routing intents.",
            Results = results.ToArray(),
        };

        repository.SaveLatestRun(ledger);
        return ledger;
    }

    public ModelQualificationCandidateProfile MaterializeCandidate()
    {
        var matrix = LoadOrCreateMatrix();
        var run = repository.LoadLatestRun() ?? throw new InvalidOperationException("No qualification run ledger exists.");
        var laneById = matrix.Lanes.ToDictionary(item => item.LaneId, StringComparer.Ordinal);
        var intentGroups = run.Results
            .GroupBy(item => new IntentGroupKey(item.RoutingIntent, item.ModuleId));

        var intentSummaries = new List<ModelQualificationIntentSummary>();
        var rules = new List<RuntimeRoutingRule>();

        foreach (var group in intentGroups.OrderBy(item => item.Key.RoutingIntent, StringComparer.Ordinal).ThenBy(item => item.Key.ModuleId, StringComparer.Ordinal))
        {
            var scores = group
                .GroupBy(item => item.LaneId, StringComparer.Ordinal)
                .Select(laneGroup => BuildLaneScore(laneGroup, laneById[laneGroup.Key]))
                .OrderByDescending(item => RankLane(item))
                .ThenBy(item => item.AverageLatencyMs)
                .ThenBy(item => item.LaneId, StringComparer.Ordinal)
                .ToArray();

            var preferred = scores.FirstOrDefault(score => score.SuccessRate > 0);
            if (preferred is null)
            {
                continue;
            }

            var fallbacks = scores
                .Where(score => !string.Equals(score.LaneId, preferred.LaneId, StringComparison.Ordinal) && score.SuccessRate > 0)
                .Take(2)
                .ToArray();
            var rejects = scores
                .Where(score => score.SuccessRate <= 0)
                .Select(score => score.LaneId)
                .ToArray();

            intentSummaries.Add(new ModelQualificationIntentSummary
            {
                RoutingIntent = group.Key.RoutingIntent,
                ModuleId = group.Key.ModuleId,
                PreferredLaneId = preferred.LaneId,
                FallbackLaneIds = fallbacks.Select(item => item.LaneId).ToArray(),
                RejectLaneIds = rejects,
                LaneScores = scores,
            });

            var preferredLane = laneById[preferred.LaneId];
            var fallbackRoutes = fallbacks.Select(item => ToRoute(laneById[item.LaneId])).ToArray();
            rules.Add(new RuntimeRoutingRule
            {
                RuleId = BuildRuleId(group.Key.RoutingIntent, group.Key.ModuleId),
                RoutingIntent = group.Key.RoutingIntent,
                ModuleId = group.Key.ModuleId,
                Summary = $"Preferred {preferred.LaneId} from qualification run {run.RunId}.",
                PreferredRoute = ToRoute(preferredLane),
                FallbackRoutes = fallbackRoutes,
            });
        }

        var candidateId = $"routing-candidate-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var candidate = new ModelQualificationCandidateProfile
        {
            CandidateId = candidateId,
            MatrixId = matrix.MatrixId,
            SourceRunId = run.RunId,
            Summary = $"Candidate routing profile derived from qualification run {run.RunId}.",
            Profile = new RuntimeRoutingProfile
            {
                ProfileId = $"candidate-{matrix.MatrixId}",
                Version = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss"),
                SourceQualificationId = candidateId,
                Summary = $"Candidate routing profile from qualification matrix {matrix.MatrixId}.",
                CreatedAt = DateTimeOffset.UtcNow,
                Rules = rules.ToArray(),
            },
            Intents = intentSummaries.ToArray(),
        };

        repository.SaveCandidate(candidate);
        return candidate;
    }

    public RuntimeRoutingProfile PromoteCandidate(string? candidateId = null)
    {
        var candidate = repository.LoadCandidate() ?? throw new InvalidOperationException("No candidate routing profile exists.");
        if (!string.IsNullOrWhiteSpace(candidateId) && !string.Equals(candidate.CandidateId, candidateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Candidate '{candidateId}' does not match the current candidate '{candidate.CandidateId}'.");
        }

        var active = new RuntimeRoutingProfile
        {
            ProfileId = candidate.Profile.ProfileId,
            Version = candidate.Profile.Version,
            SourceQualificationId = candidate.CandidateId,
            Summary = candidate.Profile.Summary,
            CreatedAt = candidate.Profile.CreatedAt,
            ActivatedAt = DateTimeOffset.UtcNow,
            Rules = candidate.Profile.Rules,
        };
        routingProfileRepository.SaveActive(active);
        return active;
    }

    private static ModelQualificationLaneScore BuildLaneScore(IGrouping<string, ModelQualificationResult> laneGroup, ModelQualificationLane lane)
    {
        var results = laneGroup.ToArray();
        var successRate = results.Length == 0 ? 0 : results.Count(item => item.Success) / (double)results.Length;
        var formatRate = results.Length == 0 ? 0 : results.Count(item => item.FormatValid) / (double)results.Length;
        var avgQuality = results.Length == 0 ? 0 : results.Average(item => item.QualityScore);
        var avgLatency = results.Where(item => item.LatencyMs.HasValue).Select(item => (double)item.LatencyMs!.Value).DefaultIfEmpty(0).Average();
        var decision = successRate <= 0 ? "reject" : "candidate";
        var reason = successRate <= 0
            ? "Lane did not complete successfully in this intent group."
            : $"success_rate={successRate:P0}; format_validity={formatRate:P0}; avg_quality={avgQuality:F1}; avg_latency_ms={avgLatency:F0}";

        return new ModelQualificationLaneScore
        {
            LaneId = lane.LaneId,
            ProviderId = lane.ProviderId,
            BackendId = lane.BackendId,
            RequestFamily = lane.RequestFamily,
            Model = lane.Model,
            SuccessRate = successRate,
            FormatValidityRate = formatRate,
            AverageQualityScore = avgQuality,
            AverageLatencyMs = avgLatency,
            Decision = decision,
            Reason = reason,
        };
    }

    private static double RankLane(ModelQualificationLaneScore score)
    {
        return (score.SuccessRate * 1000d) + (score.FormatValidityRate * 100d) + (score.AverageQualityScore * 10d) - (score.AverageLatencyMs / 10000d);
    }

    private static RuntimeRoutingRoute ToRoute(ModelQualificationLane lane)
    {
        return new RuntimeRoutingRoute
        {
            ProviderId = lane.ProviderId,
            BackendId = lane.BackendId,
            RoutingProfileId = lane.RoutingProfileId,
            RequestFamily = lane.RequestFamily,
            BaseUrl = lane.BaseUrl,
            ApiKeyEnvironmentVariable = lane.ApiKeyEnvironmentVariable,
            Model = lane.Model,
        };
    }

    private static string BuildRuleId(string routingIntent, string? moduleId)
    {
        var suffix = string.IsNullOrWhiteSpace(moduleId)
            ? string.Empty
            : "-" + moduleId.Replace('/', '-').Replace('\\', '-').Replace('_', '-');
        return $"{routingIntent.Replace('_', '-')}{suffix}".ToLowerInvariant();
    }

    private static bool EvaluateFormatValidity(ModelQualificationCase qualificationCase, string output)
    {
        if (qualificationCase.ExpectedFormat != ModelQualificationExpectedFormat.Json)
        {
            return !string.IsNullOrWhiteSpace(output);
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            return qualificationCase.RequiredJsonFields.All(field => document.RootElement.TryGetProperty(field, out _));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static double EvaluateQualityScore(ModelQualificationCase qualificationCase, WorkerExecutionResult execution, bool formatValid)
    {
        if (!execution.Succeeded)
        {
            return 0;
        }

        var output = execution.ResponsePreview ?? execution.Rationale ?? execution.Summary ?? string.Empty;
        if (qualificationCase.ExpectedFormat == ModelQualificationExpectedFormat.Json)
        {
            return formatValid ? 5 : 0;
        }

        if (output.Length >= 120)
        {
            return 4;
        }

        if (output.Length >= 40)
        {
            return 3;
        }

        return output.Length > 0 ? 2 : 0;
    }

    private static string BuildNotes(ModelQualificationCase qualificationCase, WorkerExecutionResult execution, bool formatValid)
    {
        if (!execution.Succeeded)
        {
            return execution.FailureReason ?? execution.Summary;
        }

        return qualificationCase.ExpectedFormat == ModelQualificationExpectedFormat.Json
            ? (formatValid ? "Structured output matched required fields." : "Structured output did not match required fields.")
            : "Text output captured.";
    }

    private static ModelQualificationMatrix CreateDefaultMatrix()
    {
        var lanes = new List<ModelQualificationLane>();
        if (HasEnvironmentValue("GEMINI_API_KEY"))
        {
            lanes.Add(new ModelQualificationLane
            {
                LaneId = "gemini-native-balanced",
                ProviderId = "gemini",
                BackendId = "gemini_api",
                RequestFamily = "generate_content",
                Model = "gemini-2.5-pro",
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                ApiKeyEnvironmentVariable = "GEMINI_API_KEY",
                RoutingProfileId = "gemini-worker-balanced",
                RouteGroup = "gemini_native",
                ObservedVariance = "low",
                Summary = "Gemini native lane for current connected model qualification.",
            });
        }

        if (HasEnvironmentValue("N1N_API_KEY"))
        {
            lanes.Add(new ModelQualificationLane
            {
                LaneId = "n1n-responses",
                ProviderId = "openai",
                BackendId = "openai_api",
                RequestFamily = "responses_api",
                Model = ResolveEnvironmentValue("CARVES_N1N_MODEL", "CARVES_OPENAI_WORKER_MODEL", "CARVES_OPENAI_MODEL") ?? "gpt-4.1",
                BaseUrl = ResolveEnvironmentValue("CARVES_N1N_BASE_URL") ?? "https://hk.n1n.ai/v1",
                ApiKeyEnvironmentVariable = "N1N_API_KEY",
                RoutingProfileId = "worker-codegen-fast",
                RouteGroup = "n1n",
                ObservedVariance = "high",
                Summary = "OpenAI-compatible responses lane through n1n.",
            });
        }

        if (HasEnvironmentValue("GROQ_API_KEY"))
        {
            lanes.Add(new ModelQualificationLane
            {
                LaneId = "groq-chat",
                ProviderId = "openai",
                BackendId = "openai_api",
                RequestFamily = "chat_completions",
                Model = "llama-3.3-70b-versatile",
                BaseUrl = "https://api.groq.com/openai/v1",
                ApiKeyEnvironmentVariable = "GROQ_API_KEY",
                RoutingProfileId = "worker-codegen-fast",
                RouteGroup = "groq",
                ObservedVariance = "low",
                Summary = "OpenAI-compatible chat-completions lane through Groq.",
            });
        }

        if (HasEnvironmentValue("DEEPSEEK_API_KEY"))
        {
            lanes.Add(new ModelQualificationLane
            {
                LaneId = "deepseek-chat",
                ProviderId = "openai",
                BackendId = "openai_api",
                RequestFamily = "chat_completions",
                Model = "deepseek-chat",
                BaseUrl = "https://api.deepseek.com/v1",
                ApiKeyEnvironmentVariable = "DEEPSEEK_API_KEY",
                RoutingProfileId = "worker-codegen-fast",
                RouteGroup = "deepseek",
                ObservedVariance = "low",
                Summary = "OpenAI-compatible chat-completions lane through DeepSeek.",
            });
        }

        var codexApiKeyVariable = ResolveCodexApiKeyVariable();
        if (codexApiKeyVariable is not null && HasCodexBridgeConfigured())
        {
            lanes.Add(new ModelQualificationLane
            {
                LaneId = "codex-sdk-threaded",
                ProviderId = "codex",
                BackendId = "codex_sdk",
                RequestFamily = "codex_sdk",
                Model = Environment.GetEnvironmentVariable("CARVES_CODEX_MODEL") ?? "gpt-5-codex",
                BaseUrl = Environment.GetEnvironmentVariable("CARVES_CODEX_BASE_URL") ?? "https://api.openai.com/v1",
                ApiKeyEnvironmentVariable = codexApiKeyVariable,
                RoutingProfileId = "codex-worker-trusted",
                RouteGroup = "codex_sdk",
                ObservedVariance = "medium",
                Summary = "Codex SDK lane through the governed Node bridge.",
            });
        }

        if (HasCodexCliAvailable())
        {
            lanes.Add(new ModelQualificationLane
            {
                LaneId = "codex-cli-local",
                ProviderId = "codex",
                BackendId = "codex_cli",
                RequestFamily = "codex_exec",
                Model = ResolveEnvironmentValue("CARVES_CODEX_CLI_MODEL") ?? "codex-cli",
                BaseUrl = "local://codex-cli",
                ApiKeyEnvironmentVariable = string.Empty,
                RoutingProfileId = "codex-worker-local-cli",
                RouteGroup = "codex_cli",
                ObservedVariance = "low",
                Summary = "Local Codex CLI lane for governed code.small execution.",
            });
        }

        return new ModelQualificationMatrix
        {
            Summary = "Qualification matrix for currently connected Gemini, openai-compatible gateway, and governed Codex worker lanes.",
            CreatedAt = DateTimeOffset.UtcNow,
            Lanes = lanes.ToArray(),
            Cases =
            [
                new ModelQualificationCase
                {
                    CaseId = "patch-draft",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Suggest a minimal C# patch plan for adding a schemaVersion field to ResultEnvelope while preserving backward compatibility. Return a concise markdown response with sections Changed Files, Compatibility, and Validation.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    Summary = "Small precise patch-draft prompt.",
                },
                new ModelQualificationCase
                {
                    CaseId = "failure-summary",
                    RoutingIntent = "failure_summary",
                    ModuleId = "Execution/Failures",
                    Prompt = "Summarize this failure for an operator in three bullets: 'Worker failed after 3 retries due to timeout while validating ResultEnvelope changes.'",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    Summary = "Failure-summary projection prompt.",
                },
                new ModelQualificationCase
                {
                    CaseId = "structured-output",
                    RoutingIntent = "structured_output",
                    Prompt = "Return a JSON object with fields risk_level, root_cause, mitigation_steps for this failure: 'Worker failed after 3 retries due to timeout.'",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["risk_level", "root_cause", "mitigation_steps"],
                    Summary = "Strict JSON/schema prompt.",
                },
                new ModelQualificationCase
                {
                    CaseId = "reasoning-summary",
                    RoutingIntent = "reasoning_summary",
                    Prompt = "Analyze why infinite retry loops may occur in a Planner/Worker/Host execution system and propose a concise mitigation strategy.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                    Summary = "Reasoning-summary prompt.",
                },
            ],
        };
    }

    private static bool HasEnvironmentValue(string variableName)
    {
        return !string.IsNullOrWhiteSpace(ResolveEnvironmentValue(variableName));
    }

    private static bool ShouldRefreshDynamicConnectedMatrix(ModelQualificationMatrix persisted, ModelQualificationMatrix current)
    {
        if (!string.Equals(persisted.MatrixId, "current-connected-lanes", StringComparison.Ordinal))
        {
            return false;
        }

        if (persisted.DefaultAttempts != current.DefaultAttempts)
        {
            return true;
        }

        var persistedLanes = persisted.Lanes
            .OrderBy(item => item.LaneId, StringComparer.Ordinal)
            .Select(item => string.Join('|', item.LaneId, item.ProviderId, item.BackendId, item.RequestFamily, item.Model, item.BaseUrl, item.ApiKeyEnvironmentVariable, item.RoutingProfileId))
            .ToArray();
        var currentLanes = current.Lanes
            .OrderBy(item => item.LaneId, StringComparer.Ordinal)
            .Select(item => string.Join('|', item.LaneId, item.ProviderId, item.BackendId, item.RequestFamily, item.Model, item.BaseUrl, item.ApiKeyEnvironmentVariable, item.RoutingProfileId))
            .ToArray();
        if (!persistedLanes.SequenceEqual(currentLanes, StringComparer.Ordinal))
        {
            return true;
        }

        var persistedCases = persisted.Cases
            .OrderBy(item => item.CaseId, StringComparer.Ordinal)
            .Select(item => string.Join('|', item.CaseId, item.RoutingIntent, item.ModuleId ?? string.Empty, item.Prompt, ((int)item.ExpectedFormat).ToString(), string.Join(',', item.RequiredJsonFields), item.Attempts?.ToString() ?? string.Empty))
            .ToArray();
        var currentCases = current.Cases
            .OrderBy(item => item.CaseId, StringComparer.Ordinal)
            .Select(item => string.Join('|', item.CaseId, item.RoutingIntent, item.ModuleId ?? string.Empty, item.Prompt, ((int)item.ExpectedFormat).ToString(), string.Join(',', item.RequiredJsonFields), item.Attempts?.ToString() ?? string.Empty))
            .ToArray();
        return !persistedCases.SequenceEqual(currentCases, StringComparer.Ordinal);
    }

    private static string? ResolveEnvironmentValue(params string[] variableNames)
    {
        foreach (var variableName in variableNames)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                continue;
            }

            var processValue = Environment.GetEnvironmentVariable(variableName);
            if (!string.IsNullOrWhiteSpace(processValue))
            {
                return processValue;
            }

            var userValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.User);
            if (!string.IsNullOrWhiteSpace(userValue))
            {
                return userValue;
            }

            var machineValue = Environment.GetEnvironmentVariable(variableName, EnvironmentVariableTarget.Machine);
            if (!string.IsNullOrWhiteSpace(machineValue))
            {
                return machineValue;
            }
        }

        return null;
    }

    private static string? ResolveCodexApiKeyVariable()
    {
        if (HasEnvironmentValue("CODEX_API_KEY"))
        {
            return "CODEX_API_KEY";
        }

        if (HasEnvironmentValue("OPENAI_API_KEY"))
        {
            return "OPENAI_API_KEY";
        }

        return null;
    }

    private static bool HasCodexBridgeConfigured()
    {
        var explicitPath = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return true;
        }

        var repoRoot = Environment.GetEnvironmentVariable("CARVES_REPO_ROOT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "scripts", "codex-worker-bridge", "bridge.mjs");
        return File.Exists(candidate);
    }

    private static bool HasCodexCliAvailable()
    {
        var explicitPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return true;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var candidates = OperatingSystem.IsWindows()
            ? new[] { "codex.exe", "codex.cmd", "codex.bat" }
            : new[] { "codex" };
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(directory => candidates.Any(candidate => File.Exists(Path.Combine(directory, candidate))));
    }

    private readonly record struct IntentGroupKey(string RoutingIntent, string? ModuleId);
}
