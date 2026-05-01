namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool TryPrepareNativePackagedRepository(
        string packagedRoot,
        string workRepoRoot,
        MatrixNativeInstalledCommands commands,
        List<MatrixNativeProofStep> steps,
        out MatrixNativePackagedFailure failure)
    {
        failure = null!;
        if (!TryRunNativePackagedProcessStep(packagedRoot, workRepoRoot, steps, "git_init", "git -c init.defaultBranch=main init", "git", ["-c", "init.defaultBranch=main", "init"], "Failed to initialize native packaged proof repository.", out failure)
            || !TryRunNativePackagedProcessStep(packagedRoot, workRepoRoot, steps, "git_config_email", "git config user.email", "git", ["config", "user.email", "matrix-smoke@example.invalid"], "Failed to configure native packaged proof git email.", out failure)
            || !TryRunNativePackagedProcessStep(packagedRoot, workRepoRoot, steps, "git_config_name", "git config user.name", "git", ["config", "user.name", "CARVES Matrix Smoke"], "Failed to configure native packaged proof git name.", out failure))
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

        if (!TryRunNativePackagedProcessStep(packagedRoot, workRepoRoot, steps, "git_add_baseline", "git add .", "git", ["add", "."], "Failed to stage native packaged proof baseline.", out failure)
            || !TryRunNativePackagedProcessStep(packagedRoot, workRepoRoot, steps, "git_commit_baseline", "git commit baseline", "git", ["commit", "-m", "baseline"], "Failed to commit native packaged proof baseline.", out failure))
        {
            return false;
        }

        if (!TryRunNativePackagedJsonStep(packagedRoot, workRepoRoot, steps, "guard_init", "carves-guard init --json", commands.CarvesGuard, ["--repo-root", workRepoRoot, "init", "--json"], "guard-init.json", "Guard init failed in the native packaged proof repository.", out var guardInitJson, out failure))
        {
            return false;
        }

        if (!string.Equals(GetString(ParseNativeFullReleaseProjectJson(guardInitJson, "guard init"), "schema_version"), "guard-init.v1", StringComparison.Ordinal))
        {
            failure = new MatrixNativePackagedFailure("guard_init", ["native_packaged_guard_init_schema_mismatch"], "Guard init returned an unexpected schema.");
            return false;
        }

        if (!TryRunNativePackagedProcessStep(packagedRoot, workRepoRoot, steps, "git_add_guard_policy", "git add .ai/guard-policy.json", "git", ["add", ".ai/guard-policy.json"], "Failed to stage native packaged proof Guard policy.", out failure)
            || !TryRunNativePackagedProcessStep(packagedRoot, workRepoRoot, steps, "git_commit_guard_policy", "git commit add guard policy", "git", ["commit", "-m", "add guard policy"], "Failed to commit native packaged proof Guard policy.", out failure))
        {
            return false;
        }

        File.AppendAllText(Path.Combine(workRepoRoot, "src", "App.cs"), "public static class MatrixSmokePatch { public static int Count() => 1; }" + Environment.NewLine);
        File.AppendAllText(Path.Combine(workRepoRoot, "tests", "AppTests.cs"), "public static class MatrixSmokePatchTests { public static bool CountTest() => MatrixSmokePatch.Count() == 1; }" + Environment.NewLine);
        return true;
    }
}
