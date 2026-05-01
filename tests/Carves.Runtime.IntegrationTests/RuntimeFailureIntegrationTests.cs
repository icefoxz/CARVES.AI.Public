using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeFailureIntegrationTests
{
    [Fact]
    public void ProviderFailure_PersistsFailurePolicyAndFailsTask()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticPendingTask("T-INTEGRATION-FAILURE");
        sandbox.WriteAiProviderConfig("""
{
  "provider": "openai",
  "enabled": true,
  "model": "gpt-5-mini",
  "base_url": "https://api.openai.invalid/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 300,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", null);
        try
        {
            var exitCode = Program.Main(["--repo-root", sandbox.RootPath, "run-next"]);
            var taskNodeJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-FAILURE.json"));
            var sessionJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json"));
            var failureJson = File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "last_failure.json"));

            Assert.NotEqual(0, exitCode);
            Assert.Contains("\"status\": \"blocked\"", taskNodeJson, StringComparison.Ordinal);
            Assert.Contains("\"status\": \"paused\"", sessionJson, StringComparison.Ordinal);
            Assert.Contains("\"failure_type\": \"worker_execution_failure\"", failureJson, StringComparison.Ordinal);
            Assert.Contains("\"action\": \"block_task\"", failureJson, StringComparison.Ordinal);
            Assert.True(Directory.EnumerateFiles(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "runtime-failures"), "*.json").Any());
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }
}
