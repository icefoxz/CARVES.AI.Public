namespace Carves.Runtime.Application.Interaction;

public sealed class PromptKernelService
{
    private readonly string kernelPath;

    public PromptKernelService(string repoRoot)
    {
        kernelPath = ResolveKernelPath(repoRoot);
    }

    public PromptKernelDefinition GetKernel()
    {
        var body = File.ReadAllText(kernelPath);
        var summary = File.ReadLines(kernelPath)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
            ?.Trim()
            ?? "Base CARVES interaction kernel.";
        return new PromptKernelDefinition(
            "carves-prompt-kernel",
            "1.0",
            kernelPath,
            ["attached-ai", "planner", "worker", "agent"],
            summary,
            body);
    }

    private static string ResolveKernelPath(string repoRoot)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "templates", "interaction", "CARVES_PROMPT_KERNEL.md"),
            Path.Combine(repoRoot, "templates", "interaction", "CARVES_PROMPT_KERNEL.md"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "templates", "interaction", "CARVES_PROMPT_KERNEL.md")),
        };

        var path = candidates.FirstOrDefault(File.Exists);
        return path ?? throw new InvalidOperationException("CARVES prompt kernel asset was not found.");
    }
}
