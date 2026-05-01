using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CurrentModelQualificationServiceTests
{
    [Fact]
    public void Run_PersistsStructuredLedgerWithRepeatedAttemptsAndFailures()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(CreateMatrix(defaultAttempts: 2));
        var service = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubQualificationLaneExecutor());

        var ledger = service.Run();
        var loaded = qualificationRepository.LoadLatestRun();

        Assert.NotNull(loaded);
        Assert.Equal(12, ledger.Results.Length);
        Assert.Equal(ledger.RunId, loaded!.RunId);
        Assert.Contains(ledger.Results, item =>
            item.LaneId == "gemini-native-balanced"
            && item.CaseId == "structured-output"
            && item.Success
            && item.FormatValid
            && item.ProviderId == "gemini"
            && item.BackendId == "gemini_api"
            && item.RequestFamily == "generate_content");
        Assert.Contains(ledger.Results, item =>
            item.LaneId == "groq-chat"
            && item.Success == false
            && item.HttpStatus == 429
            && item.FailureKind == WorkerFailureKind.TransientInfra
            && !string.IsNullOrWhiteSpace(item.Notes));
    }

    [Fact]
    public void MaterializeCandidate_CreatesCandidateWithoutChangingActiveProfile()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(CreateMatrix(defaultAttempts: 1));
        qualificationRepository.SaveLatestRun(new ModelQualificationRunLedger
        {
            RunId = "qual-run-001",
            MatrixId = "matrix-alpha",
            Results =
            [
                CreateResult("gemini-native-balanced", "gemini", "gemini_api", "generate_content", "patch_draft", "Execution/ResultEnvelope", true, true, 5, 500),
                CreateResult("n1n-responses", "openai", "openai_api", "responses_api", "patch_draft", "Execution/ResultEnvelope", true, true, 4, 650),
                CreateResult("groq-chat", "openai", "openai_api", "chat_completions", "patch_draft", "Execution/ResultEnvelope", false, false, 0, 0, WorkerFailureKind.TransientInfra, 429),
            ],
        });
        var service = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubQualificationLaneExecutor());

        var candidate = service.MaterializeCandidate();

        Assert.Equal("qual-run-001", candidate.SourceRunId);
        Assert.Single(candidate.Profile.Rules);
        Assert.Equal("gemini-native-balanced", candidate.Intents[0].PreferredLaneId);
        Assert.Equal("generate_content", candidate.Profile.Rules[0].PreferredRoute.RequestFamily);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta", candidate.Profile.Rules[0].PreferredRoute.BaseUrl);
        Assert.Contains("n1n-responses", candidate.Intents[0].FallbackLaneIds);
        Assert.Equal("responses_api", candidate.Profile.Rules[0].FallbackRoutes[0].RequestFamily);
        Assert.Equal("N1N_API_KEY", candidate.Profile.Rules[0].FallbackRoutes[0].ApiKeyEnvironmentVariable);
        Assert.Contains("groq-chat", candidate.Intents[0].RejectLaneIds);
        Assert.Null(routingRepository.LoadActive());
    }

    [Fact]
    public void PromoteCandidate_WritesActiveRoutingProfileFromCandidateTruth()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        qualificationRepository.SaveCandidate(new ModelQualificationCandidateProfile
        {
            CandidateId = "routing-candidate-001",
            MatrixId = "matrix-alpha",
            SourceRunId = "qual-run-001",
            Profile = new RuntimeRoutingProfile
            {
                ProfileId = "candidate-matrix-alpha",
                Version = "20260318120000",
                Summary = "Candidate profile from qualification.",
                Rules =
                [
                    new RuntimeRoutingRule
                    {
                        RuleId = "patch-draft-execution-resultenvelope",
                        RoutingIntent = "patch_draft",
                        ModuleId = "Execution/ResultEnvelope",
                        PreferredRoute = new RuntimeRoutingRoute
                        {
                            ProviderId = "gemini",
                            BackendId = "gemini_api",
                            RoutingProfileId = "gemini-worker-balanced",
                            RequestFamily = "generate_content",
                            BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                            ApiKeyEnvironmentVariable = "GEMINI_API_KEY",
                            Model = "gemini-2.5-pro",
                        },
                    },
                ],
            },
        });
        var service = new CurrentModelQualificationService(
            qualificationRepository,
            routingRepository,
            new StubQualificationLaneExecutor());

        var promoted = service.PromoteCandidate("routing-candidate-001");
        var active = routingRepository.LoadActive();

        Assert.NotNull(active);
        Assert.Equal("routing-candidate-001", promoted.SourceQualificationId);
        Assert.Equal("routing-candidate-001", active!.SourceQualificationId);
        Assert.NotNull(active.ActivatedAt);
        Assert.Single(active.Rules);
    }

    [Fact]
    public void LoadOrCreateMatrix_AdmitsCodexWorkerLanesWhenBridgeCliAndApiKeyExist()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var bridgePath = workspace.WriteFile("bridge/bridge.mjs", "export {};");
        var cliPath = CreateFakeCodexCli(workspace);
        var originalBridge = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        var originalCliPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        var originalCliModel = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_MODEL");
        var originalCodexApiKey = Environment.GetEnvironmentVariable("CODEX_API_KEY");
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", bridgePath);
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", cliPath);
        Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", "codex-cli");
        Environment.SetEnvironmentVariable("CODEX_API_KEY", "test-codex-key");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        try
        {
            var service = new CurrentModelQualificationService(
                qualificationRepository,
                routingRepository,
                new StubQualificationLaneExecutor());

            var matrix = service.LoadOrCreateMatrix();

            Assert.Contains(matrix.Lanes, lane => lane.LaneId == "codex-sdk-threaded" && lane.ApiKeyEnvironmentVariable == "CODEX_API_KEY");
            Assert.Contains(matrix.Lanes, lane => lane.LaneId == "codex-cli-local" && lane.BackendId == "codex_cli" && lane.Model == "codex-cli");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT", originalBridge);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_PATH", originalCliPath);
            Environment.SetEnvironmentVariable("CARVES_CODEX_CLI_MODEL", originalCliModel);
            Environment.SetEnvironmentVariable("CODEX_API_KEY", originalCodexApiKey);
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
        }
    }

    [Fact]
    public void LoadOrCreateMatrix_UsesConfiguredN1nModelAndBaseUrl()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var originalN1nApiKey = Environment.GetEnvironmentVariable("N1N_API_KEY");
        var originalN1nModel = Environment.GetEnvironmentVariable("CARVES_N1N_MODEL");
        var originalN1nBaseUrl = Environment.GetEnvironmentVariable("CARVES_N1N_BASE_URL");
        Environment.SetEnvironmentVariable("N1N_API_KEY", "test-n1n-key");
        Environment.SetEnvironmentVariable("CARVES_N1N_MODEL", "gpt-5.4");
        Environment.SetEnvironmentVariable("CARVES_N1N_BASE_URL", "https://gateway.example/v1");
        try
        {
            var service = new CurrentModelQualificationService(
                qualificationRepository,
                routingRepository,
                new StubQualificationLaneExecutor());

            var matrix = service.LoadOrCreateMatrix();
            var lane = Assert.Single(matrix.Lanes, item => item.LaneId == "n1n-responses");

            Assert.Equal("gpt-5.4", lane.Model);
            Assert.Equal("https://gateway.example/v1", lane.BaseUrl);
        }
        finally
        {
            Environment.SetEnvironmentVariable("N1N_API_KEY", originalN1nApiKey);
            Environment.SetEnvironmentVariable("CARVES_N1N_MODEL", originalN1nModel);
            Environment.SetEnvironmentVariable("CARVES_N1N_BASE_URL", originalN1nBaseUrl);
        }
    }

    [Fact]
    public void LoadOrCreateMatrix_RefreshesCurrentConnectedMatrixWhenN1nConfigurationChanges()
    {
        using var workspace = new TemporaryWorkspace();
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(new ModelQualificationMatrix
        {
            MatrixId = "current-connected-lanes",
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "n1n-responses",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "responses_api",
                    Model = "gpt-4.1",
                    BaseUrl = "https://hk.n1n.ai/v1",
                    ApiKeyEnvironmentVariable = "N1N_API_KEY",
                    RoutingProfileId = "worker-codegen-fast",
                },
            ],
            Cases =
            [
                new ModelQualificationCase
                {
                    CaseId = "patch-draft",
                    RoutingIntent = "patch_draft",
                    Prompt = "legacy",
                },
            ],
        });

        var originalN1nApiKey = Environment.GetEnvironmentVariable("N1N_API_KEY");
        var originalN1nModel = Environment.GetEnvironmentVariable("CARVES_N1N_MODEL");
        Environment.SetEnvironmentVariable("N1N_API_KEY", "test-n1n-key");
        Environment.SetEnvironmentVariable("CARVES_N1N_MODEL", "gpt-5.4");
        try
        {
            var service = new CurrentModelQualificationService(
                qualificationRepository,
                routingRepository,
                new StubQualificationLaneExecutor());

            var matrix = service.LoadOrCreateMatrix();
            var lane = Assert.Single(matrix.Lanes, item => item.LaneId == "n1n-responses");

            Assert.Equal("gpt-5.4", lane.Model);
            Assert.Contains(matrix.Cases, item => item.CaseId == "reasoning-summary");
        }
        finally
        {
            Environment.SetEnvironmentVariable("N1N_API_KEY", originalN1nApiKey);
            Environment.SetEnvironmentVariable("CARVES_N1N_MODEL", originalN1nModel);
        }
    }

    private static ModelQualificationMatrix CreateMatrix(int defaultAttempts)
    {
        return new ModelQualificationMatrix
        {
            MatrixId = "matrix-alpha",
            DefaultAttempts = defaultAttempts,
            Lanes =
            [
                new ModelQualificationLane
                {
                    LaneId = "gemini-native-balanced",
                    ProviderId = "gemini",
                    BackendId = "gemini_api",
                    RequestFamily = "generate_content",
                    Model = "gemini-2.5-pro",
                    BaseUrl = "https://generativelanguage.googleapis.com/v1beta",
                    ApiKeyEnvironmentVariable = "GEMINI_API_KEY",
                    RoutingProfileId = "gemini-worker-balanced",
                },
                new ModelQualificationLane
                {
                    LaneId = "groq-chat",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "chat_completions",
                    Model = "llama-3.3-70b-versatile",
                    BaseUrl = "https://api.groq.com/openai/v1",
                    ApiKeyEnvironmentVariable = "GROQ_API_KEY",
                    RoutingProfileId = "worker-codegen-fast",
                },
                new ModelQualificationLane
                {
                    LaneId = "n1n-responses",
                    ProviderId = "openai",
                    BackendId = "openai_api",
                    RequestFamily = "responses_api",
                    Model = "gpt-4.1",
                    BaseUrl = "https://hk.n1n.ai/v1",
                    ApiKeyEnvironmentVariable = "N1N_API_KEY",
                    RoutingProfileId = "worker-codegen-fast",
                },
            ],
            Cases =
            [
                new ModelQualificationCase
                {
                    CaseId = "patch-draft",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    Prompt = "Return a patch plan.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Text,
                },
                new ModelQualificationCase
                {
                    CaseId = "structured-output",
                    RoutingIntent = "structured_output",
                    Prompt = "Return JSON with risk_level, root_cause, mitigation_steps.",
                    ExpectedFormat = ModelQualificationExpectedFormat.Json,
                    RequiredJsonFields = ["risk_level", "root_cause", "mitigation_steps"],
                },
            ],
        };
    }

    private static ModelQualificationResult CreateResult(
        string laneId,
        string providerId,
        string backendId,
        string requestFamily,
        string routingIntent,
        string moduleId,
        bool success,
        bool formatValid,
        double quality,
        long latencyMs,
        WorkerFailureKind failureKind = WorkerFailureKind.None,
        int? httpStatus = null)
    {
        return new ModelQualificationResult
        {
            RunId = "qual-run-001",
            LaneId = laneId,
            CaseId = $"{routingIntent}-{laneId}",
            Attempt = 1,
            ProviderId = providerId,
            BackendId = backendId,
            RequestFamily = requestFamily,
            Model = laneId,
            RoutingIntent = routingIntent,
            ModuleId = moduleId,
            Success = success,
            FormatValid = formatValid,
            QualityScore = quality,
            LatencyMs = latencyMs,
            FailureKind = failureKind,
            HttpStatus = httpStatus,
        };
    }

    private sealed class StubQualificationLaneExecutor : IQualificationLaneExecutor
    {
        public WorkerExecutionResult Execute(ModelQualificationLane lane, ModelQualificationCase qualificationCase, int attempt)
        {
            if (lane.LaneId == "groq-chat")
            {
                return new WorkerExecutionResult
                {
                    TaskId = $"qualification-{qualificationCase.CaseId}",
                    BackendId = lane.BackendId,
                    ProviderId = lane.ProviderId,
                    AdapterId = "remote-api",
                    ProtocolFamily = lane.ProviderId,
                    RequestFamily = lane.RequestFamily,
                    ProfileId = lane.RoutingProfileId,
                    TrustedProfile = false,
                    Status = WorkerExecutionStatus.Failed,
                    FailureKind = WorkerFailureKind.TransientInfra,
                    FailureLayer = WorkerFailureLayer.Provider,
                    Retryable = true,
                    Configured = true,
                    Model = lane.Model,
                    Summary = "Rate limited.",
                    FailureReason = "quota exceeded",
                    ProviderStatusCode = 429,
                    ProviderLatencyMs = 200,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow.AddMilliseconds(200),
                };
            }

            var responsePreview = qualificationCase.ExpectedFormat == ModelQualificationExpectedFormat.Json
                ? """{"risk_level":"low","root_cause":"timeout","mitigation_steps":["retry"]}"""
                : $"attempt={attempt}; lane={lane.LaneId}; case={qualificationCase.CaseId}";
            return new WorkerExecutionResult
            {
                TaskId = $"qualification-{qualificationCase.CaseId}",
                BackendId = lane.BackendId,
                ProviderId = lane.ProviderId,
                AdapterId = "remote-api",
                ProtocolFamily = lane.ProviderId,
                RequestFamily = lane.RequestFamily,
                ProfileId = lane.RoutingProfileId,
                TrustedProfile = false,
                Status = WorkerExecutionStatus.Succeeded,
                FailureKind = WorkerFailureKind.None,
                FailureLayer = WorkerFailureLayer.None,
                Retryable = false,
                Configured = true,
                Model = lane.Model,
                Summary = "Succeeded.",
                ResponsePreview = responsePreview,
                ProviderStatusCode = 200,
                ProviderLatencyMs = 120 + attempt,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow.AddMilliseconds(120 + attempt),
            };
        }
    }

    private static string CreateFakeCodexCli(TemporaryWorkspace workspace)
    {
        if (OperatingSystem.IsWindows())
        {
            return workspace.WriteFile("tools/fake-codex.cmd", """
@echo off
if "%1"=="--version" (
  echo codex 0.0.0-test
  exit /b 0
)
echo unsupported 1>&2
exit /b 1
""");
        }

        var path = workspace.WriteFile("tools/fake-codex", """
#!/usr/bin/env sh
if [ "$1" = "--version" ]; then
  echo "codex 0.0.0-test"
  exit 0
fi
echo unsupported >&2
exit 1
""");
        File.SetUnixFileMode(path, UnixFileMode.UserExecute | UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return path;
    }
}
