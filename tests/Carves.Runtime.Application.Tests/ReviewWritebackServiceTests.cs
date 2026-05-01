using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Git;
using Carves.Runtime.Infrastructure.Processes;
using System.Diagnostics;

namespace Carves.Runtime.Application.Tests;

public sealed class ReviewWritebackServiceTests
{
    [Fact]
    public void Apply_CopiesApprovedFilesFromDelegatedWorktree()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ReviewWritebackService(workspace.RootPath, new StubGitClient());
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-001");
        var relativePath = "src/Synthetic/ApprovedWriteback.cs";
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "ApprovedWriteback.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class ApprovedWriteback {}");

        var attempt = service.Apply(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-001",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-001",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-001",
                    WorktreePath = worktreePath,
                    FilesWritten = [relativePath],
                },
            });

        var targetPath = Path.Combine(workspace.RootPath, "src", "Synthetic", "ApprovedWriteback.cs");
        Assert.True(attempt.CanProceed);
        Assert.True(attempt.Record.Applied);
        Assert.Null(attempt.Record.ResultCommit);
        Assert.Equal([relativePath], attempt.Record.Files);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("namespace Synthetic; public sealed class ApprovedWriteback {}", File.ReadAllText(targetPath));
        Assert.False(Directory.Exists(worktreePath));
    }

    [Fact]
    public void Apply_BlocksProtectedControlPlaneWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ReviewWritebackService(workspace.RootPath, new StubGitClient());
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-002");
        var sourcePath = Path.Combine(worktreePath, ".ai", "tasks", "graph.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "{}");

        var attempt = service.Apply(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-002",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-002",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-002",
                    WorktreePath = worktreePath,
                    FilesWritten = [".ai/tasks/graph.json"],
                },
            });

        Assert.False(attempt.CanProceed);
        Assert.Contains("protected or invalid path", attempt.FailureMessage, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, ".ai", "tasks", "graph.json")));
    }

    [Fact]
    public void Apply_AllowsRuntimeFollowUpDecisionRecordWhenManagedWorkspaceLeaseAllowsPath()
    {
        using var workspace = new TemporaryWorkspace();
        var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
        var relativePath = ".ai/runtime/agent-problem-follow-up-decisions/agent-problem-follow-up-decision-20260413043139-test.json";
        leaseRepository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases =
            [
                new ManagedWorkspaceLease
                {
                    LeaseId = "lease-writeback-runtime-001",
                    TaskId = "T-WRITEBACK-RUNTIME-001",
                    WorkspacePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-RUNTIME-001"),
                    RepoRoot = workspace.RootPath,
                    Status = ManagedWorkspaceLeaseStatus.Active,
                    AllowedWritablePaths = [relativePath],
                },
            ],
        });
        var service = new ReviewWritebackService(
            workspace.RootPath,
            new StubGitClient(),
            new ManagedWorkspacePathPolicyService(workspace.RootPath, leaseRepository));
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-RUNTIME-001");
        var sourcePath = Path.Combine(
            worktreePath,
            ".ai",
            "runtime",
            "agent-problem-follow-up-decisions",
            "agent-problem-follow-up-decision-20260413043139-test.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "{\"decision\":\"accept_as_governed_planning_input\"}");

        var attempt = service.Apply(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-RUNTIME-001",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-RUNTIME-001",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-RUNTIME-001",
                    WorktreePath = worktreePath,
                    FilesWritten = [relativePath],
                },
            });

        var targetPath = Path.Combine(
            workspace.RootPath,
            ".ai",
            "runtime",
            "agent-problem-follow-up-decisions",
            "agent-problem-follow-up-decision-20260413043139-test.json");
        Assert.True(attempt.CanProceed, attempt.FailureMessage);
        Assert.True(attempt.Record.Applied);
        Assert.Equal([relativePath], attempt.Record.Files);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("{\"decision\":\"accept_as_governed_planning_input\"}", File.ReadAllText(targetPath));
    }

    [Fact]
    public void Apply_BlocksHostOnlyPathThroughManagedWorkspacePolicy()
    {
        using var workspace = new TemporaryWorkspace();
        var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
        leaseRepository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases =
            [
                new ManagedWorkspaceLease
                {
                    LeaseId = "lease-writeback-host-only-001",
                    TaskId = "T-WRITEBACK-HOST-ONLY-001",
                    WorkspacePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-HOST-ONLY-001"),
                    RepoRoot = workspace.RootPath,
                    Status = ManagedWorkspaceLeaseStatus.Active,
                    AllowedWritablePaths = [".ai/tasks/graph.json"],
                },
            ],
        });
        var service = new ReviewWritebackService(
            workspace.RootPath,
            new StubGitClient(),
            new ManagedWorkspacePathPolicyService(workspace.RootPath, leaseRepository));
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-HOST-ONLY-001");
        var sourcePath = Path.Combine(worktreePath, ".ai", "tasks", "graph.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "{}");

        var attempt = service.Apply(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-HOST-ONLY-001",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-HOST-ONLY-001",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-HOST-ONLY-001",
                    WorktreePath = worktreePath,
                    FilesWritten = [".ai/tasks/graph.json"],
                },
            });

        Assert.False(attempt.CanProceed);
        Assert.Contains("host-only", attempt.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, ".ai", "tasks", "graph.json")));
    }

    [Fact]
    public void Apply_CapturesResultCommitWhenDelegatedWorktreeIsGitRepository()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ReviewWritebackService(workspace.RootPath, new SnapshotGitClient());
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-003");
        var relativePath = "src/Synthetic/CommittedWriteback.cs";
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "CommittedWriteback.cs");
        Directory.CreateDirectory(Path.Combine(worktreePath, ".git"));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class CommittedWriteback {}");

        var attempt = service.Apply(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-003",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-003",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-003",
                    WorktreePath = worktreePath,
                    FilesWritten = [relativePath],
                },
            });

        Assert.True(attempt.CanProceed);
        Assert.Equal("deadbeefcafebabe", attempt.Record.ResultCommit);
        Assert.Contains("captured result commit deadbeefcafebabe", attempt.Record.Summary, StringComparison.Ordinal);
        Assert.False(Directory.Exists(worktreePath));
    }

    [Fact]
    public void Apply_DoesNotRequireResultCommitForManagedManifestWorkspace()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ReviewWritebackService(workspace.RootPath, new StubGitClient());
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-MANAGED");
        var relativePath = "docs/agentcoach-quadrant-planner.html";
        var sourcePath = Path.Combine(worktreePath, "docs", "agentcoach-quadrant-planner.html");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(
            Path.Combine(worktreePath, ".carves-worktree.json"),
            $$"""
            {
              "RepoRoot": "{{workspace.RootPath.Replace("\\", "\\\\")}}",
              "TaskId": "T-WRITEBACK-MANAGED",
              "Mode": "managed"
            }
            """);
        File.WriteAllText(sourcePath, "<!doctype html><title>Managed</title>");

        var attempt = service.Apply(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-MANAGED",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-MANAGED",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-MANAGED",
                    WorktreePath = worktreePath,
                    FilesWritten = [relativePath],
                },
            });

        Assert.True(attempt.CanProceed, attempt.FailureMessage);
        Assert.True(attempt.Record.Applied);
        Assert.Null(attempt.Record.ResultCommit);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "docs", "agentcoach-quadrant-planner.html")));
        Assert.False(Directory.Exists(worktreePath));
    }

    [Fact]
    public void Preview_ReportsResultCommitAsUnavailableWithoutGitWorktree()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ReviewWritebackService(workspace.RootPath, new StubGitClient());
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-005");
        var relativePath = "src/Synthetic/PreviewWriteback.cs";
        var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "PreviewWriteback.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class PreviewWriteback {}");

        var preview = service.Preview(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-005",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-005",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-005",
                    WorktreePath = worktreePath,
                    FilesWritten = [relativePath],
                },
            });

        Assert.True(preview.CanProceed);
        Assert.True(preview.WillApply);
        Assert.False(preview.WillCaptureResultCommit);
        Assert.Equal([relativePath], preview.Files);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, "src", "Synthetic", "PreviewWriteback.cs")));
    }

    [Fact]
    public void Apply_BlocksScopeEscapeOutsideManagedWorkspaceLease()
    {
        using var workspace = new TemporaryWorkspace();
        var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
        leaseRepository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases =
            [
                new ManagedWorkspaceLease
                {
                    LeaseId = "lease-writeback-001",
                    TaskId = "T-WRITEBACK-006",
                    WorkspacePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-006"),
                    RepoRoot = workspace.RootPath,
                    Status = ManagedWorkspaceLeaseStatus.Active,
                    AllowedWritablePaths = ["src/Synthetic/AllowedWriteback.cs"],
                },
            ],
        });
        var service = new ReviewWritebackService(
            workspace.RootPath,
            new StubGitClient(),
            new ManagedWorkspacePathPolicyService(workspace.RootPath, leaseRepository));
        var worktreePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "T-WRITEBACK-006");
        var sourcePath = Path.Combine(worktreePath, "docs", "ScopeEscape.md");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "# scope escape");

        var attempt = service.Apply(
            new PlannerReviewArtifact
            {
                TaskId = "T-WRITEBACK-006",
                ValidationPassed = true,
                SafetyOutcome = SafetyOutcome.Allow,
            },
            new WorkerExecutionArtifact
            {
                TaskId = "T-WRITEBACK-006",
                Evidence = new ExecutionEvidence
                {
                    TaskId = "T-WRITEBACK-006",
                    WorktreePath = worktreePath,
                    FilesWritten = ["docs/ScopeEscape.md"],
                },
            });

        Assert.False(attempt.CanProceed);
        Assert.Contains("scope escape", attempt.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, "docs", "ScopeEscape.md")));
    }

    [Fact]
    public void Apply_UsesDetachedGitWorktreeToCaptureRealResultCommitWithoutMovingRepoHead()
    {
        using var workspace = new TemporaryWorkspace();
        InitializeGitRepository(workspace.RootPath);
        var initialCommit = RunGitCapture(workspace.RootPath, "rev-parse", "HEAD");
        var worktreePath = Path.Combine(Path.GetTempPath(), "carves-runtime-review-writeback", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(worktreePath)!);

        try
        {
            RunGit(workspace.RootPath, "worktree", "add", "--detach", worktreePath, initialCommit);
            var relativePath = "src/Synthetic/RealCommitWriteback.cs";
            var sourcePath = Path.Combine(worktreePath, "src", "Synthetic", "RealCommitWriteback.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "namespace Synthetic; public sealed class RealCommitWriteback {}");

            var service = new ReviewWritebackService(workspace.RootPath, new GitClient(new ProcessRunner()));
            var attempt = service.Apply(
                new PlannerReviewArtifact
                {
                    TaskId = "T-WRITEBACK-004",
                    ValidationPassed = true,
                    SafetyOutcome = SafetyOutcome.Allow,
                },
                new WorkerExecutionArtifact
                {
                    TaskId = "T-WRITEBACK-004",
                    Evidence = new ExecutionEvidence
                    {
                        TaskId = "T-WRITEBACK-004",
                        WorktreePath = worktreePath,
                        FilesWritten = [relativePath],
                    },
                });

            var rootHeadAfterApproval = RunGitCapture(workspace.RootPath, "rev-parse", "HEAD");
            Assert.True(attempt.CanProceed);
            Assert.False(string.IsNullOrWhiteSpace(attempt.Record.ResultCommit));
            Assert.NotEqual(initialCommit, attempt.Record.ResultCommit);
            Assert.Equal(initialCommit, rootHeadAfterApproval);
            Assert.True(File.Exists(Path.Combine(workspace.RootPath, "src", "Synthetic", "RealCommitWriteback.cs")));
            Assert.False(Directory.Exists(worktreePath));
        }
        finally
        {
            if (Directory.Exists(worktreePath))
            {
                try
                {
                    RunGit(workspace.RootPath, "worktree", "remove", "--force", worktreePath);
                }
                catch
                {
                    Directory.Delete(worktreePath, true);
                }
            }
        }
    }

    private sealed class SnapshotGitClient : StubGitClient
    {
        public override bool IsRepository(string repoRoot)
        {
            return repoRoot.Contains(".carves-worktrees", StringComparison.OrdinalIgnoreCase);
        }

        public override string? TryCreateScopedSnapshotCommit(string repoRoot, IReadOnlyList<string> paths, string message)
        {
            return "deadbeefcafebabe";
        }
    }

    private static void InitializeGitRepository(string repoRoot)
    {
        RunGit(repoRoot, "init");
        RunGit(repoRoot, "config", "user.email", "tests@example.com");
        RunGit(repoRoot, "config", "user.name", "CARVES Tests");
        File.WriteAllText(Path.Combine(repoRoot, "README.md"), "# Test repo");
        RunGit(repoRoot, "add", ".");
        RunGit(repoRoot, "commit", "-m", "Initial commit");
    }

    private static string RunGitCapture(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {standardError}");
        }

        return standardOutput.Trim();
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        _ = RunGitCapture(workingDirectory, arguments);
    }
}
