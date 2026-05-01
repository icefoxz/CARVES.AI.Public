using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackDistributionBoundaryService
{
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackDistributionBoundaryService(IRuntimeArtifactRepository artifactRepository)
    {
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackDistributionBoundarySurface BuildSurface()
    {
        var currentAdmission = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        var currentSelection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        var history = artifactRepository.LoadRuntimePackSelectionHistory();
        return new RuntimePackDistributionBoundarySurface
        {
            Summary = "Runtime currently supports local pack validation, admission, selection, execution attribution, history, bounded rollback, and local audit evidence. Registry and rollout remain closed future lines.",
            CurrentTruth = new RuntimePackDistributionCurrentTruth
            {
                HasCurrentAdmission = currentAdmission is not null,
                HasCurrentSelection = currentSelection is not null,
                SelectionHistoryCount = history.Count,
                Summary = BuildCurrentTruthSummary(currentAdmission, currentSelection, history.Count),
            },
            LocalCapabilities =
            [
                Capability(
                    "pack_contract_validation",
                    "available",
                    "Runtime can validate pack artifact and runtime pack attribution contracts.",
                    "validate pack-artifact",
                    "validate runtime-pack-attribution"),
                Capability(
                    "runtime_local_pack_admission",
                    "available",
                    "Runtime can admit a validated pack and attribution pair into bounded local evidence.",
                    "runtime admit-pack",
                    "inspect runtime-pack-admission"),
                Capability(
                    "runtime_local_pack_selection",
                    "available",
                    "Runtime can assign one admitted pack as the current local selection.",
                    "runtime assign-pack",
                    "inspect runtime-pack-selection"),
                Capability(
                    "runtime_execution_pack_attribution",
                    "available",
                    "Execution runs and run reports carry the current selected pack by bounded reference.",
                    "inspect run",
                    "execution-contract-surface"),
                Capability(
                    "runtime_local_pack_history_and_rollback",
                    "available",
                    "Runtime can append local selection history and perform bounded rollback by explicit historical selection id.",
                    "runtime rollback-pack",
                    "api runtime-pack-selection"),
                Capability(
                    "runtime_local_pack_audit_evidence",
                    "available",
                    "Runtime can record append-only local audit evidence for pack switch and rollback decisions.",
                    "inspect runtime-pack-selection",
                    "api runtime-pack-selection"),
                Capability(
                    "runtime_pack_task_explainability",
                    "available",
                    "Runtime can explain current selection coverage plus recent pack-attributed run and report evidence for one task id.",
                    "inspect runtime-pack-task-explainability <task-id>",
                    "api runtime-pack-task-explainability <task-id>"),
                Capability(
                    "runtime_local_pack_switch_policy",
                    "available",
                    "Runtime can pin the current local selection and expose a bounded local switch policy that gates divergent assign and rollback actions.",
                    "runtime pin-current-pack",
                    "inspect runtime-pack-switch-policy"),
                Capability(
                    "runtime_pack_execution_audit",
                    "available",
                    "Runtime can inspect current selection coverage plus recent execution runs and run reports that carry selected pack references.",
                    "inspect runtime-pack-execution-audit",
                    "api runtime-pack-execution-audit")
            ],
            ClosedFutureCapabilities =
            [
                Capability(
                    "pack_registry_inventory",
                    "closed",
                    "Runtime does not maintain a remote or multi-pack registry inventory.",
                    "future"),
                Capability(
                    "pack_registry_sync",
                    "closed",
                    "Runtime does not synchronize packs from a remote registry or publication channel.",
                    "future"),
                Capability(
                    "pack_rollout_assignment",
                    "closed",
                    "Runtime does not assign packs through rollout channels, cohorts, or staged promotion policy.",
                    "future"),
                Capability(
                    "automatic_activation",
                    "closed",
                    "Runtime does not auto-activate newly admitted or remotely available packs.",
                    "future"),
                Capability(
                    "multi_pack_distribution_orchestration",
                    "closed",
                    "Runtime does not orchestrate multiple pack inventories, arbitration, or distribution lifecycles.",
                    "future")
            ],
            Notes =
            [
                "This surface is a boundary statement, not a registry or rollout implementation.",
                "Local pack lifecycle truth is explicit today; future distribution truth remains intentionally closed.",
                "Any future registry or rollout line must land as new bounded cards rather than implicit growth from local pack surfaces."
            ],
        };
    }

    private static RuntimePackBoundaryCapability Capability(
        string capabilityId,
        string status,
        string summary,
        params string[] truthRefs)
    {
        return new RuntimePackBoundaryCapability
        {
            CapabilityId = capabilityId,
            Status = status,
            Summary = summary,
            TruthRefs = truthRefs,
        };
    }

    private static string BuildCurrentTruthSummary(
        RuntimePackAdmissionArtifact? currentAdmission,
        RuntimePackSelectionArtifact? currentSelection,
        int historyCount)
    {
        var admission = currentAdmission is null
            ? "no current admission"
            : $"admission={currentAdmission.PackId}@{currentAdmission.PackVersion} ({currentAdmission.Channel})";
        var selection = currentSelection is null
            ? "no current selection"
            : $"selection={currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel})";
        return $"{admission}; {selection}; history_entries={historyCount}";
    }
}
