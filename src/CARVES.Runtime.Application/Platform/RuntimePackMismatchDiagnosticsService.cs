using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackMismatchDiagnosticsService
{
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly RuntimePackExecutionAuditService executionAuditService;

    public RuntimePackMismatchDiagnosticsService(
        ControlPlanePaths paths,
        IRuntimeArtifactRepository artifactRepository,
        ExecutionRunService executionRunService)
    {
        this.artifactRepository = artifactRepository;
        executionAuditService = new RuntimePackExecutionAuditService(paths, artifactRepository, executionRunService);
    }

    public RuntimePackMismatchDiagnosticsSurface Build()
    {
        var currentAdmission = artifactRepository.TryLoadCurrentRuntimePackAdmissionArtifact();
        var currentSelection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        var currentPolicy = artifactRepository.TryLoadCurrentRuntimePackSwitchPolicyArtifact()
            ?? RuntimePackSwitchPolicyArtifact.CreateDefault();
        var executionAudit = executionAuditService.Build();
        var diagnostics = new List<RuntimePackMismatchDiagnostic>();

        if (currentAdmission is null)
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "no_current_admission",
                Severity = "action_required",
                Summary = "No runtime-local pack admission is recorded.",
                Details = "Runtime cannot explain an active local pack line until a pack artifact and runtime attribution pair have been admitted.",
                RecommendedActions =
                [
                    "inspect runtime-pack-admission-policy",
                    "runtime admit-pack <pack-artifact-path> --attribution <runtime-pack-attribution-path>"
                ],
            });
        }

        if (currentAdmission is not null && currentSelection is null)
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "no_current_selection",
                Severity = "action_required",
                Summary = "A pack has been admitted, but no current local selection is active.",
                Details = $"Current admission is {currentAdmission.PackId}@{currentAdmission.PackVersion} ({currentAdmission.Channel}), but runtime has not assigned it as the current local pack.",
                RecommendedActions =
                [
                    $"runtime assign-pack {currentAdmission.PackId} --pack-version {currentAdmission.PackVersion} --channel {currentAdmission.Channel}",
                    "inspect runtime-pack-selection"
                ],
            });
        }

        if (currentSelection is not null && currentAdmission is null)
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "selection_without_current_admission",
                Severity = "action_required",
                Summary = "A current local selection exists without matching admitted pack evidence.",
                Details = $"Selection points to {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}), but no current admitted pack exists.",
                RecommendedActions =
                [
                    "inspect runtime-pack-selection",
                    "inspect runtime-pack-admission",
                    "runtime admit-pack <pack-artifact-path> --attribution <runtime-pack-attribution-path>"
                ],
            });
        }

        if (currentSelection is not null
            && currentAdmission is not null
            && !MatchesAdmission(currentAdmission, currentSelection))
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "selection_not_currently_admitted",
                Severity = "action_required",
                Summary = "Current selection does not match current admitted pack identity.",
                Details = $"Admission is {currentAdmission.PackId}@{currentAdmission.PackVersion} ({currentAdmission.Channel}) while selection is {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}).",
                RecommendedActions =
                [
                    "inspect runtime-pack-admission",
                    "inspect runtime-pack-selection",
                    $"runtime assign-pack {currentAdmission.PackId} --pack-version {currentAdmission.PackVersion} --channel {currentAdmission.Channel}"
                ],
            });
        }

        if (currentPolicy.PinActive && currentSelection is null)
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "pin_without_current_selection",
                Severity = "action_required",
                Summary = "Local pin policy is active, but no current selection exists.",
                Details = "The local switch policy still reports a pin even though runtime no longer has a current selected pack.",
                RecommendedActions =
                [
                    "inspect runtime-pack-switch-policy",
                    "runtime clear-pack-pin --reason reconcile-missing-selection"
                ],
            });
        }

        if (currentPolicy.PinActive
            && currentSelection is not null
            && currentPolicy.BlocksSelectionChange(currentSelection.PackId, currentSelection.PackVersion, currentSelection.Channel))
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "pin_diverges_from_current_selection",
                Severity = "action_required",
                Summary = "Local pin policy does not match the current selection identity.",
                Details = $"Pin is {currentPolicy.PackId}@{currentPolicy.PackVersion} ({currentPolicy.Channel}) while current selection is {currentSelection.PackId}@{currentSelection.PackVersion} ({currentSelection.Channel}).",
                RecommendedActions =
                [
                    "inspect runtime-pack-switch-policy",
                    "runtime clear-pack-pin --reason reconcile-divergent-pin"
                ],
            });
        }

        var divergentTaskIds = executionAudit.RecentRuns
            .Where(entry => !entry.MatchesCurrentSelection)
            .Select(entry => entry.TaskId)
            .Concat(executionAudit.RecentReports
                .Where(entry => !entry.MatchesCurrentSelection)
                .Select(entry => entry.TaskId))
            .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        if (executionAudit.CurrentSelection is null
            && (executionAudit.RecentRuns.Count > 0 || executionAudit.RecentReports.Count > 0))
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "recent_execution_without_current_selection",
                Severity = "warning",
                Summary = "Recent pack-attributed execution evidence exists without a current selection.",
                Details = executionAudit.Coverage.Summary,
                RelatedTaskIds = divergentTaskIds,
                RecommendedActions =
                [
                    "inspect runtime-pack-execution-audit",
                    .. divergentTaskIds.Select(taskId => $"inspect runtime-pack-task-explainability {taskId}")
                ],
            });
        }
        else if (divergentTaskIds.Length > 0)
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "recent_execution_diverges_from_current_selection",
                Severity = "warning",
                Summary = "Recent execution evidence diverges from the current selected pack.",
                Details = executionAudit.Coverage.Summary,
                RelatedTaskIds = divergentTaskIds,
                RecommendedActions =
                [
                    "inspect runtime-pack-execution-audit",
                    .. divergentTaskIds.Select(taskId => $"inspect runtime-pack-task-explainability {taskId}")
                ],
            });
        }

        var divergentContributionTaskIds = executionAudit.RecentRuns
            .Where(entry => entry.MatchesCurrentSelection && !entry.MatchesCurrentDeclarativeContribution)
            .Select(entry => entry.TaskId)
            .Concat(executionAudit.RecentReports
                .Where(entry => entry.MatchesCurrentSelection && !entry.MatchesCurrentDeclarativeContribution)
                .Select(entry => entry.TaskId))
            .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        if (currentSelection is not null
            && currentSelection.AdmissionSource.AssignmentMode == "overlay_assignment"
            && divergentContributionTaskIds.Length > 0)
        {
            diagnostics.Add(new RuntimePackMismatchDiagnostic
            {
                DiagnosticCode = "recent_declarative_contributions_diverge_from_current_selection",
                Severity = "warning",
                Summary = "Recent declarative pack contributions diverge from the current selected manifest contribution snapshot.",
                Details = executionAudit.Coverage.Summary,
                RelatedTaskIds = divergentContributionTaskIds,
                RecommendedActions =
                [
                    "inspect runtime-pack-execution-audit",
                    .. divergentContributionTaskIds.Select(taskId => $"inspect runtime-pack-task-explainability {taskId}")
                ],
            });
        }

        return new RuntimePackMismatchDiagnosticsSurface
        {
            CurrentAdmission = currentAdmission,
            CurrentSelection = currentSelection,
            CurrentPolicy = currentPolicy,
            ExecutionCoverage = executionAudit.Coverage,
            Diagnostics = diagnostics,
            Summary = diagnostics.Count == 0
                ? "Runtime-local pack admission, selection, switch policy, and recent execution evidence are aligned."
                : $"Runtime pack mismatch diagnostics reported {diagnostics.Count} bounded issue(s).",
            Notes =
            [
                "Diagnostics stay summary-first and local-runtime scoped.",
                "Suggested next actions are advisory only; the surface does not auto-switch packs or mutate registry state.",
                "Registry, rollout, automatic activation, and multi-pack orchestration remain closed."
            ],
        };
    }

    private static bool MatchesAdmission(RuntimePackAdmissionArtifact admission, RuntimePackSelectionArtifact selection)
    {
        return string.Equals(admission.PackId, selection.PackId, StringComparison.Ordinal)
               && string.Equals(admission.PackVersion, selection.PackVersion, StringComparison.Ordinal)
               && string.Equals(admission.Channel, selection.Channel, StringComparison.Ordinal);
    }
}
