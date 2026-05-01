namespace Carves.Runtime.Domain.Tasks;

public sealed class TaskGraph
{
    private static readonly IReadOnlyDictionary<string, int> PriorityWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["P0"] = 0,
        ["P1"] = 1,
        ["P2"] = 2,
        ["P3"] = 3,
    };

    public TaskGraph(IEnumerable<TaskNode>? tasks = null, IEnumerable<string>? cards = null, DateTimeOffset? updatedAt = null)
    {
        Tasks = tasks?.ToDictionary(task => task.TaskId, StringComparer.Ordinal) ?? new Dictionary<string, TaskNode>(StringComparer.Ordinal);
        Cards = cards?.Distinct(StringComparer.Ordinal).ToList() ?? new List<string>();
        UpdatedAt = updatedAt;
    }

    public IDictionary<string, TaskNode> Tasks { get; }

    public IList<string> Cards { get; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public void AddOrReplace(TaskNode task)
    {
        Tasks[task.TaskId] = task;
        if (!string.IsNullOrWhiteSpace(task.CardId) && !Cards.Contains(task.CardId, StringComparer.Ordinal))
        {
            Cards.Add(task.CardId);
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public IReadOnlyList<TaskNode> ListTasks()
    {
        return Tasks.Values
            .OrderBy(task => PriorityWeights.GetValueOrDefault(task.Priority, 99))
            .ThenBy(task => task.TaskId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<TaskNode> ByStatus(TaskStatus status)
    {
        return ListTasks().Where(task => task.Status == status).ToArray();
    }

    public IReadOnlySet<string> CompletedTaskIds()
    {
        return ListTasks()
            .Where(task => task.Status is TaskStatus.Completed or TaskStatus.Merged)
            .Select(task => task.TaskId)
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<TaskNode> ReadyTasks()
    {
        var completed = CompletedTaskIds();
        return ListTasks()
            .Where(task => task.IsReady(completed))
            .ToArray();
    }

    public TaskNode? SelectNextReadyTask()
    {
        return ReadyTasks().FirstOrDefault();
    }

    public void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
