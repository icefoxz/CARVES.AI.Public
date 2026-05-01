using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackExecutionAttributionService
{
    private readonly RuntimePackDeclarativeContributionSnapshotService contributionSnapshotService;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackExecutionAttributionService(string repoRoot, IRuntimeArtifactRepository artifactRepository)
    {
        contributionSnapshotService = new RuntimePackDeclarativeContributionSnapshotService(repoRoot);
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackExecutionAttribution? TryLoadCurrentSelectionReference()
    {
        var selection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        if (selection is null)
        {
            return null;
        }

        return new RuntimePackExecutionAttribution
        {
            PackId = selection.PackId,
            PackVersion = selection.PackVersion,
            Channel = selection.Channel,
            ArtifactRef = selection.ArtifactRef,
            PolicyPreset = selection.ExecutionProfiles.PolicyPreset,
            GatePreset = selection.ExecutionProfiles.GatePreset,
            ValidatorProfile = selection.ExecutionProfiles.ValidatorProfile,
            RoutingProfile = selection.ExecutionProfiles.RoutingProfile,
            EnvironmentProfile = selection.ExecutionProfiles.EnvironmentProfile,
            SelectionMode = selection.SelectionMode,
            SelectedAtUtc = selection.SelectedAt,
            DeclarativeContribution = contributionSnapshotService.TryBuild(
                selection.AdmissionSource.AssignmentMode,
                selection.AdmissionSource.AssignmentRef),
        };
    }
}
