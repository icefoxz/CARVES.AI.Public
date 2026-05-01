namespace Carves.Runtime.Application.ControlPlane;

public static class ControlPlaneResidueContract
{
    public const string CleanHealthState = "healthy";
    public const string RecoverableResidueHealthState = "healthy_with_recoverable_residue";

    public const string NoRecoverableResiduePosture = "no_recoverable_runtime_residue";
    public const string RecoverableResiduePresentPosture = "recoverable_runtime_residue_present";

    public const string NoResidueSeverity = "none";
    public const string WarningResidueSeverity = "warning";
    public const string ErrorResidueSeverity = "error";

    public const string NoCleanupRequiredPosture = "none";
    public const string DryRunRecommendedCleanupPosture = "dry_run_recommended";

    public const string NoCleanupActionMode = "none";
    public const string CleanupActionId = "cleanup_runtime_residue";
    public const string CleanupActionMode = "dry_run_first";
}
