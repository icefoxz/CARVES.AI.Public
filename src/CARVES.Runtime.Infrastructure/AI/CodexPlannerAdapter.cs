using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Infrastructure.AI;

public sealed class CodexPlannerAdapter : IPlannerAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly OpenAiResponsesClient aiClient;
    private readonly AiProviderConfig config;
    private readonly string requestFamily;

    public CodexPlannerAdapter(HttpClient httpClient, AiProviderConfig config, string selectionReason)
    {
        this.config = config;
        SelectionReason = selectionReason;
        aiClient = new OpenAiResponsesClient(httpClient, config);
        requestFamily = OpenAiCompatibleRequestFamily.Resolve(config);
    }

    public string AdapterId => nameof(CodexPlannerAdapter);

    public string ProviderId => "codex";

    public string? ProfileId => config.ProfileId ?? "codex-planner-high-context";

    public bool IsConfigured => aiClient.IsConfigured;

    public bool IsRealAdapter => true;

    public string SelectionReason { get; }

    public PlannerProposalEnvelope Run(PlannerRunRequest request)
    {
        if (!config.Enabled)
        {
            throw new InvalidOperationException("Codex planner adapter is disabled in ai_provider.json.");
        }

        if (!aiClient.IsConfigured)
        {
            throw new InvalidOperationException($"Environment variable '{config.ApiKeyEnvironmentVariable}' is not set.");
        }

        var systemPrompt = BuildSystemPrompt();
        var promptEnvelope = PlannerRequestEnvelopeBuilder.Build(
            request,
            systemPrompt,
            ProviderId,
            requestFamily,
            "planner_request_serializer.v1",
            requestFamily);
        var execution = aiClient.Execute(new AiExecutionRequest(
            request.ProposalId,
            "planner proposal",
            systemPrompt,
            promptEnvelope.UserPrompt,
            Math.Max(256, config.MaxOutputTokens),
            config.Model));

        var output = execution.OutputText ?? execution.ResponsePreview;
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException("Codex planner returned no text output.");
        }

        var parsedProposal = ParseProposal(output);
        var proposal = string.IsNullOrWhiteSpace(parsedProposal.ProposalId)
            ? new PlannerProposal
            {
                ProposalId = request.ProposalId,
                PlannerBackend = string.IsNullOrWhiteSpace(parsedProposal.PlannerBackend)
                    ? $"codex_{requestFamily}"
                    : parsedProposal.PlannerBackend,
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
                Model = execution.Model,
                RunId = request.Session.SessionId,
                TaskId = request.PreviewTasks.FirstOrDefault()?.TaskId,
            },
            TokenAccountingSource = execution.InputTokens.HasValue || execution.OutputTokens.HasValue ? "provider_actual" : "local_estimate",
            ProviderReportedInputTokens = execution.InputTokens,
            ProviderReportedUncachedInputTokens = execution.InputTokens,
            ProviderReportedOutputTokens = execution.OutputTokens,
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
            ?? throw new InvalidOperationException("Codex planner returned unparsable proposal JSON.");
    }
}
