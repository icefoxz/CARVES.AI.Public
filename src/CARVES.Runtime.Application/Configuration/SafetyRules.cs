namespace Carves.Runtime.Application.Configuration;

public sealed record SafetyRules(
    int MaxFilesChanged,
    int MaxLinesChanged,
    int ReviewFilesChangedThreshold,
    int ReviewLinesChangedThreshold,
    int MaxRetryCount,
    IReadOnlyList<string> ProtectedPaths,
    IReadOnlyList<string> RestrictedPaths,
    IReadOnlyList<string> WorkerWritablePaths,
    IReadOnlyList<string> ManagedControlPlanePaths,
    IReadOnlyList<string> MemoryWritePaths,
    string MemoryWriteCapability,
    bool RequireTestsForSourceChanges,
    bool ReviewRequiredForNewModule)
{
    public static SafetyRules CreateDefault()
    {
        return new SafetyRules(
            20,
            500,
            8,
            200,
            2,
            new[] { ".git/" },
            new[] { ".ai/" },
            new[] { "src/", "tests/", "docs/" },
            new[] { ".ai/tasks/", ".ai/refactoring/", ".ai/codegraph/", ".ai/config/", ".ai/CURRENT_TASK.md", ".ai/STATE.md", ".ai/TASK_QUEUE.md" },
            new[] { ".ai/memory/" },
            "memory_write",
            true,
            true);
    }
}
