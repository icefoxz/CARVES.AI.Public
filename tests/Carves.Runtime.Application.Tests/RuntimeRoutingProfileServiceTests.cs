using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeRoutingProfileServiceTests
{
    [Fact]
    public void JsonRuntimeRoutingProfileRepository_RoundTripsActiveProfile()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var profile = new RuntimeRoutingProfile
        {
            ProfileId = "qualification-alpha",
            Version = "2026-03-18",
            SourceQualificationId = "QUAL-001",
            Summary = "Route review summaries to high-context models.",
            ActivatedAt = new DateTimeOffset(2026, 3, 18, 9, 0, 0, TimeSpan.Zero),
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "review-summary",
                    RoutingIntent = "review_summary",
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
                    FallbackRoutes =
                    [
                        new RuntimeRoutingRoute
                        {
                            ProviderId = "openai",
                            BackendId = "openai_api",
                            RoutingProfileId = "worker-codegen-fast",
                            RequestFamily = "responses_api",
                            BaseUrl = "https://hk.n1n.ai/v1",
                            ApiKeyEnvironmentVariable = "N1N_API_KEY",
                            Model = "gpt-4.1",
                        },
                    ],
                },
            ],
        };

        repository.SaveActive(profile);
        var loaded = repository.LoadActive();

        Assert.NotNull(loaded);
        Assert.Equal("qualification-alpha", loaded!.ProfileId);
        Assert.Equal("QUAL-001", loaded.SourceQualificationId);
        Assert.Single(loaded.Rules);
        Assert.Equal("review_summary", loaded.Rules[0].RoutingIntent);
        Assert.Equal("generate_content", loaded.Rules[0].PreferredRoute.RequestFamily);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta", loaded.Rules[0].PreferredRoute.BaseUrl);
        Assert.Equal("responses_api", loaded.Rules[0].FallbackRoutes[0].RequestFamily);
        Assert.Equal("N1N_API_KEY", loaded.Rules[0].FallbackRoutes[0].ApiKeyEnvironmentVariable);
        Assert.True(File.Exists(workspace.Paths.PlatformActiveRoutingProfileFile));
    }

    [Fact]
    public void RuntimeRoutingProfileService_ResolvesIntentAndModuleSpecificRule()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        repository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "qualification-beta",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "result-envelope",
                    RoutingIntent = "patch_draft",
                    ModuleId = "Execution/ResultEnvelope",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "openai",
                        BackendId = "openai_api",
                        RoutingProfileId = "worker-codegen-fast",
                        RequestFamily = "responses_api",
                        BaseUrl = "https://hk.n1n.ai/v1",
                        ApiKeyEnvironmentVariable = "N1N_API_KEY",
                        Model = "gpt-4.1",
                    },
                },
                new RuntimeRoutingRule
                {
                    RuleId = "fallback",
                    RoutingIntent = "patch_draft",
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
        });
        var service = new RuntimeRoutingProfileService(repository);

        var exact = service.Resolve("patch_draft", "Execution/ResultEnvelope");
        var fallback = service.Resolve("patch_draft", "Execution/OtherModule");
        var missing = service.Resolve("review_summary", "Execution/ResultEnvelope");

        Assert.NotNull(exact);
        Assert.Equal("result-envelope", exact!.Rule.RuleId);
        Assert.Equal("openai_api", exact.PreferredRoute.BackendId);

        Assert.NotNull(fallback);
        Assert.Equal("fallback", fallback!.Rule.RuleId);
        Assert.Equal("gemini_api", fallback.PreferredRoute.BackendId);

        Assert.Null(missing);
    }

    [Fact]
    public void RuntimeRoutingProfileService_EnrichesLegacyRoutesWithRequestFamilyFromQualificationMatrix()
    {
        using var workspace = new TemporaryWorkspace();
        var routingRepository = new JsonRuntimeRoutingProfileRepository(workspace.Paths);
        var qualificationRepository = new JsonCurrentModelQualificationRepository(workspace.Paths);
        qualificationRepository.SaveMatrix(new ModelQualificationMatrix
        {
            MatrixId = "matrix-legacy-routing",
            Lanes =
            [
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
            ],
        });
        routingRepository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "legacy-profile",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "reasoning-summary",
                    RoutingIntent = "reasoning_summary",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "openai",
                        BackendId = "openai_api",
                        RoutingProfileId = "worker-codegen-fast",
                        Model = "llama-3.3-70b-versatile",
                    },
                },
            ],
        });
        var service = new RuntimeRoutingProfileService(routingRepository, qualificationRepository);

        var active = service.LoadActive();
        var resolved = service.Resolve("reasoning_summary", null);

        Assert.NotNull(active);
        Assert.Equal("chat_completions", active!.Rules[0].PreferredRoute.RequestFamily);
        Assert.Equal("https://api.groq.com/openai/v1", active.Rules[0].PreferredRoute.BaseUrl);
        Assert.Equal("GROQ_API_KEY", active.Rules[0].PreferredRoute.ApiKeyEnvironmentVariable);
        Assert.NotNull(resolved);
        Assert.Equal("chat_completions", resolved!.PreferredRoute.RequestFamily);
        Assert.Null(routingRepository.LoadActive()!.Rules[0].PreferredRoute.RequestFamily);
    }
}
