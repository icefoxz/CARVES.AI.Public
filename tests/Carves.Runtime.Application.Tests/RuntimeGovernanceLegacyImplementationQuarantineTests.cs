using System.Text.Json;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeGovernanceLegacyImplementationQuarantineTests
{
    private static readonly string[] AllowedReclaimClasses =
    [
        "delete_ready",
        "internal_support",
        "legacy_shape_guarded",
        "blocked_unknown",
    ];

    private static readonly string[] ExpectedDeletedArtifacts =
    [
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.RuntimeHotspotBacklogDrain.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.RuntimeHotspotCrossFamilyPatterns.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.RuntimePackagingProofFederationMaturity.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.RuntimeControlledGovernanceProof.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.RuntimeValidationLabProofHandoff.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.RuntimeGovernanceProgramReaudit.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.RuntimeHotspotBacklogDrain.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.RuntimeHotspotCrossFamilyPatterns.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.RuntimePackagingProofFederationMaturity.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.RuntimeControlledGovernanceProof.cs",
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceFormatter.RuntimeValidationLabProofHandoff.cs",
    ];

    private static readonly string[] ExpectedInternalSupportArtifacts =
    [
        "src/CARVES.Runtime.Application/ControlPlane/OperatorSurfaceService.RuntimeGovernanceProgramReaudit.cs",
        "src/CARVES.Runtime.Application/Platform/RuntimeGovernanceProgramReauditService.cs",
        "src/CARVES.Runtime.Application/Platform/RuntimeHotspotBacklogDrainService.cs",
        "src/CARVES.Runtime.Application/Platform/RuntimeHotspotCrossFamilyPatternService.cs",
        "src/CARVES.Runtime.Application/Platform/RuntimePackagingProofFederationMaturityService.cs",
        "src/CARVES.Runtime.Application/Platform/RuntimeControlledGovernanceProofService.cs",
        "src/CARVES.Runtime.Application/Platform/RuntimeValidationLabProofHandoffService.cs",
        "src/CARVES.Runtime.Application/Platform/SurfaceModels/RuntimeGovernanceProgramReauditSurface.cs",
        "src/CARVES.Runtime.Application/Platform/SurfaceModels/RuntimeHotspotBacklogDrainSurface.cs",
        "src/CARVES.Runtime.Application/Platform/SurfaceModels/RuntimeHotspotCrossFamilyPatternSurface.cs",
        "src/CARVES.Runtime.Application/Platform/SurfaceModels/RuntimePackagingProofFederationMaturitySurface.cs",
        "src/CARVES.Runtime.Application/Platform/SurfaceModels/RuntimeControlledGovernanceProofSurface.cs",
        "src/CARVES.Runtime.Application/Platform/SurfaceModels/RuntimeValidationLabProofHandoffSurface.cs",
        "tests/Carves.Runtime.Application.Tests/RuntimeGovernanceProgramReauditServiceTests.cs",
        "tests/Carves.Runtime.Application.Tests/RuntimeHotspotBacklogDrainServiceTests.cs",
        "tests/Carves.Runtime.Application.Tests/RuntimeHotspotCrossFamilyPatternServiceTests.cs",
        "tests/Carves.Runtime.Application.Tests/RuntimePackagingProofFederationMaturityServiceTests.cs",
        "tests/Carves.Runtime.Application.Tests/RuntimeControlledGovernanceProofServiceTests.cs",
        "tests/Carves.Runtime.Application.Tests/RuntimeValidationLabProofHandoffServiceTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimeGovernanceArchiveStatusHostContractTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimeGovernanceProgramReauditHostContractTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimeHotspotBacklogDrainHostContractTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimeControlledGovernanceProofHostContractTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimePackagingProofFederationMaturityHostContractTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimeValidationLabProofHandoffHostContractTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimeProjectionSmokeHostContractTests.cs",
        "tests/Carves.Runtime.IntegrationTests/RuntimeSurfaceCommandRegistryHostContractTests.cs",
    ];

    [Fact]
    public void QuarantineEvidence_ClassifiesEveryTargetArtifactAndMatchesFileState()
    {
        var repoRoot = RepoRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repoRoot,
            ".ai",
            "evidence",
            "runtime",
            "surface-inventory",
            "CARD-935-legacy-implementation-quarantine.json")));

        var root = document.RootElement;
        Assert.Equal("CARD-935", root.GetProperty("card_id").GetString());
        Assert.Equal("T-CARD-935-001", root.GetProperty("task_id").GetString());
        Assert.Equal(11, root.GetProperty("decision_summary").GetProperty("delete_ready").GetInt32());
        Assert.Equal(27, root.GetProperty("decision_summary").GetProperty("internal_support").GetInt32());
        Assert.Equal(0, root.GetProperty("decision_summary").GetProperty("legacy_shape_guarded").GetInt32());
        Assert.Equal(0, root.GetProperty("decision_summary").GetProperty("blocked_unknown").GetInt32());

        var artifacts = root.GetProperty("artifacts")
            .EnumerateArray()
            .ToDictionary(
                artifact => artifact.GetProperty("path").GetString() ?? "",
                StringComparer.Ordinal);

        foreach (var path in ExpectedDeletedArtifacts)
        {
            Assert.True(artifacts.TryGetValue(path, out var artifact), path);
            Assert.Equal("delete_ready", artifact.GetProperty("reclaim_class").GetString());
            Assert.True(artifact.GetProperty("deleted_in_card").GetBoolean(), path);
            Assert.False(File.Exists(Path.Combine(repoRoot, path)), path);
            Assert.True(artifact.GetProperty("evidence_refs").GetArrayLength() > 0, path);
        }

        foreach (var path in ExpectedInternalSupportArtifacts)
        {
            Assert.True(artifacts.TryGetValue(path, out var artifact), path);
            Assert.Equal("internal_support", artifact.GetProperty("reclaim_class").GetString());
            Assert.False(artifact.GetProperty("deleted_in_card").GetBoolean(), path);
            Assert.True(File.Exists(Path.Combine(repoRoot, path)), path);
            Assert.True(artifact.GetProperty("evidence_refs").GetArrayLength() > 0, path);
        }

        foreach (var artifact in artifacts.Values)
        {
            Assert.Contains(artifact.GetProperty("reclaim_class").GetString(), AllowedReclaimClasses);
        }
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, ".ai")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to find repository root.");
    }
}
