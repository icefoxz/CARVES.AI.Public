using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Tasks;
using DomainCodeGraph = Carves.Runtime.Domain.CodeGraph.CodeGraph;

namespace Carves.Runtime.Application.Memory;

public sealed class MemoryService
{
    private readonly IMemoryRepository repository;
    private readonly ExecutionContextBuilder executionContextBuilder;

    public MemoryService(IMemoryRepository repository, ExecutionContextBuilder executionContextBuilder)
    {
        this.repository = repository;
        this.executionContextBuilder = executionContextBuilder;
    }

    public MemoryBundle BundleForTask(TaskNode task, DomainCodeGraph? codeGraph = null)
    {
        var moduleNames = task.Scope
            .Select(scope => scope.Replace("/", "_", StringComparison.Ordinal).Replace(".", "_", StringComparison.Ordinal).Trim('_'))
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new MemoryBundle
        {
            Architecture = repository.LoadCategory("architecture"),
            Modules = repository.LoadRelevantModules(moduleNames),
            Patterns = repository.LoadCategory("patterns"),
            Project = repository.LoadCategory("project"),
            ExecutionContext = executionContextBuilder.Build(task, codeGraph),
        };
    }

    public IReadOnlyList<MemoryDocument> LoadModuleMemoryDocuments()
    {
        return repository.LoadCategory("modules");
    }

    public IReadOnlyList<MemoryDocument> LoadProjectMemoryDocuments()
    {
        return repository.LoadCategory("project");
    }

    public IReadOnlyList<MemoryDocument> LoadCategory(string category)
    {
        return repository.LoadCategory(category);
    }
}
