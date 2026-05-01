using Carves.Runtime.Host;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static int RunSearch(string repoRoot, IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2)
        {
            Console.Error.WriteLine("Usage: carves search <code|task|memory|evidence> <query>");
            return 2;
        }

        var query = string.Join(' ', arguments.Skip(1)).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            Console.Error.WriteLine("Usage: carves search <code|task|memory|evidence> <query>");
            return 2;
        }

        var services = RuntimeComposition.Create(repoRoot);
        return arguments[0].ToLowerInvariant() switch
        {
            "code" => SearchCode(services.CodeGraphQueryService.LoadIndex(), query),
            "task" => SearchTasks(services.TaskGraphService.Load(), query),
            "memory" => Delegate(repoRoot, "memory", ["search", query], TransportPreference.Cold),
            "evidence" => Delegate(repoRoot, "evidence", ["search", query], TransportPreference.Cold),
            _ => Fail("Usage: carves search <code|task|memory|evidence> <query>", 2),
        };
    }

    private static int SearchCode(Carves.Runtime.Domain.CodeGraph.CodeGraphIndex index, string query)
    {
        var modules = index.Modules
            .Where(module => module.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(module => module.Name)
            .ToArray();
        var files = index.Files
            .Where(file => file.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(file => file.Path)
            .ToArray();
        var symbols = index.Callables
            .Where(callable => callable.QualifiedName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(callable => callable.QualifiedName)
            .ToArray();
        var types = index.Types
            .Where(type => type.QualifiedName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(type => type.QualifiedName)
            .ToArray();

        if (modules.Length == 0 && files.Length == 0 && symbols.Length == 0 && types.Length == 0)
        {
            Console.WriteLine($"No code matches for '{query}'.");
            return 1;
        }

        Console.WriteLine($"Code search: {query}");
        WriteSection("Modules", modules);
        WriteSection("Files", files);
        WriteSection("Types", types);
        WriteSection("Symbols", symbols);
        return 0;
    }

    private static int SearchTasks(Carves.Runtime.Domain.Tasks.TaskGraph graph, string query)
    {
        var matches = graph.ListTasks()
            .Where(task =>
                task.TaskId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || task.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(task.CardId) && task.CardId.Contains(query, StringComparison.OrdinalIgnoreCase))
                || task.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(task => $"{task.TaskId} [{task.Status}] {task.Title}")
            .ToArray();

        if (matches.Length == 0)
        {
            Console.WriteLine($"No task matches for '{query}'.");
            return 1;
        }

        Console.WriteLine($"Task search: {query}");
        WriteSection("Tasks", matches);
        return 0;
    }

    private static void WriteSection(string heading, IReadOnlyList<string> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        Console.WriteLine($"{heading}:");
        foreach (var item in items)
        {
            Console.WriteLine($"  - {item}");
        }
    }
}
