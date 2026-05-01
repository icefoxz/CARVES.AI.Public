using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class ClaudePlannerAdapter : IPlannerAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly HttpClient httpClient;
    private readonly AiProviderConfig config;
    private readonly string? apiKey;

    public ClaudePlannerAdapter(HttpClient httpClient, AiProviderConfig config, string selectionReason)
    {
        this.httpClient = httpClient;
        this.config = config;
        SelectionReason = selectionReason;
        apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable);
    }

    public string AdapterId => nameof(ClaudePlannerAdapter);

    public string ProviderId => "claude";

    public string? ProfileId => config.ProfileId ?? "claude-planner-high-context";

    public bool IsConfigured => config.Enabled && !string.IsNullOrWhiteSpace(apiKey);

    public bool IsRealAdapter => true;

    public string SelectionReason { get; }

    public PlannerProposalEnvelope Run(PlannerRunRequest request)
    {
        if (!config.Enabled)
        {
            throw new InvalidOperationException("Claude planner adapter is disabled in ai_provider.json.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException($"Environment variable '{config.ApiKeyEnvironmentVariable}' is not set.");
        }

        var systemPrompt = BuildSystemPrompt();
        var promptEnvelope = PlannerRequestEnvelopeBuilder.Build(
            request,
            systemPrompt,
            ProviderId,
            "2023-06-01",
            "anthropic_messages_request.v1",
            "messages_api");
        var payload = BuildRequestPayload(systemPrompt, promptEnvelope.UserPrompt);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{config.BaseUrl.TrimEnd('/')}/messages")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("x-api-key", apiKey);
        message.Headers.Add("anthropic-version", "2023-06-01");
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = httpClient.Send(message);
        var responseBody = HttpContentSyncReader.ReadAsString(response.Content);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Claude Messages API returned {(int)response.StatusCode}: {responseBody}");
        }

        var responseEnvelope = JsonSerializer.Deserialize<ClaudeResponseEnvelope>(responseBody, JsonOptions)
            ?? throw new InvalidOperationException("Claude Messages API returned an empty payload.");
        var output = string.Join("\n", responseEnvelope.Content.Where(item => !string.IsNullOrWhiteSpace(item.Text)).Select(item => item.Text));
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("Claude planner returned no text output.");
        }

        var parsedProposal = ParseProposal(output);
        var proposal = string.IsNullOrWhiteSpace(parsedProposal.ProposalId)
            ? new PlannerProposal
            {
                ProposalId = request.ProposalId,
                PlannerBackend = string.IsNullOrWhiteSpace(parsedProposal.PlannerBackend) ? "claude_messages" : parsedProposal.PlannerBackend,
                GoalSummary = string.IsNullOrWhiteSpace(parsedProposal.GoalSummary) ? request.GoalSummary : parsedProposal.GoalSummary,
                RecommendedAction = parsedProposal.RecommendedAction,
                SleepRecommendation = parsedProposal.SleepRecommendation,
                EscalationRecommendation = parsedProposal.EscalationRecommendation,
                ProposedCards = parsedProposal.ProposedCards,
                ProposedTasks = parsedProposal.ProposedTasks,
                Dependencies = parsedProposal.Dependencies,
                RiskFlags = parsedProposal.RiskFlags,
                Confidence = parsedProposal.Confidence,
                Rationale = parsedProposal.Rationale,
            }
            : parsedProposal;

        return new PlannerProposalEnvelope
        {
            ProposalId = request.ProposalId,
            AdapterId = AdapterId,
            ProviderId = ProviderId,
            ProfileId = ProfileId,
            Configured = true,
            UsedFallback = false,
            WakeReason = request.WakeReason,
            WakeDetail = request.WakeDetail,
            Proposal = proposal,
            RawResponsePreview = output.Length > 400 ? output[..400] : output,
            RequestEnvelopeDraft = promptEnvelope.Draft with
            {
                Model = config.Model,
                RunId = request.Session.SessionId,
                TaskId = request.PreviewTasks.FirstOrDefault()?.TaskId,
            },
            TokenAccountingSource = responseEnvelope.Usage?.InputTokens.HasValue == true || responseEnvelope.Usage?.OutputTokens.HasValue == true
                ? "provider_actual"
                : "local_estimate",
            ProviderReportedInputTokens = responseEnvelope.Usage?.InputTokens,
            ProviderReportedUncachedInputTokens = responseEnvelope.Usage?.InputTokens,
            ProviderReportedOutputTokens = responseEnvelope.Usage?.OutputTokens,
        };
    }

    private static string BuildSystemPrompt()
    {
        return """
You are the planner brain for CARVES.Runtime.
Return JSON only.
Do not include markdown fences.
Do not write code.
Only propose governed planner tasks and dependencies.
Use snake_case enum strings.
""";
    }

    private string BuildRequestPayload(string systemPrompt, string userPrompt)
    {
        var requestBody = new ClaudeMessagesRequest
        {
            Model = config.Model,
            MaxTokens = Math.Max(256, config.MaxOutputTokens),
            System = systemPrompt,
            Messages =
            [
                new ClaudeMessage
                {
                    Role = "user",
                    Content = userPrompt,
                },
            ],
        };

        return JsonSerializer.Serialize(requestBody, JsonOptions);
    }

    private static PlannerProposal ParseProposal(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Trim('`').Trim();
            if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["json".Length..].Trim();
            }
        }

        return JsonSerializer.Deserialize<PlannerProposal>(trimmed, JsonOptions)
            ?? throw new InvalidOperationException("Claude planner returned unparsable proposal JSON.");
    }

    private sealed class ClaudeMessagesRequest
    {
        public string Model { get; init; } = string.Empty;

        public int MaxTokens { get; init; }

        public string System { get; init; } = string.Empty;

        public IReadOnlyList<ClaudeMessage> Messages { get; init; } = Array.Empty<ClaudeMessage>();
    }

    private sealed class ClaudeMessage
    {
        public string Role { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;
    }

    private sealed class ClaudeResponseEnvelope
    {
        public IReadOnlyList<ClaudeResponseContent> Content { get; init; } = Array.Empty<ClaudeResponseContent>();

        public ClaudeUsageEnvelope? Usage { get; init; }
    }

    private sealed class ClaudeResponseContent
    {
        public string Type { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;
    }

    private sealed class ClaudeUsageEnvelope
    {
        public int? InputTokens { get; init; }

        public int? OutputTokens { get; init; }
    }
}
