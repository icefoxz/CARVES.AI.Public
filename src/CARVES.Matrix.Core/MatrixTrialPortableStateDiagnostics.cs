namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static IReadOnlyList<TrialDiagnosticReadback>? TryBuildPortablePackageStateExceptionDiagnostics(
        string command,
        Exception exception)
    {
        if (command != "collect" || exception is not InvalidOperationException)
        {
            return null;
        }

        if (exception.Message.Contains("Portable package already scored", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_package_already_scored",
                    "This portable Agent Trial package has already produced a score.",
                    ".carves-pack/state.json",
                    "Run RESULT.cmd or result.sh to read the existing score, or run RESET.cmd or reset.sh before testing another agent in this folder.",
                    "portable_package_already_scored")
            ];
        }

        if (exception.Message.Contains("Portable package already failed", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_package_failed",
                    "This portable Agent Trial package already failed during scoring.",
                    ".carves-pack/state.json",
                    "Inspect the failed evidence under results/, or run RESET.cmd or reset.sh before another local practice run.",
                    "portable_package_failed")
            ];
        }

        if (exception.Message.Contains("Portable package already contaminated", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_package_contaminated",
                    "This portable Agent Trial package is already marked contaminated.",
                    ".carves-pack/state.json",
                    "Run RESET.cmd or reset.sh to archive local residue before another practice run. Use a fresh extraction for the cleanest strict comparison.",
                    "portable_package_contaminated")
            ];
        }

        if (exception.Message.Contains("Portable package state missing", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_package_state_missing",
                    "The portable package state file is missing from the scorer authority area.",
                    ".carves-pack/state.json",
                    "Extract or generate a fresh package; package state must live outside agent-workspace/.",
                    "portable_package_state_missing")
            ];
        }

        if (exception.Message.Contains("Portable package state invalid", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_package_state_invalid",
                    "The portable package state file is invalid or has an unsupported state.",
                    ".carves-pack/state.json",
                    "Extract a fresh package instead of hand-editing scorer state.",
                    "portable_package_state_invalid")
            ];
        }

        if (exception.Message.Contains("Portable package stale results", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_stale_results",
                    "The portable package already contains local or submit-bundle result files before scoring.",
                    "results/",
                    "Run RESULT.cmd or result.sh to read the previous result, or run RESET.cmd or reset.sh before testing another agent.",
                    "portable_package_stale_results")
            ];
        }

        if (exception.Message.Contains("Portable package judge evidence present", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_judge_evidence_present",
                    "The agent workspace already contains judge-generated evidence before scoring.",
                    "agent-workspace/artifacts/",
                    "Run RESET.cmd or reset.sh to clear judge evidence before another practice run. The tested agent should only write artifacts/agent-report.json.",
                    "portable_package_judge_evidence_present")
            ];
        }

        if (exception.Message.Contains("Portable package unexpected root entry", StringComparison.Ordinal))
        {
            return
            [
                PortablePackageStateDiagnostic(
                    "trial_portable_unexpected_package_file",
                    "The package root contains unexpected files outside the allowed package layout.",
                    "<package-root>",
                    "Run RESET.cmd or reset.sh to park unexpected root files under history, then open only agent-workspace/ in the tested agent.",
                    "portable_package_unexpected_root_entry")
            ];
        }

        return null;
    }

    private static TrialDiagnosticReadback PortablePackageStateDiagnostic(
        string code,
        string message,
        string evidenceRef,
        string nextStep,
        string reasonCode)
    {
        return new TrialDiagnosticReadback(
            code,
            "user_setup",
            "error",
            message,
            evidenceRef,
            "carves test collect",
            nextStep,
            [reasonCode]);
    }
}
