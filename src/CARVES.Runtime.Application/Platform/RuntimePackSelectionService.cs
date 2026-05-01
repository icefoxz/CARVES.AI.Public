using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackSelectionService
{
    private const int SurfaceHistoryLimit = 10;
    private const int SurfaceAuditLimit = 10;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackSelectionService(IRuntimeArtifactRepository artifactRepository)
    {
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackSelectionResult Assign(string packId, string? packVersion, string? channel, string? reason)
    {
        var currentAdmission = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        if (currentAdmission is null)
        {
            return RuntimePackSelectionResult.Rejected(
                "Runtime pack selection rejected because no admitted local pack evidence exists yet.",
                ["runtime_pack_selection_requires_admission"]);
        }

        var failures = new List<string>();
        var checksPassed = new List<string>();
        CompareMatch("pack_id_mismatch", "pack id", currentAdmission.PackId, packId, failures, checksPassed);

        if (!string.IsNullOrWhiteSpace(packVersion))
        {
            CompareMatch("pack_version_mismatch", "pack version", currentAdmission.PackVersion, packVersion.Trim(), failures, checksPassed);
        }
        else
        {
            checksPassed.Add("pack version defaults to admitted current");
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            CompareMatch("pack_channel_mismatch", "channel", currentAdmission.Channel, channel.Trim(), failures, checksPassed);
        }
        else
        {
            checksPassed.Add("channel defaults to admitted current");
        }

        var currentPolicy = artifactRepository.TryLoadCurrentRuntimePackSwitchPolicyArtifact();
        if (currentPolicy is not null
            && currentPolicy.BlocksSelectionChange(
                currentAdmission.PackId,
                currentAdmission.PackVersion,
                currentAdmission.Channel))
        {
            return RuntimePackSelectionResult.Rejected(
                "Runtime pack selection rejected because the active local pack pin blocks switching away from the pinned identity.",
                ["runtime_pack_selection_blocked_by_local_pin"]);
        }

        if (failures.Count > 0)
        {
            return RuntimePackSelectionResult.Rejected(
                "Runtime pack selection rejected because the requested identity does not match admitted local pack evidence.",
                failures);
        }

        var selectionReason = string.IsNullOrWhiteSpace(reason)
            ? "Selected as the current local runtime pack through explicit operator assignment."
            : reason.Trim();
        var artifact = BuildSelectionArtifact(
            currentAdmission,
            artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact(),
            "manual_local_assignment",
            selectionReason,
            checksPassed,
            rollbackTargetSelectionId: null,
            $"Runtime-local selection set {currentAdmission.PackId}@{currentAdmission.PackVersion} ({currentAdmission.Channel}) as the current pack.");

        artifactRepository.SaveRuntimePackSelectionArtifact(artifact);
        artifactRepository.SaveRuntimePackSelectionAuditEntry(BuildAuditEntry(artifact, "selection_assigned"));
        return RuntimePackSelectionResult.Accepted(artifact);
    }

    public RuntimePackSelectionResult Rollback(string selectionId, string? reason)
    {
        var currentAdmission = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        if (currentAdmission is null)
        {
            return RuntimePackSelectionResult.Rejected(
                "Runtime pack rollback rejected because no admitted local pack evidence exists yet.",
                ["runtime_pack_rollback_requires_admission"]);
        }

        var target = artifactRepository.TryLoadRuntimePackSelectionArtifact(selectionId);
        if (target is null)
        {
            return RuntimePackSelectionResult.Rejected(
                "Runtime pack rollback rejected because the requested historical selection was not found.",
                ["runtime_pack_selection_target_missing"]);
        }

        if (!MatchesAdmission(currentAdmission, target))
        {
            return RuntimePackSelectionResult.Rejected(
                "Runtime pack rollback rejected because the requested historical selection does not match the currently admitted pack identity.",
                ["runtime_pack_selection_target_not_currently_admitted"]);
        }

        var currentPolicy = artifactRepository.TryLoadCurrentRuntimePackSwitchPolicyArtifact();
        if (currentPolicy is not null
            && currentPolicy.BlocksSelectionChange(
                target.PackId,
                target.PackVersion,
                target.Channel))
        {
            return RuntimePackSelectionResult.Rejected(
                "Runtime pack rollback rejected because the active local pack pin blocks switching away from the pinned identity.",
                ["runtime_pack_rollback_blocked_by_local_pin"]);
        }

        var currentSelection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        var rollbackReason = string.IsNullOrWhiteSpace(reason)
            ? $"Rolled back current runtime pack selection to historical selection {target.SelectionId}."
            : reason.Trim();
        var artifact = BuildSelectionArtifact(
            currentAdmission,
            currentSelection,
            "manual_local_rollback",
            rollbackReason,
            [
                "rollback target found in local selection history",
                "rollback target matches current admitted identity"
            ],
            target.SelectionId,
            $"Runtime-local rollback restored {target.PackId}@{target.PackVersion} ({target.Channel}) from historical selection {target.SelectionId}.");

        artifactRepository.SaveRuntimePackSelectionArtifact(artifact);
        artifactRepository.SaveRuntimePackSelectionAuditEntry(BuildAuditEntry(artifact, "selection_rolled_back"));
        return RuntimePackSelectionResult.Accepted(artifact);
    }

    public RuntimePackSelectionSurface BuildSurface()
    {
        var currentSelection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        var currentAdmission = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        var history = artifactRepository.LoadRuntimePackSelectionHistory(SurfaceHistoryLimit);
        var auditTrail = artifactRepository.LoadRuntimePackSelectionAuditEntries(SurfaceAuditLimit);
        var eligibleTargets = currentAdmission is null
            ? Array.Empty<RuntimePackSelectionArtifact>()
            : history
                .Where(item => !string.IsNullOrWhiteSpace(item.SelectionId))
                .Where(item => currentSelection is null || !string.Equals(item.SelectionId, currentSelection.SelectionId, StringComparison.Ordinal))
                .Where(item => MatchesAdmission(currentAdmission, item))
                .ToArray();
        return new RuntimePackSelectionSurface
        {
            CurrentSelection = currentSelection,
            CurrentAdmission = currentAdmission,
            History = history,
            AuditSummary = BuildAuditSummary(auditTrail),
            AuditTrail = auditTrail,
            RollbackContext = new RuntimePackSelectionRollbackContext
            {
                CanRollback = eligibleTargets.Length > 0,
                CurrentAdmissionIdentity = currentAdmission is null
                    ? "(none)"
                    : $"{currentAdmission.PackId}@{currentAdmission.PackVersion} ({currentAdmission.Channel})",
                EligibleTargets = eligibleTargets,
                Summary = BuildRollbackSummary(currentAdmission, currentSelection, eligibleTargets),
            },
            Summary = currentSelection is null
                ? "No runtime-local pack selection has been recorded yet."
                : currentSelection.Summary,
            Notes =
            [
                "Runtime-local selection is bounded local truth derived from admitted pack evidence.",
                "Selection history is append-only local evidence and does not introduce registry rollout, publication state, or multi-pack arbitration.",
                "Pack switch and rollback audit evidence is additive local runtime truth, not a registry or rollout ledger.",
                "Current selection must match the currently admitted pack identity.",
                "If a local pack pin is active, assign and rollback stay constrained to that pinned identity until the pin is cleared.",
            ],
        };
    }

    private RuntimePackSelectionArtifact BuildSelectionArtifact(
        RuntimePackAdmissionArtifact currentAdmission,
        RuntimePackSelectionArtifact? previousSelection,
        string selectionMode,
        string selectionReason,
        IReadOnlyCollection<string> checksPassed,
        string? rollbackTargetSelectionId,
        string summary)
    {
        return new RuntimePackSelectionArtifact
        {
            SelectionId = $"packsel-{Guid.NewGuid():N}",
            PackId = currentAdmission.PackId,
            PackVersion = currentAdmission.PackVersion,
            Channel = currentAdmission.Channel,
            RuntimeStandardVersion = currentAdmission.RuntimeStandardVersion,
            PackArtifactPath = currentAdmission.PackArtifactPath,
            RuntimePackAttributionPath = currentAdmission.RuntimePackAttributionPath,
            ArtifactRef = currentAdmission.ArtifactRef,
            ExecutionProfiles = currentAdmission.ExecutionProfiles,
            AdmissionSource = currentAdmission.Source,
            AdmissionCapturedAt = currentAdmission.CapturedAt,
            SelectionMode = selectionMode,
            SelectionReason = selectionReason,
            PreviousSelectionId = previousSelection?.SelectionId,
            RollbackTargetSelectionId = rollbackTargetSelectionId,
            ChecksPassed =
            [
                .. checksPassed,
                "selection remains local-runtime scoped",
                "selection is derived from admitted current evidence"
            ],
            Summary = summary,
        };
    }

    private static bool MatchesAdmission(RuntimePackAdmissionArtifact admission, RuntimePackSelectionArtifact selection)
    {
        return string.Equals(admission.PackId, selection.PackId, StringComparison.Ordinal)
               && string.Equals(admission.PackVersion, selection.PackVersion, StringComparison.Ordinal)
               && string.Equals(admission.Channel, selection.Channel, StringComparison.Ordinal);
    }

    private static RuntimePackSelectionAuditEntry BuildAuditEntry(RuntimePackSelectionArtifact artifact, string eventKind)
    {
        return new RuntimePackSelectionAuditEntry
        {
            AuditId = $"packaudit-{Guid.NewGuid():N}",
            EventKind = eventKind,
            SelectionId = artifact.SelectionId,
            PreviousSelectionId = artifact.PreviousSelectionId,
            RollbackTargetSelectionId = artifact.RollbackTargetSelectionId,
            PackId = artifact.PackId,
            PackVersion = artifact.PackVersion,
            Channel = artifact.Channel,
            ArtifactRef = artifact.ArtifactRef,
            SelectionMode = artifact.SelectionMode,
            Reason = artifact.SelectionReason,
            Summary = artifact.Summary,
            ChecksPassed = artifact.ChecksPassed,
        };
    }

    private static string BuildRollbackSummary(
        RuntimePackAdmissionArtifact? currentAdmission,
        RuntimePackSelectionArtifact? currentSelection,
        IReadOnlyCollection<RuntimePackSelectionArtifact> eligibleTargets)
    {
        if (currentSelection is null)
        {
            return "Rollback unavailable because no current local pack selection exists yet.";
        }

        if (currentAdmission is null)
        {
            return "Rollback unavailable because no current admitted local pack evidence exists.";
        }

        return eligibleTargets.Count == 0
            ? "Rollback unavailable because no historical selection matches the current admitted pack identity."
            : $"Rollback can target {eligibleTargets.Count} historical selection(s) that match the current admitted pack identity.";
    }

    private static string BuildAuditSummary(IReadOnlyList<RuntimePackSelectionAuditEntry> auditTrail)
    {
        if (auditTrail.Count == 0)
        {
            return "No local pack switch or rollback audit evidence has been recorded yet.";
        }

        var latest = auditTrail[0];
        return $"Recent local pack audit events: {auditTrail.Count}. Latest={latest.EventKind} {latest.PackId}@{latest.PackVersion} ({latest.Channel}).";
    }

    private static void CompareMatch(
        string failureCode,
        string description,
        string left,
        string right,
        ICollection<string> failures,
        ICollection<string> checksPassed)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            checksPassed.Add($"{description} matches");
            return;
        }

        failures.Add(failureCode);
    }
}

public sealed class RuntimePackSelectionResult
{
    private RuntimePackSelectionResult(
        bool selected,
        string summary,
        RuntimePackSelectionArtifact? artifact,
        IReadOnlyList<string> failureCodes)
    {
        Selected = selected;
        Summary = summary;
        Artifact = artifact;
        FailureCodes = failureCodes;
    }

    public bool Selected { get; }

    public string Summary { get; }

    public RuntimePackSelectionArtifact? Artifact { get; }

    public IReadOnlyList<string> FailureCodes { get; }

    public static RuntimePackSelectionResult Accepted(RuntimePackSelectionArtifact artifact)
    {
        return new RuntimePackSelectionResult(true, artifact.Summary, artifact, Array.Empty<string>());
    }

    public static RuntimePackSelectionResult Rejected(string summary, IReadOnlyList<string> failureCodes)
    {
        return new RuntimePackSelectionResult(false, summary, null, failureCodes);
    }
}
