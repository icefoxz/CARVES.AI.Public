using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackSwitchPolicyService
{
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackSwitchPolicyService(IRuntimeArtifactRepository artifactRepository)
    {
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackSwitchPolicyResult PinCurrentSelection(string? reason)
    {
        var currentSelection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        if (currentSelection is null)
        {
            return RuntimePackSwitchPolicyResult.Rejected(
                "Runtime pack pin rejected because no current local selection exists yet.",
                ["runtime_pack_pin_requires_current_selection"]);
        }

        var artifact = new RuntimePackSwitchPolicyArtifact
        {
            PolicyId = $"packpolicy-{Guid.NewGuid():N}",
            PolicyMode = "local_pin_current_selection",
            PinActive = true,
            PinnedSelectionId = currentSelection.SelectionId,
            PackId = currentSelection.PackId,
            PackVersion = currentSelection.PackVersion,
            Channel = currentSelection.Channel,
            Reason = string.IsNullOrWhiteSpace(reason)
                ? "Pinned the current local runtime pack selection."
                : reason.Trim(),
            Summary = $"Runtime-local pack switch policy pinned {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}) from selection {currentSelection.SelectionId}.",
            ChecksPassed =
            [
                "pin stays local-runtime scoped",
                "pin derives from current local selection",
                "registry and rollout remain closed"
            ],
        };

        artifactRepository.SaveRuntimePackSwitchPolicyArtifact(artifact);
        artifactRepository.SaveRuntimePackPolicyAuditEntry(BuildPolicyAuditEntry(
            artifact,
            "pin_current_selection",
            "switch_policy_pin",
            null,
            null,
            $"Pinned current local runtime pack selection {currentSelection.SelectionId}."));
        return RuntimePackSwitchPolicyResult.Succeeded(artifact);
    }

    public RuntimePackSwitchPolicyResult ClearPin(string? reason)
    {
        var artifact = RuntimePackSwitchPolicyArtifact.CreateDefault() with
        {
            PolicyId = $"packpolicy-{Guid.NewGuid():N}",
            Reason = string.IsNullOrWhiteSpace(reason)
                ? "Cleared the current local pack pin."
                : reason.Trim(),
            Summary = "Runtime-local pack switch policy is unpinned; assign and rollback may proceed within existing local admission and history constraints."
        };

        artifactRepository.SaveRuntimePackSwitchPolicyArtifact(artifact);
        artifactRepository.SaveRuntimePackPolicyAuditEntry(BuildPolicyAuditEntry(
            artifact,
            "clear_pack_pin",
            "switch_policy_clear",
            null,
            null,
            "Cleared the current local runtime pack pin."));
        return RuntimePackSwitchPolicyResult.Succeeded(artifact);
    }

    public RuntimePackSwitchPolicyArtifact BuildCurrentPolicy()
    {
        return artifactRepository.TryLoadCurrentRuntimePackSwitchPolicyArtifact()
               ?? RuntimePackSwitchPolicyArtifact.CreateDefault();
    }

    public RuntimePackSwitchPolicyArtifact SaveCurrentPolicy(RuntimePackSwitchPolicyArtifact artifact)
    {
        artifactRepository.SaveRuntimePackSwitchPolicyArtifact(artifact);
        return artifact;
    }

    public RuntimePackSwitchPolicySurface BuildSurface()
    {
        var currentSelection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        var currentPolicy = BuildCurrentPolicy();
        return new RuntimePackSwitchPolicySurface
        {
            CurrentSelection = currentSelection,
            CurrentPolicy = currentPolicy,
            Summary = currentPolicy.PinActive
                ? $"Runtime-local pack switch policy pins {currentPolicy.PackId}@{currentPolicy.PackVersion} ({currentPolicy.Channel}) and blocks divergent local assign or rollback."
                : "Runtime-local pack switch policy is unpinned; local assign and rollback remain bounded only by admission and history truth.",
            Notes =
            [
                "This policy line is local-runtime scoped and explicit.",
                "Local pin policy constrains assign-pack and rollback-pack but does not introduce registry or rollout behavior.",
                "Automatic activation, remote assignment, and multi-pack orchestration remain closed."
            ],
        };
    }

    private static RuntimePackPolicyAuditEntry BuildPolicyAuditEntry(
        RuntimePackSwitchPolicyArtifact policy,
        string eventKind,
        string sourceKind,
        string? packageId,
        string? packagePath,
        string summary)
    {
        return new RuntimePackPolicyAuditEntry
        {
            EventKind = eventKind,
            SourceKind = sourceKind,
            PackageId = packageId,
            PackagePath = packagePath,
            ResultingAdmissionPolicyId = null,
            ResultingSwitchPolicyId = policy.PolicyId,
            Summary = summary,
            ChecksPassed =
            [
                "policy audit remained local-runtime scoped",
                "resulting switch policy reference was captured"
            ],
        };
    }
}

public sealed class RuntimePackSwitchPolicyResult
{
    private RuntimePackSwitchPolicyResult(
        bool accepted,
        string summary,
        RuntimePackSwitchPolicyArtifact? artifact,
        IReadOnlyList<string> failureCodes)
    {
        Accepted = accepted;
        Summary = summary;
        Artifact = artifact;
        FailureCodes = failureCodes;
    }

    public bool Accepted { get; }

    public string Summary { get; }

    public RuntimePackSwitchPolicyArtifact? Artifact { get; }

    public IReadOnlyList<string> FailureCodes { get; }

    public static RuntimePackSwitchPolicyResult Succeeded(RuntimePackSwitchPolicyArtifact artifact)
    {
        return new RuntimePackSwitchPolicyResult(true, artifact.Summary, artifact, Array.Empty<string>());
    }

    public static RuntimePackSwitchPolicyResult Rejected(string summary, IReadOnlyList<string> failureCodes)
    {
        return new RuntimePackSwitchPolicyResult(false, summary, null, failureCodes);
    }
}
