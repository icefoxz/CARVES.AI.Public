using System.Diagnostics;
using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;

namespace Carves.Matrix.Tests;

public sealed class MatrixTrialPortablePackageTests
{
    [Fact]
    public void TrialPackage_CreatesReadyAgentWorkspaceAndExternalAuthority()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli(
                "trial",
                "package",
                "--output",
                packageRoot,
                "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-agent-trial-portable-package.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("package", root.GetProperty("command").GetString());
            Assert.Equal("prepared", root.GetProperty("status").GetString());
            Assert.True(root.GetProperty("offline").GetBoolean());
            Assert.False(root.GetProperty("server_submission").GetBoolean());
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item => item.GetString() == "not_tamper_proof");
            Assert.Contains(root.GetProperty("next_steps").EnumerateArray(), item => item.GetString() == "Open only agent-workspace/ in the tested agent.");
            Assert.Contains(root.GetProperty("next_steps").EnumerateArray(), item => item.GetString() == "Use COPY_THIS_TO_AGENT_BLIND.txt in a fresh thread for strict comparison.");
            Assert.Contains(root.GetProperty("next_steps").EnumerateArray(), item => item.GetString() == "Use COPY_THIS_TO_AGENT_GUIDED.txt only for learning or local practice.");
            Assert.Contains(root.GetProperty("next_steps").EnumerateArray(), item => item.GetString() == "Run RESULT.cmd or result.sh to view the previous score after closing the score window.");
            Assert.Contains(root.GetProperty("next_steps").EnumerateArray(), item => item.GetString() == "Run RESET.cmd or reset.sh to clear this local attempt before testing another agent in the same folder.");

            var agentWorkspace = Path.Combine(packageRoot, "agent-workspace");
            var authorityRoot = Path.Combine(packageRoot, ".carves-pack");
            Assert.Equal(Path.GetFullPath(agentWorkspace), root.GetProperty("agent_workspace_root").GetString());
            Assert.Equal(Path.GetFullPath(authorityRoot), root.GetProperty("scorer_authority_root").GetString());
            Assert.True(File.Exists(Path.Combine(packageRoot, "README-FIRST.md")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_BLIND.txt")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_GUIDED.txt")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "SCORE.cmd")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "score.sh")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "RESULT.cmd")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "result.sh")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "RESET.cmd")));
            Assert.True(File.Exists(Path.Combine(packageRoot, "reset.sh")));
            Assert.EndsWith("RESULT.cmd", root.GetProperty("result_cmd_path").GetString(), StringComparison.Ordinal);
            Assert.EndsWith("result.sh", root.GetProperty("result_sh_path").GetString(), StringComparison.Ordinal);
            Assert.EndsWith("RESET.cmd", root.GetProperty("reset_cmd_path").GetString(), StringComparison.Ordinal);
            Assert.EndsWith("reset.sh", root.GetProperty("reset_sh_path").GetString(), StringComparison.Ordinal);
            var readme = File.ReadAllText(Path.Combine(packageRoot, "README-FIRST.md"));
            var blindPrompt = File.ReadAllText(Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_BLIND.txt"));
            var guidedPrompt = File.ReadAllText(Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_GUIDED.txt"));
            using var taskContractDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(agentWorkspace, ".carves", "trial", "task-contract.json")));
            var taskContract = taskContractDocument.RootElement;
            var taskContractAllowedPaths = taskContract.GetProperty("allowed_paths").EnumerateArray().Select(path => path.GetString() ?? string.Empty).ToArray();
            var taskContractForbiddenPaths = taskContract.GetProperty("forbidden_paths").EnumerateArray().Select(path => path.GetString() ?? string.Empty).ToArray();
            var taskContractRequiredCommands = taskContract.GetProperty("required_commands").EnumerateArray().Select(command => command.GetString() ?? string.Empty).ToArray();
            Assert.Contains("extract", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Open only `agent-workspace/`", readme, StringComparison.Ordinal);
            Assert.Contains("strict comparison", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COPY_THIS_TO_AGENT_BLIND.txt", readme, StringComparison.Ordinal);
            Assert.Contains("learning or local practice", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("COPY_THIS_TO_AGENT_GUIDED.txt", readme, StringComparison.Ordinal);
            Assert.Contains("./score.sh", readme, StringComparison.Ordinal);
            Assert.Contains("SCORE.cmd", readme, StringComparison.Ordinal);
            Assert.Contains("./result.sh", readme, StringComparison.Ordinal);
            Assert.Contains("RESULT.cmd", readme, StringComparison.Ordinal);
            Assert.Contains("./reset.sh", readme, StringComparison.Ordinal);
            Assert.Contains("RESET.cmd", readme, StringComparison.Ordinal);
            Assert.Contains("You can run `./score.sh` or `SCORE.cmd` again after scoring", readme, StringComparison.Ordinal);
            Assert.Contains("Reset clears the agent workspace", readme, StringComparison.Ordinal);
            Assert.Contains("Local-only non-claims", readme, StringComparison.Ordinal);
            Assert.DoesNotContain("carves-matrix", readme, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fresh agent thread", blindPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("agent-workspace/", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("Do not open the package root", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("RESET.cmd", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("reset.sh", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("artifacts/agent-report.json", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("src/bounded-fixture.js", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("tests/bounded-fixture.test.js", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("artifacts/agent-report.template.json", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("schema_version", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("agent-report.v0", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("do not add extra top-level fields", blindPrompt, StringComparison.OrdinalIgnoreCase);
            foreach (var allowedPath in taskContractAllowedPaths)
            {
                Assert.Contains($"`{allowedPath}`", blindPrompt, StringComparison.Ordinal);
                Assert.Contains($"`{allowedPath}`", guidedPrompt, StringComparison.Ordinal);
            }

            foreach (var forbiddenPath in taskContractForbiddenPaths)
            {
                Assert.Contains($"`{forbiddenPath}`", blindPrompt, StringComparison.Ordinal);
                Assert.Contains($"`{forbiddenPath}`", guidedPrompt, StringComparison.Ordinal);
            }

            foreach (var requiredCommand in taskContractRequiredCommands)
            {
                Assert.Contains($"`{requiredCommand}`", blindPrompt, StringComparison.Ordinal);
                Assert.Contains($"`{requiredCommand}`", guidedPrompt, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("dotnet run --project", blindPrompt, StringComparison.Ordinal);
            Assert.Contains("Practice mode", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("not for strict comparison", guidedPrompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("agent-workspace/", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("RESULT.cmd", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("result.sh", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("node tests/bounded-fixture.test.js", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("src/bounded-fixture.js", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("tests/bounded-fixture.test.js", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("artifacts/agent-report.template.json", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("schema_version", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("agent-report.v0", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("git status --short", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("artifacts/test-evidence.json", guidedPrompt, StringComparison.Ordinal);
            Assert.Contains("artifacts/diff-scope-summary.json", guidedPrompt, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(agentWorkspace, "AGENTS.md")));
            Assert.True(File.Exists(Path.Combine(agentWorkspace, ".carves", "trial", "task-contract.json")));
            Assert.True(Directory.Exists(Path.Combine(agentWorkspace, ".git")));
            Assert.False(Directory.Exists(Path.Combine(agentWorkspace, ".carves-pack")));
            Assert.False(File.Exists(Path.Combine(agentWorkspace, "SCORE.cmd")));
            Assert.False(File.Exists(Path.Combine(agentWorkspace, "score.sh")));
            Assert.False(File.Exists(Path.Combine(agentWorkspace, "RESULT.cmd")));
            Assert.False(File.Exists(Path.Combine(agentWorkspace, "result.sh")));
            Assert.False(File.Exists(Path.Combine(agentWorkspace, "RESET.cmd")));
            Assert.False(File.Exists(Path.Combine(agentWorkspace, "reset.sh")));
            var scoreCmd = File.ReadAllText(Path.Combine(packageRoot, "SCORE.cmd"));
            var scoreSh = File.ReadAllText(Path.Combine(packageRoot, "score.sh"));
            var resultCmd = File.ReadAllText(Path.Combine(packageRoot, "RESULT.cmd"));
            var resultSh = File.ReadAllText(Path.Combine(packageRoot, "result.sh"));
            var resetCmd = File.ReadAllText(Path.Combine(packageRoot, "RESET.cmd"));
            var resetSh = File.ReadAllText(Path.Combine(packageRoot, "reset.sh"));
            Assert.Contains("test collect", scoreCmd, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pause", scoreCmd, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("git", scoreCmd, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("agent-workspace", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("agent-report.json", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("Mode: local only", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("Package already scored. Showing the previous local result.", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("CARVES scorer/service was not found.", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("if exist \"results\\local\\matrix-agent-trial-result-card.md\"", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("No local result card was produced.", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("Run RESULT.cmd to view this result again.", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("Run RESET.cmd before testing another agent", scoreCmd, StringComparison.Ordinal);
            Assert.Contains("test collect", scoreSh, StringComparison.Ordinal);
            Assert.Contains("command -v git", scoreSh, StringComparison.Ordinal);
            Assert.Contains("agent-workspace", scoreSh, StringComparison.Ordinal);
            Assert.Contains("agent-report.json", scoreSh, StringComparison.Ordinal);
            Assert.Contains("Mode: local only", scoreSh, StringComparison.Ordinal);
            Assert.Contains("Package already scored. Showing the previous local result.", scoreSh, StringComparison.Ordinal);
            Assert.Contains("CARVES scorer/service was not found.", scoreSh, StringComparison.Ordinal);
            Assert.Contains("if [ -f \"results/local/matrix-agent-trial-result-card.md\" ]", scoreSh, StringComparison.Ordinal);
            Assert.Contains("No local result card was produced.", scoreSh, StringComparison.Ordinal);
            Assert.Contains("Run ./result.sh to view this result again.", scoreSh, StringComparison.Ordinal);
            Assert.Contains("Run ./reset.sh before testing another agent", scoreSh, StringComparison.Ordinal);
            Assert.Contains("tools\\carves\\carves.exe", resultCmd, StringComparison.Ordinal);
            Assert.Contains("./tools/carves/carves", resultSh, StringComparison.Ordinal);
            Assert.Contains("%CARVES% test result %*", resultCmd, StringComparison.Ordinal);
            Assert.Contains("\"$CARVES\" test result \"$@\"", resultSh, StringComparison.Ordinal);
            Assert.DoesNotContain("type \"results\\local\\matrix-agent-trial-result-card.md\"", resultCmd, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cat \"results/local/matrix-agent-trial-result-card.md\"", resultSh, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("What reset clears", resetCmd, StringComparison.Ordinal);
            Assert.Contains("What reset keeps", resetCmd, StringComparison.Ordinal);
            Assert.Contains("%CARVES% test reset %*", resetCmd, StringComparison.Ordinal);
            Assert.Contains("What reset clears", resetSh, StringComparison.Ordinal);
            Assert.Contains("What reset keeps", resetSh, StringComparison.Ordinal);
            Assert.Contains("\"$CARVES\" test reset \"$@\"", resetSh, StringComparison.Ordinal);
            Assert.DoesNotContain("carves-matrix", scoreCmd, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("carves-matrix", scoreSh, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("--workspace", scoreCmd, StringComparison.Ordinal);
            Assert.DoesNotContain("--workspace", scoreSh, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", scoreCmd, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", scoreSh, StringComparison.Ordinal);
            Assert.DoesNotContain("--workspace", resetCmd, StringComparison.Ordinal);
            Assert.DoesNotContain("--workspace", resetSh, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", resetCmd, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", resetSh, StringComparison.Ordinal);

            Assert.True(File.Exists(Path.Combine(authorityRoot, "pack-manifest.json")));
            Assert.True(File.Exists(Path.Combine(authorityRoot, "baseline-manifest.json")));
            Assert.True(File.Exists(Path.Combine(authorityRoot, "authority", "task-contract.json")));
            Assert.True(File.Exists(Path.Combine(authorityRoot, "authority", "instruction-pack.json")));
            Assert.True(File.Exists(Path.Combine(authorityRoot, "expected", "task-contract.json")));
            Assert.True(File.Exists(Path.Combine(authorityRoot, "expected", "instruction-pack.json")));
            Assert.True(File.Exists(Path.Combine(authorityRoot, "scorer", "scoring-contract.json")));
            Assert.True(File.Exists(Path.Combine(authorityRoot, "state.json")));
            Assert.True(Directory.Exists(Path.Combine(packageRoot, "results", "submit-bundle")));

            using var stateDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(authorityRoot, "state.json")));
            Assert.Equal("carves-portable-agent-trial-state.v0", stateDocument.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal("ready_for_agent", stateDocument.RootElement.GetProperty("state").GetString());
            Assert.Equal("results/local", stateDocument.RootElement.GetProperty("local_results_root").GetString());
            Assert.Equal("results/submit-bundle", stateDocument.RootElement.GetProperty("submit_bundle_root").GetString());

            using var manifestDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(authorityRoot, "pack-manifest.json")));
            var manifest = manifestDocument.RootElement;
            Assert.Equal("carves-portable-agent-trial-pack.v0", manifest.GetProperty("schema_version").GetString());
            Assert.Equal("official-agent-dev-safety-v1-local-mvp", manifest.GetProperty("pack_id").GetString());
            Assert.Equal("agent-trial-local-safety-posture", manifest.GetProperty("scoring_profile_id").GetString());
            Assert.Equal("0.2.0-local", manifest.GetProperty("scoring_profile_version").GetString());
            Assert.Equal("0.1.0-local", manifest.GetProperty("required_carves_min_version").GetString());
            Assert.Equal("agent-workspace", manifest.GetProperty("agent_workspace").GetString());
            Assert.Equal(".carves-pack", manifest.GetProperty("scorer_authority").GetString());
            Assert.Equal("results/submit-bundle", manifest.GetProperty("submit_bundle_root").GetString());
            Assert.False(manifest.GetProperty("server_submission").GetBoolean());
            Assert.False(manifest.GetProperty("leaderboard_eligible").GetBoolean());

            using var expectedDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(authorityRoot, "expected", "task-contract.json")));
            Assert.Equal("task_contract", expectedDocument.RootElement.GetProperty("artifact_kind").GetString());
            Assert.Equal("agent-workspace/.carves/trial/task-contract.json", expectedDocument.RootElement.GetProperty("workspace_relative_path").GetString());

            Assert.Equal(string.Empty, RunGit(agentWorkspace, "status", "--porcelain"));
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialPackage_RefusesNonEmptyOutputAndAllowsSafePackageOverwrite()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-overwrite-" + Guid.NewGuid().ToString("N"));
        try
        {
            var first = RunMatrixCli("trial", "package", "--output", packageRoot, "--json");
            var second = RunMatrixCli("trial", "package", "--output", packageRoot, "--json");
            var forced = RunMatrixCli("trial", "package", "--output", packageRoot, "--force", "--json");

            Assert.Equal(0, first.ExitCode);
            Assert.Equal(1, second.ExitCode);
            using var failedDocument = JsonDocument.Parse(second.StandardOutput);
            Assert.Equal("failed", failedDocument.RootElement.GetProperty("status").GetString());
            Assert.Contains(
                failedDocument.RootElement.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "trial_package_output_not_empty");

            Assert.Equal(0, forced.ExitCode);
            using var forcedDocument = JsonDocument.Parse(forced.StandardOutput);
            Assert.Equal("prepared", forcedDocument.RootElement.GetProperty("status").GetString());
            Assert.True(File.Exists(Path.Combine(packageRoot, ".carves-pack", "pack-manifest.json")));
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    [Fact]
    public void TrialPackage_OutputDoesNotAskForWorkspaceOrBundlePaths()
    {
        var packageRoot = Path.Combine(Path.GetTempPath(), "carves-trial-package-copy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = RunMatrixCli("trial", "package", "--output", packageRoot);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Open only this folder in the tested agent:", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("agent-workspace", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("--workspace", result.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("--bundle-root", result.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(packageRoot);
        }
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "git";
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, stderr);
        return stdout.Trim();
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

}
