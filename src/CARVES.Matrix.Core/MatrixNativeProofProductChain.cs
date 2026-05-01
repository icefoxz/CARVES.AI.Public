using Carves.Audit.Core;
using Carves.Guard.Core;
using Carves.Handoff.Core;
using Carves.Shield.Core;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record MatrixNativeProofProductChainResult(
        string ShieldEvaluateStdout,
        string ShieldBadgeStdout);

    private static bool TryRunNativeProofProductChain(
        string artifactRoot,
        MatrixOptions options,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        out MatrixNativeProofProductChainResult productChain,
        out int exitCode)
    {
        productChain = null!;
        exitCode = 0;

        if (!TryRunNativeProductCliStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "guard_check",
                "carves-guard check --json",
                () => GuardCliRunner.Run(workRepoRoot, ["check", "--json"], null, "carves-guard", GuardRuntimeTransportPreference.Cold),
                "Guard check failed in the native proof chain.",
                out _,
                out exitCode,
                [0, 1]))
        {
            return false;
        }

        if (!TryRunNativeProductCliStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "handoff_draft",
                "carves-handoff draft --json",
                () => HandoffCliRunner.Run(workRepoRoot, ["draft", "--json"]),
                "Handoff draft failed in the native proof chain.",
                out _,
                out exitCode))
        {
            return false;
        }

        if (!TryRunNativeProductCliStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "audit_evidence",
                "carves-audit evidence --json --output .carves/shield-evidence.json",
                () => AuditCliRunner.Run(workRepoRoot, ["evidence", "--json", "--output", ".carves/shield-evidence.json"]),
                "Audit evidence generation failed in the native proof chain.",
                out _,
                out exitCode))
        {
            return false;
        }

        if (!TryRunNativeProductCliStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "shield_evaluate",
                "carves-shield evaluate .carves/shield-evidence.json --json --output combined",
                () => ShieldCliRunner.Run(workRepoRoot, ["evaluate", ".carves/shield-evidence.json", "--json", "--output", "combined"], "carves-shield"),
                "Shield evaluation failed in the native proof chain.",
                out var shieldEvaluate,
                out exitCode))
        {
            return false;
        }

        if (!TryRunNativeProductCliStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "shield_badge",
                "carves-shield badge .carves/shield-evidence.json --json --output docs/shield-badge.svg",
                () => ShieldCliRunner.Run(workRepoRoot, ["badge", ".carves/shield-evidence.json", "--json", "--output", "docs/shield-badge.svg"], "carves-shield"),
                "Shield badge generation failed in the native proof chain.",
                out var shieldBadge,
                out exitCode))
        {
            return false;
        }

        productChain = new MatrixNativeProofProductChainResult(shieldEvaluate.Stdout, shieldBadge.Stdout);
        return true;
    }

    private static bool TryRunNativeProductCliStep(
        string artifactRoot,
        MatrixOptions options,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        string stepId,
        string command,
        Func<int> action,
        string failureMessage,
        out MatrixNativeProofStepCapture capture,
        out int exitCode,
        IReadOnlyCollection<int>? acceptedExitCodes = null)
    {
        capture = RunNativeCliStep(stepId, command, action, acceptedExitCodes);
        if (AppendNativeStep(steps, capture, out var failedStep))
        {
            exitCode = 0;
            return true;
        }

        exitCode = WriteNativeProofFailure(artifactRoot, options, workRepoRoot, steps, failedStep.StepId, failedStep.ReasonCodes, failureMessage);
        return false;
    }
}
