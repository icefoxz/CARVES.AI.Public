using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentGovernanceKernel(RuntimeAgentGovernanceKernelSurface surface)
    {
        var policy = surface.Policy;
        var lines = new List<string>
        {
            "Runtime agent governance kernel",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Policy version: {policy.PolicyVersion}",
            $"Summary: {policy.Summary}",
            "Governed entry order:",
        };

        foreach (var step in policy.GovernedEntryOrder)
        {
            lines.Add($"- {step}");
        }

        lines.Add("Path families:");
        foreach (var family in policy.PathFamilies.OrderBy(item => item.FamilyId, StringComparer.Ordinal))
        {
            lines.Add($"- {family.FamilyId} [{family.LifecycleClass} | {family.CommitClass} | {family.CorrectAction}]");
            lines.Add($"  patterns: {string.Join(" | ", family.PathPatterns)}");
            lines.Add($"  summary: {family.Summary}");
        }

        lines.Add("Mixed roots:");
        foreach (var root in policy.MixedRoots.OrderBy(item => item.RootPath, StringComparer.Ordinal))
        {
            lines.Add($"- {root.RootPath}");
            lines.Add($"  summary: {root.Summary}");
            lines.Add($"  child exceptions: {(root.ChildExceptions.Length == 0 ? "(none)" : string.Join(" | ", root.ChildExceptions))}");
        }

        lines.Add("Governance-boundary docs:");
        foreach (var document in policy.GovernanceBoundaryDocPatterns.OrderBy(item => item, StringComparer.Ordinal))
        {
            lines.Add($"- {document}");
        }

        lines.Add("Unclassified default:");
        lines.Add($"- lifecycle: {policy.UnclassifiedDefault.LifecycleClass}");
        lines.Add($"  commit: {policy.UnclassifiedDefault.CommitClass}");
        lines.Add($"  action: {policy.UnclassifiedDefault.CorrectAction}");
        lines.Add($"  summary: {policy.UnclassifiedDefault.Summary}");

        lines.Add("Initialization contract:");
        lines.Add($"- report heading: {policy.InitializationContract.ReportHeading}");
        lines.Add($"  source heading: {policy.InitializationContract.SourcesHeading}");
        lines.Add($"  required fields: {string.Join(" | ", policy.InitializationContract.RequiredFields)}");
        lines.Add($"  source fields: {string.Join(" | ", policy.InitializationContract.SourceFields)}");
        lines.Add($"  entry path shape: {policy.InitializationContract.EntryPathShape}");
        lines.Add($"  runtime state shape: {policy.InitializationContract.RuntimeStateShape}");

        lines.Add("Applied governance contract:");
        lines.Add($"- judgment heading: {policy.AppliedGovernanceContract.JudgmentHeading}");
        lines.Add($"  table columns: {string.Join(" | ", policy.AppliedGovernanceContract.TableColumns)}");
        lines.Add($"  verdict fields: {string.Join(" | ", policy.AppliedGovernanceContract.VerdictFields)}");
        lines.Add($"  automatic fail conditions: {string.Join(" | ", policy.AppliedGovernanceContract.AutomaticFailConditions)}");

        lines.Add("Bootstrap packet contract:");
        lines.Add($"- surface id: {policy.BootstrapPacketContract.SurfaceId}");
        lines.Add($"  startup mode: {policy.BootstrapPacketContract.StartupMode}");
        lines.Add($"  required packet fields: {string.Join(" | ", policy.BootstrapPacketContract.RequiredPacketFields)}");
        lines.Add($"  default inspect commands: {string.Join(" | ", policy.BootstrapPacketContract.DefaultInspectCommands)}");

        lines.Add("Warm-resume contract:");
        lines.Add($"- surface id: {policy.WarmResumeContract.SurfaceId}");
        lines.Add($"  resume modes: {string.Join(" | ", policy.WarmResumeContract.ResumeModes)}");
        lines.Add($"  required receipt fields: {string.Join(" | ", policy.WarmResumeContract.RequiredReceiptFields)}");
        lines.Add($"  default inspect commands: {string.Join(" | ", policy.WarmResumeContract.DefaultInspectCommands)}");
        lines.Add($"  invalidation triggers: {string.Join(" | ", policy.WarmResumeContract.InvalidationTriggers)}");

        lines.Add("Task overlay contract:");
        lines.Add($"- surface id: {policy.TaskOverlayContract.SurfaceId}");
        lines.Add($"  required overlay fields: {string.Join(" | ", policy.TaskOverlayContract.RequiredOverlayFields)}");
        lines.Add($"  default inspect commands: {string.Join(" | ", policy.TaskOverlayContract.DefaultInspectCommands)}");

        lines.Add("Model profiles:");
        foreach (var profile in policy.ModelProfiles.OrderBy(item => item.ProfileId, StringComparer.Ordinal))
        {
            lines.Add($"- {profile.ProfileId}: ceiling={profile.GovernanceCeiling}; max_startup_sources={profile.MaxStartupSources}; requires_task_overlay={profile.RequiresTaskOverlay}; deep_governance_ready={profile.DeepGovernanceReady}");
            lines.Add($"  startup surfaces: {string.Join(" | ", profile.StartupSurfaces)}");
            lines.Add($"  allowed actions: {string.Join(" | ", profile.AllowedActions)}");
            lines.Add($"  forbidden actions: {string.Join(" | ", profile.ForbiddenActions)}");
        }

        lines.Add("Loop/stall guard:");
        lines.Add($"- detector window: {policy.LoopStallGuardPolicy.DetectorWindow}");
        lines.Add($"  detection lineage: {policy.LoopStallGuardPolicy.DetectionLineage}");
        lines.Add($"  strict profiles: {string.Join(" | ", policy.LoopStallGuardPolicy.StrictProfileIds)}");
        foreach (var rule in policy.LoopStallGuardPolicy.Rules.OrderBy(item => item.PatternType, StringComparer.Ordinal))
        {
            lines.Add($"  - {rule.PatternType}: standard={rule.StandardOutcome}; weak={rule.WeakOutcome}");
        }

        lines.Add("Weak execution lanes:");
        foreach (var lane in policy.WeakExecutionLanes.OrderBy(item => item.LaneId, StringComparer.Ordinal))
        {
            lines.Add($"- {lane.LaneId}: profile={lane.ModelProfileId}; task_types={string.Join(" | ", lane.AllowedTaskTypes)}");
            lines.Add($"  runtime surfaces: {string.Join(" | ", lane.RequiredRuntimeSurfaces)}");
            lines.Add($"  stop conditions: {string.Join(" | ", lane.StopConditions)}");
        }

        lines.Add("Not yet proven:");
        foreach (var item in policy.NotYetProven)
        {
            lines.Add($"- {item}");
        }

        if (policy.Notes.Length > 0)
        {
            lines.Add("Notes:");
            foreach (var note in policy.Notes)
            {
                lines.Add($"- {note}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
