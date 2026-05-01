using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool VerifySummaryProofCapabilities(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root)
    {
        var proofMode = GetString(root, "proof_mode");
        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.NativeMinimalProofMode, StringComparison.Ordinal))
        {
            return VerifyExpectedProofCapabilities(
                issues,
                summaryRelativePath,
                root,
                MatrixProofSummaryPublicContract.NativeMinimalProofMode,
                MatrixProofSummaryPublicContract.NativeMinimalExecutionBackend,
                projectMode: true,
                packagedInstall: false,
                fullRelease: false,
                powershell: false,
                sourceCheckout: false);
        }

        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.FullReleaseProofMode, StringComparison.Ordinal))
        {
            if (string.Equals(GetString(root, "proof_capabilities", "proof_lane"), MatrixProofSummaryPublicContract.NativeFullReleaseProofLane, StringComparison.Ordinal))
            {
                return VerifyExpectedProofCapabilities(
                    issues,
                    summaryRelativePath,
                    root,
                    MatrixProofSummaryPublicContract.NativeFullReleaseProofLane,
                    MatrixProofSummaryPublicContract.NativeFullReleaseExecutionBackend,
                    projectMode: true,
                    packagedInstall: true,
                    fullRelease: true,
                    powershell: false,
                    sourceCheckout: true);
            }

            return VerifyExpectedProofCapabilities(
                issues,
                summaryRelativePath,
                root,
                MatrixProofSummaryPublicContract.FullReleaseProofMode,
                MatrixProofSummaryPublicContract.FullReleaseExecutionBackend,
                projectMode: true,
                packagedInstall: true,
                fullRelease: true,
                powershell: true,
                sourceCheckout: true);
        }

        return true;
    }

    private static bool VerifyExpectedProofCapabilities(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root,
        string proofLane,
        string executionBackend,
        bool projectMode,
        bool packagedInstall,
        bool fullRelease,
        bool powershell,
        bool sourceCheckout)
    {
        var consistent = true;
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.proof_lane", proofLane, GetString(root, "proof_capabilities", "proof_lane"));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.execution_backend", executionBackend, GetString(root, "proof_capabilities", "execution_backend"));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.coverage.project_mode", FormatBool(projectMode), FormatBool(GetBool(root, "proof_capabilities", "coverage", "project_mode")));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.coverage.packaged_install", FormatBool(packagedInstall), FormatBool(GetBool(root, "proof_capabilities", "coverage", "packaged_install")));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.coverage.full_release", FormatBool(fullRelease), FormatBool(GetBool(root, "proof_capabilities", "coverage", "full_release")));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.requirements.powershell", FormatBool(powershell), FormatBool(GetBool(root, "proof_capabilities", "requirements", "powershell")));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.requirements.source_checkout", FormatBool(sourceCheckout), FormatBool(GetBool(root, "proof_capabilities", "requirements", "source_checkout")));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.requirements.dotnet_sdk", "true", FormatBool(GetBool(root, "proof_capabilities", "requirements", "dotnet_sdk")));
        consistent &= VerifySummaryField(issues, summaryRelativePath, "proof_capabilities.requirements.git", "true", FormatBool(GetBool(root, "proof_capabilities", "requirements", "git")));
        return consistent;
    }
}
