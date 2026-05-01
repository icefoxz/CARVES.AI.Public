using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeCoreAdapterBoundaryServiceTests
{
    [Fact]
    public void Build_ProjectsCoreTruthAdapterArtifactsAndProjections()
    {
        using var workspace = new TemporaryWorkspace();
        var config = TestSystemConfigFactory.Create(["src", "tests"]);
        var service = new RuntimeCoreAdapterBoundaryService(workspace.RootPath, workspace.Paths, config);

        var surface = service.Build();

        Assert.Equal("runtime-core-adapter-boundary", surface.SurfaceId);
        Assert.Contains(surface.Families, family => family.FamilyId == "task_truth" && family.Classification == "core_truth");
        Assert.Contains(surface.Families, family => family.FamilyId == "platform_provider_definition_truth" && family.Classification == "adapter_artifact");
        Assert.Contains(surface.Families, family => family.FamilyId == "governed_markdown_mirror" && family.Classification == "projection");
        Assert.Contains(surface.Families, family => family.FamilyId == "agent_bootstrap_projection" && family.PathRefs.Contains("AGENTS.md", StringComparer.Ordinal));
        Assert.Contains(surface.Families, family => family.FamilyId == "provider_adapter_implementation" && family.Classification == "adapter_artifact");
        Assert.Contains(surface.CoreContracts, contract => contract.ContractId == "execution_packet" && contract.SchemaPath == "docs/contracts/execution-packet.schema.json");
        Assert.Contains(surface.CoreContracts, contract => contract.ContractId == "execution_run_report" && contract.ForbiddenEmbeddedPayloads.Contains("raw_transcript", StringComparer.Ordinal));
        Assert.Contains(surface.CoreContracts, contract => contract.ContractId == "safety_verdict" && contract.AllowedExtensionReferences.Contains("artifact_ref", StringComparer.Ordinal));
    }
}
