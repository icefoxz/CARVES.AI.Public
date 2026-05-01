using System.Net.Http.Headers;
using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

public sealed partial class OpenAiResponsesClient : IAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient httpClient;
    private readonly AiProviderConfig config;
    private readonly string? apiKey;
    private readonly string requestFamily;

    public OpenAiResponsesClient(HttpClient httpClient, AiProviderConfig config)
    {
        this.httpClient = httpClient;
        this.config = config;
        apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable);
        requestFamily = OpenAiCompatibleRequestFamily.Resolve(config);
    }

    public string ClientName => nameof(OpenAiResponsesClient);

    public bool IsConfigured => config.Enabled && !string.IsNullOrWhiteSpace(apiKey);

    public AiExecutionRecord Execute(AiExecutionRequest request)
    {
        if (!config.Enabled)
        {
            throw new InvalidOperationException("OpenAI provider is disabled in ai_provider.json.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Environment variable '{config.ApiKeyEnvironmentVariable}' is not set.");
        }

        var model = request.ModelOverride ?? config.Model;
        var requestBody = BuildRequestBody(model, request);
        using var message = new HttpRequestMessage(HttpMethod.Post, ResolveEndpoint())
        {
            Content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (!string.IsNullOrWhiteSpace(config.Organization))
        {
            message.Headers.Add("OpenAI-Organization", config.Organization);
        }
        if (!string.IsNullOrWhiteSpace(config.Project))
        {
            message.Headers.Add("OpenAI-Project", config.Project);
        }

        using var response = httpClient.Send(message);
        var responseBody = HttpContentSyncReader.ReadAsString(response.Content);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI-compatible API returned {(int)response.StatusCode}: {responseBody}");
        }

        var payload = requestFamily == OpenAiCompatibleRequestFamily.ChatCompletions
            ? ParseChatCompletionsResponse(responseBody, model)
            : ParseResponsesResponse(responseBody, model);
        var outputText = payload.OutputText;
        if (string.IsNullOrWhiteSpace(outputText))
        {
            throw new InvalidOperationException("OpenAI-compatible API returned no text output.");
        }

        return new AiExecutionRecord
        {
            Provider = "openai",
            Model = payload.Model ?? model,
            Configured = true,
            Succeeded = true,
            RequestId = payload.Id,
            RequestPreview = BuildPreview(request.Input),
            RequestHash = Hash(request.Input),
            ResponsePreview = BuildPreview(outputText),
            ResponseHash = Hash(outputText),
            OutputText = outputText,
            InputTokens = payload.InputTokens,
            OutputTokens = payload.OutputTokens,
            CapturedAt = DateTimeOffset.UtcNow,
        };
    }

    private string ResolveEndpoint()
    {
        return requestFamily == OpenAiCompatibleRequestFamily.ChatCompletions
            ? $"{config.BaseUrl.TrimEnd('/')}/chat/completions"
            : $"{config.BaseUrl.TrimEnd('/')}/responses";
    }
}
