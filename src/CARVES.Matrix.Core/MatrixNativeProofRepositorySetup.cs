using Carves.Guard.Core;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool TryPrepareNativeProofRepository(
        string artifactRoot,
        MatrixOptions options,
        List<MatrixNativeProofStep> steps,
        out string? workRepoRoot,
        out int exitCode)
    {
        workRepoRoot = null;
        exitCode = 0;

        try
        {
            workRepoRoot = CreateNativeWorkRepoRoot(options.WorkRoot);
            Directory.CreateDirectory(workRepoRoot);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException)
        {
            exitCode = WriteNativeProofFailure(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "work_repo_setup",
                ["native_work_repo_setup_failed"],
                $"Failed to prepare the bounded external git repository: {ex.GetType().Name}: {ex.Message}");
            return false;
        }

        if (!TryRunNativeRepositoryProcessStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "git_init",
                "git init",
                ["init"],
                "Failed to initialize the bounded external git repository.",
                out exitCode))
        {
            return false;
        }

        if (!TryRunNativeRepositoryProcessStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "git_config_email",
                "git config user.email",
                ["config", "user.email", "matrix-native@carves.local"],
                "Failed to configure git author email for the bounded external git repository.",
                out exitCode))
        {
            return false;
        }

        if (!TryRunNativeRepositoryProcessStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "git_config_name",
                "git config user.name",
                ["config", "user.name", "CARVES Matrix Native Proof"],
                "Failed to configure git author name for the bounded external git repository.",
                out exitCode))
        {
            return false;
        }

        Directory.CreateDirectory(Path.Combine(workRepoRoot, "src"));
        File.WriteAllText(Path.Combine(workRepoRoot, "README.md"), "# Matrix native proof target\n");
        File.WriteAllText(Path.Combine(workRepoRoot, "src", "app.txt"), "baseline\n");

        var preparedWorkRepoRoot = workRepoRoot;
        var guardInit = RunNativeCliStep(
            "guard_init",
            "carves-guard init --json",
            () => GuardCliRunner.Run(preparedWorkRepoRoot, ["init", "--json"], null, "carves-guard", GuardRuntimeTransportPreference.Cold));
        if (!AppendNativeStep(steps, guardInit, out var failedStep))
        {
            exitCode = WriteNativeProofFailure(artifactRoot, options, workRepoRoot, steps, failedStep.StepId, failedStep.ReasonCodes, "Guard init failed in the bounded external git repository.");
            return false;
        }

        if (!TryRunNativeRepositoryProcessStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "git_add_baseline",
                "git add .",
                ["add", "."],
                "Failed to stage the bounded baseline repository.",
                out exitCode))
        {
            return false;
        }

        if (!TryRunNativeRepositoryProcessStep(
                artifactRoot,
                options,
                workRepoRoot,
                steps,
                "git_commit_baseline",
                "git commit baseline",
                ["commit", "-m", "baseline"],
                "Failed to commit the bounded baseline repository.",
                out exitCode))
        {
            return false;
        }

        File.AppendAllText(Path.Combine(workRepoRoot, "src", "app.txt"), "native proof change\n");
        return true;
    }

    private static bool TryRunNativeRepositoryProcessStep(
        string artifactRoot,
        MatrixOptions options,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        string stepId,
        string command,
        IReadOnlyList<string> arguments,
        string failureMessage,
        out int exitCode)
    {
        var capture = RunNativeProcessStep(stepId, command, workRepoRoot, "git", arguments);
        if (AppendNativeStep(steps, capture, out var failedStep))
        {
            exitCode = 0;
            return true;
        }

        exitCode = WriteNativeProofFailure(artifactRoot, options, workRepoRoot, steps, failedStep.StepId, ["native_work_repo_setup_failed", .. failedStep.ReasonCodes], failureMessage);
        return false;
    }
}
