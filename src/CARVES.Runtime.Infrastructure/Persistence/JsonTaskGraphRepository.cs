using System.Text.Json;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonTaskGraphRepository : ITaskGraphRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;
    private readonly AuthoritativeTruthStoreService authoritativeTruthStoreService;

    public JsonTaskGraphRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null, AuthoritativeTruthStoreService? authoritativeTruthStoreService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
        this.authoritativeTruthStoreService = authoritativeTruthStoreService ?? new AuthoritativeTruthStoreService(paths, this.lockService);
    }

    public TaskGraph Load()
    {
        EnsureInitialized();

        var graphDocument = Deserialize<TaskGraphDocument>(authoritativeTruthStoreService.TaskGraphFile, paths.TaskGraphFile)
                            ?? JsonTaskGraphDocumentMapper.CreateEmptyGraphDocument();
        ValidateGraphSchema(graphDocument.Version);
        var tasks = LoadTaskNodes(graphDocument.Tasks);
        return JsonTaskGraphDocumentMapper.ToTaskGraph(tasks, graphDocument);
    }

    public void Save(TaskGraph graph)
    {
        WithWriteLock(() =>
        {
            authoritativeTruthStoreService.WithWriterLease(authoritativeTruthStoreService.TaskGraphFile, "task-graph-save", () =>
            {
                EnsureInitialized(writerLockHeld: true);
                var summaries = PersistTaskNodes(graph.ListTasks(), writerLockHeld: true);
                var document = JsonTaskGraphDocumentMapper.ToGraphDocument(graph, summaries, DateTimeOffset.UtcNow);
                var graphPayload = JsonSerializer.Serialize(document, JsonOptions);
                authoritativeTruthStoreService.WriteAuthoritativeThenMirror(
                    authoritativeTruthStoreService.TaskGraphFile,
                    paths.TaskGraphFile,
                    graphPayload,
                    writerLockHeld: true);
                return 0;
            });
            return 0;
        });
    }

    public void Upsert(TaskNode task)
    {
        UpsertRange([task]);
    }

    public void UpsertRange(IEnumerable<TaskNode> tasks)
    {
        var changedTasks = tasks.ToArray();
        if (changedTasks.Length == 0)
        {
            return;
        }

        WithWriteLock(() =>
        {
            authoritativeTruthStoreService.WithWriterLease(authoritativeTruthStoreService.TaskGraphFile, "task-graph-upsert", () =>
            {
                EnsureInitialized(writerLockHeld: true);
                var graph = Load();
                foreach (var task in changedTasks)
                {
                    graph.AddOrReplace(task);
                }

                PersistTaskNodes(changedTasks, writerLockHeld: true);
                var summaries = BuildTaskSummaries(graph.ListTasks());
                var document = JsonTaskGraphDocumentMapper.ToGraphDocument(graph, summaries, DateTimeOffset.UtcNow);
                var graphPayload = JsonSerializer.Serialize(document, JsonOptions);
                authoritativeTruthStoreService.WriteAuthoritativeThenMirror(
                    authoritativeTruthStoreService.TaskGraphFile,
                    paths.TaskGraphFile,
                    graphPayload,
                    writerLockHeld: true);
                return 0;
            });
            return 0;
        });
    }

    public T WithWriteLock<T>(Func<T> action)
    {
        using var _ = lockService.Acquire("task-graph");
        return action();
    }

    private IReadOnlyList<TaskNode> LoadTaskNodes(IEnumerable<TaskSummaryDocument> summaries)
    {
        var tasks = new List<TaskNode>();

        foreach (var summary in summaries)
        {
            var nodePath = JsonTaskGraphDocumentMapper.ResolveNodePath(paths, summary.NodeFile);
            if (nodePath is null)
            {
                continue;
            }

            var nodeDocument = Deserialize<TaskNodeDocument>(
                authoritativeTruthStoreService.GetTaskNodePath(Path.GetFileNameWithoutExtension(nodePath)),
                nodePath);
            if (nodeDocument is null)
            {
                continue;
            }

            tasks.Add(JsonTaskGraphDocumentMapper.ToTaskNode(nodeDocument));
        }

        return tasks;
    }

    private IReadOnlyList<TaskSummaryDocument> PersistTaskNodes(IEnumerable<TaskNode> tasks, bool writerLockHeld = false)
    {
        var summaries = new List<TaskSummaryDocument>();

        foreach (var task in tasks)
        {
            var nodeFile = JsonTaskGraphDocumentMapper.GetNodeRelativePath(task.TaskId);
            var nodePath = Path.Combine(paths.TaskNodesRoot, $"{task.TaskId}.json");
            var nodePayload = JsonSerializer.Serialize(JsonTaskGraphDocumentMapper.ToTaskNodeDocument(task), JsonOptions);
            authoritativeTruthStoreService.WriteAuthoritativeThenMirror(
                authoritativeTruthStoreService.GetTaskNodePath(task.TaskId),
                nodePath,
                nodePayload,
                writerLockHeld);
            summaries.Add(JsonTaskGraphDocumentMapper.ToTaskSummaryDocument(task, nodeFile));
        }

        return summaries;
    }

    private static IReadOnlyList<TaskSummaryDocument> BuildTaskSummaries(IEnumerable<TaskNode> tasks)
    {
        return tasks
            .Select(task => JsonTaskGraphDocumentMapper.ToTaskSummaryDocument(
                task,
                JsonTaskGraphDocumentMapper.GetNodeRelativePath(task.TaskId)))
            .ToArray();
    }

    private void EnsureInitialized(bool writerLockHeld = false)
    {
        Directory.CreateDirectory(paths.TasksRoot);
        Directory.CreateDirectory(paths.TaskNodesRoot);
        authoritativeTruthStoreService.EnsureInitialized();

        if (!File.Exists(paths.TaskGraphFile) && !File.Exists(authoritativeTruthStoreService.TaskGraphFile))
        {
            var payload = JsonSerializer.Serialize(JsonTaskGraphDocumentMapper.CreateEmptyGraphDocument(), JsonOptions);
            authoritativeTruthStoreService.WriteAuthoritativeThenMirror(
                authoritativeTruthStoreService.TaskGraphFile,
                paths.TaskGraphFile,
                payload,
                writerLockHeld);
        }
    }

    private static void ValidateGraphSchema(int version)
    {
        if (version != 0 && version != RuntimeProtocol.GraphSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported task graph schema version '{version}'.");
        }
    }

    private static T? Deserialize<T>(string authoritativePath, string mirrorPath)
    {
        var path = ResolveTaskTruthReadPath(authoritativePath, mirrorPath);
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(SharedFileAccess.ReadAllText(path), JsonOptions);
    }

    private static string ResolveTaskTruthReadPath(string authoritativePath, string mirrorPath)
    {
        var authoritativeExists = File.Exists(authoritativePath);
        var mirrorExists = File.Exists(mirrorPath);
        if (!authoritativeExists)
        {
            return mirrorPath;
        }

        if (!mirrorExists)
        {
            return authoritativePath;
        }

        return File.GetLastWriteTimeUtc(mirrorPath) > File.GetLastWriteTimeUtc(authoritativePath)
            && !HaveSameContent(authoritativePath, mirrorPath)
            ? mirrorPath
            : authoritativePath;
    }

    private static bool HaveSameContent(string leftPath, string rightPath)
    {
        var leftInfo = new FileInfo(leftPath);
        var rightInfo = new FileInfo(rightPath);
        if (leftInfo.Length != rightInfo.Length)
        {
            return false;
        }

        return string.Equals(
            SharedFileAccess.ReadAllText(leftPath),
            SharedFileAccess.ReadAllText(rightPath),
            StringComparison.Ordinal);
    }
}
