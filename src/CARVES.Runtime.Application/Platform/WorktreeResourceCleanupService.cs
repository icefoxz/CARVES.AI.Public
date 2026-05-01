using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using System.Security.Cryptography;
using System.Text;

namespace Carves.Runtime.Application.Platform;

public sealed class WorktreeResourceCleanupService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeSessionRepository sessionRepository;
    private readonly IWorkerLeaseRepository workerLeaseRepository;
    private readonly IWorktreeRuntimeRepository worktreeRuntimeRepository;
    private readonly IGitClient gitClient;

    public WorktreeResourceCleanupService(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        TaskGraphService taskGraphService,
        IRuntimeSessionRepository sessionRepository,
        IWorkerLeaseRepository workerLeaseRepository,
        IWorktreeRuntimeRepository worktreeRuntimeRepository,
        IGitClient gitClient)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.systemConfig = systemConfig;
        this.taskGraphService = taskGraphService;
        this.sessionRepository = sessionRepository;
        this.workerLeaseRepository = workerLeaseRepository;
        this.worktreeRuntimeRepository = worktreeRuntimeRepository;
        this.gitClient = gitClient;
    }

    public ResourceCleanupReport Cleanup(string trigger, bool includeRuntimeResidue = true, bool includeEphemeralResidue = true)
    {
        var session = sessionRepository.Load();
        var activeTaskIds = new HashSet<string>(
            workerLeaseRepository.Load()
                .Where(item => item.Status == WorkerLeaseStatus.Active)
                .Select(item => item.TaskId),
            StringComparer.Ordinal);
        foreach (var taskId in taskGraphService.Load().ListTasks()
                     .Where(item => item.Status is Domain.Tasks.TaskStatus.Running or Domain.Tasks.TaskStatus.ApprovalWait or Domain.Tasks.TaskStatus.Review)
                     .Select(item => item.TaskId))
        {
            activeTaskIds.Add(taskId);
        }

        if (session is not null)
        {
            foreach (var taskId in session.ActiveTaskIds)
            {
                activeTaskIds.Add(taskId);
            }

            foreach (var taskId in session.ReviewPendingTaskIds)
            {
                activeTaskIds.Add(taskId);
            }
        }

        var snapshot = worktreeRuntimeRepository.Load();
        var pendingRebuildSources = new HashSet<string>(
            snapshot.PendingRebuilds
                .Select(item => item.SourceWorktreePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);
        var worktreeRoot = Path.GetFullPath(Path.Combine(repoRoot, systemConfig.WorktreeRoot));
        var keptRecords = new List<WorktreeRuntimeRecord>();
        var actions = new List<string>();
        var removedWorktreeCount = 0;
        var removedRecordCount = 0;
        var preservedActiveCount = 0;

        foreach (var record in snapshot.Records.OrderByDescending(item => item.CreatedAt))
        {
            var normalizedPath = string.IsNullOrWhiteSpace(record.WorktreePath) ? null : Path.GetFullPath(record.WorktreePath);
            if (activeTaskIds.Contains(record.TaskId))
            {
                keptRecords.Add(record);
                preservedActiveCount += 1;
                continue;
            }

            if (normalizedPath is not null && pendingRebuildSources.Contains(normalizedPath))
            {
                keptRecords.Add(record);
                continue;
            }

            if (normalizedPath is not null && Directory.Exists(normalizedPath))
            {
                TryDeleteWorktree(repoRoot, gitClient, normalizedPath);
                removedWorktreeCount += 1;
                actions.Add($"Removed stale worktree '{normalizedPath}' for task {record.TaskId}.");
            }

            removedRecordCount += 1;
        }

        if (Directory.Exists(worktreeRoot))
        {
            var referencedPaths = new HashSet<string>(
                keptRecords.Select(item => Path.GetFullPath(item.WorktreePath)),
                StringComparer.OrdinalIgnoreCase);
            foreach (var sourcePath in pendingRebuildSources)
            {
                referencedPaths.Add(sourcePath);
            }

            foreach (var directory in Directory.EnumerateDirectories(worktreeRoot))
            {
                if (string.Equals(Path.GetFileName(directory), "_quarantine", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(directory);
                if (referencedPaths.Contains(fullPath))
                {
                    continue;
                }

                TryDeleteWorktree(repoRoot, gitClient, fullPath);
                removedWorktreeCount += 1;
                actions.Add($"Removed orphaned worktree '{fullPath}'.");
            }
        }

        worktreeRuntimeRepository.Save(new WorktreeRuntimeSnapshot
        {
            Records = keptRecords
                .OrderByDescending(item => item.CreatedAt)
                .ToArray(),
            PendingRebuilds = snapshot.PendingRebuilds,
        });

        var removedRuntimeResidueCount = includeRuntimeResidue
            ? CleanupHostRuntimeResidue(actions)
            : 0;
        var removedEphemeralResidueCount = includeEphemeralResidue
            ? CleanupRepoEphemeralResidue(actions)
            : 0;

        return new ResourceCleanupReport
        {
            Trigger = trigger,
            ExecutedAt = DateTimeOffset.UtcNow,
            RemovedWorktreeCount = removedWorktreeCount,
            RemovedRecordCount = removedRecordCount,
            RemovedRuntimeResidueCount = removedRuntimeResidueCount,
            RemovedEphemeralResidueCount = removedEphemeralResidueCount,
            PreservedActiveWorktreeCount = preservedActiveCount,
            Actions = actions,
            Summary = BuildSummary(trigger, removedWorktreeCount, removedRecordCount, removedRuntimeResidueCount, removedEphemeralResidueCount, preservedActiveCount),
        };
    }

    private int CleanupHostRuntimeResidue(ICollection<string> actions)
    {
        var runtimeDirectory = ResolveHostRuntimeDirectory();
        if (!Directory.Exists(runtimeDirectory))
        {
            return 0;
        }

        var activeDeploymentDirectory = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var removed = 0;
        foreach (var directory in Directory.EnumerateDirectories(runtimeDirectory))
        {
            var fullPath = Path.GetFullPath(directory);
            if (string.Equals(fullPath, activeDeploymentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(Path.GetFileName(directory), "deployments", StringComparison.OrdinalIgnoreCase))
            {
                removed += CleanupInactiveDeploymentGenerations(fullPath, activeDeploymentDirectory, actions);
                continue;
            }

            if (string.Equals(Path.GetFileName(directory), "cold-commands", StringComparison.OrdinalIgnoreCase))
            {
                removed += CleanupColdCommandBuildGenerations(fullPath, actions);
                continue;
            }

            if (TryDeleteDirectory(fullPath))
            {
                removed += 1;
                actions.Add($"Removed stale host runtime residue '{fullPath}'.");
            }
        }

        foreach (var file in Directory.EnumerateFiles(runtimeDirectory, "*.tmp", SearchOption.AllDirectories))
        {
            if (TryDeleteFile(file))
            {
                removed += 1;
                actions.Add($"Removed stale temporary file '{file}'.");
            }
        }

        return removed;
    }

    private string ResolveHostRuntimeDirectory()
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(repoRoot)))
            .ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "carves-runtime-host", hash[..16]);
    }

    private static int CleanupInactiveDeploymentGenerations(string deploymentsDirectory, string activeDeploymentDirectory, ICollection<string> actions)
    {
        if (!Directory.Exists(deploymentsDirectory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var directory in Directory.EnumerateDirectories(deploymentsDirectory))
        {
            var fullPath = Path.GetFullPath(directory);
            if (string.Equals(fullPath, activeDeploymentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TryDeleteDirectory(fullPath))
            {
                removed += 1;
                actions.Add($"Removed inactive host deployment generation '{fullPath}'.");
            }
        }

        return removed;
    }

    private static int CleanupColdCommandBuildGenerations(string coldCommandsDirectory, ICollection<string> actions)
    {
        if (!Directory.Exists(coldCommandsDirectory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var directory in Directory.EnumerateDirectories(coldCommandsDirectory))
        {
            var fullPath = Path.GetFullPath(directory);
            if (TryDeleteDirectory(fullPath))
            {
                removed += 1;
                actions.Add($"Removed stale cold-command build generation '{fullPath}'.");
            }
        }

        return removed;
    }

    private int CleanupRepoEphemeralResidue(ICollection<string> actions)
    {
        var removed = 0;
        removed += CleanupUntrackedPlanningDraftResidue(actions);
        removed += CleanupEphemeralDirectory(Path.Combine(paths.AiRoot, "codegraph", "tmp"), actions);
        removed += CleanupEphemeralDirectory(Path.Combine(paths.RuntimeRoot, "tmp"), actions);
        removed += CleanupEphemeralDirectory(Path.Combine(paths.RuntimeRoot, "staging"), actions);
        removed += CleanupEphemeralDirectory(Path.Combine(paths.ArtifactsRoot, "tmp"), actions);
        removed += CleanupEphemeralTempFiles(paths.TasksRoot, actions);
        removed += CleanupEphemeralTempFiles(Path.Combine(paths.AiRoot, "codegraph"), actions);
        removed += CleanupEphemeralTempFiles(paths.RuntimeRoot, actions);
        removed += CleanupEphemeralTempFiles(paths.ArtifactsRoot, actions);
        removed += CleanupEphemeralTempFiles(paths.PlatformRuntimeStateRoot, actions);
        return removed;
    }

    private int CleanupUntrackedPlanningDraftResidue(ICollection<string> actions)
    {
        if (!gitClient.IsRepository(repoRoot))
        {
            return 0;
        }

        var removed = 0;
        foreach (var relativePath in gitClient.GetUntrackedPaths(repoRoot))
        {
            if (!IsPlanningDraftResidue(relativePath))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (File.Exists(fullPath))
            {
                TryDeleteFile(fullPath);
                removed += 1;
                actions.Add($"Removed untracked planning draft residue '{relativePath}'.");
                continue;
            }

            if (Directory.Exists(fullPath))
            {
                TryDeleteDirectory(fullPath);
                removed += 1;
                actions.Add($"Removed untracked planning draft residue directory '{relativePath}'.");
            }
        }

        return removed;
    }

    private static int CleanupEphemeralDirectory(string path, ICollection<string> actions)
    {
        if (!Directory.Exists(path))
        {
            return 0;
        }

        TryDeleteDirectory(path);
        actions.Add($"Removed ephemeral directory '{path}'.");
        return 1;
    }

    private static int CleanupEphemeralTempFiles(string root, ICollection<string> actions)
    {
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories))
        {
            TryDeleteFile(file);
            removed += 1;
            actions.Add($"Removed ephemeral file '{file}'.");
        }

        return removed;
    }

    private static string BuildSummary(string trigger, int removedWorktrees, int removedRecords, int removedRuntimeResidue, int removedEphemeralResidue, int preservedActive)
    {
        return $"{trigger}: removed {removedWorktrees} worktree(s), pruned {removedRecords} record(s), cleaned {removedRuntimeResidue} runtime residue item(s), pruned {removedEphemeralResidue} ephemeral item(s), preserved {preservedActive} active worktree(s).";
    }

    private static void TryDeleteWorktree(string repoRoot, IGitClient gitClient, string worktreePath)
    {
        try
        {
            if (gitClient.IsRepository(repoRoot))
            {
                gitClient.TryRemoveWorktree(repoRoot, worktreePath);
            }
            else
            {
                TryDeleteDirectory(worktreePath);
            }
        }
        catch
        {
            TryDeleteDirectory(worktreePath);
        }
    }

    private static bool TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }

            return !Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return !File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPlanningDraftResidue(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return normalized.StartsWith(".ai/runtime/planning/card-drafts/CARD-", StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(".ai/runtime/planning/taskgraph-drafts/TG-CARD-", StringComparison.OrdinalIgnoreCase);
    }
}
