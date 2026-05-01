using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeFirstRunOperatorPacketSurface
{
    public string SchemaVersion { get; init; } = "runtime-first-run-operator-packet.v1";

    public string SurfaceId { get; init; } = "runtime-first-run-operator-packet";

    public string PacketPath { get; init; } = string.Empty;

    public string InternalBetaGatePath { get; init; } = string.Empty;

    public string TrustedBootstrapTruthPath { get; init; } = string.Empty;

    public string OnboardingAccelerationContractPath { get; init; } = string.Empty;

    public string AlphaSetupPath { get; init; } = string.Empty;

    public string AlphaQuickstartPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string OverallPosture { get; init; } = string.Empty;

    public string InternalBetaGatePosture { get; init; } = string.Empty;

    public string CurrentProofSource { get; init; } = string.Empty;

    public string CurrentOperatorState { get; init; } = string.Empty;

    public string TruthOwner { get; init; } = "runtime_control_kernel";

    public string PacketOwnership { get; init; } = "runtime_owned_first_run_operator_packet";

    public IReadOnlyList<string> ProjectIdentification { get; init; } = [];

    public IReadOnlyList<string> BootstrapTruthFamilies { get; init; } = [];

    public IReadOnlyList<string> RequiredOperatorActions { get; init; } = [];

    public IReadOnlyList<string> AllowedAiAssistance { get; init; } = [];

    public IReadOnlyList<string> ExitCriteria { get; init; } = [];

    public IReadOnlyList<string> RequiredEvidenceBundle { get; init; } = [];

    public IReadOnlyList<string> EntryCommands { get; init; } = [];

    public IReadOnlyList<string> MinimumOnboardingReads { get; init; } = [];

    public IReadOnlyList<string> MinimumOnboardingNextSteps { get; init; } = [];

    public IReadOnlyList<string> BlockedClaims { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
