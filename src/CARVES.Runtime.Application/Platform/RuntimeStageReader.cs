namespace Carves.Runtime.Application.Platform;

public static class RuntimeStageReader
{
    public static string? TryRead(string repoRoot)
    {
        var statePath = Path.Combine(repoRoot, ".ai", "STATE.md");
        if (!File.Exists(statePath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(statePath))
        {
            if (!line.StartsWith("Runtime stage:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return line["Runtime stage:".Length..].Trim();
        }

        return null;
    }
}
