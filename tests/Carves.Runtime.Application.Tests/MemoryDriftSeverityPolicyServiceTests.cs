using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Infrastructure.Memory;
using System.Text.Json;

namespace Carves.Runtime.Application.Tests;

public sealed class MemoryDriftSeverityPolicyServiceTests
{
    [Fact]
    public void DriftSeverityPolicy_FixesCriticalHighAndSuggestionBehaviors()
    {
        var service = new MemoryDriftSeverityPolicyService();

        Assert.Equal(OpportunitySeverity.Critical, service.ResolveOpportunitySeverity("canonical_fact_without_evidence"));
        Assert.True(service.GetEntry("canonical_fact_without_evidence").BlocksRelease);
        Assert.Equal(OpportunitySeverity.High, service.ResolveOpportunitySeverity("dependency_contradiction"));
        Assert.True(service.GetEntry("dependency_contradiction").BlocksHighRiskTask);
        Assert.Equal(OpportunitySeverity.Low, service.ResolveOpportunitySeverity("missing_module_memory"));
        Assert.True(service.GetEntry("missing_module_memory").SuggestionOnly);
    }

    [Fact]
    public void DriftSeverityDocument_MatchesRuntimePolicyTable()
    {
        var service = new MemoryDriftSeverityPolicyService();
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "MEMORY_DRIFT_SEVERITY_V1.json")));
        using var document = JsonDocument.Parse(json);

        var drifts = document.RootElement.GetProperty("drifts")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("drift_code").GetString()!, item => item, StringComparer.Ordinal);

        Assert.Equal("memory-drift-severity.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());
        Assert.Equal(service.GetEntries().Count, drifts.Count);

        foreach (var entry in service.GetEntries())
        {
            var item = drifts[entry.DriftCode];
            Assert.Equal(entry.Severity.ToString().ToLowerInvariant(), item.GetProperty("severity").GetString());
            Assert.Equal(entry.Behavior, item.GetProperty("behavior").GetString());
            Assert.Equal(entry.BlocksRelease, item.GetProperty("blocks_release").GetBoolean());
            Assert.Equal(entry.BlocksHighRiskTask, item.GetProperty("blocks_high_risk_task").GetBoolean());
            Assert.Equal(entry.SuggestionOnly, item.GetProperty("suggestion_only").GetBoolean());
        }
    }

    [Fact]
    public void MemoryDriftOpportunityDetector_UsesPolicyForMissingModuleMemorySeverity()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new MemoryAuditService(
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new SingleModuleCodeGraphQueryService("Workbench"));
        var detector = new MemoryDriftOpportunityDetector(service);

        var observation = Assert.Single(detector.Detect());

        Assert.Equal(OpportunitySeverity.Low, observation.Severity);
        Assert.Equal("suggestion", observation.Metadata["drift_behavior"]);
    }

    private sealed class SingleModuleCodeGraphQueryService : ICodeGraphQueryService
    {
        private readonly string moduleName;

        public SingleModuleCodeGraphQueryService(string moduleName)
        {
            this.moduleName = moduleName;
        }

        public CodeGraphManifest LoadManifest() => new();

        public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries() => Array.Empty<CodeGraphModuleEntry>();

        public CodeGraphIndex LoadIndex() => new()
        {
            Modules =
            [
                new CodeGraphModuleEntry("module-1", moduleName, $"src/{moduleName}/", "summary", Array.Empty<string>(), Array.Empty<string>()),
            ],
        };

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries) => CodeGraphScopeAnalysis.Empty;

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries) => CodeGraphImpactAnalysis.Empty;
    }
}
