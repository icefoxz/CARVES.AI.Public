using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class OpenAiCompatibleProtocol
{
    private object BuildPayload(string model, WorkerExecutionRequest request)
    {
        return requestFamily == OpenAiCompatibleRequestFamily.ChatCompletions
            ? BuildChatCompletionsPayload(model, request)
            : BuildResponsesPayload(model, request);
    }

    private object BuildResponsesPayload(string model, WorkerExecutionRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["instructions"] = request.Instructions,
            ["input"] = request.Input,
            ["max_output_tokens"] = Math.Max(64, request.MaxOutputTokens),
        };

        var reasoningEffort = string.IsNullOrWhiteSpace(request.ReasoningEffort) ? config.ReasoningEffort : request.ReasoningEffort;
        if (model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(reasoningEffort))
        {
            payload["reasoning"] = new Dictionary<string, string>
            {
                ["effort"] = reasoningEffort,
            };
        }

        return payload;
    }

    private static object BuildChatCompletionsPayload(string model, WorkerExecutionRequest request)
    {
        var messages = new List<Dictionary<string, string>>();
        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            messages.Add(new Dictionary<string, string>
            {
                ["role"] = "system",
                ["content"] = request.Instructions,
            });
        }

        messages.Add(new Dictionary<string, string>
        {
            ["role"] = "user",
            ["content"] = request.Input,
        });

        return new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages,
            ["max_tokens"] = Math.Max(64, request.MaxOutputTokens),
            ["stream"] = false,
        };
    }
}
