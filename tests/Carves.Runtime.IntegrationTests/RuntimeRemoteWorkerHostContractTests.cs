using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeRemoteWorkerHostContractTests
{
    [Fact]
    public void RuntimeRemoteWorkerQualification_ProjectsProvidersAndClosedMaterializedLanes()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-remote-worker-qualification");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-remote-worker-qualification");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime remote worker qualification", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("claude/claude_api", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("gemini/gemini_api", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("patch_draft", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var policies = document.RootElement.GetProperty("current_policies").EnumerateArray().ToArray();
        Assert.Contains(policies, item => item.GetProperty("provider_id").GetString() == "claude");
        Assert.Contains(policies, item => item.GetProperty("provider_id").GetString() == "gemini");
        Assert.Contains(
            policies.Where(item => item.GetProperty("provider_id").GetString() == "gemini")
                .SelectMany(item => item.GetProperty("lanes").EnumerateArray()),
            item => item.GetProperty("routing_intent").GetString() == "patch_draft" && !item.GetProperty("allowed").GetBoolean());
    }

    [Fact]
    public void RuntimeRemoteWorkerOnboardingChecklist_ProjectsReusableChecklist()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-remote-worker-onboarding-checklist");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-remote-worker-onboarding-checklist");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime remote worker onboarding checklist", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Current activation mode: external_app_cli_only", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SDK/API worker backends allowed: False", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("SDK/API worker boundary: closed_until_separate_governed_activation", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Activation state: closed_current_policy_future_external_adapter_onboarding_only", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("external_agent_app_cli_adapter_governed_onboarding", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Claude is not blocked by provider name", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("protocol_adapter", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("checkpoint_proof", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        Assert.Equal("external_app_cli_only", document.RootElement.GetProperty("current_activation_mode").GetString());
        Assert.False(document.RootElement.GetProperty("sdk_api_worker_backends_allowed").GetBoolean());
        Assert.Equal("closed_until_separate_governed_activation", document.RootElement.GetProperty("sdk_api_worker_boundary").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("disallowed_worker_backend_ids").EnumerateArray().Select(item => item.GetString()),
            item => item == "claude_api");
        var providers = document.RootElement.GetProperty("providers").EnumerateArray().ToArray();
        Assert.Contains(providers, item => item.GetProperty("provider_id").GetString() == "claude");
        Assert.Contains(providers, item => item.GetProperty("provider_id").GetString() == "gemini");
        Assert.All(providers, item =>
        {
            Assert.Equal("closed_current_policy_future_external_adapter_onboarding_only", item.GetProperty("activation_state").GetString());
            Assert.False(item.GetProperty("current_worker_selection_eligible").GetBoolean());
        });
    }

    private static ProgramHarness.ProgramRunResult RunProgram(params string[] arguments)
    {
        return ProgramHarness.Run(arguments);
    }
}
