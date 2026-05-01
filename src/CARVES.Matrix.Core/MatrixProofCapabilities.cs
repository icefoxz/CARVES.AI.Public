namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static object BuildNativeMinimalProofCapabilities()
    {
        return BuildProofCapabilities(
            MatrixProofSummaryPublicContract.NativeMinimalProofMode,
            MatrixProofSummaryPublicContract.NativeMinimalExecutionBackend,
            projectMode: true,
            packagedInstall: false,
            fullRelease: false,
            powershell: false,
            sourceCheckout: false);
    }

    private static object BuildFullReleaseProofCapabilities()
    {
        return BuildProofCapabilities(
            MatrixProofSummaryPublicContract.FullReleaseProofMode,
            MatrixProofSummaryPublicContract.FullReleaseExecutionBackend,
            projectMode: true,
            packagedInstall: true,
            fullRelease: true,
            powershell: true,
            sourceCheckout: true);
    }

    private static object BuildNativeFullReleaseProofCapabilities()
    {
        return BuildProofCapabilities(
            MatrixProofSummaryPublicContract.NativeFullReleaseProofLane,
            MatrixProofSummaryPublicContract.NativeFullReleaseExecutionBackend,
            projectMode: true,
            packagedInstall: true,
            fullRelease: true,
            powershell: false,
            sourceCheckout: true);
    }

    private static object BuildProofCapabilities(
        string proofLane,
        string executionBackend,
        bool projectMode,
        bool packagedInstall,
        bool fullRelease,
        bool powershell,
        bool sourceCheckout)
    {
        return new
        {
            proof_lane = proofLane,
            execution_backend = executionBackend,
            coverage = new
            {
                project_mode = projectMode,
                packaged_install = packagedInstall,
                full_release = fullRelease,
            },
            requirements = new
            {
                powershell,
                source_checkout = sourceCheckout,
                dotnet_sdk = true,
                git = true,
            },
        };
    }
}
