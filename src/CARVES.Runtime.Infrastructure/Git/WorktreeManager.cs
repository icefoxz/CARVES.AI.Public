using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Git;

namespace Carves.Runtime.Infrastructure.Git;

public sealed class WorktreeManager : IWorktreeManager
{
    private const string ManifestFileName = ".carves-worktree.json";
    private static readonly ManualResetEventSlim RetryDelayGate = new(initialState: false);
    private readonly IGitClient gitClient;

    public WorktreeManager(IGitClient gitClient)
    {
        this.gitClient = gitClient;
    }

    public string ResolveWorktreeRoot(SystemConfig systemConfig, string repoRoot)
    {
        return Path.GetFullPath(Path.Combine(repoRoot, systemConfig.WorktreeRoot));
    }

    public string PrepareWorktree(SystemConfig systemConfig, string repoRoot, string taskId, string? startPoint)
    {
        var worktreeRoot = ResolveWorktreeRoot(systemConfig, repoRoot);
        Directory.CreateDirectory(worktreeRoot);

        var safeTaskId = string.Concat(taskId.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character));
        var worktreePath = Path.Combine(worktreeRoot, safeTaskId);
        DeleteDirectoryIfPresent(worktreePath);

        var reference = string.IsNullOrWhiteSpace(startPoint) ? gitClient.TryGetCurrentCommit(repoRoot) : startPoint;
        var mode = "managed";
        if (!string.IsNullOrWhiteSpace(reference) && gitClient.IsRepository(repoRoot) && gitClient.TryCreateDetachedWorktree(repoRoot, worktreePath, reference))
        {
            mode = "git";
        }
        else
        {
            CopyManagedSnapshot(repoRoot, worktreePath, worktreeRoot);
        }

        WriteManifest(worktreePath, repoRoot, taskId, reference, mode);
        return worktreePath;
    }

    public void CleanupWorktree(string worktreePath)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
        {
            return;
        }

        var manifest = LoadManifest(worktreePath);
        if (manifest is not null && string.Equals(manifest.Mode, "git", StringComparison.OrdinalIgnoreCase))
        {
            gitClient.TryRemoveWorktree(manifest.RepoRoot, worktreePath);
        }
        else
        {
            DeleteDirectoryIfPresent(worktreePath);
        }

        DeleteEmptyParents(Path.GetDirectoryName(worktreePath));
    }

    private static void WriteManifest(string worktreePath, string repoRoot, string taskId, string? startPoint, string mode)
    {
        var manifestPath = Path.Combine(worktreePath, ManifestFileName);
        var manifest = new WorktreeManifest(repoRoot, taskId, startPoint ?? string.Empty, mode, DateTimeOffset.UtcNow);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static WorktreeManifest? LoadManifest(string worktreePath)
    {
        var manifestPath = Path.Combine(worktreePath, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorktreeManifest>(File.ReadAllText(manifestPath));
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                DelayBeforeRetry(attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                DelayBeforeRetry(attempt);
            }
        }
    }

    private static void DelayBeforeRetry(int attempt)
    {
        RetryDelayGate.Wait(TimeSpan.FromMilliseconds(50 * attempt));
    }

    private static void CopyManagedSnapshot(string sourceRoot, string destinationRoot, string managedWorktreeRoot)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot))
        {
            var name = Path.GetFileName(directory);
            if (name is ".git" or ".ai" or ".carves-platform" or "bin" or "obj" or "TestResults")
            {
                continue;
            }

            if (string.Equals(Path.GetFullPath(directory), Path.GetFullPath(managedWorktreeRoot), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyManagedSnapshot(directory, Path.Combine(destinationRoot, name), managedWorktreeRoot);
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot))
        {
            var name = Path.GetFileName(file);
            if (name.Equals(".carves-worktree.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destinationRoot, name), overwrite: true);
        }
    }

    private static void DeleteEmptyParents(string? path)
    {
        while (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
            path = Path.GetDirectoryName(path);
        }
    }

    private sealed record WorktreeManifest(string RepoRoot, string TaskId, string StartPoint, string Mode, DateTimeOffset CreatedAt);
}
