using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeDefaultWorkflowProofServiceTests
{
    [Fact]
    public void Build_SeparatesStructuralProofFromCurrentRuntimeBlockers()
    {
        using var workspace = new TemporaryWorkspace();
        WriteDefaultWorkflowDocs(workspace);

        var surface = new RuntimeDefaultWorkflowProofService(workspace.RootPath).Build(
            BuildThreadStart(threadStartReady: false),
            BuildShortContext(),
            BuildMarkdownBudget(),
            BuildCoverage(),
            BuildResourcePack());

        Assert.Equal("runtime-default-workflow-proof", surface.SurfaceId);
        Assert.True(surface.WorkflowProofComplete);
        Assert.False(surface.CurrentRuntimeReady);
        Assert.Equal("default_workflow_proof_ready_current_repo_blocked", surface.OverallPosture);
        Assert.Empty(surface.StructuralGaps);
        Assert.Contains(surface.CurrentRuntimeBlockers, gap => gap == "runtime_not_initialized");
        Assert.Equal(0, surface.PostInitializationMarkdownTokens);
        Assert.True(surface.GovernanceSurfaceCoverageComplete);
        Assert.Contains(surface.DefaultPath, step => step.StepId == "first_thread_start" && step.Command == "carves agent start --json");
        Assert.Contains(surface.WarmPath, step => step.StepId == "warm_reorientation" && step.Command == "carves agent context --json");
        Assert.Contains(surface.OptionalProofAndTroubleshootingPath, step => step.SurfaceId == "runtime-default-workflow-proof");
        Assert.Contains(surface.NonClaims, claim =>
            claim.Contains("does not claim the current repo or target is ready", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_FlagsMissingDefaultWorkflowDocumentationAsStructuralGap()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md", "carves agent start --json");
        workspace.WriteFile("docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md", "carves agent context --json");

        var surface = new RuntimeDefaultWorkflowProofService(workspace.RootPath).Build(
            BuildThreadStart(threadStartReady: true),
            BuildShortContext(),
            BuildMarkdownBudget(),
            BuildCoverage(),
            BuildResourcePack());

        Assert.False(surface.WorkflowProofComplete);
        Assert.Equal("default_workflow_proof_blocked_by_structural_gaps", surface.OverallPosture);
        Assert.Contains(surface.StructuralGaps, gap => gap == "default_workflow_check_failed:quickstart_documents_default_workflow");
        Assert.Contains(surface.StructuralGaps, gap => gap == "default_workflow_check_failed:consumer_pack_documents_default_workflow");
    }

    private static void WriteDefaultWorkflowDocs(TemporaryWorkspace workspace)
    {
        workspace.WriteFile(
            "docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md",
            """
            carves agent start --json
            carves agent context --json
            carves api runtime-default-workflow-proof
            runtime-default-workflow-proof
            """);
        workspace.WriteFile(
            "docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md",
            """
            carves agent start --json
            carves agent context --json
            carves api runtime-default-workflow-proof
            runtime-default-workflow-proof
            """);
    }

    private static RuntimeAgentThreadStartSurface BuildThreadStart(bool threadStartReady)
    {
        return new RuntimeAgentThreadStartSurface
        {
            OverallPosture = threadStartReady ? "agent_thread_start_ready" : "agent_thread_start_blocked",
            ThreadStartReady = threadStartReady,
            OneCommandForNewThread = "carves agent start --json",
            NextGovernedCommand = "carves init [target-path] --json",
            NextCommandSource = "pilot_status",
            CurrentStageId = "attach_target",
            CurrentStageStatus = threadStartReady ? "ready" : "blocked",
            Gaps = threadStartReady ? [] : ["runtime_not_initialized"],
            MinimalAgentRules = ["Use the troubleshooting readbacks only when the single start payload reports a gap."],
            TroubleshootingReadbacks = ["carves pilot start --json"],
        };
    }

    private static RuntimeAgentShortContextSurface BuildShortContext()
    {
        return new RuntimeAgentShortContextSurface
        {
            ShortContextReady = true,
            PrimaryCommands =
            [
                new RuntimeAgentShortContextCommandRef
                {
                    Command = "carves agent context --json",
                    SurfaceId = "runtime-agent-short-context",
                },
            ],
        };
    }

    private static RuntimeMarkdownReadPathBudgetSurface BuildMarkdownBudget()
    {
        return new RuntimeMarkdownReadPathBudgetSurface
        {
            WithinBudget = true,
            PostInitializationDefault = new MarkdownReadPathBudgetSummary
            {
                MaxDefaultMarkdownTokens = 900,
                EstimatedDefaultMarkdownTokens = 0,
                WithinBudget = true,
            },
            GeneratedMarkdownViews = new MarkdownReadPathBudgetSummary
            {
                DeferredMarkdownTokens = 2400,
                WithinBudget = true,
            },
            DefaultMachineReadPath = ["carves agent context --json"],
        };
    }

    private static RuntimeGovernanceSurfaceCoverageAuditSurface BuildCoverage()
    {
        return new RuntimeGovernanceSurfaceCoverageAuditSurface
        {
            CoverageComplete = true,
            RequiredSurfaceCount = 12,
            CoveredSurfaceCount = 12,
            BlockingGapCount = 0,
        };
    }

    private static RuntimeExternalConsumerResourcePackSurface BuildResourcePack()
    {
        return new RuntimeExternalConsumerResourcePackSurface
        {
            CommandEntries =
            [
                BuildCommand("carves agent start --json", "runtime-agent-thread-start"),
                BuildCommand("carves agent context --json", "runtime-agent-short-context"),
                BuildCommand("carves api runtime-markdown-read-path-budget", "runtime-markdown-read-path-budget"),
                BuildCommand("carves api runtime-governance-surface-coverage-audit", "runtime-governance-surface-coverage-audit"),
                BuildCommand("carves api runtime-default-workflow-proof", "runtime-default-workflow-proof"),
            ],
        };
    }

    private static RuntimeExternalConsumerCommandEntrySurface BuildCommand(string command, string surfaceId)
    {
        return new RuntimeExternalConsumerCommandEntrySurface
        {
            Command = command,
            SurfaceId = surfaceId,
            AuthorityClass = "read_only",
            ConsumerUse = "test default workflow command",
        };
    }
}
