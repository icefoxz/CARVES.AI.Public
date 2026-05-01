using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeMarkdownReadPathBudgetServiceTests
{
    [Fact]
    public void Build_DefersGeneratedMarkdownViewsAfterInitialization()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# repo");
        workspace.WriteFile("AGENTS.md", "# agents");
        workspace.WriteFile(".ai/STATE.md", "# state");
        workspace.WriteFile(".ai/CURRENT_TASK.md", "# current");
        workspace.WriteFile(".ai/TASK_QUEUE.md", new string('q', 8_000));
        workspace.WriteFile("docs/task.md", "# task");
        workspace.WriteFile("docs/guides/AGENT_APPLIED_GOVERNANCE_TEST.md", "# governance");

        var bootstrap = new RuntimeAgentBootstrapPacketSurface
        {
            Packet = new AgentBootstrapPacket
            {
                HotPathContext = new AgentBootstrapHotPathContext
                {
                    CurrentTaskId = "T-BUDGET-001",
                    MarkdownReadPolicy = new AgentMarkdownReadPolicy
                    {
                        RequiredInitialSources = ["README.md", "AGENTS.md"],
                        EscalationTriggers = ["mixed diff judgment"],
                        ReadTiers =
                        [
                            new AgentMarkdownReadTier
                            {
                                TierId = "deep_governance_escalation",
                                Sources = ["docs/guides/AGENT_APPLIED_GOVERNANCE_TEST.md"],
                            },
                        ],
                    },
                },
            },
        };
        var overlay = new RuntimeAgentTaskBootstrapOverlaySurface
        {
            Overlay = new AgentTaskBootstrapOverlay
            {
                TaskId = "T-BUDGET-001",
                MarkdownReadGuidance = new AgentTaskMarkdownReadGuidance
                {
                    TaskScopedMarkdownRefs = ["docs/task.md"],
                    EscalationTriggers = ["task scope ambiguity"],
                },
            },
        };

        var surface = new RuntimeMarkdownReadPathBudgetService(workspace.RootPath, ControlPlanePaths.FromRepoRoot(workspace.RootPath))
            .Build(bootstrap, overlay, "T-BUDGET-001");

        Assert.Equal("runtime-markdown-read-path-budget", surface.SurfaceId);
        Assert.True(surface.PostInitializationDefault.WithinBudget);
        Assert.Equal(0, surface.PostInitializationDefault.EstimatedDefaultMarkdownTokens);
        Assert.True(surface.GeneratedMarkdownViews.DeferredMarkdownTokens > 0);
        Assert.Contains(surface.DefaultMachineReadPath, command => command == "carves agent context --json");
        Assert.Contains(surface.DeferredMarkdownSources, source => source.Contains(".ai/TASK_QUEUE.md", StringComparison.Ordinal));
        Assert.Contains(surface.EscalationTriggers, trigger => trigger == "mixed diff judgment");
        Assert.Contains(surface.EscalationTriggers, trigger => trigger == "task scope ambiguity");

        var taskQueue = Assert.Single(surface.Items, item => item.Path == ".ai/TASK_QUEUE.md");
        Assert.Equal("defer_after_initialization", taskQueue.ReadAction);
        Assert.True(taskQueue.OverSingleFileBudget);

        var taskRef = Assert.Single(surface.Items, item => item.Path == "docs/task.md");
        Assert.Equal("targeted_read_when_named_by_overlay", taskRef.ReadAction);
        Assert.Equal("carves api runtime-agent-task-overlay T-BUDGET-001", taskRef.ReplacementSurface);

        Assert.Contains(surface.NonClaims, claim =>
            claim.Contains("does not read large Markdown bodies", StringComparison.Ordinal));
    }
}
