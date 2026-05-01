using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed class RoutingProfileSurfaceTests
{
    [Fact]
    public void ColdInspectAndApiRoutingProfile_ReturnActiveProfileWithoutId()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var repository = new JsonRuntimeRoutingProfileRepository(ControlPlanePaths.FromRepoRoot(sandbox.RootPath));
        repository.SaveActive(new RuntimeRoutingProfile
        {
            ProfileId = "profile-surface",
            Summary = "Integration routing profile surface",
            Rules =
            [
                new RuntimeRoutingRule
                {
                    RuleId = "rule-surface",
                    RoutingIntent = "failure_summary",
                    ModuleId = "src/runtime/failures",
                    Summary = "Surface rule",
                    PreferredRoute = new RuntimeRoutingRoute
                    {
                        ProviderId = "openai",
                        BackendId = "openai_api",
                        RoutingProfileId = "worker-codegen-fast",
                        RequestFamily = "responses_api",
                        BaseUrl = "https://hk.n1n.ai/v1",
                        ApiKeyEnvironmentVariable = "N1N_API_KEY",
                        Model = "gpt-4o-mini",
                    },
                },
            ],
        });

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "routing-profile");
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "routing-profile");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Contains("Active routing profile: profile-surface", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("rule-surface", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("responses_api", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("https://hk.n1n.ai/v1", inspect.StandardOutput, StringComparison.Ordinal);

        Assert.Equal(0, api.ExitCode);
        Assert.Contains("\"profile_id\": \"profile-surface\"", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"rule_id\": \"rule-surface\"", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"request_family\": \"responses_api\"", api.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"base_url\": \"https://hk.n1n.ai/v1\"", api.StandardOutput, StringComparison.Ordinal);
    }
}
