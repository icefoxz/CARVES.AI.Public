using System.Text.Json;
using System.Text.RegularExpressions;

namespace Carves.Runtime.IntegrationTests;

internal static class FormalPlanningScenarioHarness
{
    public static (string CardId, string TaskId, string TaskGraphDraftId) ProvisionPlanBoundTaskWithoutWorkspaceLease(
        RepoSandbox sandbox,
        string taskTitle,
        string taskDescription,
        IReadOnlyList<string> scope,
        IReadOnlyList<string> acceptance)
    {
        var acceptedIntentPath = Path.Combine(sandbox.RootPath, ".ai", "memory", "PROJECT.md");
        var draftPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "intent_draft.json");
        if (File.Exists(acceptedIntentPath))
        {
            File.Delete(acceptedIntentPath);
        }

        if (File.Exists(draftPath))
        {
            File.Delete(draftPath);
        }

        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "draft", "--persist").ExitCode);
        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "focus", "candidate-first-slice").ExitCode);
        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_validation_artifact", "resolved").ExitCode);
        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "decision", "first_slice_boundary", "resolved").ExitCode);
        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "intent", "candidate", "candidate-first-slice", "ready_to_plan").ExitCode);
        Assert.Equal(0, ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "init").ExitCode);

        var exportCardPath = Path.Combine(sandbox.RootPath, "drafts", "plan-card.json");
        var exportCard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "plan", "export-card", exportCardPath);
        Assert.Equal(0, exportCard.ExitCode);

        var createCard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "create-card-draft", exportCardPath);
        Assert.Equal(0, createCard.ExitCode);
        var cardId = ExtractFirstMatch(createCard.StandardOutput, @"Created card draft (?<card_id>CARD-[A-Za-z0-9-]+)\.", "card_id");

        var approveCard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-card", cardId, "approved for phase-5 gate proof");
        Assert.Equal(0, approveCard.ExitCode);

        var taskId = $"T-{cardId}-001";
        var taskGraphPayloadPath = Path.Combine(sandbox.RootPath, "drafts", "taskgraph-draft.json");
        File.WriteAllText(
            taskGraphPayloadPath,
            JsonSerializer.Serialize(
                new
                {
                    card_id = cardId,
                    tasks = new object[]
                    {
                        new
                        {
                            task_id = taskId,
                            title = taskTitle,
                            description = taskDescription,
                            scope = scope.ToArray(),
                            acceptance = acceptance.ToArray(),
                            proof_target = new
                            {
                                kind = "focused_behavior",
                                description = "Prove phase-5 plan and managed workspace gates from plan-bound task truth.",
                            },
                        },
                    },
                },
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                    WriteIndented = true,
                }));

        var createTaskGraph = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "create-taskgraph-draft", taskGraphPayloadPath);
        Assert.Equal(0, createTaskGraph.ExitCode);
        var taskGraphDraftId = ExtractFirstMatch(createTaskGraph.StandardOutput, @"Created taskgraph draft (?<draft_id>TG-[A-Za-z0-9-]+) for", "draft_id");

        var approveTaskGraph = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-taskgraph-draft", taskGraphDraftId, "approved for phase-5 gate proof");
        Assert.Equal(0, approveTaskGraph.ExitCode);

        return (cardId, taskId, taskGraphDraftId);
    }

    private static string ExtractFirstMatch(string text, string pattern, string groupName)
    {
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant);
        Assert.True(match.Success, text);
        return match.Groups[groupName].Value;
    }
}
