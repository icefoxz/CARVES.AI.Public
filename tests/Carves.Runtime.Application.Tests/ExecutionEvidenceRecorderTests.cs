using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionEvidenceRecorderTests
{
    [Fact]
    public void Record_WritesEvidenceEnvelopeAndSupplementalLogs()
    {
        using var workspace = new TemporaryWorkspace();
        var recorder = new ExecutionEvidenceRecorder(workspace.Paths, new StubDiffGitClient());
        var report = new TaskRunReport
        {
            TaskId = "T-CARD-250-001",
            Request = new WorkerRequest
            {
                Task = new TaskNode
                {
                    TaskId = "T-CARD-250-001",
                    Scope = ["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"],
                },
            },
            Session = new ExecutionSession("T-CARD-250-001", "Evidence task", "repo", false, "abc123", "CodexCliWorkerAdapter", workspace.RootPath, DateTimeOffset.UtcNow),
            Patch = new PatchSummary(
                1,
                12,
                4,
                false,
                ["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"]),
            Validation = new ValidationResult
            {
                CommandResults =
                [
                    new CommandExecutionRecord(["dotnet", "build"], 0, "build passed", string.Empty, false, workspace.RootPath, "validation-build", DateTimeOffset.UtcNow),
                    new CommandExecutionRecord(["dotnet", "test"], 0, "test passed", string.Empty, false, workspace.RootPath, "validation-test", DateTimeOffset.UtcNow),
                ],
            },
            WorkerExecution = new WorkerExecutionResult
            {
                TaskId = "T-CARD-250-001",
                RunId = "RUN-T-CARD-250-001-001",
                BackendId = "codex_cli",
                ProviderId = "codex",
                AdapterId = "CodexCliWorkerAdapter",
                Status = WorkerExecutionStatus.Succeeded,
                ChangedFiles = ["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"],
                CommandTrace =
                [
                    new CommandExecutionRecord(["codex", "exec"], 0, "ok", string.Empty, false, workspace.RootPath, "worker", DateTimeOffset.UtcNow),
                ],
            },
        };

        var evidence = recorder.Record(report);
        var runRoot = Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, "RUN-T-CARD-250-001-001");

        Assert.Equal(ExecutionEvidenceSource.Host, evidence.EvidenceSource);
        Assert.Equal(ExecutionEvidenceCompleteness.Complete, evidence.EvidenceCompleteness);
        Assert.Equal(ExecutionEvidenceStrength.Replayable, evidence.EvidenceStrength);
        Assert.Equal(["src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs"], evidence.DeclaredScopeFiles);
        Assert.Contains("dotnet build", evidence.CommandsExecuted);
        Assert.Contains("src/CARVES.Runtime.Application/ControlPlane/ResultIngestionService.cs", evidence.FilesWritten);
        Assert.Equal("abc123", evidence.BaseCommit);
        Assert.False(string.IsNullOrWhiteSpace(evidence.CommandTraceHash));
        Assert.False(string.IsNullOrWhiteSpace(evidence.PatchHash));
        Assert.True(File.Exists(Path.Combine(runRoot, "evidence.json")));
        Assert.True(File.Exists(Path.Combine(runRoot, "command.log")));
        Assert.True(File.Exists(Path.Combine(runRoot, "build.log")));
        Assert.True(File.Exists(Path.Combine(runRoot, "test.log")));
        Assert.True(File.Exists(Path.Combine(runRoot, "patch.diff")));
        Assert.Contains("diff --git", File.ReadAllText(Path.Combine(runRoot, "patch.diff")), StringComparison.Ordinal);
    }

    private sealed class StubDiffGitClient : StubGitClient
    {
        public override bool IsRepository(string repoRoot)
        {
            return true;
        }

        public override string? TryGetUncommittedDiff(string repoRoot, IReadOnlyList<string>? paths = null)
        {
            var target = paths?.FirstOrDefault() ?? "src/example.cs";
            return $"diff --git a/{target} b/{target}{Environment.NewLine}--- a/{target}{Environment.NewLine}+++ b/{target}{Environment.NewLine}@@{Environment.NewLine}+ evidence-backed change";
        }
    }
}
