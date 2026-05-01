namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string PortablePackageSchemaVersion = "matrix-agent-trial-portable-package.v0";
    private const string DefaultPortablePackageDirectoryName = "carves-agent-trial-pack";

    private sealed record TrialPortablePackageResult(
        string SchemaVersion,
        string Command,
        string Status,
        bool Offline,
        bool ServerSubmission,
        string PackageRoot,
        string AgentWorkspaceRoot,
        string ScorerAuthorityRoot,
        string ResultsRoot,
        string SubmitBundleRoot,
        string ReadmePath,
        string BlindPromptPath,
        string GuidedPromptPath,
        string ScoreCmdPath,
        string ScoreShPath,
        string ResultCmdPath,
        string ResultShPath,
        string ResetCmdPath,
        string ResetShPath,
        string PackManifestPath,
        string BaselineManifestPath,
        string BaselineCommitSha,
        bool WindowsPlayable,
        string? ZipPath,
        string? ScorerManifestPath,
        string? ScorerEntrypoint,
        string? RuntimeIdentifier,
        string? BuildLabel,
        IReadOnlyList<string> NonClaims,
        IReadOnlyList<string> NextSteps);

    private static int RunTrialPackage(MatrixTrialOptions options, string commandName)
    {
        var packageRoot = ResolvePortablePackageRoot(options);
        var packRoot = ResolveTrialPackRoot(options);
        var createdAt = DateTimeOffset.UtcNow;
        ValidateWindowsPlayableScorerOptions(packageRoot, options);
        var result = WritePortablePackage(packageRoot, packRoot, options, createdAt);

        WritePortablePackageResult(result, options.Json, commandName);
        return 0;
    }

    private static string ResolvePortablePackageRoot(MatrixTrialOptions options)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(options.OutputRoot)
            ? Path.Combine(Directory.GetCurrentDirectory(), DefaultPortablePackageDirectoryName)
            : options.OutputRoot);
    }

    private static TrialPortablePackageResult WritePortablePackage(
        string packageRoot,
        string packRoot,
        MatrixTrialOptions options,
        DateTimeOffset createdAt)
    {
        PreparePortablePackageRoot(packageRoot, options.Force);

        var agentWorkspaceRoot = Path.Combine(packageRoot, "agent-workspace");
        var scorerAuthorityRoot = Path.Combine(packageRoot, ".carves-pack");
        var resultsRoot = Path.Combine(packageRoot, "results");
        var submitBundleRoot = Path.Combine(resultsRoot, "submit-bundle");

        CopyStarterPack(packRoot, agentWorkspaceRoot);
        RunTrialGit(agentWorkspaceRoot, "init");
        RunTrialGit(agentWorkspaceRoot, "config", "user.email", "agent-trial-local@example.test");
        RunTrialGit(agentWorkspaceRoot, "config", "user.name", "Agent Trial Local");
        RunTrialGit(agentWorkspaceRoot, "add", ".");
        RunTrialGit(agentWorkspaceRoot, "commit", "-m", "baseline");
        var baselineCommitSha = RunTrialGitCapture(agentWorkspaceRoot, "rev-parse", "HEAD");

        Directory.CreateDirectory(Path.Combine(scorerAuthorityRoot, "authority"));
        Directory.CreateDirectory(Path.Combine(scorerAuthorityRoot, "expected"));
        Directory.CreateDirectory(Path.Combine(scorerAuthorityRoot, "scorer"));
        Directory.CreateDirectory(Path.Combine(scorerAuthorityRoot, "baseline"));
        Directory.CreateDirectory(Path.Combine(resultsRoot, "local"));
        Directory.CreateDirectory(submitBundleRoot);

        var authority = CopyPortableAuthorityFiles(agentWorkspaceRoot, scorerAuthorityRoot);
        var pack = AgentTrialLocalJson.ReadObject(authority.PackPath);
        var challenge = AgentTrialLocalJson.ReadObject(authority.ChallengePath);
        var taskContract = AgentTrialTaskContract.From(AgentTrialLocalJson.ReadObject(authority.TaskContractPath));

        var nonClaims = BuildPortablePackageNonClaims();
        var packManifestPath = Path.Combine(scorerAuthorityRoot, "pack-manifest.json");
        var baselineManifestPath = Path.Combine(scorerAuthorityRoot, "baseline-manifest.json");
        WritePortablePackManifest(
            packManifestPath,
            createdAt,
            pack,
            challenge,
            baselineCommitSha,
            nonClaims);
        WritePortableBaselineManifest(
            baselineManifestPath,
            createdAt,
            agentWorkspaceRoot,
            baselineCommitSha);
        WriteExpectedArtifactMetadata(
            Path.Combine(scorerAuthorityRoot, "expected", "task-contract.json"),
            "task_contract",
            "agent-workspace/.carves/trial/task-contract.json",
            ".carves-pack/authority/task-contract.json",
            authority.TaskContractPath);
        WriteExpectedArtifactMetadata(
            Path.Combine(scorerAuthorityRoot, "expected", "instruction-pack.json"),
            "instruction_pack",
            "agent-workspace/.carves/trial/instruction-pack.json",
            ".carves-pack/authority/instruction-pack.json",
            authority.InstructionPackPath);
        WritePortableScoringContract(Path.Combine(scorerAuthorityRoot, "scorer", "scoring-contract.json"));
        WriteInitialPortablePackageState(scorerAuthorityRoot, createdAt);

        var readmePath = Path.Combine(packageRoot, "README-FIRST.md");
        var blindPromptPath = Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_BLIND.txt");
        var guidedPromptPath = Path.Combine(packageRoot, "COPY_THIS_TO_AGENT_GUIDED.txt");
        File.WriteAllText(readmePath, BuildPortableReadme(), System.Text.Encoding.UTF8);
        File.WriteAllText(blindPromptPath, BuildPortableBlindPrompt(taskContract), System.Text.Encoding.UTF8);
        File.WriteAllText(guidedPromptPath, BuildPortableGuidedPrompt(taskContract), System.Text.Encoding.UTF8);
        var scoreCmdPath = Path.Combine(packageRoot, "SCORE.cmd");
        var scoreShPath = Path.Combine(packageRoot, "score.sh");
        var resultCmdPath = Path.Combine(packageRoot, "RESULT.cmd");
        var resultShPath = Path.Combine(packageRoot, "result.sh");
        var resetCmdPath = Path.Combine(packageRoot, "RESET.cmd");
        var resetShPath = Path.Combine(packageRoot, "reset.sh");
        File.WriteAllText(scoreCmdPath, ToWindowsCommandFileText(BuildPortableScoreCmd()), System.Text.Encoding.UTF8);
        File.WriteAllText(scoreShPath, BuildPortableScoreSh(), System.Text.Encoding.UTF8);
        File.WriteAllText(resultCmdPath, ToWindowsCommandFileText(BuildPortableResultCmd()), System.Text.Encoding.UTF8);
        File.WriteAllText(resultShPath, BuildPortableResultSh(), System.Text.Encoding.UTF8);
        File.WriteAllText(resetCmdPath, ToWindowsCommandFileText(BuildPortableResetCmd()), System.Text.Encoding.UTF8);
        File.WriteAllText(resetShPath, BuildPortableResetSh(), System.Text.Encoding.UTF8);
        TryMakeExecutable(scoreShPath);
        TryMakeExecutable(resultShPath);
        TryMakeExecutable(resetShPath);

        var scorerBundle = AssembleWindowsPlayablePackage(packageRoot, options, createdAt);
        var nextSteps = new List<string>
        {
            "Open only agent-workspace/ in the tested agent.",
            "Use COPY_THIS_TO_AGENT_BLIND.txt in a fresh thread for strict comparison.",
            "Use COPY_THIS_TO_AGENT_GUIDED.txt only for learning or local practice.",
            "After the agent writes artifacts/agent-report.json, run score.sh or SCORE.cmd from the package root.",
            "Run RESULT.cmd or result.sh to view the previous score after closing the score window.",
            "Run RESET.cmd or reset.sh to clear this local attempt before testing another agent in the same folder.",
            "Do not edit .carves-pack/; it is local scorer authority.",
            "Reset is local cleanup only; it does not create server submission, certification, or leaderboard eligibility."
        };
        if (scorerBundle is not null)
        {
            nextSteps.Add("Distribute the Windows playable zip; it includes the package-local scorer under tools/carves/.");
        }

        return new TrialPortablePackageResult(
            PortablePackageSchemaVersion,
            "package",
            "prepared",
            Offline: true,
            ServerSubmission: false,
            packageRoot,
            agentWorkspaceRoot,
            scorerAuthorityRoot,
            resultsRoot,
            submitBundleRoot,
            readmePath,
            blindPromptPath,
            guidedPromptPath,
            scoreCmdPath,
            scoreShPath,
            resultCmdPath,
            resultShPath,
            resetCmdPath,
            resetShPath,
            packManifestPath,
            baselineManifestPath,
            baselineCommitSha,
            scorerBundle is not null,
            scorerBundle?.ZipPath,
            scorerBundle?.ManifestPath,
            scorerBundle?.Entrypoint,
            scorerBundle?.RuntimeIdentifier,
            scorerBundle?.BuildLabel,
            nonClaims,
            nextSteps);
    }

    private static string ToWindowsCommandFileText(string text)
    {
        return text.ReplaceLineEndings("\r\n");
    }

    private static void PreparePortablePackageRoot(string packageRoot, bool force)
    {
        if (File.Exists(packageRoot))
        {
            throw new InvalidOperationException($"Package output path is a file: {packageRoot}");
        }

        if (Directory.Exists(packageRoot) && Directory.EnumerateFileSystemEntries(packageRoot).Any())
        {
            if (!force)
            {
                throw new InvalidOperationException($"Package output already exists and is not empty: {packageRoot}");
            }

            if (!LooksLikePortablePackageRoot(packageRoot))
            {
                throw new InvalidOperationException($"Refusing to overwrite non-package output directory: {packageRoot}");
            }

            Directory.Delete(packageRoot, recursive: true);
        }

        Directory.CreateDirectory(packageRoot);
    }

    private static bool LooksLikePortablePackageRoot(string packageRoot)
    {
        return File.Exists(Path.Combine(packageRoot, ".carves-pack", "pack-manifest.json"))
            && Directory.Exists(Path.Combine(packageRoot, "agent-workspace"));
    }

    private static PortableAuthorityFiles CopyPortableAuthorityFiles(string agentWorkspaceRoot, string scorerAuthorityRoot)
    {
        var authorityRoot = Path.Combine(scorerAuthorityRoot, "authority");
        var packPath = CopyAuthorityFile(agentWorkspaceRoot, authorityRoot, ".carves/trial/pack.json", "pack.json");
        var challengePath = CopyAuthorityFile(agentWorkspaceRoot, authorityRoot, ".carves/trial/challenge.json", "challenge.json");
        var instructionPackPath = CopyAuthorityFile(agentWorkspaceRoot, authorityRoot, ".carves/trial/instruction-pack.json", "instruction-pack.json");
        var taskContractPath = CopyAuthorityFile(agentWorkspaceRoot, authorityRoot, ".carves/trial/task-contract.json", "task-contract.json");
        return new PortableAuthorityFiles(packPath, challengePath, instructionPackPath, taskContractPath);
    }

    private static string CopyAuthorityFile(string agentWorkspaceRoot, string authorityRoot, string workspaceRelativePath, string authorityFileName)
    {
        var source = Path.Combine(agentWorkspaceRoot, workspaceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var destination = Path.Combine(authorityRoot, authorityFileName);
        File.Copy(source, destination, overwrite: false);
        return destination;
    }

    private static void TryMakeExecutable(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
        }
    }

    private static string RunTrialGitCapture(string workspaceRoot, params string[] arguments)
    {
        try
        {
            var result = AgentTrialLocalProcessRunner.Run("git", arguments, workspaceRoot, TrialGitTimeout);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("git command failed: " + result.Stderr);
            }

            return result.Stdout.Trim();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git is unavailable.", ex);
        }
    }

    private sealed record PortableAuthorityFiles(
        string PackPath,
        string ChallengePath,
        string InstructionPackPath,
        string TaskContractPath);
}
