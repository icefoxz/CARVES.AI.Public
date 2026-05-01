namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static readonly TimeSpan TrialGitTimeout = TimeSpan.FromSeconds(30);

    private sealed record TrialOneCommandResult(
        string SchemaVersion,
        string Command,
        string Status,
        bool Offline,
        bool ServerSubmission,
        string RunId,
        string RunRoot,
        string WorkspaceRoot,
        string BundleRoot,
        string HistoryRoot,
        string? HistoryEntryRef,
        string AgentInstruction,
        IReadOnlyList<string> NonClaims,
        IReadOnlyList<TrialDiagnosticReadback> Diagnostics,
        TrialCollectionReadback? Collection,
        TrialVerificationReadback? Verification,
        TrialLocalScoreReadback? LocalScore,
        TrialResultCardReadback? ResultCard);

    private sealed record TrialRunPaths(
        string RunId,
        string CreatedAt,
        string TrialRoot,
        string RunRoot,
        string WorkspaceRoot,
        string BundleRoot,
        string HistoryRoot,
        string LatestPointerPath);

    private static int RunTrialDemo(MatrixTrialOptions options, string commandName)
    {
        var paths = CreateTrialRunPaths(options);
        PrepareOneCommandWorkspace(options, paths.WorkspaceRoot);
        ApplyBuiltInDemoAgent(paths.WorkspaceRoot);
        var result = CompleteOneCommandTrial("demo", paths, commandName);
        WriteTrialOneCommandResult(result, options.Json);
        return result.Status == "verified" ? 0 : 1;
    }

    private static int RunTrialPlay(MatrixTrialOptions options, string commandName)
    {
        var paths = CreateTrialRunPaths(options);
        PrepareOneCommandWorkspace(options, paths.WorkspaceRoot);
        if (options.DemoAgent)
        {
            ApplyBuiltInDemoAgent(paths.WorkspaceRoot);
        }
        else if (options.Json || options.NoWait)
        {
            var prepared = BuildPreparedTrialPlayResult(paths);
            WriteTrialOneCommandResult(prepared, options.Json);
            return 0;
        }
        else
        {
            WriteTrialPlayInstructions(paths);
            _ = Console.ReadLine();
        }

        var result = CompleteOneCommandTrial("play", paths, commandName);
        WriteTrialOneCommandResult(result, options.Json);
        return result.Status == "verified" ? 0 : 1;
    }

    private static TrialRunPaths CreateTrialRunPaths(MatrixTrialOptions options)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var runId = SanitizeRunId(string.IsNullOrWhiteSpace(options.RunId)
            ? createdAt.UtcDateTime.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]
            : options.RunId);
        var trialRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(options.TrialRoot)
            ? Path.Combine(Directory.GetCurrentDirectory(), "carves-trials")
            : options.TrialRoot);
        var runRoot = Path.Combine(trialRoot, BuildTrialRunDirectoryName(runId));
        return new TrialRunPaths(
            runId,
            createdAt.ToString("O"),
            trialRoot,
            runRoot,
            Path.GetFullPath(string.IsNullOrWhiteSpace(options.WorkspaceRoot) ? Path.Combine(runRoot, "workspace") : options.WorkspaceRoot),
            Path.GetFullPath(string.IsNullOrWhiteSpace(options.BundleRoot) ? Path.Combine(runRoot, "bundle") : options.BundleRoot),
            Path.GetFullPath(string.IsNullOrWhiteSpace(options.HistoryRoot) ? Path.Combine(trialRoot, "history") : options.HistoryRoot),
            Path.Combine(trialRoot, TrialLatestPointerFileName));
    }

    private static void PrepareOneCommandWorkspace(MatrixTrialOptions options, string workspaceRoot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(workspaceRoot) ?? ".");
        CopyStarterPack(ResolveTrialPackRoot(options), workspaceRoot);
        RunTrialGit(workspaceRoot, "init");
        RunTrialGit(workspaceRoot, "config", "user.email", "agent-trial-local@example.test");
        RunTrialGit(workspaceRoot, "config", "user.name", "Agent Trial Local");
        RunTrialGit(workspaceRoot, "add", ".");
        RunTrialGit(workspaceRoot, "commit", "-m", "baseline");
    }

    private static TrialOneCommandResult CompleteOneCommandTrial(string command, TrialRunPaths paths, string commandName)
    {
        var collection = AgentTrialLocalCollector.Collect(new AgentTrialLocalCollectorOptions(paths.WorkspaceRoot));
        var bundle = WriteLocalTrialBundle(paths.WorkspaceRoot, paths.BundleRoot, DateTimeOffset.UtcNow);
        var localScore = ReadTrialLocalScore(collection.TrialResultPath);
        var verification = BuildVerifyResult(bundle.BundleRoot, requireTrial: true);
        var status = ResolveTrialCommandStatus(collection, verification);
        var collectionReadback = new TrialCollectionReadback(
            collection.LocalCollectionStatus,
            collection.MissingRequiredArtifacts,
            collection.FailureReasons,
            ToPortablePath(paths.WorkspaceRoot, collection.DiffScopeSummaryPath),
            ToPortablePath(paths.WorkspaceRoot, collection.TestEvidencePath),
            ToPortablePath(paths.WorkspaceRoot, collection.TrialResultPath));
        var verificationReadback = new TrialVerificationReadback(
            verification.Status,
            verification.VerificationPosture,
            verification.ExitCode,
            verification.TrialArtifacts.Verified,
            verification.ReasonCodes);
        var historyEntryRef = WriteOneCommandHistory(paths, verification);
        var resultCard = BuildTrialResultCard(
            bundle.BundleRoot,
            localScore,
            collectionReadback,
            verificationReadback,
            writeCardFile: true);
        var result = new TrialOneCommandResult(
            TrialCommandSchemaVersion,
            command,
            status,
            Offline: true,
            ServerSubmission: false,
            paths.RunId,
            paths.RunRoot,
            paths.WorkspaceRoot,
            bundle.BundleRoot,
            paths.HistoryRoot,
            historyEntryRef,
            BuildTrialAgentInstruction(paths.WorkspaceRoot),
            BuildTrialOneCommandNonClaims(),
            BuildTrialDiagnostics(collectionReadback, verificationReadback),
            collectionReadback,
            verificationReadback,
            localScore,
            resultCard);
        WriteTrialLatestPointer(paths, result);
        return result;
    }

    private static TrialOneCommandResult BuildPreparedTrialPlayResult(TrialRunPaths paths)
    {
        return new TrialOneCommandResult(
            TrialCommandSchemaVersion,
            "play",
            "ready_for_agent",
            Offline: true,
            ServerSubmission: false,
            paths.RunId,
            paths.RunRoot,
            paths.WorkspaceRoot,
            paths.BundleRoot,
            paths.HistoryRoot,
            HistoryEntryRef: null,
            BuildTrialAgentInstruction(paths.WorkspaceRoot),
            BuildTrialOneCommandNonClaims(),
            Diagnostics: [],
            Collection: null,
            Verification: null,
            LocalScore: null,
            ResultCard: null);
    }

    private static string WriteOneCommandHistory(TrialRunPaths paths, MatrixVerifyResult verification)
    {
        RejectHistoryInsideBundle(paths.HistoryRoot, paths.BundleRoot);
        var entry = BuildTrialHistoryEntry(paths.BundleRoot, paths.RunId, verification);
        return WriteTrialHistoryEntry(paths.HistoryRoot, entry);
    }

    private static string BuildTrialRunDirectoryName(string runId)
    {
        return runId.StartsWith("run-", StringComparison.Ordinal) ? runId : "run-" + runId;
    }

    private static void RunTrialGit(string workspaceRoot, params string[] arguments)
    {
        try
        {
            var result = AgentTrialLocalProcessRunner.Run("git", arguments, workspaceRoot, TrialGitTimeout);
            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException("git command failed: " + result.Stderr);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException("git is unavailable.", ex);
        }
    }
}
