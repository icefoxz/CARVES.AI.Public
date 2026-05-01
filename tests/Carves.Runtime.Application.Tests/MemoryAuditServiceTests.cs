using System.Text.Json;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.CodeGraph;
using Carves.Runtime.Infrastructure.Memory;

namespace Carves.Runtime.Application.Tests;

public sealed class MemoryAuditServiceTests
{
    [Fact]
    public void ClassifyLegacyMarkdown_IsReadOnlyAndDoesNotWriteFacts()
    {
        using var workspace = new TemporaryWorkspace();
        var markdownPath = workspace.WriteFile(".ai/memory/project/runtime_context.md", """
# Runtime Context

Worker cannot directly write canonical memory.
""");
        var beforeHash = ComputeSha256(File.ReadAllText(markdownPath));
        var service = CreateService(workspace);

        var report = Assert.Single(service.ClassifyLegacyMarkdown(["project"]));

        Assert.Equal(".ai/memory/project/runtime_context.md", report.SourceFile);
        Assert.False(report.MutationPerformed);
        Assert.Equal(beforeHash, report.SourceHash);
        Assert.Equal(beforeHash, ComputeSha256(File.ReadAllText(markdownPath)));
        Assert.False(Directory.Exists(workspace.Paths.EvidenceFactsRoot));
    }

    [Fact]
    public void ClassifyLegacyMarkdown_ClaimsWithoutEvidenceBecomeCandidateOrOrphanNeverCanonical()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/memory/project/runtime_context.md", """
# Runtime Context

Worker cannot directly write canonical memory.

Runtime state remains review driven unless an approval route completes.
""");
        var service = CreateService(workspace);

        var report = Assert.Single(service.ClassifyLegacyMarkdown(["project"]));
        var nonExplanatoryClaims = report.Claims
            .Where(claim => claim.Classification != LegacyMarkdownClaimClassification.ExplanatoryText)
            .ToArray();

        Assert.NotEmpty(nonExplanatoryClaims);
        Assert.DoesNotContain(nonExplanatoryClaims, claim => claim.Classification == LegacyMarkdownClaimClassification.CanonicalCandidate);
        Assert.All(nonExplanatoryClaims, claim =>
            Assert.Contains(
                claim.Classification,
                new[]
                {
                    LegacyMarkdownClaimClassification.Candidate,
                    LegacyMarkdownClaimClassification.OrphanClaim,
                    LegacyMarkdownClaimClassification.ProvisionalCandidate
                }));
    }

    [Fact]
    public void BackfillDecisionTable_IsFixedAndStructureClaimsRequireCodeGraphCheck()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/memory/project/runtime_context.md", """
# Runtime Context

src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs must preserve task truth transition behavior.

This note conflicts with an older patch plan and should be reviewed before reuse.

Runtime state remains repo JSON truth across the control plane.
""");
        var service = CreateService(workspace);
        var decisionTable = service.GetBackfillDecisionTable();
        var report = Assert.Single(service.ClassifyLegacyMarkdown(["project"]));
        var structureClaim = Assert.Single(report.Claims.Where(claim => claim.Text.Contains("ResultIngestionService.cs", StringComparison.Ordinal)));
        var orphanReport = service.BuildOrphanClaimReport(["project"]);

        Assert.Equal(7, decisionTable.Count);
        Assert.Equal("legacy_readability_only", decisionTable[LegacyMarkdownClaimClassification.OrphanClaim].NextStep);
        Assert.Equal("none", decisionTable[LegacyMarkdownClaimClassification.OrphanClaim].AllowedAuthorityCeiling);
        Assert.True(structureClaim.RequiresCodeGraphCheck);
        Assert.Equal(LegacyMarkdownClaimClassification.ProvisionalCandidate, structureClaim.Classification);
        Assert.Equal("provisional_after_review", structureClaim.AllowedAuthorityCeiling);
        Assert.Contains(report.Claims, claim => claim.Classification == LegacyMarkdownClaimClassification.ConflictCandidate);
        Assert.NotEmpty(orphanReport.Claims);
    }

    [Fact]
    public void DecisionTableDocument_MatchesRuntimeDecisionCount()
    {
        using var workspace = new TemporaryWorkspace();
        var service = CreateService(workspace);
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "MARKDOWN_BACKFILL_DECISION_TABLE_V1.json")));
        using var document = JsonDocument.Parse(json);

        var classifications = document.RootElement.GetProperty("classifications");

        Assert.Equal(service.GetBackfillDecisionTable().Count, classifications.GetArrayLength());
        Assert.True(document.RootElement.GetProperty("read_only").GetBoolean());
        Assert.False(document.RootElement.GetProperty("mutates_markdown").GetBoolean());
        Assert.False(document.RootElement.GetProperty("mutates_facts").GetBoolean());
    }

    private static MemoryAuditService CreateService(TemporaryWorkspace workspace)
    {
        return new MemoryAuditService(
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new StaticCodeGraphQueryService());
    }

    private static string ComputeSha256(string value)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value.Replace("\r\n", "\n", StringComparison.Ordinal)));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class StaticCodeGraphQueryService : ICodeGraphQueryService
    {
        public CodeGraphManifest LoadManifest() => new();

        public IReadOnlyList<CodeGraphModuleEntry> LoadModuleSummaries() => Array.Empty<CodeGraphModuleEntry>();

        public CodeGraphIndex LoadIndex() => new();

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries) => CodeGraphScopeAnalysis.Empty;

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries) => CodeGraphImpactAnalysis.Empty;
    }
}
