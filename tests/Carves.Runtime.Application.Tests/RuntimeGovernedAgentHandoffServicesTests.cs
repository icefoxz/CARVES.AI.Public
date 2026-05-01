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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static PilotProblemIntakeRecord CreateBlockingPilotProblem(string repoRoot)
    {
        return new PilotProblemIntakeRecord
        {
            ProblemId = "PROBLEM-20260412-020304-ghi",
            EvidenceId = "EVIDENCE-20260412-020304-jkl",
            RepoRoot = repoRoot,
            RepoId = "target-repo",
            CurrentStageId = "workspace_submit",
            ProblemKind = "protected_truth_root_requested",
            Severity = "blocking",
            Summary = "Agent was asked to edit protected truth directly.",
            BlockedCommand = "manual edit .ai/tasks/graph.json",
            RecommendedFollowUp = "Open governed Runtime follow-up work.",
            Status = "recorded",
            RecordedAtUtc = DateTimeOffset.Parse("2026-04-12T02:03:04Z"),
        };
    }

    private static TaskGraphService CreateTaskGraphService(params TaskNode[] tasks)
    {
        return new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph(tasks)),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
    }

    private static IntentDiscoveryService CreateIntentDiscoveryService(TemporaryWorkspace workspace)
    {
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, workspace.Paths, systemConfig);
        var query = new FileCodeGraphQueryService(workspace.Paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, workspace.Paths, systemConfig, builder, query);
        return new IntentDiscoveryService(
            workspace.RootPath,
            workspace.Paths,
            new JsonIntentDraftRepository(workspace.Paths),
            understanding);
    }

    private static void WritePilotStatusDocs(TemporaryWorkspace workspace)
    {
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md", "# phase 8");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md", "# phase 9");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md", "# phase 10");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md", "# phase 11b");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md", "# phase 12");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md", "# phase 13");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-14-target-commit-plan.md", "# phase 14");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-15-target-commit-closure.md", "# phase 15");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md", "# phase 16");
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
        workspace.WriteFile("docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md", "# guide");
        workspace.WriteFile("docs/guides/CARVES_PRODUCTIZED_PILOT_STATUS.md", "# status");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md", "# problem intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md", "# problem triage");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md", "# problem follow-up");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md", "# problem follow-up decision plan");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md", "# problem follow-up decision record");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md", "# problem follow-up planning intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md", "# problem follow-up planning gate");
        workspace.WriteFile("docs/guides/CARVES_TARGET_AGENT_BOOTSTRAP_PACK.md", "# bootstrap guide");
        workspace.WriteFile("docs/guides/CARVES_RUNTIME_LOCAL_DIST.md", "# local dist");
        workspace.WriteFile("docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md", "# local dist freshness smoke guide");
        workspace.WriteFile("docs/guides/CARVES_CLI_DISTRIBUTION.md", "# cli distribution");
        workspace.WriteFile("docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md", "# external consumer resource pack");
        workspace.WriteFile("docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md", "# external agent quickstart");
        workspace.WriteFile("docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md", "# cli invocation contract");
        workspace.WriteFile("docs/guides/CARVES_CLI_ACTIVATION_PLAN.md", "# cli activation plan");
        workspace.WriteFile("docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md", "# target dist binding plan");
        workspace.WriteFile("docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md", "# frozen target proof guide");
        workspace.WriteFile("docs/runtime/runtime-governed-agent-handoff-proof.md", "# handoff proof");
        workspace.WriteFile("carves", "#!/usr/bin/env bash");
        workspace.WriteFile("carves.ps1", "# wrapper");
        workspace.WriteFile("carves.cmd", "@echo off");
    }

    private static void WriteFrozenDistProofRuntimeResources(string distRoot)
    {
        WriteDistFile(distRoot, "VERSION", "0.2.0-beta.1");
        WriteDistFile(distRoot, "carves", "#!/usr/bin/env bash");
        WriteDistFile(distRoot, "carves.ps1", "# wrapper");
        WriteDistFile(distRoot, "carves.cmd", "@echo off");
        WriteDistFile(distRoot, RuntimeCliWrapperPaths.PublishedCliManifestEntry, "published cli");
        WriteDistFile(distRoot, "docs/runtime/runtime-governed-agent-handoff-proof.md", "# handoff proof");
        WriteDistFile(distRoot, "docs/runtime/runtime-first-run-operator-packet.md", "# first run");
        WriteDistFile(distRoot, RuntimeTargetCommitHygieneService.PhaseDocumentPath, "# phase 13");
        WriteDistFile(distRoot, RuntimeTargetCommitPlanService.PhaseDocumentPath, "# phase 14");
        WriteDistFile(distRoot, RuntimeTargetCommitClosureService.PhaseDocumentPath, "# phase 15");
        WriteDistFile(distRoot, RuntimeLocalDistHandoffService.PhaseDocumentPath, "# phase 16");
        WriteDistFile(distRoot, "docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md", "# phase 17");
        WriteDistFile(distRoot, RuntimeProductPilotProofService.PhaseDocumentPath, "# phase 28");
        WriteDistFile(distRoot, RuntimeExternalConsumerResourcePackService.PhaseDocumentPath, "# phase 18");
        WriteDistFile(distRoot, RuntimeCliInvocationContractService.PhaseDocumentPath, "# phase 19");
        WriteDistFile(distRoot, RuntimeCliActivationPlanService.PhaseDocumentPath, "# phase 20");
        WriteDistFile(distRoot, RuntimeTargetDistBindingPlanService.PhaseDocumentPath, "# phase 21");
        WriteDistFile(distRoot, RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath, "# phase 22");
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
        WriteDistFile(distRoot, RuntimeProductClosurePilotGuideService.GuideDocumentPath, "# pilot guide");
        WriteDistFile(distRoot, RuntimeProductClosurePilotStatusService.GuideDocumentPath, "# pilot status");
        WriteDistFile(distRoot, RuntimeAgentProblemIntakeService.ProblemIntakeGuideDocumentPath, "# problem intake");
        WriteDistFile(distRoot, RuntimeAgentProblemTriageLedgerService.TriageLedgerGuideDocumentPath, "# problem triage");
        WriteDistFile(distRoot, RuntimeAgentProblemFollowUpCandidatesService.FollowUpCandidatesGuideDocumentPath, "# problem follow-up");
        WriteDistFile(distRoot, RuntimeAgentProblemFollowUpDecisionPlanService.DecisionPlanGuideDocumentPath, "# problem follow-up decision plan");
        WriteDistFile(distRoot, RuntimeAgentProblemFollowUpDecisionRecordService.DecisionRecordGuideDocumentPath, "# problem follow-up decision record");
        WriteDistFile(distRoot, RuntimeAgentProblemFollowUpPlanningIntakeService.PlanningIntakeGuideDocumentPath, "# problem follow-up planning intake");
        WriteDistFile(distRoot, RuntimeAgentProblemFollowUpPlanningGateService.PlanningGateGuideDocumentPath, "# problem follow-up planning gate");
        WriteDistFile(distRoot, RuntimeTargetAgentBootstrapPackService.GuideDocumentPath, "# bootstrap guide");
        WriteDistFile(distRoot, RuntimeLocalDistHandoffService.LocalDistGuideDocumentPath, "# local dist guide");
        WriteDistFile(distRoot, RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "# local dist freshness smoke guide");
        WriteDistFile(distRoot, RuntimeTargetDistBindingPlanService.GuideDocumentPath, "# target dist binding guide");
        WriteDistFile(distRoot, RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "# frozen target proof guide");
        WriteDistFile(distRoot, RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "# invocation guide");
        WriteDistFile(distRoot, RuntimeCliInvocationContractService.CliDistributionGuideDocumentPath, "# cli distribution guide");
        WriteDistFile(distRoot, RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "# activation guide");
        WriteDistFile(distRoot, RuntimeExternalConsumerResourcePackService.ResourcePackGuideDocumentPath, "# resource pack guide");
        WriteDistFile(distRoot, RuntimeExternalTargetPilotStartService.QuickstartGuideDocumentPath, "# external agent quickstart");
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var result = RunGitProcess(workingDirectory, arguments);
        Assert.Equal(0, result.ExitCode);
    }

    private static string RunGitCapture(string workingDirectory, params string[] arguments)
    {
        var result = RunGitProcess(workingDirectory, arguments);
        Assert.Equal(0, result.ExitCode);
        return result.StandardOutput.Trim();
    }

    private static GitProcessResult RunGitProcess(string workingDirectory, params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.WorkingDirectory = workingDirectory;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new GitProcessResult(process.ExitCode, standardOutput, standardError);
    }

    private static void WriteDistFile(string distRoot, string relativePath, string content)
    {
        var fullPath = Path.Combine(distRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static void WriteProjectLocalAgentEntry(string repoRoot, string? runtimeRoot = null)
    {
        var resolvedRuntimeRoot = runtimeRoot ?? repoRoot;
        var fullPath = Path.Combine(
            repoRoot,
            RuntimeTargetAgentBootstrapPackService.ProjectLocalLauncherPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(
            fullPath,
            RuntimeTargetAgentBootstrapPackService.BuildProjectLocalLauncherContent(resolvedRuntimeRoot));
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            File.SetUnixFileMode(
                fullPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        WriteDistFile(
            repoRoot,
            RuntimeTargetAgentBootstrapPackService.AgentStartMarkdownPath,
            RuntimeTargetAgentBootstrapPackService.BuildAgentStartMarkdownContent(resolvedRuntimeRoot, repoRoot));
        WriteDistFile(
            repoRoot,
            RuntimeTargetAgentBootstrapPackService.AgentStartJsonPath,
            RuntimeTargetAgentBootstrapPackService.BuildAgentStartJsonContent(resolvedRuntimeRoot, repoRoot));
        WriteDistFile(
            repoRoot,
            RuntimeTargetAgentBootstrapPackService.VisibleAgentStartPath,
            RuntimeTargetAgentBootstrapPackService.BuildVisibleAgentStartContent(resolvedRuntimeRoot, repoRoot));
    }

    private sealed record GitProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
