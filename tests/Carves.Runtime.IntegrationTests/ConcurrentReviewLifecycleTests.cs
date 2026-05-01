using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class ConcurrentReviewLifecycleTests
{
    [Fact]
    public void ApproveReview_ReleasesOnlyRelatedBlockersWhileOtherReviewRemainsPending()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-CONCURRENT-201", ["tests/Concurrent/A"]);
        sandbox.AddSyntheticReviewTask("T-CONCURRENT-202", ["tests/Concurrent/B"]);
        sandbox.AddSyntheticReviewTask("T-CONCURRENT-203", ["tests/Concurrent/A/Child"]);
        sandbox.AddSyntheticReviewTask("T-CONCURRENT-204", ["tests/Concurrent/D"]);

        var sessionPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json");
        var sessionSeed = JsonNode.Parse(File.ReadAllText(sessionPath))!.AsObject();
        sessionSeed["review_pending_task_ids"] = new JsonArray("T-CONCURRENT-201", "T-CONCURRENT-202", "T-CONCURRENT-203", "T-CONCURRENT-204");
        File.WriteAllText(sessionPath, sessionSeed.ToJsonString(new() { WriteIndented = true }));

        var approve = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "approve-review", "T-CONCURRENT-201", "Approved", "one", "review");
        var taskTwoJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-CONCURRENT-202.json")))!.AsObject();
        var taskThreeJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-CONCURRENT-203.json")))!.AsObject();
        var taskFourJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-CONCURRENT-204.json")))!.AsObject();
        var reviewOneJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", "T-CONCURRENT-201.json")))!.AsObject();
        var sessionJson = JsonNode.Parse(File.ReadAllText(Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json")))!.AsObject();
        var reviewPending = sessionJson["review_pending_task_ids"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();

        Assert.Equal(0, approve.ExitCode);
        Assert.Equal("review", taskTwoJson["status"]!.GetValue<string>());
        Assert.Equal("review", taskThreeJson["status"]!.GetValue<string>());
        Assert.Equal("review", taskFourJson["status"]!.GetValue<string>());
        Assert.Equal("approved", reviewOneJson["decision_status"]!.GetValue<string>());
        Assert.DoesNotContain("T-CONCURRENT-201", reviewPending);
        Assert.Contains("T-CONCURRENT-202", reviewPending);
        Assert.Contains("T-CONCURRENT-203", reviewPending);
        Assert.Contains("T-CONCURRENT-204", reviewPending);
    }
}
