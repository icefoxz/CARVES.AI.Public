using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Infrastructure.AI;

internal static class PlannerRequestEnvelopeBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    internal sealed record PlannerPromptEnvelope(string UserPrompt, LlmRequestEnvelopeDraft Draft);

    public static PlannerPromptEnvelope Build(
        PlannerRunRequest request,
        string systemPrompt,
        string providerId,
        string providerApiVersion,
        string requestSerializerVersion,
        string? requestFamily)
    {
        var segments = new List<LlmRequestEnvelopeSegmentDraft>();
        var userPrompt = new StringBuilder();
        var order = 0;

        segments.Add(NewSegment(
            "system",
            "system",
            null,
            order++,
            0,
            "system",
            "$.system",
            "system_text",
            systemPrompt,
            request.ProposalId,
            "planner_request_serializer.v1"));

        AppendLineSegment(userPrompt, segments, ref order, "proposal_id", request.ProposalId, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "goal_summary", request.GoalSummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "current_stage", request.CurrentStage, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "wake_reason", request.WakeReason.ToString(), request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "wake_detail", request.WakeDetail, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "task_graph_summary", request.TaskGraphSummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "blocked_task_summary", request.BlockedTaskSummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "opportunity_summary", request.OpportunitySummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "memory_summary", request.MemorySummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "codegraph_summary", request.CodeGraphSummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "governance_summary", request.GovernanceSummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "naming_summary", request.NamingSummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "dependency_summary", request.DependencySummary, request.ProposalId);
        AppendLineSegment(userPrompt, segments, ref order, "failure_summary", request.FailureSummary, request.ProposalId);

        userPrompt.AppendLine();
        var contextPackJson = JsonSerializer.Serialize(request.ContextPack, JsonOptions);
        var contextPackBlock = $"context_pack_json:{Environment.NewLine}{contextPackJson}";
        userPrompt.AppendLine("context_pack_json:");
        userPrompt.AppendLine(contextPackJson);
        segments.Add(NewSegment(
            "context_pack_json",
            "context_pack",
            "planner_user_prompt",
            order++,
            1,
            "user",
            "$.messages[0].content.context_pack_json",
            "context_pack_text",
            contextPackBlock,
            request.ContextPack?.PackId,
            "prose_v1"));

        userPrompt.AppendLine();
        const string returnBlock = "Return a PlannerProposal JSON object with fields:\nproposal_id, planner_backend, goal_summary, recommended_action, sleep_recommendation, escalation_recommendation, proposed_cards, proposed_tasks, dependencies, risk_flags, confidence, rationale.\nEach proposed task must include temp_id, title, description, task_type, depends_on, scope, proposal_source, proposal_reason, confidence, priority, acceptance, constraints, metadata.";
        userPrompt.AppendLine("Return a PlannerProposal JSON object with fields:");
        userPrompt.AppendLine("proposal_id, planner_backend, goal_summary, recommended_action, sleep_recommendation, escalation_recommendation, proposed_cards, proposed_tasks, dependencies, risk_flags, confidence, rationale.");
        userPrompt.AppendLine("Each proposed task must include temp_id, title, description, task_type, depends_on, scope, proposal_source, proposal_reason, confidence, priority, acceptance, constraints, metadata.");
        segments.Add(NewSegment(
            "planner_output_contract",
            "output_contract",
            "planner_user_prompt",
            order++,
            1,
            "user",
            "$.messages[0].content.output_contract",
            "chat_message_text",
            returnBlock,
            request.ProposalId,
            "planner_request_serializer.v1"));

        return new PlannerPromptEnvelope(
            userPrompt.ToString(),
            new LlmRequestEnvelopeDraft
            {
                RequestId = request.ProposalId,
                RequestKind = "planner",
                Model = string.Empty,
                Provider = providerId,
                ProviderApiVersion = providerApiVersion,
                Tokenizer = ContextBudgetPolicyResolver.EstimatorVersion,
                RequestSerializerVersion = requestSerializerVersion,
                RunId = request.Session.SessionId,
                TaskId = request.PreviewTasks.FirstOrDefault()?.TaskId,
                PackId = request.ContextPack?.PackId,
                WholeRequestText = string.Join($"{Environment.NewLine}{Environment.NewLine}", new[] { systemPrompt, userPrompt.ToString().TrimEnd() }),
                Segments = segments,
            });
    }

    private static void AppendLineSegment(
        StringBuilder builder,
        ICollection<LlmRequestEnvelopeSegmentDraft> segments,
        ref int order,
        string fieldId,
        string value,
        string sourceItemId)
    {
        var line = $"{fieldId}: {value}";
        builder.AppendLine(line);
        segments.Add(NewSegment(
            fieldId,
            fieldId,
            "planner_user_prompt",
            order++,
            1,
            "user",
            $"$.messages[0].content.{fieldId}",
            "chat_message_text",
            line,
            sourceItemId,
            "planner_request_serializer.v1"));
    }

    private static LlmRequestEnvelopeSegmentDraft NewSegment(
        string segmentId,
        string segmentKind,
        string? parentId,
        int order,
        int? messageIndex,
        string? role,
        string payloadPath,
        string serializationKind,
        string content,
        string? sourceItemId,
        string rendererVersion)
    {
        return new LlmRequestEnvelopeSegmentDraft
        {
            SegmentId = segmentId,
            SegmentKind = segmentKind,
            SegmentParentId = parentId,
            SegmentOrder = order,
            MessageIndex = messageIndex,
            Role = role,
            PayloadPath = payloadPath,
            SerializationKind = serializationKind,
            Content = content,
            SourceItemId = sourceItemId,
            RendererVersion = rendererVersion,
        };
    }
}
