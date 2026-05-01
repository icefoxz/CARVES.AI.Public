using DomainCodeGraph = Carves.Runtime.Domain.CodeGraph.CodeGraph;
using DomainCodeGraphIndex = Carves.Runtime.Domain.CodeGraph.CodeGraphIndex;

namespace Carves.Runtime.Application.CodeGraph;

public sealed record CodeGraphBuildResult(DomainCodeGraph Graph, DomainCodeGraphIndex Index, string OutputPath, string IndexPath);
