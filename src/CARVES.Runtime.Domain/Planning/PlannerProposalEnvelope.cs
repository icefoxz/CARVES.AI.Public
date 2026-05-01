using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Domain.Planning;

public sealed class PlannerProposalEnvelope
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string ProposalId { get; init; } = string.Empty;

    public string AdapterId { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    public string? ProfileId { get; init; }

    public bool Configured { get; init; }

    public bool UsedFallback { get; init; }

    public PlannerWakeReason WakeReason { get; init; } = PlannerWakeReason.None;

    public string WakeDetail { get; init; } = string.Empty;

    public PlannerProposal Proposal { get; init; } = new();

    public string RawResponsePreview { get; init; } = string.Empty;

    public LlmRequestEnvelopeDraft? RequestEnvelopeDraft { get; init; }

    public string TokenAccountingSource { get; init; } = "local_estimate";

    public int? ProviderReportedInputTokens { get; init; }

    public int? ProviderReportedCachedInputTokens { get; init; }

    public int? ProviderReportedUncachedInputTokens { get; init; }

    public int? ProviderReportedOutputTokens { get; init; }

    public int? ProviderReportedReasoningTokens { get; init; }

    public int? ProviderReportedTotalTokens { get; init; }

    public bool ReasoningTokensReportedSeparately { get; init; }

    public bool ReasoningTokensIncludedInOutput { get; init; }

    public bool ProviderTotalIncludesReasoning { get; init; }

    public PlannerProposalAcceptanceStatus AcceptanceStatus { get; set; } = PlannerProposalAcceptanceStatus.PendingValidation;

    public string AcceptanceReason { get; set; } = string.Empty;

    public IReadOnlyList<string> AcceptedTaskIds { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> RejectedTaskIds { get; set; } = Array.Empty<string>();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
