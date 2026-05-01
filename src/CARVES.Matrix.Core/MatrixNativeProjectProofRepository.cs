using Carves.Guard.Core;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool TryPrepareNativeFullReleaseProjectRepository(
        string artifactRoot,
        string outputPath,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        out MatrixNativeProjectProofResult failure)
    {
        failure = null!;
        if (!TryRunNativeFullReleaseProjectProcessStep(artifactRoot, outputPath, workRepoRoot, steps, "git_init", "git -c init.defaultBranch=main init", ["-c", "init.defaultBranch=main", "init"], "Failed to initialize native project proof repository.", out failure))
        {
            return false;
        }

        if (!TryRunNativeFullReleaseProjectProcessStep(artifactRoot, outputPath, workRepoRoot, steps, "git_config_email", "git config user.email", ["config", "user.email", "matrix-smoke@example.invalid"], "Failed to configure native project proof git email.", out failure)
            || !TryRunNativeFullReleaseProjectProcessStep(artifactRoot, outputPath, workRepoRoot, steps, "git_config_name", "git config user.name", ["config", "user.name", "CARVES Matrix Smoke"], "Failed to configure native project proof git name.", out failure))
        {
            return false;
        }

        Directory.CreateDirectory(Path.Combine(workRepoRoot, "src"));
        Directory.CreateDirectory(Path.Combine(workRepoRoot, "tests"));
        File.WriteAllText(Path.Combine(workRepoRoot, "src", "App.cs"), """
            namespace MatrixSmoke;

            public static class App
            {
                public static string Greeting() => "hello";
            }
            """);
        File.WriteAllText(Path.Combine(workRepoRoot, "tests", "AppTests.cs"), """
            namespace MatrixSmoke.Tests;

            public static class AppTests
            {
                public static bool Baseline() => true;
            }
            """);

        if (!TryRunNativeFullReleaseProjectProcessStep(artifactRoot, outputPath, workRepoRoot, steps, "git_add_baseline", "git add .", ["add", "."], "Failed to stage native project proof baseline.", out failure)
            || !TryRunNativeFullReleaseProjectProcessStep(artifactRoot, outputPath, workRepoRoot, steps, "git_commit_baseline", "git commit baseline", ["commit", "-m", "baseline"], "Failed to commit native project proof baseline.", out failure))
        {
            return false;
        }

        var guardInit = RunNativeCliStep(
            "guard_init",
            "carves-guard init --json",
            () => GuardCliRunner.Run(workRepoRoot, ["init", "--json"], null, "carves-guard", GuardRuntimeTransportPreference.Cold));
        WriteNativeFullReleaseProjectStepJson(artifactRoot, "project/guard-init.json", guardInit, "guard init");
        if (!AppendNativeStep(steps, guardInit, out var failedStep))
        {
            failure = FailedProjectStep(artifactRoot, outputPath, workRepoRoot, steps, failedStep, "Guard init failed in the native project proof repository.");
            return false;
        }

        if (!TryRunNativeFullReleaseProjectProcessStep(artifactRoot, outputPath, workRepoRoot, steps, "git_add_guard_policy", "git add .ai/guard-policy.json", ["add", ".ai/guard-policy.json"], "Failed to stage native project proof Guard policy.", out failure)
            || !TryRunNativeFullReleaseProjectProcessStep(artifactRoot, outputPath, workRepoRoot, steps, "git_commit_guard_policy", "git commit add guard policy", ["commit", "-m", "add guard policy"], "Failed to commit native project proof Guard policy.", out failure))
        {
            return false;
        }

        File.AppendAllText(Path.Combine(workRepoRoot, "src", "App.cs"), "public static class MatrixSmokePatch { public static int Count() => 1; }" + Environment.NewLine);
        File.AppendAllText(Path.Combine(workRepoRoot, "tests", "AppTests.cs"), "public static class MatrixSmokePatchTests { public static bool CountTest() => MatrixSmokePatch.Count() == 1; }" + Environment.NewLine);
        return true;
    }

    private static bool TryRunNativeFullReleaseProjectProcessStep(
        string artifactRoot,
        string outputPath,
        string workRepoRoot,
        List<MatrixNativeProofStep> steps,
        string stepId,
        string command,
        IReadOnlyList<string> arguments,
        string failureMessage,
        out MatrixNativeProjectProofResult failure)
    {
        failure = null!;
        var capture = RunNativeProcessStep(stepId, command, workRepoRoot, "git", arguments);
        if (AppendNativeStep(steps, capture, out var failedStep))
        {
            return true;
        }

        failure = FailedProjectStep(artifactRoot, outputPath, workRepoRoot, steps, failedStep, failureMessage);
        return false;
    }

    private static void WriteNativeFullReleaseGuardWorkflowFixture(string repositoryRoot)
    {
        var workflowRoot = ResolveNativeRelativePath(repositoryRoot, ".github/workflows");
        Directory.CreateDirectory(workflowRoot);
        File.WriteAllText(Path.Combine(workflowRoot, "carves-guard.yml"), """
            name: CARVES Guard

            on:
              pull_request:

            jobs:
              guard:
                runs-on: ubuntu-latest
                steps:
                  - uses: actions/checkout@v4
                  - name: Check AI patch boundary
                    run: carves-guard check --json
            """);
    }
}
