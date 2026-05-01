using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

public sealed class FallbackAiClient : IAiClient
{
    private readonly IAiClient primary;
    private readonly IAiClient fallback;

    public FallbackAiClient(IAiClient primary, IAiClient fallback)
    {
        this.primary = primary;
        this.fallback = fallback;
    }

    public string ClientName => primary.ClientName;

    public bool IsConfigured => primary.IsConfigured || fallback.IsConfigured;

    public AiExecutionRecord Execute(AiExecutionRequest request)
    {
        try
        {
            return primary.Execute(request);
        }
        catch (Exception exception)
        {
            var fallbackRecord = fallback.Execute(request);
            return new AiExecutionRecord
            {
                Provider = fallbackRecord.Provider,
                Model = fallbackRecord.Model,
                Configured = fallbackRecord.Configured,
                Succeeded = fallbackRecord.Succeeded,
                UsedFallback = true,
                FallbackProvider = fallback.ClientName,
                RequestId = fallbackRecord.RequestId,
                RequestPreview = fallbackRecord.RequestPreview,
                RequestHash = fallbackRecord.RequestHash,
                ResponsePreview = fallbackRecord.ResponsePreview,
                ResponseHash = fallbackRecord.ResponseHash,
                OutputText = fallbackRecord.OutputText,
                FailureReason = exception.Message,
                InputTokens = fallbackRecord.InputTokens,
                OutputTokens = fallbackRecord.OutputTokens,
                CapturedAt = DateTimeOffset.UtcNow,
            };
        }
    }
}
