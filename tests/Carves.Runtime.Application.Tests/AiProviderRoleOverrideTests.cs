using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Infrastructure.AI;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class AiProviderRoleOverrideTests
{
    [Fact]
    public void ControlPlaneConfigRepository_LoadsAiProviderProfilesAndRoleBindings()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/config/ai_provider.json", """
{
  "default_profile": "shared-codex",
  "provider": "codex",
  "enabled": true,
  "model": "gpt-5-codex",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 45,
  "max_output_tokens": 500,
  "reasoning_effort": "low",
  "profiles": {
    "shared-codex": {
      "provider": "codex",
      "model": "gpt-5-codex",
      "base_url": "https://api.openai.com/v1",
      "api_key_environment_variable": "OPENAI_API_KEY",
      "request_family": "responses_api"
    }
  },
  "roles": {
    "worker": "shared-codex",
    "planner": "shared-codex"
  }
}
""");

        var repository = new FileControlPlaneConfigRepository(workspace.Paths);
        var config = repository.LoadAiProviderConfig();
        var worker = config.ResolveForRole("worker");
        var planner = config.ResolveForRole("planner");

        Assert.Equal("codex", config.Provider);
        Assert.Equal("shared-codex", config.DefaultProfileId);
        Assert.Equal("shared-codex", config.ProfileId);
        Assert.Equal("shared-codex", config.GetRoleProfiles()["worker"]);
        Assert.Equal("shared-codex", config.GetRoleProfiles()["planner"]);
        Assert.Equal("codex", worker.Provider);
        Assert.Equal("gpt-5-codex", worker.Model);
        Assert.Equal("OPENAI_API_KEY", worker.ApiKeyEnvironmentVariable);
        Assert.Equal("shared-codex", worker.ProfileId);
        Assert.Equal("codex", planner.Provider);
        Assert.Equal("gpt-5-codex", planner.Model);
        Assert.Equal("responses_api", planner.RequestFamily);
        Assert.Equal("shared-codex", planner.ProfileId);
    }

    [Fact]
    public void PlannerAdapterFactory_UsesCodexPlannerProfileBindingWhenGlobalProviderIsCodex()
    {
        var config = AiProviderConfig.CreateProviderDefaults("codex", true, false, 45, 500, "low") with
        {
            DefaultProfileId = "shared-codex",
            ProfileId = "shared-codex",
            Profiles = new Dictionary<string, AiProviderConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["shared-codex"] = AiProviderConfig.CreateProviderDefaults("codex", true, false, 45, 500, "low") with
                {
                    ProfileId = "shared-codex",
                },
            },
            RoleProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["planner"] = "shared-codex",
            },
        };

        var registry = PlannerAdapterFactory.Create(config);

        var adapter = Assert.IsAssignableFrom<IPlannerAdapter>(registry.ActiveAdapter);
        Assert.Equal("CodexPlannerAdapter", adapter.AdapterId);
        Assert.Equal("codex", adapter.ProviderId);
        Assert.Equal("shared-codex", adapter.ProfileId);
        Assert.Contains("selected the codex planner adapter", adapter.SelectionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CodexPlannerAdapter_ParsesPlannerProposalFromOpenAiResponsesPayload()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        try
        {
            var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
{
  "id": "resp_planner_123",
  "model": "gpt-5-codex",
  "output": [
    {
      "type": "message",
      "content": [
        {
          "type": "output_text",
          "text": "{ \"proposal_id\": \"P-CODEX\", \"planner_backend\": \"codex_responses_api\", \"goal_summary\": \"Codex planning test\", \"recommended_action\": \"sleep\", \"sleep_recommendation\": \"existing_governed_work\", \"escalation_recommendation\": \"none\", \"proposed_tasks\": [{ \"temp_id\": \"T-1\", \"title\": \"Codex task\", \"description\": \"Codex planner task\", \"task_type\": \"planning\", \"depends_on\": [], \"scope\": [\"src\"], \"proposal_source\": \"codex\", \"proposal_reason\": \"planner output\", \"confidence\": 0.92, \"priority\": \"P2\", \"acceptance\": [\"done\"], \"constraints\": [\"bounded\"], \"metadata\": {} }], \"dependencies\": [], \"risk_flags\": [], \"confidence\": 0.92, \"rationale\": \"Codex planner proposal.\" }"
        }
      ]
    }
  ],
  "usage": {
    "input_tokens": 11,
    "output_tokens": 23
  }
}
""", Encoding.UTF8, "application/json"),
            });
            var httpClient = new HttpClient(handler);
            var config = AiProviderConfig.CreateProviderDefaults("codex", true, false, 45, 500, "low") with
            {
                ProfileId = "shared-codex",
                RequestFamily = "responses_api",
            };
            var adapter = new CodexPlannerAdapter(httpClient, config, "configured codex planner");
            var request = new PlannerRunRequest
            {
                ProposalId = "P-CODEX",
                GoalSummary = "Codex planning test",
                CurrentStage = "runtime",
                WakeReason = PlannerWakeReason.None,
                WakeDetail = "test wake",
                TaskGraphSummary = "graph summary",
                BlockedTaskSummary = "blocked summary",
                OpportunitySummary = "opportunity summary",
                MemorySummary = "memory summary",
                CodeGraphSummary = "code graph summary",
                GovernanceSummary = "governance summary",
                NamingSummary = "naming summary",
                DependencySummary = "dependency summary",
                FailureSummary = "failure summary",
            };

            var envelope = adapter.Run(request);

            Assert.Equal("CodexPlannerAdapter", envelope.AdapterId);
            Assert.Equal("codex", envelope.ProviderId);
            Assert.Equal("shared-codex", envelope.ProfileId);
            Assert.Equal("P-CODEX", envelope.Proposal.ProposalId);
            Assert.Equal("codex_responses_api", envelope.Proposal.PlannerBackend);
            Assert.Equal("Codex planning test", envelope.Proposal.GoalSummary);
            Assert.Equal(PlannerRecommendedAction.Sleep, envelope.Proposal.RecommendedAction);
            Assert.Equal(PlannerSleepReason.ExistingGovernedWork, envelope.Proposal.SleepRecommendation);
            Assert.Single(envelope.Proposal.ProposedTasks);
            Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
            Assert.EndsWith("/responses", handler.LastRequest.RequestUri!.ToString(), StringComparison.Ordinal);
            Assert.Equal("Bearer test-openai-key", handler.LastRequest.Headers.Authorization?.ToString());
            Assert.Contains("proposal_id: P-CODEX", handler.LastRequestBody, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void ResolveForRole_StillSupportsLegacyInlineRoleOverrides()
    {
        var config = AiProviderConfig.CreateProviderDefaults("codex", true, false, 45, 500, "low") with
        {
            RoleOverrides = new Dictionary<string, AiProviderRoleConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["worker"] = new(
                    Provider: "claude",
                    Model: "claude-sonnet-4-5",
                    BaseUrl: "https://api.anthropic.com/v1",
                    ApiKeyEnvironmentVariable: "ANTHROPIC_API_KEY",
                    RequestFamily: "messages_api"),
            },
        };

        var worker = config.ResolveForRole("worker");

        Assert.Equal("claude", worker.Provider);
        Assert.Equal("claude-sonnet-4-5", worker.Model);
        Assert.Equal("messages_api", worker.RequestFamily);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            this.handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null
                ? string.Empty
                : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return handler(request);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }
    }
}
