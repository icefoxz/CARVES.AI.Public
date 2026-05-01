using Carves.Runtime.Domain.CodeGraph;

namespace Carves.Runtime.Application.CodeGraph;

public interface ICodeGraphQueryService
{
    CodeGraphManifest LoadManifest();

    IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries();

    CodeGraphIndex LoadIndex();

    CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries);

    CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries);
}
