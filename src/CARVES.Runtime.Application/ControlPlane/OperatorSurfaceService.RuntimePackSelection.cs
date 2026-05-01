using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RuntimeAssignPack(string packId, string? packVersion, string? channel, string? reason)
    {
        var result = CreateRuntimePackSelectionService().Assign(packId, packVersion, channel, reason);
        if (!result.Selected)
        {
            var lines = new List<string>
            {
                "Runtime pack selection rejected.",
                result.Summary,
            };

            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return FormatRuntimePackSelection(CreateRuntimePackSelectionService().BuildSurface());
    }

    public OperatorCommandResult RuntimeRollbackPack(string selectionId, string? reason)
    {
        var result = CreateRuntimePackSelectionService().Rollback(selectionId, reason);
        if (!result.Selected)
        {
            var lines = new List<string>
            {
                "Runtime pack rollback rejected.",
                result.Summary,
            };

            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return FormatRuntimePackSelection(CreateRuntimePackSelectionService().BuildSurface());
    }

    public OperatorCommandResult InspectRuntimePackSelection()
    {
        return FormatRuntimePackSelection(CreateRuntimePackSelectionService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimePackSelection()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackSelectionService().BuildSurface()));
    }

    private RuntimePackSelectionService CreateRuntimePackSelectionService()
    {
        return new RuntimePackSelectionService(artifactRepository);
    }

    private static OperatorCommandResult FormatRuntimePackSelection(RuntimePackSelectionSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack selection",
            surface.Summary,
        };

        if (surface.CurrentSelection is null)
        {
            lines.Add("Current selection: none");
        }
        else
        {
            lines.Add($"Current selection: {surface.CurrentSelection.PackId}@{surface.CurrentSelection.PackVersion} ({surface.CurrentSelection.Channel})");
            lines.Add($"Selection mode: {surface.CurrentSelection.SelectionMode}");
            lines.Add($"Selection reason: {surface.CurrentSelection.SelectionReason}");
            lines.Add($"Admission captured at: {surface.CurrentSelection.AdmissionCapturedAt:O}");
            if (!string.IsNullOrWhiteSpace(surface.CurrentSelection.ArtifactRef))
            {
                lines.Add($"Artifact ref: {surface.CurrentSelection.ArtifactRef}");
            }

            lines.Add(
                $"Profiles: policy={surface.CurrentSelection.ExecutionProfiles.PolicyPreset}, gate={surface.CurrentSelection.ExecutionProfiles.GatePreset}, validator={surface.CurrentSelection.ExecutionProfiles.ValidatorProfile}, environment={surface.CurrentSelection.ExecutionProfiles.EnvironmentProfile}, routing={surface.CurrentSelection.ExecutionProfiles.RoutingProfile}");
            if (surface.CurrentSelection.ChecksPassed.Length > 0)
            {
                lines.Add($"Checks passed: {string.Join("; ", surface.CurrentSelection.ChecksPassed)}");
            }
        }

        if (surface.CurrentAdmission is not null)
        {
            lines.Add($"Current admission: {surface.CurrentAdmission.PackId}@{surface.CurrentAdmission.PackVersion} ({surface.CurrentAdmission.Channel})");
        }

        lines.Add($"Rollback: {surface.RollbackContext.Summary}");
        if (surface.RollbackContext.EligibleTargets.Count > 0)
        {
            lines.Add("Rollback targets:");
            foreach (var target in surface.RollbackContext.EligibleTargets.Take(5))
            {
                lines.Add($"- {target.SelectionId}: {target.PackId}@{target.PackVersion} ({target.Channel}) mode={target.SelectionMode}");
            }
        }

        if (surface.History.Count > 0)
        {
            lines.Add("Selection history:");
            foreach (var entry in surface.History.Take(5))
            {
                var previous = string.IsNullOrWhiteSpace(entry.PreviousSelectionId) ? "(none)" : entry.PreviousSelectionId;
                lines.Add($"- {entry.SelectionId}: {entry.PackId}@{entry.PackVersion} ({entry.Channel}) mode={entry.SelectionMode}; previous={previous}");
            }
        }

        lines.Add($"Audit: {surface.AuditSummary}");
        if (surface.AuditTrail.Count > 0)
        {
            lines.Add("Audit trail:");
            foreach (var entry in surface.AuditTrail.Take(5))
            {
                var previous = string.IsNullOrWhiteSpace(entry.PreviousSelectionId) ? "(none)" : entry.PreviousSelectionId;
                var rollbackTarget = string.IsNullOrWhiteSpace(entry.RollbackTargetSelectionId) ? "(none)" : entry.RollbackTargetSelectionId;
                lines.Add($"- {entry.AuditId}: {entry.EventKind} selection={entry.SelectionId}; previous={previous}; rollback_target={rollbackTarget}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
