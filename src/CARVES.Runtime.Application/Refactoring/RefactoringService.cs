using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Refactoring;

public sealed partial class RefactoringService : IRefactoringService
{
    private readonly string repoRoot;
    private readonly SystemConfig systemConfig;
    private readonly IGitClient gitClient;
    private readonly TaskGraphService taskGraphService;
    private readonly IRefactoringBacklogRepository backlogRepository;

    public RefactoringService(
        string repoRoot,
        SystemConfig systemConfig,
        IGitClient gitClient,
        TaskGraphService taskGraphService,
        IRefactoringBacklogRepository backlogRepository)
    {
        this.repoRoot = repoRoot;
        this.systemConfig = systemConfig;
        this.gitClient = gitClient;
        this.taskGraphService = taskGraphService;
        this.backlogRepository = backlogRepository;
    }

    public RefactoringBacklogSnapshot DetectAndStore()
    {
        var now = DateTimeOffset.UtcNow;
        var findings = DetectFindings();
        var existing = backlogRepository.Load();
        var items = new Dictionary<string, RefactoringBacklogItem>(StringComparer.Ordinal);
        foreach (var existingItem in existing.Items)
        {
            var fingerprint = string.IsNullOrWhiteSpace(existingItem.Fingerprint)
                ? $"{existingItem.Kind}|{existingItem.Path}"
                : existingItem.Fingerprint;
            if (string.IsNullOrWhiteSpace(fingerprint) || items.ContainsKey(fingerprint))
            {
                continue;
            }

            items[fingerprint] = NormalizeExistingItem(existingItem, fingerprint, now);
        }
        var seenFingerprints = new HashSet<string>(StringComparer.Ordinal);

        foreach (var finding in findings)
        {
            var fingerprint = BuildFingerprint(finding);
            seenFingerprints.Add(fingerprint);

            if (!items.TryGetValue(fingerprint, out var item))
            {
                item = new RefactoringBacklogItem
                {
                    ItemId = BuildStableId("RB", fingerprint, finding.Path),
                    Fingerprint = fingerprint,
                    FirstDetectedAt = now,
                };
                items[fingerprint] = item;
            }

            item.MarkObserved(finding, MapPriority(finding), now);
        }

        foreach (var item in items.Values.Where(item => !seenFingerprints.Contains(item.Fingerprint) && item.Status != RefactoringBacklogStatus.Suppressed))
        {
            item.MarkResolved(now);
        }

        var snapshot = new RefactoringBacklogSnapshot
        {
            Version = 1,
            GeneratedAt = now,
            Items = items.Values
                .OrderBy(item => PriorityWeight(item.Priority))
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.ItemId, StringComparer.Ordinal)
                .ToArray(),
        };

        backlogRepository.Save(snapshot);
        return snapshot;
    }

    public RefactoringBacklogSnapshot LoadBacklog()
    {
        return backlogRepository.Load();
    }

    public RefactoringTaskMaterializationResult MaterializeSuggestedTasks()
    {
        var snapshot = backlogRepository.Load();
        var now = DateTimeOffset.UtcNow;
        var graph = taskGraphService.Load();
        var queueSnapshot = BuildQueueSnapshot(snapshot, graph);
        PersistQueueSnapshot(queueSnapshot);
        var selectedItemIds = queueSnapshot.Queues
            .SelectMany(queue => queue.BacklogItemIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (HasHigherPriorityActiveWork(graph))
        {
            return new RefactoringTaskMaterializationResult(
                selectedItemIds,
                Array.Empty<string>(),
                true,
                queueSnapshot.Queues.Select(queue => queue.QueueId).ToArray(),
                queueSnapshot.Queues.Select(queue => $".ai/refactoring/queues/{queue.QueueId}.json").ToArray());
        }

        var baseCommit = gitClient.TryGetCurrentCommit(repoRoot);
        var suggestedTasks = new List<TaskNode>();
        var knownTaskIds = graph.Tasks.Keys.ToHashSet(StringComparer.Ordinal);
        var selectedItems = snapshot.Items
            .Where(item => selectedItemIds.Contains(item.ItemId))
            .ToDictionary(item => item.ItemId, StringComparer.Ordinal);

        foreach (var queue in queueSnapshot.Queues)
        {
            var taskId = queue.SuggestedTaskId ?? BuildStableId("T-REFQ", queue.QueueId, queue.QueueId);
            if (!knownTaskIds.Contains(taskId))
            {
                suggestedTasks.Add(BuildSuggestedTask(queue, baseCommit));
                knownTaskIds.Add(taskId);
            }

            foreach (var itemId in queue.BacklogItemIds)
            {
                if (selectedItems.TryGetValue(itemId, out var item))
                {
                    item.MarkSuggested(taskId, now);
                }
            }
        }

        if (suggestedTasks.Count > 0)
        {
            taskGraphService.AddTasks(suggestedTasks);
        }

        backlogRepository.Save(new RefactoringBacklogSnapshot
        {
            Version = snapshot.Version,
            GeneratedAt = now,
            Items = snapshot.Items,
        });

        return new RefactoringTaskMaterializationResult(
            Array.Empty<string>(),
            suggestedTasks.Select(task => task.TaskId).ToArray(),
            false,
            queueSnapshot.Queues.Select(queue => queue.QueueId).ToArray(),
            queueSnapshot.Queues.Select(queue => $".ai/refactoring/queues/{queue.QueueId}.json").ToArray());
    }
}
