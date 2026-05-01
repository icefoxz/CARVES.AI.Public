namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentOperatorFeedbackClosureSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-operator-feedback-closure.v1";

    public string SurfaceId { get; init; } = "runtime-agent-operator-feedback-closure";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string GuidePath { get; init; } = string.Empty;

    public string DeliveryReadinessGuidePath { get; init; } = string.Empty;

    public string FirstRunPacketPath { get; init; } = string.Empty;

    public string ValidationBundleGuidePath { get; init; } = string.Empty;

    public string FailureRecoveryPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string FeedbackOwnership { get; init; } = "runtime_owned_operator_guidance_projection";

    public IReadOnlyList<RuntimeAgentOperatorFeedbackBundleSurface> FeedbackBundles { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentOperatorFeedbackBundleSurface
{
    public string BundleId { get; init; } = string.Empty;

    public string TriggerState { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Commands { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}
