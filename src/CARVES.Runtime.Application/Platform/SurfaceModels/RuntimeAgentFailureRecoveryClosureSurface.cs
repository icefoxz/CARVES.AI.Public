namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentFailureRecoveryClosureSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-failure-recovery-closure.v1";

    public string SurfaceId { get; init; } = "runtime-agent-failure-recovery-closure";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string RuntimeFailureModelPath { get; init; } = string.Empty;

    public string RetryPolicyPath { get; init; } = string.Empty;

    public string RecoveryPolicyPath { get; init; } = string.Empty;

    public string RuntimeConsistencyPath { get; init; } = string.Empty;

    public string LifecycleReconciliationPath { get; init; } = string.Empty;

    public string ExpiredRunClassificationPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string RecoveryOwnership { get; init; } = "runtime_owned_failure_classification_and_operator_handoff";

    public IReadOnlyList<string> RetryableFailureKinds { get; init; } = [];

    public IReadOnlyList<string> RecoveryOutcomes { get; init; } = [];

    public IReadOnlyList<string> ReadOnlyVisibilityCommands { get; init; } = [];

    public IReadOnlyList<string> OperatorHandoffCommands { get; init; } = [];

    public IReadOnlyList<RuntimeAgentFailureRecoveryClassSurface> FailureClasses { get; init; } = [];

    public IReadOnlyList<string> BlockedBehaviors { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeAgentFailureRecoveryClassSurface
{
    public string FailureClassId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> FailureKinds { get; init; } = [];

    public IReadOnlyList<string> Signals { get; init; } = [];

    public string RetryPosture { get; init; } = string.Empty;

    public string RuntimeOutcome { get; init; } = string.Empty;

    public string OperatorHandoff { get; init; } = string.Empty;

    public IReadOnlyList<string> GoverningRefs { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
