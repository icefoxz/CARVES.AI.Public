namespace Carves.Runtime.Application.Tests;

public sealed class AlphaGuardTrustBasisCoverageAuditTests
{
    [Fact]
    public void TrustBasisAudit_ListsEveryAlphaGuardRuleId()
    {
        var audit = ReadAlphaAudit();
        var requiredRuleIds = new[]
        {
            "policy.invalid",
            "policy.unsupported_schema_version",
            "diff.empty",
            "git.status_failed",
            "git.diff_failed",
            "path.protected_prefix",
            "path.outside_allowed_prefix",
            "budget.max_changed_files",
            "budget.max_total_additions",
            "budget.max_total_deletions",
            "budget.max_renames",
            "budget.max_file_additions",
            "budget.max_file_deletions",
            "dependency.manifest_without_lockfile",
            "dependency.new_dependency.unverified",
            "dependency.lockfile_without_manifest",
            "shape.rename_with_content_change",
            "shape.delete_without_replacement",
            "shape.generated_path",
            "shape.missing_tests_for_source_changes",
            "shape.mixed_feature_and_refactor",
            "runtime.execution_boundary_denied",
            "runtime.safety_blocked",
            "runtime.packet_enforcement_blocked",
            "runtime.execution_blocked",
            "runtime.execution_review_required",
        };

        foreach (var ruleId in requiredRuleIds)
        {
            Assert.Contains(ruleId, audit, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TrustBasisAudit_MapsRequiredIntegrationSurfacesAndResidualRisks()
    {
        var audit = ReadAlphaAudit();
        var requiredTerms = new[]
        {
            "GuardDiffAdapter",
            "GuardPolicyEvaluator",
            "WorkerExecutionBoundaryService",
            "SafetyService",
            "PacketEnforcementService",
            "TaskStatusTransitionPolicy",
            "guard check",
            "guard run",
            "experimental",
            "mode_stability=experimental",
            "stable_external_entry=false",
            "docs/beta/guard-run-beta-posture.md",
            "docs/beta/guard-beta-strict-audit-hardening.md",
            "GuardDiffAdapterTests",
            "RG-015",
            "RG-016",
            "guard explain",
            "Residual Safety Limitations",
            "not OS sandbox containment",
            "does not intercept system calls",
            "does not automatically roll back arbitrary writes",
            "RG-002",
        };

        foreach (var term in requiredTerms)
        {
            Assert.Contains(term, audit, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AlphaDocs_StateNonOsSandboxLimitation()
    {
        var repoRoot = ResolveRepoRoot();
        var alphaReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "README.md"));
        var safetyModel = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "safety-model.md"));

        Assert.Contains("not an operating-system sandbox", alphaReadme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not an OS-level sandbox", safetyModel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Diff-only mode", safetyModel, StringComparison.Ordinal);
        Assert.Contains("Task-aware mode", safetyModel, StringComparison.Ordinal);
    }

    private static string ReadAlphaAudit()
    {
        return File.ReadAllText(Path.Combine(ResolveRepoRoot(), "docs", "alpha", "trust-basis-coverage-audit.md"));
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
