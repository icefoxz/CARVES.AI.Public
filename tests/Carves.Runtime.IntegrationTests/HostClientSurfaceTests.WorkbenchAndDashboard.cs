using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Host;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class HostClientSurfaceTests
{
    [Fact]
    public void Workbench_ReviewText_IgnoresStaleSessionReviewTaskIds()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-WORKBENCH-LIVE");

        var sessionPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json");
        var session = JsonNode.Parse(File.ReadAllText(sessionPath))!.AsObject();
        session["review_pending_task_ids"] = new JsonArray("T-INTEGRATION-WORKBENCH-MISSING", "T-INTEGRATION-WORKBENCH-LIVE");
        session["last_review_task_id"] = "T-INTEGRATION-WORKBENCH-MISSING";
        session["last_task_id"] = "T-INTEGRATION-WORKBENCH-MISSING";
        File.WriteAllText(sessionPath, session.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "workbench", "review");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CARVES Workbench", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("T-INTEGRATION-WORKBENCH-LIVE", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("stale session refs=1", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("T-INTEGRATION-WORKBENCH-MISSING", result.StandardOutput, StringComparison.Ordinal);
    }


    [Fact]
    public async Task Dashboard_CommandReturnsUrlAndLocalhostServesHtml()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "dashboard");
            var url = dashboard.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .First(line => line.StartsWith("Dashboard:", StringComparison.Ordinal))
                .Split(' ', 2)[1];

            using var client = new HttpClient();
            var html = await client.GetStringAsync(url);

            Assert.Equal(0, dashboard.ExitCode);
            Assert.Contains("http://127.0.0.1", dashboard.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("CARVES Local Developer Dashboard", html, StringComparison.Ordinal);
            Assert.Contains("Host Session", html, StringComparison.Ordinal);
            Assert.Contains("Runtime Truth", html, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void HostPauseResume_ProjectsControlStateIntoDiscoveryAndDashboardText()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var pause = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "pause", "operator pause for dashboard validation");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status");
            var dashboard = ProgramHarness.Run("--repo-root", sandbox.RootPath, "dashboard", "--text");
            var resume = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "resume", "operator resume after dashboard validation");

            Assert.Equal(0, pause.ExitCode);
            Assert.Contains("Paused host control", pause.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, status.ExitCode);
            Assert.Contains("Host control state: paused", status.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, dashboard.ExitCode);
            Assert.Contains("Host control: paused", dashboard.StandardOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, resume.ExitCode);
            Assert.Contains("Resumed host control", resume.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public async Task Workbench_CommandReturnsUrlAndLocalhostServesHtml()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-WORKBENCH-HTML");

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");
            var url = workbench.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .First(line => line.StartsWith("Workbench:", StringComparison.Ordinal))
                .Split(' ', 2)[1];

            using var client = new HttpClient();
            var html = await client.GetStringAsync(url);

            Assert.Equal(0, workbench.ExitCode);
            Assert.Contains("http://127.0.0.1", workbench.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("CARVES Workbench", html, StringComparison.Ordinal);
            Assert.Contains("Review Queue", html, StringComparison.Ordinal);
            Assert.Contains("T-INTEGRATION-WORKBENCH-HTML", html, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public async Task Workbench_ActionRoute_UsesSharedReviewAndSyncServices()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-WORKBENCH-ACTION");

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");
            var url = workbench.StandardOutput.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .First(line => line.StartsWith("Workbench:", StringComparison.Ordinal))
                .Split(' ', 2)[1];

            using var client = new HttpClient();
            var response = await client.PostAsync(
                new Uri(new Uri(url), "/workbench/action"),
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["action"] = "approve",
                    ["target_id"] = "T-INTEGRATION-WORKBENCH-ACTION",
                    ["reason"] = "workbench approval proof",
                    ["return_path"] = "/workbench/review",
                }));
            var html = await response.Content.ReadAsStringAsync();

            Assert.True(response.IsSuccessStatusCode);
            Assert.Contains("Action completed", html, StringComparison.Ordinal);
            Assert.Contains("approve", html, StringComparison.Ordinal);

            var nodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-WORKBENCH-ACTION.json");
            var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
            Assert.Equal("completed", taskNode["status"]!.GetValue<string>());

            var reviewArtifactPath = Path.Combine(sandbox.RootPath, ".ai", "artifacts", "reviews", "T-INTEGRATION-WORKBENCH-ACTION.json");
            var reviewArtifact = JsonNode.Parse(File.ReadAllText(reviewArtifactPath))!.AsObject();
            Assert.Equal("approved", reviewArtifact["decision_status"]!.GetValue<string>());

            var sessionPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json");
            var session = JsonNode.Parse(File.ReadAllText(sessionPath))!.AsObject();
            Assert.Empty(session["review_pending_task_ids"]!.AsArray());
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }


    [Fact]
    public void SyncState_ReconcilesStaleReviewBoundarySession()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        sandbox.AddSyntheticReviewTask("T-INTEGRATION-STALE-REVIEW");

        var nodePath = Path.Combine(sandbox.RootPath, ".ai", "tasks", "nodes", "T-INTEGRATION-STALE-REVIEW.json");
        var taskNode = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        taskNode["status"] = "completed";
        var metadata = taskNode["metadata"]?.AsObject() ?? new JsonObject();
        const string runId = "RUN-T-INTEGRATION-STALE-REVIEW-001";
        metadata["execution_run_latest_id"] = runId;
        metadata["execution_run_latest_status"] = "Running";
        metadata["execution_run_active_id"] = runId;
        metadata["execution_run_current_step_index"] = "1";
        metadata["execution_run_current_step_title"] = "Implement the scoped change set.";
        metadata["execution_run_count"] = "1";
        taskNode["metadata"] = metadata;
        File.WriteAllText(nodePath, taskNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var runRoot = Path.Combine(sandbox.RootPath, ".ai", "runtime", "runs", "T-INTEGRATION-STALE-REVIEW");
        Directory.CreateDirectory(runRoot);
        var run = new ExecutionRun
        {
            RunId = runId,
            TaskId = "T-INTEGRATION-STALE-REVIEW",
            Status = ExecutionRunStatus.Running,
            CurrentStepIndex = 1,
            Steps =
            [
                new ExecutionStep
                {
                    StepId = $"{runId}-STEP-001",
                    Title = "Inspect task context and authoritative truth.",
                    Kind = ExecutionStepKind.Inspect,
                    Status = ExecutionStepStatus.Completed,
                    StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                    EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                },
                new ExecutionStep
                {
                    StepId = $"{runId}-STEP-002",
                    Title = "Implement the scoped change set.",
                    Kind = ExecutionStepKind.Implement,
                    Status = ExecutionStepStatus.InProgress,
                    StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                },
            ],
        };
        File.WriteAllText(Path.Combine(runRoot, $"{runId}.json"), JsonSerializer.Serialize(run, CamelCaseJsonOptions));

        var sync = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "sync-state");
        var sessionPath = Path.Combine(sandbox.RootPath, ".ai", "runtime", "live-state", "session.json");
        var session = JsonNode.Parse(File.ReadAllText(sessionPath))!.AsObject();
        var updatedTask = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        var updatedRun = JsonSerializer.Deserialize<ExecutionRun>(File.ReadAllText(Path.Combine(runRoot, $"{runId}.json")), CamelCaseJsonOptions)
            ?? throw new InvalidOperationException("Expected stale execution run to deserialize.");

        Assert.Equal(0, sync.ExitCode);
        Assert.Contains("Reconciled review boundary state", sync.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Reconciled execution run truth", sync.StandardOutput, StringComparison.Ordinal);
        Assert.Empty(session["review_pending_task_ids"]!.AsArray());
        Assert.Equal("Abandoned", updatedTask["metadata"]!["execution_run_latest_status"]!.GetValue<string>());
        Assert.Null(updatedTask["metadata"]!["execution_run_active_id"]);
        Assert.Equal(ExecutionRunStatus.Abandoned, updatedRun.Status);
    }


    [Fact]
    public void HostMethodologyAndAsyncResumeGate_SurfacesAreInspectable()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        var payloadDirectory = Path.Combine(sandbox.RootPath, ".ai", "integration-payloads", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(payloadDirectory);
        var cardPayload = Path.Combine(payloadDirectory, "runtime-card.json");
        File.WriteAllText(cardPayload, JsonSerializer.Serialize(new
        {
            card_id = "CARD-245-INTEGRATION",
            title = "Runtime host governance",
            goal = "Extend the runtime dashboard and host control surface.",
            acceptance = new[] { "host control is inspectable", "run drilldown is visible" },
        }));

        try
        {
            ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");

            var create = ProgramHarness.Run("--repo-root", sandbox.RootPath, "create-card-draft", cardPayload);
            var methodology = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect", "methodology", "CARD-245-INTEGRATION");
            var asyncGate = ProgramHarness.Run("--repo-root", sandbox.RootPath, "inspect", "async-resume-gate");

            Assert.Equal(0, create.ExitCode);
            Assert.Equal(0, methodology.ExitCode);
            Assert.Contains("Methodology applies: True", methodology.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("Coverage:", methodology.StandardOutput, StringComparison.Ordinal);
            Assert.Equal(0, asyncGate.ExitCode);
            Assert.Contains("Resume gate schema:", asyncGate.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("CARD-152", asyncGate.StandardOutput, StringComparison.Ordinal);
        }
        finally
        {
            StopHost(sandbox.RootPath);
        }
    }

}
