using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string TrialCommandSchemaVersion = "matrix-agent-trial-local-command.v0";
    private const string OfficialStarterPackRelativePath = "docs/matrix/starter-packs/official-agent-dev-safety-v1-local-mvp";

    private static int RunTrial(IReadOnlyList<string> arguments, string commandName)
    {
        var options = MatrixTrialOptions.Parse(arguments);
        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            Console.Error.WriteLine(options.Error);
            Console.Error.WriteLine($"Try: {commandName} trial plan --workspace <path>");
            return 2;
        }

        try
        {
            return options.Command switch
            {
                "plan" => RunTrialPlan(options, commandName),
                "prepare" => RunTrialPrepare(options, commandName),
                "package" => RunTrialPackage(options, commandName),
                "demo" => RunTrialDemo(options, commandName),
                "play" => RunTrialPlay(options, commandName),
                "collect" => RunTrialCollect(options, commandName, verifyAfterCollect: false),
                "reset" => RunTrialReset(options),
                "verify" => RunTrialVerify(options, commandName),
                "result" => RunTrialResult(options),
                "latest" => RunTrialLatest(options),
                "local" => RunTrialCollect(options, commandName, verifyAfterCollect: true),
                "record" => RunTrialRecord(options),
                "compare" => RunTrialCompare(options),
                _ => 2,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            return WriteTrialFailure(options.Command, options.Json, BuildTrialExceptionDiagnostics(options.Command, ex));
        }
    }

    private static int RunTrialPlan(MatrixTrialOptions options, string commandName)
    {
        var workspaceRoot = ResolveTrialWorkspaceRoot(options);
        var bundleRoot = ResolveTrialBundleRoot(options, workspaceRoot);
        var plan = BuildTrialCommandResult(
            "plan",
            "ready",
            workspaceRoot,
            bundleRoot,
            commandName,
            Collection: null,
            Verification: null,
            LocalScore: null,
            ResultCard: null);

        WriteTrialResult(plan, options.Json);
        return 0;
    }

    private static int RunTrialPrepare(MatrixTrialOptions options, string commandName)
    {
        var workspaceRoot = ResolveTrialWorkspaceRoot(options);
        var bundleRoot = ResolveTrialBundleRoot(options, workspaceRoot);
        var packRoot = ResolveTrialPackRoot(options);
        CopyStarterPack(packRoot, workspaceRoot);

        var result = BuildTrialCommandResult(
            "prepare",
            "prepared",
            workspaceRoot,
            bundleRoot,
            commandName,
            Collection: null,
            Verification: null,
            LocalScore: null,
            ResultCard: null);

        WriteTrialResult(result, options.Json);
        return 0;
    }

    private static int RunTrialCollect(MatrixTrialOptions options, string commandName, bool verifyAfterCollect)
    {
        var portablePackage = TryResolvePortablePackageCollectPaths(options);
        ThrowIfPortableCollectRootMissing(options, portablePackage);
        GuardPortablePackageBeforeCollect(portablePackage);

        var workspaceRoot = portablePackage?.WorkspaceRoot ?? ResolveTrialWorkspaceRoot(options);
        var bundleRoot = portablePackage?.SubmitBundleRoot ?? ResolveTrialBundleRoot(options, workspaceRoot);
        AgentTrialLocalCollectorResult collection;
        TrialBundleWriteResult bundle;
        TrialLocalScoreReadback? localScore;
        try
        {
            collection = AgentTrialLocalCollector.Collect(new AgentTrialLocalCollectorOptions(workspaceRoot));
            bundle = WriteLocalTrialBundle(workspaceRoot, bundleRoot, DateTimeOffset.UtcNow);
            localScore = ReadTrialLocalScore(collection.TrialResultPath);
        }
        catch (Exception ex) when (portablePackage is not null && ex is (IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException))
        {
            MarkPortablePackageCollectException(portablePackage, ex);
            throw;
        }

        MatrixVerifyResult? verification = null;
        var shouldVerifyAfterCollect = verifyAfterCollect || portablePackage is not null;
        if (shouldVerifyAfterCollect)
        {
            verification = BuildVerifyResult(bundle.BundleRoot, requireTrial: true);
        }

        var status = ResolveTrialCommandStatus(collection, verification);
        var collectionReadback = new TrialCollectionReadback(
            collection.LocalCollectionStatus,
            collection.MissingRequiredArtifacts,
            collection.FailureReasons,
            ToPortablePath(workspaceRoot, collection.DiffScopeSummaryPath),
            ToPortablePath(workspaceRoot, collection.TestEvidencePath),
            ToPortablePath(workspaceRoot, collection.TrialResultPath));
        var verificationReadback = verification is null
            ? null
            : new TrialVerificationReadback(
                verification.Status,
                verification.VerificationPosture,
                verification.ExitCode,
                verification.TrialArtifacts.Verified,
                verification.ReasonCodes);
        var result = BuildTrialCommandResult(
            verifyAfterCollect ? "local" : "collect",
            status,
            workspaceRoot,
            bundle.BundleRoot,
            commandName,
            collectionReadback,
            verificationReadback,
            localScore,
            BuildTrialResultCard(
                bundle.BundleRoot,
                localScore,
                collectionReadback,
                verificationReadback,
                writeCardFile: true));

        WritePortablePackageLocalResults(portablePackage, result);
        MarkPortablePackageCollectFinished(portablePackage, status, collectionReadback, verificationReadback);
        WriteTrialResult(result, options.Json);
        return status == "verified" || status == "collected" ? 0 : 1;
    }

    private static int RunTrialVerify(MatrixTrialOptions options, string commandName)
    {
        var bundleRoot = ResolveTrialVerifyBundleRoot(options);
        var verification = BuildVerifyResult(bundleRoot, requireTrial: true);
        var localScore = ReadTrialLocalScore(Path.Combine(bundleRoot, "trial", "carves-agent-trial-result.json"));
        var verificationReadback = new TrialVerificationReadback(
            verification.Status,
            verification.VerificationPosture,
            verification.ExitCode,
            verification.TrialArtifacts.Verified,
            verification.ReasonCodes);
        var result = BuildTrialCommandResult(
            "verify",
            verification.IsVerified ? "verified" : "verification_failed",
            WorkspaceRoot: null,
            bundleRoot,
            commandName,
            Collection: null,
            verificationReadback,
            localScore,
            BuildTrialResultCard(
                bundleRoot,
                localScore,
                Collection: null,
                verificationReadback,
                writeCardFile: false));

        WriteTrialResult(result, options.Json);
        return verification.ExitCode;
    }

    private static string ResolveTrialCommandStatus(AgentTrialLocalCollectorResult collection, MatrixVerifyResult? verification)
    {
        if (!string.Equals(collection.LocalCollectionStatus, "collectable", StringComparison.Ordinal))
        {
            return "collection_failed";
        }

        if (verification is null)
        {
            return "collected";
        }

        return verification.IsVerified ? "verified" : "verification_failed";
    }

    private static TrialCommandResult BuildTrialCommandResult(
        string command,
        string status,
        string? WorkspaceRoot,
        string bundleRoot,
        string commandName,
        TrialCollectionReadback? Collection,
        TrialVerificationReadback? Verification,
        TrialLocalScoreReadback? LocalScore,
        TrialResultCardReadback? ResultCard)
    {
        var workspaceRoot = WorkspaceRoot is null ? null : Path.GetFullPath(WorkspaceRoot);
        var evidenceRoot = workspaceRoot is null ? null : Path.Combine(workspaceRoot, "artifacts");
        var verifyCommand = $"{commandName} verify {QuoteForDisplay(bundleRoot)} --trial --json";
        return new TrialCommandResult(
            TrialCommandSchemaVersion,
            command,
            status,
            Offline: true,
            ServerSubmission: false,
            workspaceRoot,
            evidenceRoot,
            bundleRoot,
            AgentRunDirectory: workspaceRoot,
            PromptPath: workspaceRoot is null ? null : Path.Combine(workspaceRoot, "prompts", "official-v1-local-mvp", "task-001-bounded-edit.prompt.md"),
            AgentReportPath: workspaceRoot is null ? null : Path.Combine(workspaceRoot, "artifacts", "agent-report.json"),
            VerifyCommand: verifyCommand,
            Steps:
            [
                "prepare: materialize the starter pack into a clean workspace",
                "run: run the agent inside the workspace using the prompt sample and AGENTS.md constraints",
                "collect: write summary evidence under artifacts/ and a Matrix trial bundle",
                "verify: run strict Matrix verification against the local trial bundle"
            ],
            NonClaims:
            [
                "does_not_submit_to_server",
                "not_leaderboard_eligible",
                "not_certification",
                "no_prompt_or_model_response_upload"
            ],
            BuildTrialDiagnostics(Collection, Verification),
            Collection,
            Verification,
            LocalScore,
            ResultCard);
    }

    private static string ResolveTrialWorkspaceRoot(MatrixTrialOptions options)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(options.WorkspaceRoot) ? "." : options.WorkspaceRoot);
    }

    private static string ResolveTrialBundleRoot(MatrixTrialOptions options, string workspaceRoot)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(options.BundleRoot)
            ? Path.Combine(workspaceRoot, "artifacts", "matrix-trial-bundle")
            : options.BundleRoot);
    }

    private static string ResolveTrialPackRoot(MatrixTrialOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PackRoot))
        {
            return Path.GetFullPath(options.PackRoot);
        }

        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, OfficialStarterPackRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to find the official starter pack. Pass --pack-root <path>.");
    }

    private static void CopyStarterPack(string packRoot, string workspaceRoot)
    {
        if (!Directory.Exists(packRoot))
        {
            throw new DirectoryNotFoundException($"Starter pack root does not exist: {packRoot}");
        }

        if (Directory.Exists(workspaceRoot) && Directory.EnumerateFileSystemEntries(workspaceRoot).Any())
        {
            throw new InvalidOperationException($"Workspace already exists and is not empty: {workspaceRoot}");
        }

        Directory.CreateDirectory(workspaceRoot);
        foreach (var sourcePath in Directory.EnumerateFiles(packRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(packRoot, sourcePath);
            var destinationPath = Path.Combine(workspaceRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: false);
        }
    }

    private static string ToPortablePath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    private static string QuoteForDisplay(string path)
    {
        return path.Contains(' ', StringComparison.Ordinal) ? $"\"{path}\"" : path;
    }

}
