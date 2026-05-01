using System.Text.Json;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Platform;

internal static class RuntimeWorkingModesSemantics
{
    public static string ResolveCurrentMode(RuntimeManagedWorkspaceSurface workspaceSurface)
    {
        if (workspaceSurface.ActiveLeases.Count > 0
            && string.Equals(workspaceSurface.ModeDHardeningState, "active", StringComparison.Ordinal))
        {
            return "mode_d_scoped_task_workspace";
        }

        return workspaceSurface.OverallPosture switch
        {
            "task_bound_workspace_active" => "mode_c_task_bound_workspace",
            "task_bound_workspace_ready_to_issue" => "mode_c_task_bound_workspace",
            "waiting_for_bound_task_truth_before_managed_workspace_issuance" => "mode_c_task_bound_workspace",
            _ => "mode_a_open_repo_advisory",
        };
    }

    public static string ResolveCurrentModeSummary(string currentMode, RuntimeManagedWorkspaceSurface workspaceSurface)
    {
        return currentMode switch
        {
            "mode_d_scoped_task_workspace" => $"Scoped task workspace is active on posture '{workspaceSurface.OverallPosture}', and Mode D hardening fails closed on host-only, deny, or scope-escape writes before official ingress.",
            "mode_c_task_bound_workspace" => $"Managed workspace posture is '{workspaceSurface.OverallPosture}', so task-bound workspace issuance is the current hard-mode lane while official truth remains host-routed.",
            _ => "Open-repo advisory mode remains the active compatibility baseline until formal planning and managed workspace issuance move work into a leased workspace.",
        };
    }

    public static string ResolveStrongestRuntimeSupportedMode(RuntimeManagedWorkspaceSurface workspaceSurface)
    {
        return workspaceSurface.IsValid
               && string.Equals(workspaceSurface.PathPolicyEnforcementState, "active", StringComparison.Ordinal)
            ? "mode_e_brokered_execution"
            : "mode_c_task_bound_workspace";
    }

    public static string ResolvePlanningCouplingPosture(
        IntentDiscoveryStatus status,
        FormalPlanningPacket? packet,
        RuntimeManagedWorkspaceSurface workspaceSurface)
    {
        if (workspaceSurface.ActiveLeases.Count > 0
            || packet?.LinkedTruth.TaskIds.Count > 0)
        {
            return "p2_task_bound_guidance";
        }

        if (status.Draft?.ActivePlanningCard is not null || packet is not null)
        {
            return "p3_host_mediated_planning";
        }

        return status.Draft is null
            ? "p0_passive_guidance"
            : "p1_bootstrapped_guidance";
    }

    public static string ResolvePlanningCouplingSummary(
        string planningCouplingPosture,
        IntentDiscoveryStatus status,
        FormalPlanningPacket? packet,
        RuntimeManagedWorkspaceSurface workspaceSurface)
    {
        return planningCouplingPosture switch
        {
            "p2_task_bound_guidance" => workspaceSurface.ActiveLeases.Count > 0
                ? $"Formal planning entered through `plan init` and now projects task-bound guidance through packet '{packet?.PlanHandle ?? "(none)"}' and {workspaceSurface.ActiveLeases.Count} active workspace lease(s)."
                : $"Formal planning entered through `plan init` and now projects task-bound guidance through packet '{packet?.PlanHandle ?? "(none)"}' even though no active workspace lease exists yet.",
            "p3_host_mediated_planning" => $"Formal planning is mediated by one active planning card '{status.Draft?.ActivePlanningCard?.PlanningCardId ?? packet?.PlanningCardId ?? "(none)"}' before taskgraph persistence, workspace issuance, or governed execution continue.",
            "p1_bootstrapped_guidance" => "Runtime has a live intent draft and guided-planning context, but formal planning has not yet entered one active planning card.",
            _ => "Runtime doctrine and read surfaces are available, but planning remains advisory until the agent or operator enters `plan init`.",
        };
    }

    public static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
