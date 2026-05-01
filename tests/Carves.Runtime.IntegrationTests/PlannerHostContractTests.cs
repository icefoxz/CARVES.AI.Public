using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class PlannerHostContractTests
{
    [Fact]
    public void PlannerCommands_RunLocalPlannerAndPersistProposalArtifact()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearPlannerGeneratedTasks();
        sandbox.ClearRefactoringSuggestions();
        sandbox.ClearOpportunities();
        sandbox.AddSyntheticRefactoringSmell();

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "planner", "run", "--dry-run", "--wake", "opportunity-delta-detected", "--detail", "integration local planner");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "planner", "status");
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var proposalId = sessionJson["planner_proposal_id"]!.GetValue<string>();
        var proposalPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "planner", $"{proposalId}.json");

        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, run.ExitCode);
        Assert.Equal(0, status.ExitCode);
        Assert.Contains("Planner proposal id:", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planner acceptance:", run.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Planner state:", status.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("sleeping", sessionJson["planner_lifecycle_state"]!.GetValue<string>());
        Assert.Equal("opportunity_delta_detected", sessionJson["planner_wake_reason"]!.GetValue<string>());
        Assert.True(File.Exists(proposalPath));
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(proposalPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlannerCommands_RunClaudePlannerAndPersistStructuredProposal()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.ClearPlannerGeneratedTasks();
        sandbox.ClearRefactoringSuggestions();
        sandbox.ClearOpportunities();
        sandbox.AddSyntheticRefactoringSmell();
        var plannerProposalJson = """
{"proposal_id":"claude-proposal","planner_backend":"claude_messages","goal_summary":"close a governed planning gap","recommended_action":"propose_work","sleep_recommendation":"existing_governed_work","escalation_recommendation":"none","proposed_cards":[],"proposed_tasks":[{"temp_id":"tmp-planner-memory","title":"Audit planner memory","description":"Refresh planner memory truth after opportunity evaluation.","task_type":"planning","priority":"P2","depends_on":[],"scope":[".ai/memory/modules/carves_runtime_application.md"],"proposal_source":"memory_audit","proposal_reason":"memory drift opportunity remains open","confidence":0.82,"acceptance":["governed planning task exists"],"constraints":["keep memory read-only until approved"],"metadata":{"origin_opportunity_id":"OPP-test-origin"}}],"dependencies":[],"risk_flags":[],"confidence":0.82,"rationale":"Claude proposes governed follow-up work only."}
""";
        await using var server = new StubResponsesApiServer($$"""
{
  "content": [
    {
      "type": "text",
      "text": {{System.Text.Json.JsonSerializer.Serialize(plannerProposalJson)}}
    }
  ]
}
""");
        sandbox.WriteAiProviderConfig($$"""
{
  "provider": "claude",
  "enabled": true,
  "model": "claude-3-7-sonnet-latest",
  "base_url": "{{server.Url}}",
  "api_key_environment_variable": "ANTHROPIC_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 10,
  "max_output_tokens": 600,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
""");

        var originalApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-anthropic-key");
        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
            var run = ProgramHarness.Run("--repo-root", sandbox.RootPath, "planner", "run", "--dry-run", "--wake", "explicit-human-wake", "--detail", "integration claude planner");
            var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
            var proposalId = sessionJson["planner_proposal_id"]!.GetValue<string>();
            var proposalPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "planner", $"{proposalId}.json");

            Assert.Equal(0, start.ExitCode);
            Assert.Equal(0, run.ExitCode);
            Assert.Contains("Planner adapter: ClaudePlannerAdapter/claude", run.StandardOutput, StringComparison.Ordinal);
            Assert.Equal("ClaudePlannerAdapter", sessionJson["planner_adapter_id"]!.GetValue<string>());
            Assert.True(File.Exists(proposalPath));
            Assert.Contains("\"provider_id\": \"claude\"", File.ReadAllText(proposalPath), StringComparison.Ordinal);
            Assert.Contains("\"acceptance_status\": \"accepted\"", File.ReadAllText(proposalPath), StringComparison.Ordinal);
            Assert.Contains("\"model\":\"claude-3-7-sonnet-latest\"", server.LastRequestBody, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void PlannerSleepAndWakeCommands_PersistLifecycleTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "session", "start", "--dry-run");
        var sleep = ProgramHarness.Run("--repo-root", sandbox.RootPath, "planner", "sleep", "waiting-for-human-action", "operator hold");
        var wake = ProgramHarness.Run("--repo-root", sandbox.RootPath, "planner", "wake", "explicit-human-wake", "resume planner");
        var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "planner", "status");
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();

        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, sleep.ExitCode);
        Assert.Equal(0, wake.ExitCode);
        Assert.Equal(0, status.ExitCode);
        Assert.Contains("Planner wake reason: explicit human wake", status.StandardOutput, StringComparison.Ordinal);
        Assert.Equal("idle", sessionJson["planner_lifecycle_state"]!.GetValue<string>());
        Assert.Equal("explicit_human_wake", sessionJson["planner_wake_reason"]!.GetValue<string>());
        Assert.Equal("resume planner", sessionJson["planner_lifecycle_reason"]!.GetValue<string>());
    }
}
