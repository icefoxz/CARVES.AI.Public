using System.Net;
using System.Net.Http;
using System.Text;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Infrastructure.AI;

namespace Carves.Runtime.Application.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class OpenAiResponsesClientTests
{
    [Fact]
    public void Execute_ResponsesFamily_ParsesOutputAndUsesResponsesEndpoint()
    {
        const string responseJson = """
{
  "id": "resp_test_123",
  "model": "gpt-5-mini",
  "output": [
    {
      "type": "message",
      "content": [
        {
          "type": "output_text",
          "text": "scope: tests only"
        }
      ]
    }
  ],
  "usage": {
    "input_tokens": 12,
    "output_tokens": 9
  }
}
""";

        using var environment = new EnvironmentVariableScope("OPENAI_API_KEY", "test-key");
        using var handler = new StubHandler((request, _) =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiResponsesClient(httpClient, CreateConfig());

        var result = client.Execute(new AiExecutionRequest(
            "T-OPENAI-RESPONSES",
            "Responses test",
            "Keep it brief.",
            "Verify the responses payload path.",
            300));

        Assert.Equal("scope: tests only", result.OutputText);
        Assert.Equal(12, result.InputTokens);
        Assert.Equal(9, result.OutputTokens);
        Assert.Equal("https://example.test/v1/responses", handler.LastRequestUri);
        Assert.Contains("\"model\":\"gpt-5-mini\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_ChatCompletionsFamily_ParsesOutputAndUsesChatEndpoint()
    {
        const string responseJson = """
{
  "id": "chatcmpl_test_456",
  "model": "gpt-4.1",
  "choices": [
    {
      "message": {
        "content": "chat path verified"
      }
    }
  ],
  "usage": {
    "prompt_tokens": 7,
    "completion_tokens": 5
  }
}
""";

        using var environment = new EnvironmentVariableScope("OPENAI_API_KEY", "test-key");
        using var handler = new StubHandler((request, _) =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            };
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAiResponsesClient(httpClient, CreateConfig(requestFamily: "chat_completions", model: "gpt-4.1"));

        var result = client.Execute(new AiExecutionRequest(
            "T-OPENAI-CHAT",
            "Chat test",
            "Answer plainly.",
            "Verify the chat completions payload path.",
            300));

        Assert.Equal("chat path verified", result.OutputText);
        Assert.Equal(7, result.InputTokens);
        Assert.Equal(5, result.OutputTokens);
        Assert.Equal("https://example.test/v1/chat/completions", handler.LastRequestUri);
        Assert.Contains("\"messages\":", handler.LastRequestBody, StringComparison.Ordinal);
    }

    private static AiProviderConfig CreateConfig(string? requestFamily = null, string model = "gpt-5-mini")
    {
        return new AiProviderConfig(
            "openai",
            true,
            model,
            "https://example.test/v1",
            "OPENAI_API_KEY",
            false,
            30,
            300,
            "low",
            requestFamily,
            null,
            null);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public string? LastRequestUri { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : ReadBody(request.Content);
            return handler(request, cancellationToken);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return handler(request, cancellationToken);
        }

        private static string ReadBody(HttpContent content)
        {
            using var stream = content.ReadAsStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string name;
        private readonly string? originalValue;

        public EnvironmentVariableScope(string name, string value)
        {
            this.name = name;
            originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(name, originalValue);
        }
    }
}
