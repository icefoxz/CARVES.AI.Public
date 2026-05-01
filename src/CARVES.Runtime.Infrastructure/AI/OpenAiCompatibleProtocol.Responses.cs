using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class OpenAiCompatibleProtocol
{
    private ParsedOpenAiResponse ParseResponsesApiResponse(WorkerExecutionRequest request, HttpTransportResponse response)
    {
        var payload = JsonSerializer.Deserialize<ResponseEnvelope>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI-compatible protocol returned an empty responses payload.");
        return new ParsedOpenAiResponse(
            payload.Id,
            payload.Model ?? request.ModelOverride ?? config.Model,
            ExtractOutputText(payload),
            payload.Usage?.InputTokens,
            payload.Usage?.OutputTokens);
    }

    private ParsedOpenAiResponse ParseChatCompletionsResponse(WorkerExecutionRequest request, HttpTransportResponse response)
    {
        var payload = JsonSerializer.Deserialize<ChatCompletionsEnvelope>(response.Body, JsonOptions)
            ?? throw new InvalidOperationException("OpenAI-compatible protocol returned an empty chat completions payload.");
        return new ParsedOpenAiResponse(
            payload.Id,
            payload.Model ?? request.ModelOverride ?? config.Model,
            ExtractChatCompletionText(payload),
            payload.Usage?.PromptTokens,
            payload.Usage?.CompletionTokens);
    }

    private static string ExtractOutputText(ResponseEnvelope payload)
    {
        if (payload.Output is null)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        foreach (var item in payload.Output)
        {
            if (item.Content is null)
            {
                continue;
            }

            foreach (var content in item.Content)
            {
                if (string.Equals(content.Type, "output_text", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(content.Text))
                {
                    parts.Add(content.Text);
                }
            }
        }

        return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string ExtractChatCompletionText(ChatCompletionsEnvelope payload)
    {
        if (payload.Choices is null)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            payload.Choices
                .Select(choice => choice.Message?.Content)
                .Where(content => !string.IsNullOrWhiteSpace(content)));
    }

    private sealed class ResponseEnvelope
    {
        public string? Id { get; init; }

        public string? Model { get; init; }

        public OutputItem[]? Output { get; init; }

        public UsageEnvelope? Usage { get; init; }
    }

    private sealed class OutputItem
    {
        public string? Type { get; init; }

        public ContentItem[]? Content { get; init; }
    }

    private sealed class ContentItem
    {
        public string? Type { get; init; }

        public string? Text { get; init; }
    }

    private sealed class UsageEnvelope
    {
        public int? InputTokens { get; init; }

        public int? OutputTokens { get; init; }
    }

    private sealed class ChatCompletionsEnvelope
    {
        public string? Id { get; init; }

        public string? Model { get; init; }

        public ChatChoiceEnvelope[]? Choices { get; init; }

        public ChatUsageEnvelope? Usage { get; init; }
    }

    private sealed class ChatChoiceEnvelope
    {
        public ChatMessageEnvelope? Message { get; init; }
    }

    private sealed class ChatMessageEnvelope
    {
        public string? Content { get; init; }
    }

    private sealed class ChatUsageEnvelope
    {
        public int? PromptTokens { get; init; }

        public int? CompletionTokens { get; init; }
    }

    private sealed record ParsedOpenAiResponse(
        string? Id,
        string? Model,
        string OutputText,
        int? InputTokens,
        int? OutputTokens);
}
