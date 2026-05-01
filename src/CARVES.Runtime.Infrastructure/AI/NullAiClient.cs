using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

public sealed class NullAiClient : IAiClient
{
    public string ClientName => nameof(NullAiClient);

    public bool IsConfigured => false;

    public AiExecutionRecord Execute(AiExecutionRequest request)
    {
        var requestPreview = request.Input.Length > 160 ? request.Input[..160] : request.Input;
        return AiExecutionRecord.Skipped(
            ClientName,
            request.ModelOverride ?? "none",
            "Provider disabled; runtime used NullAiClient.",
            requestPreview,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant());
    }
}
