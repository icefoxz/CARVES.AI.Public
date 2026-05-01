using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

public sealed partial class OpenAiResponsesClient
{
    private string BuildRequestBody(string model, AiExecutionRequest request)
    {
        var payload = requestFamily == OpenAiCompatibleRequestFamily.ChatCompletions
            ? BuildChatCompletionsPayload(model, request)
            : BuildResponsesPayload(model, request);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private object BuildResponsesPayload(string model, AiExecutionRequest request)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["instructions"] = request.Instructions,
            ["input"] = request.Input,
            ["max_output_tokens"] = Math.Max(64, request.MaxOutputTokens),
        };

        if (model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
        {
            payload["reasoning"] = new Dictionary<string, string>
            {
                ["effort"] = string.IsNullOrWhiteSpace(config.ReasoningEffort) ? "low" : config.ReasoningEffort,
            };
        }

        return payload;
    }

    private static object BuildChatCompletionsPayload(string model, AiExecutionRequest request)
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

    private static string BuildPreview(string value)
    {
        return value.Length > 160 ? value[..160] : value;
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
