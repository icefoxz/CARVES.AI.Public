using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class WorkerExternalCodexActivationTests
{
    [Fact]
    public void WorkerActivateExternalAppCli_AdmitsCurrentCliBackendAndKeepsSdkApiClosed()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var policyPath = Path.Combine(sandbox.RootPath, ".carves-platform", "policies", "worker-selection.policy.json");
        WriteNullWorkerPolicy(policyPath);

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var dryRun = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "worker",
                "activate-external-app-cli",
                "--dry-run",
                "--reason",
                "phase7-provider-neutral-dry-run");
            var afterDryRun = JsonNode.Parse(File.ReadAllText(policyPath))!.AsObject();
            var activation = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-external-app-cli-activation",
                "--reason",
                "phase7-provider-neutral-activation");
            var operatorSessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions", "--kind", "operator");
            var afterActivation = JsonNode.Parse(File.ReadAllText(policyPath))!.AsObject();
            var allowedBackends = afterActivation["allowed_backend_ids"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();

            Assert.Equal(0, dryRun.ExitCode);
            Assert.Contains("External app/CLI worker activation dry run", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Current activation mode: external_app_cli_only", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Provider-neutral external app/CLI policy: True", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Future external app/CLI adapters require governed onboarding: True", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("SDK/API worker boundary: closed_until_separate_governed_activation", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Equal("null_worker", afterDryRun["preferred_backend_id"]!.GetValue<string>());
            Assert.Equal(0, activation.ExitCode);
            Assert.Contains("\"schema_version\": \"worker-selection-external-app-cli-activation.v1\"", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"activation_mode\": \"external_app_cli_only\"", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"current_activation_mode\": \"external_app_cli_only\"", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"current_materialized_worker_backend_id\": \"codex_cli\"", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"provider_neutral_external_app_cli_policy\": true", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"future_external_app_cli_adapters_require_governed_onboarding\": true", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"sdk_api_worker_boundary\": \"closed_until_separate_governed_activation\"", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"external_agent_app_cli_adapter_governed_onboarding\"", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"claude_api\"", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, operatorSessions.ExitCode);
            Assert.Contains("\"last_operation\": \"activate_external_app_cli_api\"", operatorSessions.StandardOutput, StringComparison.Ordinal);
            Assert.Equal("codex_cli", afterActivation["preferred_backend_id"]!.GetValue<string>());
            Assert.Contains("codex_cli", allowedBackends);
            Assert.Contains("null_worker", allowedBackends);
            Assert.DoesNotContain("codex_sdk", allowedBackends);
            Assert.DoesNotContain("openai_api", allowedBackends);
            Assert.DoesNotContain("claude_api", allowedBackends);
            Assert.DoesNotContain("gemini_api", allowedBackends);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public void WorkerActivateExternalCodex_AdmitsOnlyCodexCliAndNullWorkerFallback()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        var policyPath = Path.Combine(sandbox.RootPath, ".carves-platform", "policies", "worker-selection.policy.json");
        WriteNullWorkerPolicy(policyPath);

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var dryRun = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "worker",
                "activate-external-codex",
                "--dry-run",
                "--reason",
                "phase20-dry-run");
            var afterDryRun = JsonNode.Parse(File.ReadAllText(policyPath))!.AsObject();
            var activation = ProgramHarness.Run(
                "--repo-root",
                sandbox.RootPath,
                "api",
                "worker-external-codex-activation",
                "--reason",
                "phase20-activation");
            var operatorSessions = ProgramHarness.Run("--repo-root", sandbox.RootPath, "api", "actor-sessions", "--kind", "operator");
            var afterActivation = JsonNode.Parse(File.ReadAllText(policyPath))!.AsObject();
            var allowedBackends = afterActivation["allowed_backend_ids"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            var fallbackBackends = afterActivation["fallback_backend_ids"]!.AsArray()
                .Select(item => item!.GetValue<string>())
                .ToArray();
            var validate = ProgramHarness.Run("--repo-root", sandbox.RootPath, "policy", "validate");

            Assert.Equal(0, dryRun.ExitCode);
            Assert.Contains("External Codex worker activation dry run", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("SDK/API backends closed: True", dryRun.StandardOutput, StringComparison.Ordinal);
            Assert.Equal("null_worker", afterDryRun["preferred_backend_id"]!.GetValue<string>());
            Assert.Equal(0, activation.ExitCode);
            Assert.Contains("\"applied\": true", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"sdk_api_backends_closed\": true", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"codex_cli_allowed\": true", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("\"codex_sdk_allowed\": false", activation.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, operatorSessions.ExitCode);
            Assert.Contains("\"last_operation\": \"activate_external_codex_api\"", operatorSessions.StandardOutput, StringComparison.Ordinal);
            Assert.Equal("codex_cli", afterActivation["preferred_backend_id"]!.GetValue<string>());
            Assert.Contains("codex_cli", allowedBackends);
            Assert.Contains("null_worker", allowedBackends);
            Assert.DoesNotContain("codex_sdk", allowedBackends);
            Assert.DoesNotContain("openai_api", allowedBackends);
            Assert.DoesNotContain("claude_api", allowedBackends);
            Assert.DoesNotContain("gemini_api", allowedBackends);
            Assert.Equal(["null_worker"], fallbackBackends);
            Assert.Equal(0, validate.ExitCode);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

    private static void StopHost(string repoRoot)
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "worker external Codex activation cleanup");
    }

    private static void WriteNullWorkerPolicy(string policyPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllText(
            policyPath,
            """
{
  "version": "1.0",
  "preferred_backend_id": "null_worker",
  "default_trust_profile_id": "workspace_build_test",
  "allow_routing_fallback": true,
  "fallback_backend_ids": [
    "null_worker"
  ],
  "allowed_backend_ids": [
    "null_worker"
  ]
}
""");
    }
}
