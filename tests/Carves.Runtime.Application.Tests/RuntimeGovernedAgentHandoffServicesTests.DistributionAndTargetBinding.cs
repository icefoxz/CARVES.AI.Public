using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed partial class RuntimeGovernedAgentHandoffServicesTests
{
    [Fact]
    public void TargetAgentBootstrapPack_MaterializesMissingFilesWithoutOverwritingRootAgents()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md", "# phase 17");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md", "# phase 18");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md", "# phase 19");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md", "# phase 20");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md", "# phase 21");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md", "# phase 22");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md", "# phase 23");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "# phase 24");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "# phase 25");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "# phase 26a");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md", "# phase 26");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md", "# phase 27");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md", "# phase 28");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md", "# phase 29");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md", "# phase 30");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md", "# phase 31");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", "# phase 32");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md", "# phase 33");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md", "# phase 34");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "# phase 35");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "# phase 36");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md", "# phase 37");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md", "# phase 38");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md", "# phase 39");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", "# phase 40");
        workspace.WriteFile("docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md", "# bootstrap guide");
        workspace.WriteFile("docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md", "# invocation guide");
        workspace.WriteFile("docs/guides/CARVES_CLI_ACTIVATION_PLAN.md", "# activation guide");
        workspace.WriteFile("docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md", "# target dist binding guide");
        workspace.WriteFile("docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md", "# local dist freshness smoke guide");
        workspace.WriteFile("docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md", "# frozen target proof guide");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md", "# problem intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md", "# problem triage");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md", "# problem follow-up");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md", "# problem follow-up decision plan");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md", "# problem follow-up decision record");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md", "# problem follow-up planning intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md", "# problem follow-up planning gate");
        workspace.WriteFile(".ai/runtime.json", "{}");
        const string existingRootAgents = "# Target-owned AGENTS\n\nKeep this text.\n";
        workspace.WriteFile("AGENTS.md", existingRootAgents);

        var service = new RuntimeTargetAgentBootstrapPackService(workspace.RootPath);
        var readOnly = service.Build(writeRequested: false);
        var materialized = service.Build(writeRequested: true);

        Assert.True(readOnly.IsValid);
        Assert.Equal("target_agent_bootstrap_materialization_required", readOnly.OverallPosture);
        Assert.Contains(readOnly.MissingFiles, file => file == ".ai/AGENT_BOOTSTRAP.md");
        Assert.Contains(readOnly.MissingFiles, file => file == ".carves/carves");
        Assert.Contains(readOnly.MissingFiles, file => file == ".carves/AGENT_START.md");
        Assert.Contains(readOnly.MissingFiles, file => file == ".carves/agent-start.json");
        Assert.Contains(readOnly.MissingFiles, file => file == "CARVES_START.md");
        Assert.False(readOnly.RootAgentsContainsCarvesEntry);
        Assert.Equal("target_owned_root_agents_preserved_manual_carves_entry_recommended", readOnly.RootAgentsIntegrationPosture);
        Assert.Contains(".carves/AGENT_START.md", readOnly.RootAgentsSuggestedPatch, StringComparison.Ordinal);
        Assert.Contains(".carves/carves", readOnly.RootAgentsSuggestedPatch, StringComparison.Ordinal);
        Assert.Equal("carves agent bootstrap --write", readOnly.RecommendedNextAction);
        Assert.True(materialized.IsValid);
        Assert.Equal("target_agent_bootstrap_materialized", materialized.OverallPosture);
        Assert.True(materialized.TargetAgentBootstrapExists);
        Assert.True(materialized.RootAgentsExists);
        Assert.True(materialized.ProjectLocalLauncherExists);
        Assert.True(materialized.AgentStartMarkdownExists);
        Assert.True(materialized.AgentStartJsonExists);
        Assert.True(materialized.VisibleAgentStartExists);
        Assert.Contains(materialized.MaterializedFiles, file => file == ".ai/AGENT_BOOTSTRAP.md");
        Assert.Contains(materialized.MaterializedFiles, file => file == ".carves/carves");
        Assert.Contains(materialized.MaterializedFiles, file => file == ".carves/AGENT_START.md");
        Assert.Contains(materialized.MaterializedFiles, file => file == ".carves/agent-start.json");
        Assert.Contains(materialized.MaterializedFiles, file => file == "CARVES_START.md");
        Assert.Contains(materialized.SkippedFiles, file => file == "AGENTS.md");
        Assert.False(materialized.RootAgentsContainsCarvesEntry);
        Assert.Equal("target_owned_root_agents_preserved_manual_carves_entry_recommended", materialized.RootAgentsIntegrationPosture);
        Assert.Contains(".carves/AGENT_START.md", materialized.RootAgentsSuggestedPatch, StringComparison.Ordinal);
        Assert.Contains(".carves/carves", materialized.RootAgentsSuggestedPatch, StringComparison.Ordinal);
        Assert.Equal(existingRootAgents, File.ReadAllText(Path.Combine(workspace.RootPath, "AGENTS.md")));
        var projectLocalLauncherPath = Path.Combine(workspace.RootPath, ".carves", "carves");
        var projectLocalLauncher = File.ReadAllText(projectLocalLauncherPath);
        Assert.Contains(RuntimeCliWrapperPaths.PreferredWrapperPath(workspace.RootPath), projectLocalLauncher, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            Assert.True((File.GetUnixFileMode(projectLocalLauncherPath) & UnixFileMode.UserExecute) != 0);
        }

        var agentStartMarkdown = File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "AGENT_START.md"));
        Assert.Contains(".carves/carves", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains("agent start --json", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains("If the operator says `start CARVES`", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains("Copy/Paste Prompt", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains("Do not plan or edit before that readback.", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains("CARVES_START.md", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains("null_worker", agentStartMarkdown, StringComparison.Ordinal);

        var visibleStart = File.ReadAllText(Path.Combine(workspace.RootPath, "CARVES_START.md"));
        Assert.Contains("start CARVES", visibleStart, StringComparison.Ordinal);
        Assert.Contains("Copy/Paste Prompt", visibleStart, StringComparison.Ordinal);
        Assert.Contains("Do not plan or edit before that readback.", visibleStart, StringComparison.Ordinal);
        Assert.Contains(".carves/AGENT_START.md", visibleStart, StringComparison.Ordinal);
        Assert.Contains(".carves/carves agent start --json", visibleStart, StringComparison.Ordinal);
        Assert.Contains("not Host readiness proof", visibleStart, StringComparison.Ordinal);

        using var agentStartJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "agent-start.json")));
        var agentStartRoot = agentStartJson.RootElement;
        Assert.Equal("carves.agent_start.v1", agentStartRoot.GetProperty("schema_version").GetString());
        Assert.Equal(workspace.RootPath, agentStartRoot.GetProperty("runtime_root").GetString());
        Assert.Equal(workspace.RootPath, agentStartRoot.GetProperty("target_repo_root").GetString());
        Assert.Equal("CARVES_START.md", agentStartRoot.GetProperty("visible_start_file").GetString());
        Assert.Equal(".carves/carves agent start --json", agentStartRoot.GetProperty("first_agent_command").GetString());
        Assert.Equal("start CARVES", agentStartRoot.GetProperty("human_start_prompt").GetString());
        Assert.Contains(
            "Do not plan or edit before that readback.",
            agentStartRoot.GetProperty("copy_paste_prompt").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "read .carves/AGENT_START.md, then run .carves/carves agent start --json",
            agentStartRoot.GetProperty("agent_instruction").GetString());
        Assert.Equal("null_worker_current_version_no_api_sdk_worker_execution", agentStartRoot.GetProperty("worker_execution_boundary").GetString());

        Assert.Contains(".carves/AGENT_START.md", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains(".carves/carves", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("agent start --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot invocation --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot activation --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot dist-smoke --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot dist-binding --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot target-proof --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot residue --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot ignore-plan --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot ignore-record --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot status --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot resources --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot problem-intake --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot triage --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot follow-up --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot dist --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot proof --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot commit-plan", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
        Assert.Contains("pilot closure --json", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md")), StringComparison.Ordinal);
    }


    [Fact]
    public void TargetAgentBootstrapPack_MaterializesMinimalRootAgentsWhenAbsent()
    {
        using var workspace = new TemporaryWorkspace();
        WriteTargetAgentBootstrapPackDependencyDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");

        var service = new RuntimeTargetAgentBootstrapPackService(workspace.RootPath);
        var readOnly = service.Build(writeRequested: false);
        var materialized = service.Build(writeRequested: true);

        Assert.True(readOnly.IsValid);
        Assert.Contains(readOnly.MissingFiles, file => file == "AGENTS.md");
        Assert.Contains(readOnly.MissingFiles, file => file == "CARVES_START.md");
        Assert.False(readOnly.RootAgentsContainsCarvesEntry);
        Assert.Equal("root_agents_missing_can_materialize_minimal_carves_entry", readOnly.RootAgentsIntegrationPosture);
        Assert.Contains(".carves/AGENT_START.md", readOnly.RootAgentsSuggestedPatch, StringComparison.Ordinal);
        Assert.Contains(".carves/carves", readOnly.RootAgentsSuggestedPatch, StringComparison.Ordinal);

        Assert.True(materialized.IsValid);
        Assert.Contains(materialized.MaterializedFiles, file => file == "AGENTS.md");
        Assert.Contains(materialized.MaterializedFiles, file => file == "CARVES_START.md");
        Assert.True(materialized.RootAgentsExists);
        Assert.True(materialized.RootAgentsContainsCarvesEntry);
        Assert.Equal("root_agents_generated_minimal_carves_entry", materialized.RootAgentsIntegrationPosture);
        Assert.Equal(string.Empty, materialized.RootAgentsSuggestedPatch);

        var rootAgents = File.ReadAllText(Path.Combine(workspace.RootPath, "AGENTS.md"));
        Assert.Contains(".carves/AGENT_START.md", rootAgents, StringComparison.Ordinal);
        Assert.Contains(".carves/carves", rootAgents, StringComparison.Ordinal);
        Assert.Contains("agent start --json", rootAgents, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "CARVES_START.md")));

        var targetBootstrap = File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "AGENT_BOOTSTRAP.md"));
        Assert.Contains("prefer `.carves/carves`", targetBootstrap, StringComparison.Ordinal);
        Assert.Contains(".carves/carves gateway status", targetBootstrap, StringComparison.Ordinal);
        Assert.Contains(".carves/carves status --watch --iterations 1 --interval-ms 0", targetBootstrap, StringComparison.Ordinal);
        Assert.Contains("global `carves` shim is only a locator/dispatcher", targetBootstrap, StringComparison.Ordinal);

        var agentStartMarkdown = File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "AGENT_START.md"));
        Assert.Contains("## Is CARVES Running?", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains(".carves/carves gateway status", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains(".carves/carves status --watch --iterations 1 --interval-ms 0", agentStartMarkdown, StringComparison.Ordinal);
        Assert.Contains("Do not use a global `carves` shim as authority inside this target", agentStartMarkdown, StringComparison.Ordinal);

        using var agentStartJson = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "agent-start.json")));
        Assert.Contains(
            agentStartJson.RootElement.GetProperty("visible_gateway_commands").EnumerateArray(),
            command => command.GetString() == ".carves/carves gateway status");
        Assert.Equal(".carves/carves gateway", agentStartJson.RootElement.GetProperty("foreground_gateway_command").GetString());
        Assert.Contains(
            "global carves shim is only a locator/dispatcher",
            agentStartJson.RootElement.GetProperty("global_shim_rule").GetString(),
            StringComparison.Ordinal);

        var visibleStart = File.ReadAllText(Path.Combine(workspace.RootPath, "CARVES_START.md"));
        Assert.Contains(".carves/carves gateway status", visibleStart, StringComparison.Ordinal);
        Assert.Contains(".carves/carves status --watch --iterations 1 --interval-ms 0", visibleStart, StringComparison.Ordinal);
        Assert.Contains("Do not use a global `carves` shim as authority", visibleStart, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetAgentBootstrapPack_RefreshesGeneratedStartProjectionWithoutOverwritingRootAgents()
    {
        using var workspace = new TemporaryWorkspace();
        WriteTargetAgentBootstrapPackDependencyDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        const string existingRootAgents = "# Target-owned AGENTS\n\nKeep this text.\n";
        workspace.WriteFile("AGENTS.md", existingRootAgents);
        workspace.WriteFile(".carves/AGENT_START.md", "# CARVES Agent Start\n\nOld generated start packet.\n");
        workspace.WriteFile(".carves/agent-start.json", """
{
  "schema_version": "carves.agent_start.v1",
  "human_start_prompt": "start CARVES"
}
""");
        workspace.WriteFile("CARVES_START.md", "# Start CARVES\n\nThis file is a visible pointer for coding agents.\n");

        var service = new RuntimeTargetAgentBootstrapPackService(workspace.RootPath);
        var materialized = service.Build(writeRequested: true);

        Assert.True(materialized.IsValid);
        Assert.Equal(existingRootAgents, File.ReadAllText(Path.Combine(workspace.RootPath, "AGENTS.md")));
        Assert.Contains(materialized.MaterializedFiles, file => file == ".carves/AGENT_START.md");
        Assert.Contains(materialized.MaterializedFiles, file => file == ".carves/agent-start.json");
        Assert.Contains(materialized.MaterializedFiles, file => file == "CARVES_START.md");

        Assert.Contains(
            "Do not plan or edit before that readback.",
            File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "AGENT_START.md")),
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not plan or edit before that readback.",
            File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "agent-start.json")),
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not plan or edit before that readback.",
            File.ReadAllText(Path.Combine(workspace.RootPath, "CARVES_START.md")),
            StringComparison.Ordinal);
        Assert.Contains(
            ".carves/carves gateway status",
            File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "AGENT_START.md")),
            StringComparison.Ordinal);
        Assert.Contains(
            "global_shim_rule",
            File.ReadAllText(Path.Combine(workspace.RootPath, ".carves", "agent-start.json")),
            StringComparison.Ordinal);
        Assert.Contains(
            ".carves/carves status --watch --iterations 1 --interval-ms 0",
            File.ReadAllText(Path.Combine(workspace.RootPath, "CARVES_START.md")),
            StringComparison.Ordinal);
    }


    [Fact]
    public void TargetCommitHygiene_ClassifiesOfficialTruthOutputResidueAndUnknownPaths()
    {
        var officialTruth = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/tasks/nodes/T-CARD-001-001.json");
        var attachConfig = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/config/system.json");
        var codegraphProjection = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/codegraph/index.json");
        var opportunityIndex = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/opportunities/index.json");
        var refactoringBacklog = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/refactoring/backlog.json");
        var projectBoundary = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/PROJECT_BOUNDARY.md");
        var ignoreDecisionRecord = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/runtime/target-ignore-decisions/example.json");
        var pilotProblem = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/runtime/pilot-problems/PROBLEM-20260412-020304-abc.json");
        var pilotEvidence = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/runtime/pilot-evidence/PILOT-20260412-020304-def.json");
        var followUpDecision = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/runtime/agent-problem-follow-up-decisions/agent-problem-follow-up-decision-20260412020304-abc.json");
        var targetOutput = RuntimeTargetCommitHygieneService.ProjectPath("M", "docs/agentcoach-product-brief.md");
        var localResidue = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/runtime/live-state/session.json");
        var attachLock = RuntimeTargetCommitHygieneService.ProjectPath("??", ".ai/runtime/attach.lock.json");
        var agentPayload = RuntimeTargetCommitHygieneService.ProjectPath("??", ".carves-agent/problem-report.json");
        var unclassified = RuntimeTargetCommitHygieneService.ProjectPath("??", "secrets/local.txt");

        Assert.Equal("official_target_truth", officialTruth.PathClass);
        Assert.Equal("commit_candidate", officialTruth.CommitPosture);
        Assert.Equal("official_target_truth", attachConfig.PathClass);
        Assert.Equal("official_target_truth", codegraphProjection.PathClass);
        Assert.Equal("official_target_truth", opportunityIndex.PathClass);
        Assert.Equal("official_target_truth", refactoringBacklog.PathClass);
        Assert.Equal("official_target_truth", projectBoundary.PathClass);
        Assert.Equal("official_target_truth", ignoreDecisionRecord.PathClass);
        Assert.Equal("commit_candidate", ignoreDecisionRecord.CommitPosture);
        Assert.Equal("official_target_truth", pilotProblem.PathClass);
        Assert.Equal("commit_candidate", pilotProblem.CommitPosture);
        Assert.Equal("official_target_truth", pilotEvidence.PathClass);
        Assert.Equal("commit_candidate", pilotEvidence.CommitPosture);
        Assert.Equal("official_target_truth", followUpDecision.PathClass);
        Assert.Equal("commit_candidate", followUpDecision.CommitPosture);
        Assert.Equal("target_output_candidate", targetOutput.PathClass);
        Assert.Equal("commit_candidate_after_review_match", targetOutput.CommitPosture);
        Assert.Equal("local_or_tooling_residue", localResidue.PathClass);
        Assert.Equal("exclude_from_commit_by_default", localResidue.CommitPosture);
        Assert.Equal("local_or_tooling_residue", attachLock.PathClass);
        Assert.Equal("exclude_from_commit_by_default", attachLock.CommitPosture);
        Assert.Equal("local_or_tooling_residue", agentPayload.PathClass);
        Assert.Equal("exclude_from_commit_by_default", agentPayload.CommitPosture);
        Assert.Equal("unclassified", unclassified.PathClass);
        Assert.Equal("operator_review_first", unclassified.CommitPosture);
    }


    [Fact]
    public void LocalDistHandoff_ProjectsFrozenDistBinding()
    {
        using var workspace = new TemporaryWorkspace();
        var distRoot = Path.Combine(workspace.RootPath, ".dist", "CARVES.Runtime-0.2.0-beta.1");
        WriteDistFile(distRoot, "docs/runtime/runtime-governed-agent-handoff-proof.md", "# proof");
        WriteDistFile(distRoot, "docs/runtime/runtime-first-run-operator-packet.md", "# first run");
        WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-15-target-commit-closure.md", "# phase 15");
        WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md", "# phase 16");
        WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md", "# phase 23");
        WriteDistFile(distRoot, "docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md", "# local dist freshness smoke guide");
        WriteDistFile(distRoot, "docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md", "# frozen target proof guide");
        WriteDistFile(distRoot, "docs/guides/CARVES_RUNTIME_LOCAL_DIST.md", "# local dist");
        WriteDistFile(distRoot, "docs/guides/CARVES_CLI_DISTRIBUTION.md", "# cli distribution");
        WriteDistFile(distRoot, "docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md", "# invocation contract");
        WriteDistFile(distRoot, "docs/guides/CARVES_CLI_ACTIVATION_PLAN.md", "# activation plan");
        WriteDistFile(distRoot, "docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md", "# target dist binding plan");
        WriteDistFile(distRoot, "VERSION", "0.2.0-beta.1");
        WriteDistFile(distRoot, "carves", "#!/usr/bin/env bash");
        WriteDistFile(distRoot, "carves.ps1", "# wrapper");
        WriteDistFile(distRoot, RuntimeCliWrapperPaths.PublishedCliManifestEntry, "published cli");
        WriteDistFile(
            distRoot,
            "MANIFEST.json",
            JsonSerializer.Serialize(
                new
                {
                    schema_version = "carves-runtime-dist.v1",
                    version = "0.2.0-beta.1",
                    source_commit = "abc123",
                    output_path = distRoot,
                    published_cli_entry = RuntimeCliWrapperPaths.PublishedCliManifestEntry,
                },
                JsonOptions));
        workspace.WriteFile(
            ".ai/runtime/attach-handshake.json",
            JsonSerializer.Serialize(
                new
                {
                    request = new
                    {
                        runtime_root = distRoot,
                    },
                    acknowledgement = new
                    {
                        status = "attached",
                    },
                },
                JsonOptions));

        var surface = new RuntimeLocalDistHandoffService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-local-dist-handoff", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("local_dist_handoff_ready", surface.OverallPosture);
        Assert.Equal("local_dist", surface.RuntimeRootKind);
        Assert.Equal("attach_handshake_runtime_root", surface.RuntimeDocumentRootMode);
        Assert.True(surface.StableExternalConsumptionReady);
        Assert.True(surface.ExternalTargetBoundToRuntimeRoot);
        Assert.False(surface.RuntimeRootMatchesRepoRoot);
        Assert.Equal("0.2.0-beta.1", surface.ManifestVersion);
        Assert.Equal("abc123", surface.ManifestSourceCommit);
        Assert.Contains(surface.RequiredSmokeCommands, command => command == "carves pilot invocation --json");
        Assert.Contains(surface.RequiredSmokeCommands, command => command == "carves pilot activation --json");
        Assert.Contains(surface.RequiredSmokeCommands, command => command == "carves pilot dist-smoke --json");
        Assert.Contains(surface.RequiredSmokeCommands, command => command == "carves pilot dist-binding --json");
        Assert.Contains(surface.RequiredSmokeCommands, command => command == "carves pilot dist --json");
        Assert.Contains(surface.RequiredSmokeCommands, command => command == "carves pilot target-proof --json");
    }


    [Fact]
    public void LocalDistFreshnessSmoke_ProjectsFreshDistManifestAndReadbacks()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("CARVES.Runtime.sln", string.Empty);
        workspace.WriteFile("carves", "#!/usr/bin/env bash");
        workspace.WriteFile("carves.ps1", "# source wrapper");
        workspace.WriteFile("carves.cmd", "@echo off");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline");
        var sourceHead = RunGitCapture(workspace.RootPath, "rev-parse", "HEAD");
        var distRoot = Path.GetFullPath(Path.Combine(workspace.RootPath, "..", ".dist", $"CARVES.Runtime-{RuntimeAlphaVersion.Current}"));

        try
        {
            WriteDistFile(distRoot, "VERSION", RuntimeAlphaVersion.Current);
            WriteDistFile(distRoot, "carves", "#!/usr/bin/env bash");
            WriteDistFile(distRoot, "carves.ps1", "# dist wrapper");
            WriteDistFile(distRoot, RuntimeCliWrapperPaths.PublishedCliManifestEntry, "published cli");
            WriteDistFile(distRoot, RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath, "# phase 22");
            WriteDistFile(distRoot, RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "# local dist freshness smoke guide");
            WriteDistFile(distRoot, RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath, "# phase 23");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "# phase 24");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "# phase 25");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "# phase 26a");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md", "# phase 26");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md", "# phase 27");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md", "# phase 28");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md", "# phase 29");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md", "# phase 30");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md", "# phase 31");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", "# phase 32");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md", "# phase 33");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md", "# phase 34");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "# phase 35");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "# phase 36");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md", "# phase 37");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md", "# phase 38");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md", "# phase 39");
            WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", "# phase 40");
            WriteDistFile(distRoot, RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "# frozen target proof guide");
            WriteDistFile(distRoot, RuntimeTargetDistBindingPlanService.GuideDocumentPath, "# target dist binding guide");
            WriteDistFile(distRoot, RuntimeLocalDistHandoffService.LocalDistGuideDocumentPath, "# local dist guide");
            WriteDistFile(distRoot, RuntimeLocalDistHandoffService.CliDistributionGuideDocumentPath, "# cli distribution guide");
            WriteDistFile(distRoot, RuntimeProductClosurePilotGuideService.GuideDocumentPath, "# pilot guide");
            WriteDistFile(
                distRoot,
                "MANIFEST.json",
                JsonSerializer.Serialize(
                    new
                    {
                        schema_version = "carves-runtime-dist.v1",
                        version = RuntimeAlphaVersion.Current,
                        source_commit = sourceHead,
                        source_repo_root = workspace.RootPath,
                        output_path = distRoot,
                        published_cli_entry = RuntimeCliWrapperPaths.PublishedCliManifestEntry,
                    },
                    JsonOptions));

            var surface = new RuntimeLocalDistFreshnessSmokeService(workspace.RootPath).Build();

            Assert.True(surface.IsValid);
            Assert.Equal("runtime-local-dist-freshness-smoke", surface.SurfaceId);
            Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
            Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.CurrentProductClosureDocumentPath);
            Assert.Equal("docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md", surface.CurrentProductClosureGuideDocumentPath);
            Assert.Equal("local_dist_freshness_smoke_ready", surface.OverallPosture);
            Assert.True(surface.LocalDistFreshnessSmokeReady);
            Assert.True(surface.SourceGitHeadDetected);
            Assert.True(surface.SourceGitWorktreeClean);
            Assert.Equal(sourceHead, surface.SourceGitHead);
            Assert.Equal(distRoot, surface.CandidateDistRoot);
            Assert.Equal(RuntimeAlphaVersion.Current, surface.DistVersion);
            Assert.True(surface.CandidateDistHasPhaseDocument);
            Assert.True(surface.CandidateDistHasPublishedCli);
            Assert.Equal(RuntimeCliWrapperPaths.PublishedCliManifestEntry, surface.CandidateDistPublishedCliEntry);
            Assert.Equal(RuntimeCliWrapperPaths.PublishedCliManifestEntry, surface.ManifestPublishedCliEntry);
            Assert.True(surface.ManifestPublishedCliEntryMatchesPublishedCli);
            Assert.True(surface.CandidateDistHasGuideDocument);
            Assert.True(surface.CandidateDistHasTargetBindingGuide);
            Assert.True(surface.CandidateDistHasLocalDistGuide);
            Assert.True(surface.CandidateDistHasCliDistributionGuide);
            Assert.True(surface.CandidateDistHasCurrentProductClosureDocument);
            Assert.True(surface.CandidateDistHasCurrentProductClosureGuide);
            Assert.True(surface.ManifestSourceCommitMatchesSourceHead);
            Assert.Contains(surface.RequiredSourceCommands, command => command == $".\\scripts\\pack-runtime-dist.ps1 -Version {RuntimeAlphaVersion.Current} -Force");
            Assert.Contains(surface.RequiredSourceCommands, command => command == "carves pilot dist-smoke --json");
            Assert.Contains(surface.RequiredDistReadbackCommands, command => command.Contains("pilot dist-smoke --json", StringComparison.Ordinal));
            Assert.Contains(surface.RequiredDistReadbackCommands, command => command.Contains("pilot dist-binding --json", StringComparison.Ordinal));
            Assert.Contains(surface.RequiredDistReadbackCommands, command => command.Contains("pilot target-proof --json", StringComparison.Ordinal));
            Assert.Contains(surface.RequiredDistReadbackCommands, command => command.Contains("pilot problem-intake --json", StringComparison.Ordinal));
            Assert.Contains(surface.RequiredDistReadbackCommands, command => command.Contains("pilot follow-up --json", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(distRoot))
            {
                Directory.Delete(distRoot, recursive: true);
            }
        }
    }


    [Fact]
    public void TargetDistBindingPlan_ProjectsCandidateDistAndOperatorRetargetCommands()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("CARVES.Runtime.sln", string.Empty);
        workspace.WriteFile("carves", "#!/usr/bin/env bash");
        workspace.WriteFile("carves.ps1", "# wrapper");
        workspace.WriteFile("carves.cmd", "@echo off");

        var distRoot = Path.GetFullPath(Path.Combine(workspace.RootPath, "..", ".dist", $"CARVES.Runtime-{RuntimeAlphaVersion.Current}"));
        WriteDistFile(distRoot, "VERSION", RuntimeAlphaVersion.Current);
        WriteDistFile(distRoot, "carves", "#!/usr/bin/env bash");
        WriteDistFile(distRoot, "carves.ps1", "# dist wrapper");
        WriteDistFile(distRoot, RuntimeCliWrapperPaths.PublishedCliManifestEntry, "published cli");
        WriteDistFile(
            distRoot,
            "MANIFEST.json",
            JsonSerializer.Serialize(
                new
                {
                    schema_version = "carves-runtime-dist.v1",
                    version = RuntimeAlphaVersion.Current,
                    source_commit = "phase21abc",
                    output_path = distRoot,
                    published_cli_entry = RuntimeCliWrapperPaths.PublishedCliManifestEntry,
                },
                JsonOptions));

        var surface = new RuntimeTargetDistBindingPlanService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-target-dist-binding-plan", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("target_dist_binding_plan_ready_for_initial_attach", surface.OverallPosture);
        Assert.True(surface.DistBindingPlanComplete);
        Assert.Equal("initial_attach_via_local_dist", surface.RecommendedBindingMode);
        Assert.Equal(distRoot, surface.CandidateDistRoot);
        Assert.True(surface.CandidateDistExists);
        Assert.True(surface.CandidateDistHasManifest);
        Assert.True(surface.CandidateDistHasVersion);
        Assert.True(surface.CandidateDistHasWrapper);
        Assert.Equal(RuntimeAlphaVersion.Current, surface.CandidateDistVersion);
        Assert.Equal("phase21abc", surface.CandidateDistSourceCommit);
        Assert.Contains(surface.OperatorBindingCommands, command => command.Contains("init . --json", StringComparison.Ordinal));
        Assert.Contains(surface.OperatorBindingCommands, command => command.Contains("pilot dist-smoke --json", StringComparison.Ordinal));
        Assert.Contains(surface.OperatorBindingCommands, command => command.Contains("pilot dist-binding --json", StringComparison.Ordinal));
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot dist-smoke --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot dist-binding --json");
        Assert.Contains(surface.Gaps, gap => gap == "target_runtime_not_initialized");
    }


    [Fact]
    public void FrozenDistTargetReadbackProof_CompletesForInitializedTargetBoundToFreshDist()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceRoot = Path.Combine(workspace.RootPath, "runtime-source");
        Directory.CreateDirectory(sourceRoot);
        WriteDistFile(sourceRoot, "README.md", "# source\n");
        RunGit(sourceRoot, "init");
        RunGit(sourceRoot, "config", "user.email", "carves-tests@example.invalid");
        RunGit(sourceRoot, "config", "user.name", "CARVES Tests");
        RunGit(sourceRoot, "add", ".");
        RunGit(sourceRoot, "commit", "-m", "source baseline");
        var sourceHead = RunGitCapture(sourceRoot, "rev-parse", "HEAD");

        var distRoot = Path.Combine(workspace.RootPath, ".dist", "CARVES.Runtime-0.2.0-beta.1");
        WriteFrozenDistProofRuntimeResources(distRoot);
        WriteDistFile(
            distRoot,
            "MANIFEST.json",
            JsonSerializer.Serialize(
                new
                {
                    schema_version = "carves-runtime-dist.v1",
                    version = "0.2.0-beta.1",
                    source_commit = sourceHead,
                    source_repo_root = sourceRoot,
                    output_path = distRoot,
                    published_cli_entry = RuntimeCliWrapperPaths.PublishedCliManifestEntry,
                },
                JsonOptions));

        var targetRoot = Path.Combine(workspace.RootPath, "target");
        Directory.CreateDirectory(targetRoot);
        WriteDistFile(targetRoot, "PROJECT.md", "# target\n");
        WriteDistFile(
            targetRoot,
            ".ai/runtime.json",
            JsonSerializer.Serialize(new { runtime_root = distRoot }, JsonOptions));
        WriteDistFile(
            targetRoot,
            ".ai/runtime/attach-handshake.json",
            JsonSerializer.Serialize(
                new
                {
                    request = new
                    {
                        runtime_root = distRoot,
                    },
                    acknowledgement = new
                    {
                        status = "attached",
                    },
                },
                JsonOptions));
        WriteDistFile(targetRoot, ".ai/AGENT_BOOTSTRAP.md", "# bootstrap\n");
        WriteDistFile(targetRoot, "AGENTS.md", "# agents\n");
        WriteProjectLocalAgentEntry(targetRoot, distRoot);
        RunGit(targetRoot, "init");
        RunGit(targetRoot, "config", "user.email", "carves-tests@example.invalid");
        RunGit(targetRoot, "config", "user.name", "CARVES Tests");
        RunGit(targetRoot, "add", ".");
        RunGit(targetRoot, "commit", "-m", "target baseline");

        var surface = new RuntimeFrozenDistTargetReadbackProofService(targetRoot).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-frozen-dist-target-readback-proof", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("frozen_dist_target_readback_proof_complete", surface.OverallPosture);
        Assert.True(surface.FrozenDistTargetReadbackProofComplete);
        Assert.True(surface.CliInvocationContractComplete);
        Assert.True(surface.CliActivationPlanComplete);
        Assert.True(surface.TargetAgentBootstrapReady);
        Assert.True(surface.LocalDistFreshnessSmokeReady);
        Assert.True(surface.TargetDistBindingPlanComplete);
        Assert.True(surface.TargetBoundToLocalDist);
        Assert.True(surface.StableExternalConsumptionReady);
        Assert.Equal("local_dist", surface.RuntimeRootKind);
        Assert.True(surface.RuntimeInitialized);
        Assert.True(surface.GitRepositoryDetected);
        Assert.True(surface.TargetGitWorktreeClean);
        Assert.Empty(surface.Gaps);
        Assert.Contains(surface.RequiredSourceReadbackCommands, command => command == "carves pilot target-proof --json");
        Assert.Contains(surface.RequiredTargetReadbackCommands, command => command == "carves pilot target-proof --json");
    }


    [Fact]
    public void ProductPilotProof_AggregatesDistAndCommitClosureGapsWithoutParentGitDiscovery()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var surface = new RuntimeProductPilotProofService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-product-pilot-proof", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("product_pilot_proof_waiting_for_local_dist_freshness_smoke", surface.OverallPosture);
        Assert.False(surface.ProductPilotProofComplete);
        Assert.False(surface.LocalDistFreshnessSmokeReady);
        Assert.False(surface.StableExternalConsumptionReady);
        Assert.False(surface.GitRepositoryDetected);
        Assert.Contains(surface.Gaps, gap => gap == "local_dist_freshness_smoke_not_ready");
        Assert.Contains(surface.Gaps, gap => gap == "stable_external_consumption_not_ready");
        Assert.Contains(surface.Gaps, gap => gap == "target_git_repository_not_detected");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot dist-smoke --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot residue --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot ignore-plan --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot ignore-record --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot proof --json");
    }

    private static void WriteTargetAgentBootstrapPackDependencyDocs(TemporaryWorkspace workspace)
    {
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md", "# phase 17");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md", "# phase 18");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md", "# phase 19");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md", "# phase 20");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md", "# phase 21");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md", "# phase 22");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md", "# phase 23");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "# phase 24");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "# phase 25");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "# phase 26a");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md", "# phase 26");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-27-target-residue-policy.md", "# phase 27");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md", "# phase 28");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md", "# phase 29");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md", "# phase 30");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md", "# phase 31");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", "# phase 32");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md", "# phase 33");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md", "# phase 34");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "# phase 35");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "# phase 36");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md", "# phase 37");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md", "# phase 38");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md", "# phase 39");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", "# phase 40");
        workspace.WriteFile("docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md", "# bootstrap guide");
        workspace.WriteFile("docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md", "# invocation guide");
        workspace.WriteFile("docs/guides/CARVES_CLI_ACTIVATION_PLAN.md", "# activation guide");
        workspace.WriteFile("docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md", "# target dist binding guide");
        workspace.WriteFile("docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md", "# local dist freshness smoke guide");
        workspace.WriteFile("docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md", "# frozen target proof guide");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md", "# problem intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md", "# problem triage");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md", "# problem follow-up");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md", "# problem follow-up decision plan");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md", "# problem follow-up decision record");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md", "# problem follow-up planning intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md", "# problem follow-up planning gate");
    }

}
