using System.Xml.Linq;
using Carves.Runtime.Application.Guard;

namespace Carves.Runtime.Application.Tests;

public sealed class AlphaGuardReleaseCheckpointTests
{
    [Fact]
    public void ReleaseCheckpoint_MatchesSourceMarkerAndPackageVersion()
    {
        var repoRoot = ResolveRepoRoot();
        var releaseNotePath = Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar));
        var releaseNote = File.ReadAllText(releaseNotePath);
        var project = XDocument.Load(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "carves.csproj"));

        Assert.Equal("alpha-guard-0.2.0-beta.1", GuardReleaseInfo.CheckpointId);
        Assert.Equal("0.2.0-beta.1", GuardReleaseInfo.RuntimePackageVersion);
        Assert.Equal("0.2.0-beta.1", ReadProperty(project, "Version"));
        Assert.Contains(GuardReleaseInfo.CheckpointId, releaseNote, StringComparison.Ordinal);
        Assert.Contains($"Runtime package version: `{GuardReleaseInfo.RuntimePackageVersion}`", releaseNote, StringComparison.Ordinal);
        Assert.Contains(GuardReleaseInfo.PolicySchemaVersion, File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "release-checkpoint.md")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(GuardReleaseInfo.ScopeContractPath, releaseNote, StringComparison.Ordinal);
        Assert.Contains(GuardReleaseInfo.BetaReleaseCheckpointPath, releaseNote, StringComparison.Ordinal);
        Assert.Equal("docs/beta/guard-beta-release-checkpoint.md", GuardReleaseInfo.BetaReleaseCheckpointPath);
        Assert.Equal("docs/release/alpha-guard-0.2.0-beta.1.md", GuardReleaseInfo.ReleaseNotePath);
    }

    [Fact]
    public void BetaScopeContract_DocumentsStableEntryExperimentalRunAndNonGoals()
    {
        var repoRoot = ResolveRepoRoot();
        var scopeContract = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ScopeContractPath.Replace('/', Path.DirectorySeparatorChar)));
        var requiredPhrases = new[]
        {
            "Stable Beta External Entry",
            "carves guard check --json",
            "carves guard audit",
            "carves guard report",
            "carves guard explain <run-id>",
            "carves guard run <task-id> --json",
            "experimental",
            "OS sandbox containment",
            "semantic review",
            "AI planning",
            "token optimization",
            "Runtime governance onboarding",
            "guard-policy.v1",
            "docs/release/alpha-guard-0.1.0-alpha.2.md",
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, scopeContract, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReleaseCheckpoint_DocumentsBetaIdentityAndPreservesAlphaHistory()
    {
        var repoRoot = ResolveRepoRoot();
        var releaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));
        var alphaReleaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.PreviousAlphaReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));
        var requiredPhrases = new[]
        {
            "Beta release checkpoint complete for external pilot use",
            "Stable Beta External Entry",
            "Beta Release Checkpoint",
            "docs/beta/guard-beta-release-checkpoint.md",
            "ready for external pilots through diff-only `guard check`",
            "carves guard check --json",
            "carves guard audit",
            "carves guard report",
            "carves guard explain <run-id>",
            "carves guard run <task-id> --json",
            "experimental",
            "not OS-level containment",
            "does not intercept system calls",
            "does not automatically roll back arbitrary writes",
            "Semantic correctness, architecture quality, AI planning, token optimization, and Runtime governance onboarding are outside the Beta scope",
            "Cross-platform packaged install proof, remote registry posture, policy upgrade compatibility, decision-store hardening, external pilot matrix, CI proof lane, Beta threat model, task-aware `guard run` posture, and final Beta release checkpoint have evidence in this checkpoint line",
            "No remaining Beta gate blocks `0.2.0-beta.1` external pilot use through diff-only `guard check`",
            "Before production, CARVES still needs",
            "docs/release/alpha-guard-0.1.0-alpha.2.md",
            "docs/beta/guard-install-and-distribution.md",
            "docs/beta/guard-external-pilot-matrix.md",
            "docs/beta/guard-beta-ci-proof-lane.md",
            "docs/beta/guard-beta-threat-model.md",
            "docs/beta/guard-run-beta-posture.md",
            "scripts/beta/guard-packaged-install-smoke.ps1",
            "scripts/beta/guard-external-pilot-matrix.ps1",
            "scripts/beta/guard-beta-proof-lane.ps1",
            "0 pilot-discovered block-level product issues",
            "policy/process-level patch admission",
            "This is not remote registry publication",
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, releaseNote, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(GuardReleaseInfo.PreviousAlphaCheckpointId, alphaReleaseNote, StringComparison.Ordinal);
        Assert.Contains("Alpha checkpoint, not production/stable readiness.", alphaReleaseNote, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseCheckpoint_DefaultDocsDoNotExposeRuntimeGovernanceSprawl()
    {
        var repoRoot = ResolveRepoRoot();
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var alphaReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "README.md"));
        var guardDocs = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "README.md"));
        var wikiHome = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "Home.md"));
        var wikiIndex = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "README.md"));
        var beginnerZh = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "guard-beginner-guide.zh-CN.md"));
        var beginnerEn = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "guard-beginner-guide.en.md"));
        var glossaryZh = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "glossary.zh-CN.md"));
        var glossaryEn = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "glossary.en.md"));
        var workflowZh = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "workflow.zh-CN.md"));
        var workflowEn = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "workflow.en.md"));
        var githubActionsZh = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "github-actions.zh-CN.md"));
        var githubActionsEn = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "wiki", "github-actions.en.md"));
        var publicDocs = new[] { readme, alphaReadme, guardDocs, wikiHome, wikiIndex, beginnerZh, beginnerEn, glossaryZh, glossaryEn, workflowZh, workflowEn, githubActionsZh, githubActionsEn };
        var forbiddenDefaultTerms = new[]
        {
            "CARVES.Runtime",
            "Runtime",
            "taskgraph",
            "task-aware",
            "guard run",
            "resident host",
            "sibling",
            "CARVES.Kernel",
            "CARVES.Operator",
            "CARVES.Unity",
            "governance",
            "control-plane",
            ".ai/tasks",
            ".ai/memory",
            "CARVES.Runtime.Cli",
            "runtime-governance-program-reaudit",
            "runtime-governance-surface-coverage-audit",
            "Wave 50",
            "Wave 60",
            "Wave 70",
        };

        foreach (var document in publicDocs)
        {
            foreach (var term in forbiddenDefaultTerms)
            {
                Assert.DoesNotContain(term, document, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Contains("CARVES.Guard", readme, StringComparison.Ordinal);
        Assert.Contains("docs/guard/README.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/guard/wiki/guard-beginner-guide.zh-CN.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/guard/wiki/guard-beginner-guide.en.md", readme, StringComparison.Ordinal);
        Assert.Contains("docs/guard/wiki/github-actions.zh-CN.md", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void HardeningCheckpoint_RecordsClosedGapsVerificationAndBetaPosture()
    {
        var repoRoot = ResolveRepoRoot();
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "hardening-checkpoint.md"));
        var alphaReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "README.md"));
        var releaseCheckpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "release-checkpoint.md"));
        var releaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));
        var requiredPhrases = new[]
        {
            "alpha-guard-hardening-2026-04-14",
            "Status: Alpha hardening complete for the stable external diff-only entry.",
            "CARD-735",
            "CARD-736",
            "CARD-737",
            "CARD-738",
            "CARD-739",
            "Passed: 42/42",
            "Passed: 15/15",
            "smoke: alpha_guard_packaged_install",
            "remote_registry_published: false",
            "truth_required: task=False; card=False; graph=False",
            "Alpha Guard is not OS sandbox containment.",
            "Task-aware mode is experimental",
            "Beta planning may begin for the stable diff-only Guard external entry.",
            "remote registry publication",
            "cross-platform packaged install smoke",
            "production safety language",
            "External users do not need Runtime wave history",
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, checkpoint, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("CARVES.Guard", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("carves guard check --json", alphaReadme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docs/guard/README.md", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("guard-beginner-guide.zh-CN.md", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("guard-beginner-guide.en.md", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("docs/alpha/hardening-checkpoint.md", releaseCheckpoint, StringComparison.Ordinal);
        Assert.Contains("docs/alpha/hardening-checkpoint.md", releaseNote, StringComparison.Ordinal);
        Assert.Contains("Phase 2 evidence covers cross-platform packaged install proof", releaseCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase 3 evidence covers three external repo shapes", releaseCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase 4 evidence covers focused Guard Application tests", releaseCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase 5 evidence freezes production safety language", releaseCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase 6 evidence decides task-aware `guard run` posture", releaseCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase 7 evidence records the final Beta release checkpoint", releaseCheckpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(GuardReleaseInfo.BetaReleaseCheckpointPath, releaseCheckpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void BetaThreatModel_DocumentsNarrowSafetyClaimAndNonClaims()
    {
        var repoRoot = ResolveRepoRoot();
        var threatModel = File.ReadAllText(Path.Combine(repoRoot, "docs", "beta", "guard-beta-threat-model.md"));
        var safetyModel = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "safety-model.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var requiredPhrases = new[]
        {
            "Status: Phase 5 Beta safety language frozen.",
            "policy/process-level patch admission control",
            "already materialized git patch",
            "not a real-time execution sandbox",
            "OS-level sandbox containment",
            "syscall interception",
            "Windows job-object containment",
            "filesystem virtualization",
            "network isolation",
            "prevention of every write before it happens",
            "automatic rollback of arbitrary writes",
            "semantic correctness of generated code",
            "Task-aware mode also does not become universal OS containment.",
            "This is a local sidecar read model",
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, threatModel, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("docs/beta/guard-beta-threat-model.md", safetyModel, StringComparison.Ordinal);
        Assert.Contains("not an operating-system sandbox", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BetaGuardRunPosture_DecidesExperimentalModeAndPromotionProof()
    {
        var repoRoot = ResolveRepoRoot();
        var posture = File.ReadAllText(Path.Combine(repoRoot, "docs", "beta", "guard-run-beta-posture.md"));
        var releaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));
        var cliGuardSource = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Guard.cs"));
        var cliHelpSource = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Help.cs"));
        var requiredPhrases = new[]
        {
            "Status: Phase 6 Beta task-aware posture decided.",
            "Decision: keep `carves guard run <task-id> --json` experimental for `0.2.0-beta.1`.",
            "stable external Beta entry remains",
            "mode_stability=experimental",
            "stable_external_entry=false",
            "requires_runtime_task_truth=true",
            "Do not promote guard run by wording only.",
            "deterministic live-worker proof",
            "WorkerExecutionBoundaryService",
            "SafetyService",
            "PacketEnforcementService",
            "TaskStatusTransitionPolicy",
            "GuardRunDecisionService",
            "no live provider network access",
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, posture, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("docs/beta/guard-run-beta-posture.md", releaseNote, StringComparison.Ordinal);
        Assert.Contains("stable_external_entry = false", cliGuardSource, StringComparison.Ordinal);
        Assert.Contains("stable external Beta entry", cliGuardSource, StringComparison.Ordinal);
        Assert.Contains("experimental for this Beta", cliHelpSource, StringComparison.Ordinal);
    }

    [Fact]
    public void BetaReleaseCheckpoint_DocumentsFinalReadinessLimitationsAndProductionGap()
    {
        var repoRoot = ResolveRepoRoot();
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.BetaReleaseCheckpointPath.Replace('/', Path.DirectorySeparatorChar)));
        var releaseNote = File.ReadAllText(Path.Combine(repoRoot, GuardReleaseInfo.ReleaseNotePath.Replace('/', Path.DirectorySeparatorChar)));
        var alphaReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "alpha", "README.md"));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var requiredPhrases = new[]
        {
            "Status: `0.2.0-beta.1` Beta checkpoint complete.",
            "Alpha Guard `0.2.0-beta.1` is ready for external pilots through the stable diff-only Guard entry.",
            "This is Beta readiness, not production readiness.",
            "carves guard check --json",
            "carves guard audit",
            "carves guard report",
            "carves guard explain <run-id>",
            "does not require Runtime task truth",
            "remote registry publication is deferred",
            "latest local proof lane verdict is `passed`",
            "Beta identity and version freeze",
            "external pilot matrix",
            "Beta threat model",
            "task-aware `guard run` posture",
            "Task-aware Runtime execution remains experimental",
            "mode_stability=experimental",
            "stable_external_entry=false",
            "Diff-only mode evaluates changes after another tool has already written them into the working tree",
            "No remaining Beta gate blocks `0.2.0-beta.1` external pilot use through diff-only `guard check`.",
            "Production readiness remains separate work.",
            "remote registry or approved distribution channel decision",
            "explicit support policy",
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, checkpoint, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(GuardReleaseInfo.BetaReleaseCheckpointPath, releaseNote, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard", alphaReadme, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard", readme, StringComparison.Ordinal);
        Assert.Contains("carves guard check --json", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("production ready", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OS-level sandbox containment is supported", checkpoint, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadProperty(XDocument project, string propertyName)
    {
        return project
            .Descendants(propertyName)
            .Select(element => element.Value.Trim())
            .FirstOrDefault();
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
