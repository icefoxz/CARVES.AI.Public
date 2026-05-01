using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeVendorNativeAccelerationService
{
    private readonly string repoRoot;
    private readonly Func<RuntimeAgentWorkingModesSurface> agentWorkingModesFactory;
    private readonly Func<RuntimeFormalPlanningPostureSurface> formalPlanningPostureFactory;
    private readonly RuntimeClaudeWorkerQualificationService claudeWorkerQualificationService;

    public RuntimeVendorNativeAccelerationService(
        string repoRoot,
        Func<RuntimeAgentWorkingModesSurface> agentWorkingModesFactory,
        Func<RuntimeFormalPlanningPostureSurface> formalPlanningPostureFactory,
        RuntimeClaudeWorkerQualificationService? claudeWorkerQualificationService = null)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.agentWorkingModesFactory = agentWorkingModesFactory;
        this.formalPlanningPostureFactory = formalPlanningPostureFactory;
        this.claudeWorkerQualificationService = claudeWorkerQualificationService ?? new RuntimeClaudeWorkerQualificationService();
    }

    public RuntimeVendorNativeAccelerationSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        const string phaseDocumentPath = "docs/runtime/runtime-vendor-native-acceleration.md";
        const string codexGovernanceDocumentPath = "docs/runtime/codex-native-governance-layer.md";
        const string claudeQualificationDocumentPath = "docs/runtime/claude-worker-qualification.md";
        var codexAssets = new[]
        {
            ".codex/config.toml",
            ".codex/rules/carves-control-plane.md",
            ".codex/rules/carves-execution-boundary.md",
            ".codex/skills/carves-runtime/SKILL.md",
        };

        ValidatePath(phaseDocumentPath, "Phase 8 vendor-native acceleration document", errors);
        ValidatePath(codexGovernanceDocumentPath, "Codex native governance document", errors);
        ValidatePath(claudeQualificationDocumentPath, "Claude worker qualification document", errors);

        foreach (var asset in codexAssets)
        {
            ValidatePath(asset, $"Codex governance asset '{asset}'", errors);
        }

        var agentWorkingModes = agentWorkingModesFactory();
        var formalPlanningPosture = formalPlanningPostureFactory();
        var claudeQualification = claudeWorkerQualificationService.BuildSurface();

        if (!agentWorkingModes.IsValid)
        {
            errors.Add("Portable working-mode foundation is invalid; vendor-native reinforcement cannot become ready while runtime-agent-working-modes is blocked.");
        }

        if (!formalPlanningPosture.IsValid)
        {
            errors.Add("Portable formal-planning posture is invalid; vendor-native reinforcement cannot become ready while runtime-formal-planning-posture is blocked.");
        }

        if (string.Equals(agentWorkingModes.CurrentMode, "mode_a_open_repo_advisory", StringComparison.Ordinal))
        {
            warnings.Add("Vendor-native reinforcement is available while the current runtime posture is still the advisory compatibility baseline.");
        }

        var qualifiedClaudeRoutingIntents = claudeQualification.CurrentPolicy.Lanes
            .Where(item => item.Allowed)
            .Select(item => item.RoutingIntent)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var closedClaudeRoutingIntents = claudeQualification.CurrentPolicy.Lanes
            .Where(item => !item.Allowed)
            .Select(item => item.RoutingIntent)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        var codexReady = codexAssets.All(asset => Exists(asset));
        var claudeReady = qualifiedClaudeRoutingIntents.Length > 0 && closedClaudeRoutingIntents.Length > 0;

        return new RuntimeVendorNativeAccelerationSurface
        {
            PhaseDocumentPath = phaseDocumentPath,
            CodexGovernanceDocumentPath = codexGovernanceDocumentPath,
            ClaudeQualificationDocumentPath = claudeQualificationDocumentPath,
            OverallPosture = errors.Count == 0 && codexReady && claudeReady
                ? "optional_vendor_native_acceleration_ready"
                : "blocked_by_vendor_native_acceleration_gaps",
            CurrentMode = agentWorkingModes.CurrentMode,
            PlanningCouplingPosture = agentWorkingModes.PlanningCouplingPosture,
            FormalPlanningPosture = formalPlanningPosture.OverallPosture,
            PlanHandle = formalPlanningPosture.PlanHandle,
            PlanningCardId = formalPlanningPosture.PlanningCardId,
            ManagedWorkspacePosture = formalPlanningPosture.ManagedWorkspacePosture,
            PortableFoundationSummary = $"Portable Runtime foundation remains {agentWorkingModes.CurrentMode} with {formalPlanningPosture.OverallPosture}; vendor-native layers strengthen that posture but never replace CARVES plan, workspace, or writeback gates.",
            CodexReinforcementState = codexReady ? "repo_guard_assets_ready" : "repo_guard_assets_incomplete",
            CodexReinforcementSummary = "Codex App/CLI can be reinforced by repo-local config, rules, and skill assets, but stateful mutations still route through CARVES Host and governed truth ingress.",
            CodexGovernanceAssets = codexAssets,
            ClaudeReinforcementState = claudeReady ? "bounded_runtime_qualification_ready" : "bounded_runtime_qualification_incomplete",
            ClaudeReinforcementSummary = "Claude reinforcement stays bounded to runtime-qualified non-materialized lanes; it strengthens optional local guidance and remote assessment paths without granting patch or writeback parity.",
            ClaudeQualifiedRoutingIntents = qualifiedClaudeRoutingIntents,
            ClaudeClosedRoutingIntents = closedClaudeRoutingIntents,
            RecommendedNextAction = ResolveRecommendedNextAction(agentWorkingModes, formalPlanningPosture, errors),
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This surface does not replace runtime-agent-working-modes, runtime-formal-planning-posture, runtime-managed-workspace, or plan-required/workspace-required gates as the portable foundation.",
                "Codex-native reinforcement remains additive repo governance and does not own CARVES truth writeback or control-plane mutation authority.",
                "Claude-native reinforcement remains bounded qualification for non-materialized lanes and does not grant patch/result submission parity.",
            ],
        };
    }

    private string ResolveRecommendedNextAction(
        RuntimeAgentWorkingModesSurface agentWorkingModes,
        RuntimeFormalPlanningPostureSurface formalPlanningPosture,
        IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            return "Restore the missing vendor-native doctrine or governance assets before treating Phase 8 reinforcement as queryable Runtime posture.";
        }

        if (formalPlanningPosture.MissingPrerequisites.Count > 0)
        {
            return formalPlanningPosture.RecommendedNextAction ?? "Restore the missing formal-planning prerequisites before treating vendor-native reinforcement as ready.";
        }

        if (string.Equals(agentWorkingModes.CurrentMode, "mode_a_open_repo_advisory", StringComparison.Ordinal))
        {
            return "Treat vendor-native acceleration as additive only and move formal work through `plan init` plus a task-bound workspace when you need general hard guarantees.";
        }

        return "Use Codex repo-guard assets and Claude bounded qualification as optional reinforcement only; keep stateful planning, execution, review, and writeback on CARVES Runtime surfaces.";
    }

    private void ValidatePath(string repoRelativePath, string label, List<string> errors)
    {
        if (!Exists(repoRelativePath))
        {
            errors.Add($"{label} is missing at '{repoRelativePath}'.");
        }
    }

    private bool Exists(string repoRelativePath)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fullPath);
    }
}
