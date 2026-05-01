using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Infrastructure.AI;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class RemoteApiWorkerAdapterTests
{
    [Fact]
    public void OpenAiCompatibleWorkerAdapter_MapsResponsesProtocolThroughRemoteAdapter()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 200,
                Body = """
{
  "id": "resp_123",
  "model": "gpt-5-mini",
  "output": [
    {
      "type": "message",
      "content": [
        { "type": "output_text", "text": "OpenAI-compatible worker output." }
      ]
    }
  ],
  "usage": {
    "input_tokens": 12,
    "output_tokens": 34
  }
}
""",
            });
            var registry = TestWorkerAdapterRegistryFactory.Create("openai", transport);
            var adapter = registry.Resolve("openai_api");

            var result = adapter.Execute(BuildRequest("T-OPENAI", "OpenAI-compatible worker"));

            Assert.True(result.Succeeded);
            Assert.Equal("openai_compatible", result.ProtocolFamily);
            Assert.Equal("responses_api", result.RequestFamily);
            Assert.Equal("resp_123", result.RunId);
            Assert.Equal("OpenAI-compatible worker output.", result.Summary);
            Assert.Equal("OpenAI-compatible worker output.", result.Rationale);
            Assert.Contains(result.Events, item => item.EventType == WorkerEventType.FinalSummary);
            var trace = Assert.Single(result.CommandTrace);
            Assert.Equal("remote_api", trace.Category);
            Assert.Equal(0, trace.ExitCode);
            Assert.Contains("OpenAI-compatible worker output.", trace.StandardOutput, StringComparison.Ordinal);
            Assert.Equal("Bearer test-openai-key", transport.LastRequest!.Headers["Authorization"]);
            Assert.EndsWith("/responses", transport.LastRequest.Url, StringComparison.Ordinal);
            Assert.Contains("\"instructions\":\"Follow the task contract.\"", transport.LastRequest.Body, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void RemoteApiWorkerAdapter_ProjectsSharedExecutionEnvelopeMetadata()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 200,
                Body = """
{
  "id": "resp_shared_123",
  "model": "gpt-5-mini",
  "output": [
    {
      "type": "message",
      "content": [
        { "type": "output_text", "text": "Shared envelope output." }
      ]
    }
  ],
  "usage": {
    "input_tokens": 7,
    "output_tokens": 11
  }
}
""",
            });
            var registry = TestWorkerAdapterRegistryFactory.Create("openai", transport);
            var adapter = registry.Resolve("openai_api");
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-OPENAI-SHARED-ENVELOPE",
                Title = "OpenAI shared envelope",
                Instructions = "Follow the task contract.",
                Input = "Summarize the delegated execution result envelope.",
                MaxOutputTokens = 256,
                TimeoutSeconds = 30,
                RepoRoot = "D:/Repo",
                WorktreeRoot = "D:/Repo/.carves-worktrees/test",
                BaseCommit = "abc123",
                BackendHint = string.Empty,
                ModelOverride = "gpt-5-mini",
                PriorThreadId = "previous-thread",
                Profile = WorkerExecutionProfile.UntrustedDefault,
            };

            var result = adapter.Execute(request);
            var expectedRequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant();

            Assert.True(result.Succeeded);
            Assert.Equal(request.TaskId, result.TaskId);
            Assert.Equal(request.PriorThreadId, result.PriorThreadId);
            Assert.Equal(WorkerThreadContinuity.None, result.ThreadContinuity);
            Assert.Equal("Summarize the delegated execution result envelope.", result.RequestPreview);
            Assert.Equal(expectedRequestHash, result.RequestHash);
            Assert.Equal("Shared envelope output.", result.ResponsePreview);
            Assert.Equal(adapter.SelectionReason, result.AdapterReason);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Theory]
    [InlineData("https://api.groq.com/openai/v1", "GROQ_API_KEY")]
    [InlineData("https://api.deepseek.com", "DEEPSEEK_API_KEY")]
    public void OpenAiCompatibleWorkerAdapter_MapsChatCompletionsProtocolForCompatibleBaseUrls(string baseUrl, string apiKeyEnv)
    {
        var originalApiKey = Environment.GetEnvironmentVariable(apiKeyEnv);
        Environment.SetEnvironmentVariable(apiKeyEnv, "test-compatible-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 200,
                Body = """
{
  "id": "chatcmpl_123",
  "model": "deepseek-chat",
  "choices": [
    {
      "message": {
        "role": "assistant",
        "content": "OpenAI-compatible chat completions output."
      }
    }
  ],
  "usage": {
    "prompt_tokens": 11,
    "completion_tokens": 22
  }
}
""",
            });
            var registry = TestWorkerAdapterRegistryFactory.Create(
                provider: "openai",
                transport: transport,
                model: "deepseek-chat",
                baseUrl: baseUrl,
                apiKeyEnvironmentVariable: apiKeyEnv);
            var adapter = registry.Resolve("openai_api");

            var result = adapter.Execute(BuildRequest("T-COMPAT", "OpenAI-compatible chat completions worker"));
            using var body = JsonDocument.Parse(transport.LastRequest!.Body!);

            Assert.True(result.Succeeded);
            Assert.Equal("openai_compatible", result.ProtocolFamily);
            Assert.Equal("chat_completions", result.RequestFamily);
            Assert.Equal("chatcmpl_123", result.RunId);
            Assert.Equal("OpenAI-compatible chat completions output.", result.Summary);
            Assert.Equal("Bearer test-compatible-key", transport.LastRequest.Headers["Authorization"]);
            Assert.EndsWith("/chat/completions", transport.LastRequest.Url, StringComparison.Ordinal);
            Assert.True(body.RootElement.TryGetProperty("messages", out var messages));
            Assert.Equal(2, messages.GetArrayLength());
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.Equal("user", messages[1].GetProperty("role").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(apiKeyEnv, originalApiKey);
        }
    }

    [Fact]
    public void GeminiWorkerAdapter_UsesNativeGenerateContentProtocol()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", "test-gemini-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 200,
                Body = """
{
  "candidates": [
    {
      "content": {
        "parts": [
          { "text": "Gemini worker output." }
        ]
      }
    }
  ],
  "usageMetadata": {
    "promptTokenCount": 21,
    "candidatesTokenCount": 13
  },
  "modelVersion": "gemini-2.5-pro"
}
""",
            });
            var registry = TestWorkerAdapterRegistryFactory.Create("gemini", transport);
            var adapter = registry.Resolve("gemini_api");

            var result = adapter.Execute(BuildRequest("T-GEMINI", "Gemini worker", modelOverride: "gemini-2.5-pro", reasoningEffort: "low"));
            using var body = JsonDocument.Parse(transport.LastRequest!.Body!);

            Assert.True(result.Succeeded);
            Assert.Equal("gemini_native", result.ProtocolFamily);
            Assert.Equal("generate_content", result.RequestFamily);
            Assert.Equal("Gemini worker output.", result.Summary);
            Assert.Equal("gemini-2.5-pro", result.Model);
            Assert.Single(result.CommandTrace);
            Assert.Equal("test-gemini-key", transport.LastRequest.Headers["x-goog-api-key"]);
            Assert.Contains("/models/gemini-2.5-pro:generateContent", transport.LastRequest.Url, StringComparison.Ordinal);
            Assert.True(body.RootElement.TryGetProperty("system_instruction", out _));
            Assert.True(body.RootElement.TryGetProperty("contents", out _));
            Assert.Equal(128, body.RootElement.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void ClaudeWorkerAdapter_UsesAnthropicMessagesProtocol()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-anthropic-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 200,
                Body = """
{
  "id": "msg_123",
  "model": "claude-sonnet-4-5",
  "content": [
    {
      "type": "text",
      "text": "Claude worker output."
    }
  ],
  "usage": {
    "input_tokens": 15,
    "output_tokens": 21
  }
}
""",
            });
            var registry = TestWorkerAdapterRegistryFactory.Create("claude", transport);
            var adapter = registry.Resolve("claude_api");

            var result = adapter.Execute(BuildRequest("T-CLAUDE", "Claude worker", modelOverride: "claude-sonnet-4-5"));
            using var body = JsonDocument.Parse(transport.LastRequest!.Body!);

            Assert.True(result.Succeeded);
            Assert.Equal("anthropic_native", result.ProtocolFamily);
            Assert.Equal("messages_api", result.RequestFamily);
            Assert.Equal("msg_123", result.RunId);
            Assert.Equal("Claude worker output.", result.Summary);
            Assert.Equal("test-anthropic-key", transport.LastRequest.Headers["x-api-key"]);
            Assert.Equal("2023-06-01", transport.LastRequest.Headers["anthropic-version"]);
            Assert.EndsWith("/messages", transport.LastRequest.Url, StringComparison.Ordinal);
            Assert.True(body.RootElement.TryGetProperty("system", out _));
            Assert.True(body.RootElement.TryGetProperty("messages", out var messages));
            Assert.Equal("user", messages[0].GetProperty("role").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerAdapterRegistry_UsesWorkerProfileBindingForClaudeEvenWhenGlobalProviderIsGemini()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "test-anthropic-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 200,
                Body = """
{
  "id": "msg_route_123",
  "model": "claude-sonnet-4-5",
  "content": [
    {
      "type": "text",
      "text": "Lane-specific Claude fallback executed."
    }
  ],
  "usage": {
    "input_tokens": 8,
    "output_tokens": 13
  }
}
""",
            });
            var config = AiProviderConfig.CreateProviderDefaults("gemini", true, true, 30, 500, "low") with
            {
                DefaultProfileId = "global-gemini",
                ProfileId = "global-gemini",
                Profiles = new Dictionary<string, AiProviderConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["global-gemini"] = AiProviderConfig.CreateProviderDefaults("gemini", true, true, 30, 500, "low"),
                    ["worker-claude"] = AiProviderConfig.CreateProviderDefaults("claude", true, true, 30, 500, "low") with
                    {
                        ProfileId = "worker-claude",
                    },
                },
                RoleProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["worker"] = "worker-claude",
                },
            };
            var registry = WorkerAdapterFactory.Create(config, new NullAiClient(), transport);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-ROLE-CLAUDE",
                Title = "Worker role Claude config request",
                Instructions = "Follow the task contract.",
                Input = "Use the configured Claude worker lane.",
                MaxOutputTokens = 256,
                TimeoutSeconds = 30,
                RepoRoot = "D:/Repo",
                WorktreeRoot = "D:/Repo/.carves-worktrees/test",
                BaseCommit = "abc123",
                BackendHint = "claude_api",
                ModelOverride = "claude-sonnet-4-5",
                Profile = WorkerExecutionProfile.UntrustedDefault,
            };
            var adapter = registry.Resolve("claude_api");

            var result = adapter.Execute(request);

            Assert.True(result.Succeeded);
            Assert.Contains("selected the claude worker adapter", adapter.SelectionReason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("messages_api", result.RequestFamily);
            Assert.EndsWith("/messages", transport.LastRequest!.Url, StringComparison.Ordinal);
            Assert.StartsWith("https://api.anthropic.com/v1", transport.LastRequest.Url, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", originalApiKey);
        }
    }
    [Fact]
    public void GeminiWorkerAdapter_ProjectsUnavailableHealthWithoutApiKey()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
        try
        {
            var registry = TestWorkerAdapterRegistryFactory.Create("gemini", new StubHttpTransport(_ => throw new InvalidOperationException("should not send")));
            var adapter = registry.Resolve("gemini_api");
            var health = adapter.CheckHealth();

            Assert.Equal(WorkerBackendHealthState.Unavailable, health.State);
            Assert.Contains("GEMINI_API_KEY", health.Summary, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void OpenAiCompatibleWorkerAdapter_FailurePreservesRequestedModelOverride()
    {
        var originalApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 404,
                Body = """
{
  "error": {
    "message": "model not found"
  }
}
""",
            });
            var registry = TestWorkerAdapterRegistryFactory.Create("openai", transport);
            var adapter = registry.Resolve("openai_api");
            var request = BuildRequest("T-OPENAI-FAIL", "OpenAI-compatible worker failure", modelOverride: "llama-3.3-70b-versatile");

            var result = adapter.Execute(request);

            Assert.False(result.Succeeded);
            Assert.Equal("llama-3.3-70b-versatile", result.Model);
            Assert.Equal("responses_api", result.RequestFamily);
            Assert.Equal("openai_compatible", result.ProtocolFamily);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalApiKey);
        }
    }

    [Fact]
    public void WorkerAdapterRegistry_UsesWorkerProfileBindingForOpenAiCompatibleConfigEvenWhenGlobalProviderIsGemini()
    {
        var originalOpenAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Environment.SetEnvironmentVariable("OPENAI_API_KEY", "test-openai-key");
        try
        {
            var transport = new StubHttpTransport(_ => new HttpTransportResponse
            {
                StatusCode = 200,
                Body = """
{
  "id": "resp_456",
  "model": "gpt-4.1",
  "output": [
    {
      "type": "message",
      "content": [
        { "type": "output_text", "text": "Lane-specific fallback executed." }
      ]
    }
  ],
  "usage": {
    "input_tokens": 8,
    "output_tokens": 13
  }
}
""",
            });
            var config = AiProviderConfig.CreateProviderDefaults("gemini", true, true, 30, 500, "low") with
            {
                DefaultProfileId = "global-gemini",
                ProfileId = "global-gemini",
                Profiles = new Dictionary<string, AiProviderConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["global-gemini"] = AiProviderConfig.CreateProviderDefaults("gemini", true, true, 30, 500, "low"),
                    ["worker-openai"] = AiProviderConfig.CreateProviderDefaults("openai", true, true, 30, 500, "low") with
                    {
                        Model = "gpt-4.1",
                        BaseUrl = "https://hk.n1n.ai/v1",
                        ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
                        RequestFamily = "responses_api",
                        ProfileId = "worker-openai",
                    },
                },
                RoleProfiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["worker"] = "worker-openai",
                },
            };
            var registry = WorkerAdapterFactory.Create(config, new NullAiClient(), transport);
            var request = new WorkerExecutionRequest
            {
                TaskId = "T-ROLE-OPENAI",
                Title = "Worker role OpenAI config request",
                Instructions = "Follow the task contract.",
                Input = "Use the configured OpenAI-compatible lane.",
                MaxOutputTokens = 256,
                TimeoutSeconds = 30,
                RepoRoot = "D:/Repo",
                WorktreeRoot = "D:/Repo/.carves-worktrees/test",
                BaseCommit = "abc123",
                BackendHint = "openai_api",
                ModelOverride = "gpt-4.1",
                Profile = WorkerExecutionProfile.UntrustedDefault,
            };
            var adapter = registry.Resolve("openai_api");

            var result = adapter.Execute(request);

            Assert.True(result.Succeeded);
            Assert.Contains("selected the openai worker adapter", adapter.SelectionReason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("responses_api", result.RequestFamily);
            Assert.EndsWith("/responses", transport.LastRequest!.Url, StringComparison.Ordinal);
            Assert.StartsWith("https://hk.n1n.ai/v1", transport.LastRequest.Url, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", originalOpenAiApiKey);
        }
    }

    private static WorkerExecutionRequest BuildRequest(string taskId, string title, string modelOverride = "gpt-5-mini", string? reasoningEffort = null)
    {
        return new WorkerExecutionRequest
        {
            TaskId = taskId,
            Title = title,
            Instructions = "Follow the task contract.",
            Input = "Implement the approved execution task.",
            MaxOutputTokens = 256,
            TimeoutSeconds = 30,
            RepoRoot = "D:/Repo",
            WorktreeRoot = "D:/Repo/.carves-worktrees/test",
            BaseCommit = "abc123",
            BackendHint = string.Empty,
            ModelOverride = modelOverride,
            ReasoningEffort = reasoningEffort,
            Profile = WorkerExecutionProfile.UntrustedDefault,
        };
    }
}
