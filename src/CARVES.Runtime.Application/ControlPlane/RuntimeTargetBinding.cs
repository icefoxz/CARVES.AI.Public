using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Application.ControlPlane;

public sealed record RuntimeTargetBinding(
    string RuntimeRoot,
    string TargetRoot,
    ControlPlanePaths TargetPaths,
    string WorktreeRoot,
    bool IsExternal)
{
    public static RuntimeTargetBinding Create(string runtimeRoot, string targetRoot, SystemConfig systemConfig)
    {
        var resolvedRuntimeRoot = Path.GetFullPath(runtimeRoot);
        var resolvedTargetRoot = Path.GetFullPath(targetRoot);
        if (!Directory.Exists(resolvedTargetRoot))
        {
            throw new InvalidOperationException($"Target repository '{resolvedTargetRoot}' does not exist.");
        }

        var targetPaths = ControlPlanePaths.FromRepoRoot(resolvedTargetRoot);
        if (!Directory.Exists(targetPaths.AiRoot))
        {
            throw new InvalidOperationException($"Target repository '{resolvedTargetRoot}' is missing the '.ai' control plane.");
        }

        EnsureUnderAiRoot(targetPaths.TasksRoot, targetPaths.AiRoot, "Tasks root");
        EnsureUnderAiRoot(targetPaths.ArtifactsRoot, targetPaths.AiRoot, "Artifacts root");
        EnsureUnderAiRoot(targetPaths.TaskGraphFile, targetPaths.AiRoot, "Task graph");

        var worktreeRoot = Path.GetFullPath(Path.Combine(resolvedTargetRoot, systemConfig.WorktreeRoot));
        if (IsSameOrDescendant(worktreeRoot, resolvedTargetRoot) && !IsSameOrDescendant(worktreeRoot, targetPaths.AiRoot))
        {
            throw new InvalidOperationException("Configured worktree root must stay outside the target repository or inside target .ai/.");
        }

        return new RuntimeTargetBinding(
            resolvedRuntimeRoot,
            resolvedTargetRoot,
            targetPaths,
            worktreeRoot,
            !string.Equals(resolvedRuntimeRoot, resolvedTargetRoot, StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureUnderAiRoot(string path, string aiRoot, string label)
    {
        if (!IsSameOrDescendant(path, aiRoot))
        {
            throw new InvalidOperationException($"{label} must remain under target .ai/.");
        }
    }

    private static bool IsSameOrDescendant(string candidate, string root)
    {
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
