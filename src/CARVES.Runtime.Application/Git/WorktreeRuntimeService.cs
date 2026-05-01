using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Git;

public sealed class WorktreeRuntimeService
{
    private readonly string repoRoot;
    private readonly IGitClient gitClient;
    private readonly IWorktreeRuntimeRepository repository;

    public WorktreeRuntimeService(string repoRoot, IGitClient gitClient, IWorktreeRuntimeRepository repository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.gitClient = gitClient;
        this.repository = repository;
    }

    public WorktreeRuntimeRecord RecordPrepared(string taskId, string worktreePath, string baseCommit, string? workerRunId = null)
    {
        var snapshot = repository.Load();
        var pending = snapshot.PendingRebuilds.FirstOrDefault(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal));
        var records = snapshot.Records.ToList();
        var updated = new WorktreeRuntimeRecord
        {
            TaskId = taskId,
            WorktreePath = worktreePath,
            RepoRoot = repoRoot,
            BaseCommit = baseCommit,
            State = pending is null ? WorktreeRuntimeState.Active : WorktreeRuntimeState.Rebuilt,
            RebuiltFromWorktreePath = pending?.SourceWorktreePath,
            WorkerRunId = workerRunId,
        };
        records.Add(updated);

        repository.Save(new WorktreeRuntimeSnapshot
        {
            Records = records.OrderByDescending(item => item.CreatedAt).ToArray(),
            PendingRebuilds = snapshot.PendingRebuilds
                .Where(item => !string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
                .ToArray(),
        });

        return updated;
    }

    public WorktreeRuntimeRecord? QuarantineAndRequestRebuild(string taskId, string worktreePath, string reason)
    {
        if (string.IsNullOrWhiteSpace(worktreePath) || !Directory.Exists(worktreePath))
        {
            return null;
        }

        var snapshot = repository.Load();
        var records = snapshot.Records.ToList();
        var record = records
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault(item =>
                string.Equals(item.TaskId, taskId, StringComparison.Ordinal)
                && string.Equals(item.WorktreePath, worktreePath, StringComparison.OrdinalIgnoreCase));

        var quarantineRoot = Path.Combine(Path.GetDirectoryName(worktreePath) ?? worktreePath, "_quarantine");
        Directory.CreateDirectory(quarantineRoot);
        var quarantinePath = Path.Combine(
            quarantineRoot,
            $"{Path.GetFileName(worktreePath)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

        CopyDirectory(worktreePath, quarantinePath);
        if (gitClient.IsRepository(repoRoot))
        {
            gitClient.TryRemoveWorktree(repoRoot, worktreePath);
        }
        else if (Directory.Exists(worktreePath))
        {
            Directory.Delete(worktreePath, true);
        }

        if (record is null)
        {
            record = new WorktreeRuntimeRecord
            {
                TaskId = taskId,
                WorktreePath = quarantinePath,
                RepoRoot = repoRoot,
                BaseCommit = string.Empty,
                State = WorktreeRuntimeState.Quarantined,
                QuarantineReason = reason,
            };
            records.Add(record);
        }
        else
        {
            record.State = WorktreeRuntimeState.Quarantined;
            record.QuarantineReason = reason;
            record.Touch();
            record = new WorktreeRuntimeRecord
            {
                RecordId = record.RecordId,
                TaskId = record.TaskId,
                WorktreePath = quarantinePath,
                RepoRoot = record.RepoRoot,
                BaseCommit = record.BaseCommit,
                State = WorktreeRuntimeState.Quarantined,
                QuarantineReason = reason,
                RebuiltFromWorktreePath = record.RebuiltFromWorktreePath,
                WorkerRunId = record.WorkerRunId,
                CreatedAt = record.CreatedAt,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            records.RemoveAll(item => string.Equals(item.RecordId, record.RecordId, StringComparison.Ordinal));
            records.Add(record);
        }

        var rebuilds = snapshot.PendingRebuilds
            .Where(item => !string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
            .Append(new WorktreeRebuildRequest
            {
                TaskId = taskId,
                SourceWorktreePath = quarantinePath,
                Reason = reason,
            })
            .ToArray();

        repository.Save(new WorktreeRuntimeSnapshot
        {
            Records = records.OrderByDescending(item => item.CreatedAt).ToArray(),
            PendingRebuilds = rebuilds,
        });

        return record;
    }

    public IReadOnlyList<WorktreeRuntimeRecord> Load(string? taskId = null)
    {
        return repository.Load().Records
            .Where(record => string.IsNullOrWhiteSpace(taskId) || string.Equals(record.TaskId, taskId, StringComparison.Ordinal))
            .OrderByDescending(record => record.CreatedAt)
            .ToArray();
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, file);
            var targetPath = Path.Combine(destinationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.Copy(file, targetPath, overwrite: true);
        }
    }
}
