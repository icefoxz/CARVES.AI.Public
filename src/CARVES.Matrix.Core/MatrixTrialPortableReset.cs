using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string PortablePackageResetSchemaVersion = "carves-portable-agent-trial-reset.v0";
    private const string PortablePackageResetUnsupportedOptionMessage = "trial reset only runs from a portable package root and does not accept path options.";
    private const string PortablePackageResetRootMissingMessage = "Portable package root not found for reset.";
    private const string PortablePackageResetGitMissingMessage = "Portable reset workspace git baseline missing.";

    private sealed record PortablePackageResetResult(
        string SchemaVersion,
        string Command,
        string Status,
        string PackageRoot,
        string WorkspaceRoot,
        string ResultsRoot,
        string LocalResultsRoot,
        string SubmitBundleRoot,
        string? ArchivedAttemptRoot,
        IReadOnlyList<string> ParkedRootEntries,
        IReadOnlyList<string> NextSteps);

    private static int RunTrialReset(MatrixTrialOptions options)
    {
        ThrowIfResetHasPathOptions(options);
        var paths = ResolvePortablePackageResetPaths();

        ResetPortableWorkspace(paths);
        var resetTime = DateTimeOffset.UtcNow;
        var unexpectedRootEntries = FindUnexpectedPortableRootEntries(paths.PackageRoot).ToArray();
        var archivedAttemptRoot = ArchivePortablePackageAttempt(paths, unexpectedRootEntries, resetTime);
        WriteInitialPortablePackageState(Path.Combine(paths.PackageRoot, ".carves-pack"), resetTime);

        var result = new PortablePackageResetResult(
            PortablePackageResetSchemaVersion,
            "reset",
            "reset",
            paths.PackageRoot,
            paths.WorkspaceRoot,
            paths.ResultsRoot,
            paths.LocalResultsRoot,
            paths.SubmitBundleRoot,
            archivedAttemptRoot,
            unexpectedRootEntries,
            [
                "Run another agent inside agent-workspace.",
                "After the agent writes artifacts/agent-report.json, run score again from the package root."
            ]);

        WritePortablePackageResetResult(result, options.Json);
        return 0;
    }

    private static void ThrowIfResetHasPathOptions(MatrixTrialOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.WorkspaceRoot)
            || !string.IsNullOrWhiteSpace(options.BundleRoot)
            || !string.IsNullOrWhiteSpace(options.PackRoot)
            || !string.IsNullOrWhiteSpace(options.HistoryRoot)
            || !string.IsNullOrWhiteSpace(options.TrialRoot)
            || !string.IsNullOrWhiteSpace(options.OutputRoot)
            || !string.IsNullOrWhiteSpace(options.RunId)
            || !string.IsNullOrWhiteSpace(options.BaselineRunId)
            || !string.IsNullOrWhiteSpace(options.TargetRunId)
            || options.Force
            || options.NoWait
            || options.DemoAgent)
        {
            throw new InvalidOperationException(PortablePackageResetUnsupportedOptionMessage);
        }
    }

    private static PortablePackageCollectPaths ResolvePortablePackageResetPaths()
    {
        var packageRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
        var workspaceRoot = Path.Combine(packageRoot, "agent-workspace");
        var authorityRoot = Path.Combine(packageRoot, ".carves-pack");
        if (!Directory.Exists(workspaceRoot) || !Directory.Exists(authorityRoot))
        {
            throw new InvalidOperationException(PortablePackageResetRootMissingMessage);
        }

        var resultsRoot = Path.Combine(packageRoot, "results");
        return new PortablePackageCollectPaths(
            packageRoot,
            workspaceRoot,
            resultsRoot,
            Path.Combine(resultsRoot, "local"),
            Path.Combine(resultsRoot, "submit-bundle"));
    }

    private static void ResetPortableWorkspace(PortablePackageCollectPaths paths)
    {
        if (!Directory.Exists(Path.Combine(paths.WorkspaceRoot, ".git")))
        {
            throw new InvalidOperationException(PortablePackageResetGitMissingMessage);
        }

        RunTrialGitCapture(paths.WorkspaceRoot, "rev-parse", "--is-inside-work-tree");
        RunTrialGit(paths.WorkspaceRoot, "reset", "--hard", "HEAD");
        RunTrialGit(paths.WorkspaceRoot, "clean", "-ffdx");
    }

    private static string? ArchivePortablePackageAttempt(
        PortablePackageCollectPaths paths,
        IReadOnlyList<string> unexpectedRootEntries,
        DateTimeOffset resetTime)
    {
        var hasLocalResults = HasAnyFileSystemEntry(paths.LocalResultsRoot);
        var hasSubmitBundle = HasAnyFileSystemEntry(paths.SubmitBundleRoot);
        var hasRootResidue = unexpectedRootEntries.Count > 0;
        Directory.CreateDirectory(paths.LocalResultsRoot);
        Directory.CreateDirectory(paths.SubmitBundleRoot);
        if (!hasLocalResults && !hasSubmitBundle && !hasRootResidue)
        {
            return null;
        }

        var archiveRoot = Path.Combine(
            paths.ResultsRoot,
            "history",
            resetTime.ToString("yyyyMMdd-HHmmssfff") + "-reset");
        Directory.CreateDirectory(archiveRoot);
        MoveDirectoryIfNonEmpty(paths.LocalResultsRoot, Path.Combine(archiveRoot, "local"));
        MoveDirectoryIfNonEmpty(paths.SubmitBundleRoot, Path.Combine(archiveRoot, "submit-bundle"));
        ParkUnexpectedPortableRootEntries(paths.PackageRoot, archiveRoot, unexpectedRootEntries);
        Directory.CreateDirectory(paths.LocalResultsRoot);
        Directory.CreateDirectory(paths.SubmitBundleRoot);
        return archiveRoot;
    }

    private static void MoveDirectoryIfNonEmpty(string sourceDirectory, string destinationDirectory)
    {
        if (!HasAnyFileSystemEntry(sourceDirectory))
        {
            Directory.CreateDirectory(sourceDirectory);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationDirectory)!);
        Directory.Move(sourceDirectory, destinationDirectory);
    }

    private static void ParkUnexpectedPortableRootEntries(
        string packageRoot,
        string archiveRoot,
        IReadOnlyList<string> unexpectedRootEntries)
    {
        if (unexpectedRootEntries.Count == 0)
        {
            return;
        }

        var residueRoot = Path.Combine(archiveRoot, "root-residue");
        Directory.CreateDirectory(residueRoot);
        foreach (var entryName in unexpectedRootEntries)
        {
            var sourcePath = Path.GetFullPath(Path.Combine(packageRoot, entryName));
            if (!IsPathInsideDirectory(sourcePath, packageRoot))
            {
                throw new InvalidOperationException("Portable reset refused to move a path outside the package root.");
            }

            var destinationPath = Path.Combine(residueRoot, entryName);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            if (File.Exists(sourcePath))
            {
                File.Move(sourcePath, destinationPath);
            }
            else if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destinationPath);
            }
        }
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(fullPath, fullDirectory, comparison)
            || fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, comparison);
    }

    private static void WritePortablePackageResetResult(PortablePackageResetResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        Console.WriteLine("CARVES Agent Trial reset");
        Console.WriteLine("Mode: local only (this computer only; no upload, no certification, no leaderboard).");
        Console.WriteLine("Status: reset");
        Console.WriteLine($"Package: {result.PackageRoot}");
        Console.WriteLine("Workspace reset: ok");
        Console.WriteLine($"Previous attempt archived: {result.ArchivedAttemptRoot ?? "none"}");
        Console.WriteLine("Package-root residue parked: " + (result.ParkedRootEntries.Count == 0
            ? "none"
            : string.Join(", ", result.ParkedRootEntries)));
        Console.WriteLine("Cleared: agent-workspace changes, results/local, results/submit-bundle, and unexpected package-root files.");
        Console.WriteLine("Kept: .carves-pack scorer authority, package scripts, prompts, and archived history under results/history.");
        Console.WriteLine("Not claimed: reset does not submit anything, certify anything, or make this machine tamper-proof.");
        Console.WriteLine("Next:");
        foreach (var nextStep in result.NextSteps)
        {
            Console.WriteLine($"- {nextStep}");
        }
    }
}
