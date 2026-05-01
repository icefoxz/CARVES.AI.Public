using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Infrastructure.Persistence;

internal static class JsonTaskGraphDocumentMapper
{
    public static TaskGraphDocument CreateEmptyGraphDocument()
    {
        return new TaskGraphDocument
        {
            Version = RuntimeProtocol.GraphSchemaVersion,
            UpdatedAt = null,
            Tasks = [],
            Cards = [],
        };
    }

    public static TaskGraph ToTaskGraph(IReadOnlyList<TaskNode> tasks, TaskGraphDocument document)
    {
        DateTimeOffset? updatedAt = null;
        if (!string.IsNullOrWhiteSpace(document.UpdatedAt) && DateTimeOffset.TryParse(document.UpdatedAt, out var parsed))
        {
            updatedAt = parsed;
        }

        return new TaskGraph(tasks, document.Cards, updatedAt);
    }

    public static TaskGraphDocument ToGraphDocument(TaskGraph graph, IReadOnlyList<TaskSummaryDocument> summaries, DateTimeOffset updatedAt)
    {
        return new TaskGraphDocument
        {
            Version = RuntimeProtocol.GraphSchemaVersion,
            UpdatedAt = updatedAt.ToString("O"),
            Tasks = summaries.ToList(),
            Cards = graph.Cards.Distinct(StringComparer.Ordinal).ToList(),
        };
    }

    public static string GetNodeRelativePath(string taskId)
    {
        return $"nodes/{taskId}.json";
    }

    public static string? ResolveNodePath(ControlPlanePaths paths, string? nodeFile)
    {
        if (string.IsNullOrWhiteSpace(nodeFile))
        {
            return null;
        }

        return Path.Combine(paths.TasksRoot, nodeFile.Replace('/', Path.DirectorySeparatorChar));
    }

    public static TaskSummaryDocument ToTaskSummaryDocument(TaskNode task, string nodeFile)
    {
        return new TaskSummaryDocument
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Status = ToSnakeCase(task.Status.ToString()),
            Priority = task.Priority,
            CardId = task.CardId,
            Dependencies = task.Dependencies.ToArray(),
            NodeFile = nodeFile,
        };
    }

    public static TaskNodeDocument ToTaskNodeDocument(TaskNode task)
        => JsonTaskNodeDocumentMapper.ToDocument(task);

    public static TaskNode ToTaskNode(TaskNodeDocument document)
        => JsonTaskNodeDocumentMapper.ToTaskNode(document);

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var chars = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string(chars.ToArray());
    }
}
