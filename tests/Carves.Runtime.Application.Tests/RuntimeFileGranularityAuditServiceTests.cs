using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeFileGranularityAuditServiceTests
{
    [Fact]
    public void Build_ProjectsFileGranularityPressureWithoutMutatingRepo()
    {
        using var workspace = new TemporaryWorkspace();
        WriteTinyCluster(workspace);
        WritePartialCluster(workspace);
        workspace.WriteFile("src/Sample.App/Feature/LargeCoordinator.cs", GenerateClass("LargeCoordinator", 405));
        workspace.WriteFile("tests/Sample.App.Tests/HugeContractTests.cs", GenerateClass("HugeContractTests", 810));
        workspace.WriteFile("src/Sample.App/obj/Debug/net10.0/Ignored.g.cs", GenerateClass("IgnoredGenerated", 900));

        var surface = new RuntimeFileGranularityAuditService(workspace.RootPath).Build();

        Assert.Equal("runtime-file-granularity-audit", surface.SurfaceId);
        Assert.True(surface.IsValid);
        Assert.Equal("active_maintenance_audit", surface.LifecycleClass);
        Assert.Equal("conditional_maintenance_audit", surface.ReadPathClass);
        Assert.Equal("not_default", surface.DefaultPathParticipation);
        Assert.Equal("file_granularity_pressure_observed", surface.OverallPosture);
        Assert.Equal(10, surface.Counts.TotalFileCount);
        Assert.Equal(9, surface.Counts.SourceFileCount);
        Assert.Equal(1, surface.Counts.TestFileCount);
        Assert.Equal(8, surface.Counts.TinyFileCount);
        Assert.Equal(1, surface.Counts.LargeFileCount);
        Assert.Equal(1, surface.Counts.HugeFileCount);
        Assert.Equal(1, surface.Counts.TinyClusterCount);
        Assert.Equal(1, surface.Counts.PartialFamilyClusterCount);
        Assert.Contains(surface.Findings, finding => finding.StartsWith("tiny_file_pressure:", StringComparison.Ordinal));
        Assert.Contains(surface.Findings, finding => finding.StartsWith("huge_file_pressure:", StringComparison.Ordinal));
        Assert.Contains(surface.TinyClusters, cluster =>
            cluster.DirectoryPath == "src/Sample.App/Tiny"
            && cluster.TinyFileCount == 5
            && cluster.RecommendedAction.Contains("Review as a cohesive local family", StringComparison.Ordinal));
        Assert.Contains(surface.PartialFamilyClusters, cluster =>
            cluster.FamilyName == "OrderService"
            && cluster.FileCount == 3
            && cluster.RecommendedAction.Contains("Keep the split", StringComparison.Ordinal));
        Assert.Contains(surface.LargestFiles, file =>
            file.Path == "tests/Sample.App.Tests/HugeContractTests.cs"
            && file.FileClass == "huge"
            && file.SuggestedReviewPosture == "review_for_bounded_decomposition");
        Assert.Contains(surface.CleanupCandidates, candidate =>
            candidate.CandidateType == "huge_file_decomposition_review"
            && candidate.ScopePath == "tests/Sample.App.Tests/HugeContractTests.cs"
            && candidate.RecommendedAction.Contains("behavior-preserving extraction", StringComparison.Ordinal));
        Assert.Contains(surface.CleanupCandidates, candidate =>
            candidate.CandidateType == "tiny_cluster_consolidation_review"
            && candidate.ScopePath == "src/Sample.App/Tiny"
            && candidate.NonClaims.Any(claim => claim.Contains("not apply to legitimate public contract", StringComparison.Ordinal)));
        Assert.Contains(surface.CleanupCandidates, candidate =>
            candidate.CandidateType == "partial_family_budget_review"
            && candidate.ScopePath == "src/Sample.App/Services/OrderService.*.cs"
            && candidate.ValidationHint.Contains("avoid cross-family moves", StringComparison.Ordinal));
        Assert.Equal("bounded_low_risk_first", surface.CleanupSelection.Strategy);
        Assert.Equal(3, surface.CleanupSelection.MaxBatchSize);
        Assert.Equal(1, surface.CleanupSelection.SelectedCandidateCount);
        Assert.Contains(surface.CleanupSelection.SelectedCandidates, candidate =>
            candidate.CandidateType == "huge_file_decomposition_review"
            && candidate.ScopePath == "tests/Sample.App.Tests/HugeContractTests.cs"
            && candidate.RiskClass == "lower_risk_test_scope"
            && candidate.SelectionRank == 1);
        Assert.Contains(surface.CleanupSelection.DeferredCandidateIds, candidateId =>
            candidateId.StartsWith("tiny-cluster:", StringComparison.Ordinal));
        Assert.Contains(surface.CleanupSelection.NonClaims, claim =>
            claim.Contains("not approval to edit", StringComparison.Ordinal));
        Assert.DoesNotContain(surface.LargestFiles.Concat(surface.SmallestFiles), file =>
            file.Path.Contains("Ignored.g.cs", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("read-only", StringComparison.Ordinal));
    }

    private static void WriteTinyCluster(TemporaryWorkspace workspace)
    {
        for (var index = 1; index <= 5; index++)
        {
            workspace.WriteFile(
                $"src/Sample.App/Tiny/Tiny{index}.cs",
                $"namespace Sample.App.Tiny;{Environment.NewLine}public sealed class Tiny{index} {{ }}");
        }
    }

    private static void WritePartialCluster(TemporaryWorkspace workspace)
    {
        workspace.WriteFile("src/Sample.App/Services/OrderService.Query.cs", "namespace Sample.App.Services;\npublic sealed partial class OrderService { }");
        workspace.WriteFile("src/Sample.App/Services/OrderService.Commands.cs", "namespace Sample.App.Services;\npublic sealed partial class OrderService { }");
        workspace.WriteFile("src/Sample.App/Services/OrderService.Validation.cs", "namespace Sample.App.Services;\npublic sealed partial class OrderService { }");
    }

    private static string GenerateClass(string name, int lineCount)
    {
        var lines = new List<string>
        {
            "namespace Sample;",
            $"public sealed class {name}",
            "{",
        };

        for (var index = 0; index < Math.Max(0, lineCount - 4); index++)
        {
            lines.Add($"    public int Value{index:D3} => {index};");
        }

        lines.Add("}");
        return string.Join(Environment.NewLine, lines);
    }
}
