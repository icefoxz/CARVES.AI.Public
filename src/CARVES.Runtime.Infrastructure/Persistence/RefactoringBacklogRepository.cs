using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Refactoring;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class RefactoringBacklogRepository : IRefactoringBacklogRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string backlogPath;

    public RefactoringBacklogRepository(ControlPlanePaths paths)
    {
        backlogPath = Path.Combine(paths.AiRoot, "refactoring", "backlog.json");
    }

    public RefactoringBacklogSnapshot Load()
    {
        if (!File.Exists(backlogPath))
        {
            return new RefactoringBacklogSnapshot();
        }

        var snapshot = JsonSerializer.Deserialize<RefactoringBacklogSnapshot>(File.ReadAllText(backlogPath), JsonOptions);
        return snapshot ?? new RefactoringBacklogSnapshot();
    }

    public void Save(RefactoringBacklogSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(backlogPath)!);
        File.WriteAllText(backlogPath, JsonSerializer.Serialize(snapshot, JsonOptions));
    }
}
