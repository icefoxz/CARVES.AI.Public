using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Artifacts;

public static class PromptSafeArtifactProjectionFactory
{
    private const int MaxSummaryLength = 320;
    private const int HeadLength = 220;
    private const int TailLength = 80;

    public static PromptSafeProjection Create(string? summary, string? detailText, string? detailRef)
    {
        var source = FirstNonEmpty(summary, detailText);
        if (string.IsNullOrWhiteSpace(source))
        {
            return new PromptSafeProjection
            {
                Summary = string.Empty,
                DetailRef = detailRef,
                DetailHash = string.IsNullOrWhiteSpace(detailText) ? null : Hash(detailText),
                OriginalLength = 0,
                Truncated = false,
            };
        }

        var normalized = NormalizeWhitespace(source);
        var originalLength = normalized.Length;
        var detailHash = string.IsNullOrWhiteSpace(detailText) ? Hash(normalized) : Hash(detailText!);
        if (normalized.Length <= MaxSummaryLength)
        {
            return new PromptSafeProjection
            {
                Summary = normalized,
                DetailRef = detailRef,
                DetailHash = detailHash,
                OriginalLength = originalLength,
                Truncated = false,
            };
        }

        var head = normalized[..Math.Min(HeadLength, normalized.Length)].TrimEnd();
        var tailSource = normalized[^Math.Min(TailLength, normalized.Length)..].TrimStart();
        var summaryProjection = $"{head} ... {tailSource}";
        return new PromptSafeProjection
        {
            Summary = summaryProjection,
            DetailRef = detailRef,
            DetailHash = detailHash,
            ExcerptTail = tailSource,
            OriginalLength = originalLength,
            Truncated = true,
        };
    }

    public static PromptSafeProjection ForWorkerExecution(string taskId, WorkerExecutionResult result)
    {
        return Create(
            FirstNonEmpty(result.Summary, result.FailureReason, result.ResponsePreview, result.Rationale),
            FirstNonEmpty(result.Summary, result.FailureReason, result.ResponsePreview, result.Rationale),
            GetWorkerExecutionDetailRef(taskId));
    }

    public static PromptSafeProjection ForProviderArtifact(string taskId, AiExecutionRecord record)
    {
        var summary = FirstNonEmpty(
            record.ResponsePreview,
            record.OutputText,
            record.FailureReason,
            record.WorkerAdapterReason,
            record.RequestPreview);
        return Create(summary, summary, GetProviderArtifactDetailRef(taskId));
    }

    public static PromptSafeProjection ForOperatorEvent(OperatorOsEventRecord record, string? detailRef)
    {
        return Create(record.Summary, record.Summary, detailRef);
    }

    public static string GetWorkerExecutionDetailRef(string taskId)
    {
        return $".ai/artifacts/worker-executions/{taskId}.json";
    }

    public static string GetProviderArtifactDetailRef(string taskId)
    {
        return $".ai/artifacts/provider/{taskId}.json";
    }

    public static string Hash(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string NormalizeWhitespace(string value)
    {
        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static line => line.TrimEnd())
            .ToArray();
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
    }
}
