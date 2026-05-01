using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using ApplicationTaskScheduler = Carves.Runtime.Application.TaskGraph.TaskScheduler;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public static class ApplicationBoundarySmoke
{
    public static string ParseCardId(string cardPath)
    {
        var plannerService = new PlannerService(new CardParser(), new TaskDecomposer(), new FakeGitClient(), new TaskGraphService(new InMemoryTaskGraphRepository(new DomainTaskGraph()), new ApplicationTaskScheduler()));
        return plannerService.ParseCard(cardPath).CardId;
    }

    public static TaskNode? SelectNextTask()
    {
        var repository = new InMemoryTaskGraphRepository(
            new DomainTaskGraph(
                [
                    new TaskNode
                    {
                        TaskId = "T-1",
                        Title = "Pending task",
                        Status = DomainTaskStatus.Pending,
                        Priority = "P1",
                        Dependencies = Array.Empty<string>(),
                        Scope = ["src/"],
                        Acceptance = ["accepted"],
                        AcceptanceContract = CreateAcceptanceContract("T-1"),
                    },
                ],
                ["CARD-002"]));

        return new TaskGraphService(repository, new ApplicationTaskScheduler()).NextReadyTask();
    }

    public static MarkdownProjection BuildProjection()
    {
        var graph = new DomainTaskGraph(
            [
                new TaskNode
                {
                    TaskId = "T-1",
                    Title = "Pending task",
                    Status = DomainTaskStatus.Pending,
                    Priority = "P1",
                    Dependencies = Array.Empty<string>(),
                    Scope = ["src/Alpha.cs"],
                    Acceptance = ["accepted"],
                    AcceptanceContract = CreateAcceptanceContract("T-1"),
                },
                new TaskNode
                {
                    TaskId = "T-2",
                    Title = "Completed task",
                    Status = DomainTaskStatus.Completed,
                    Priority = "P2",
                    Dependencies = Array.Empty<string>(),
                    Scope = ["tests/AlphaTests.cs"],
                    Acceptance = ["verified"],
                    AcceptanceContract = CreateAcceptanceContract("T-2"),
                },
            ],
            ["CARD-002"]);

        var task = graph.Tasks["T-1"];
        var report = new TaskRunReport
        {
            TaskId = "T-1",
            Request = new WorkerRequest
            {
                Task = task,
                Session = new ExecutionSession("T-1", "Pending task", "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.Parse("2026-03-15T00:00:00+00:00")),
            },
            Session = new ExecutionSession("T-1", "Pending task", "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.Parse("2026-03-15T00:00:00+00:00")),
            DryRun = false,
            Patch = new PatchSummary(1, 0, 0, true, ["src/Alpha.cs"]),
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence = ["validation passed"],
            },
            SafetyDecision = SafetyDecision.Allow("T-1"),
        };
        var review = new PlannerReview
        {
            Verdict = PlannerVerdict.Complete,
            Reason = "Ready to advance.",
            AcceptanceMet = true,
            BoundaryPreserved = true,
        };

        return new MarkdownProjector().Build(graph, task, report, review);
    }

    public static PlannerReview ReviewNeedsReviewOutcome()
    {
        var task = new TaskNode
        {
            TaskId = "T-REVIEW",
            Title = "Review me",
            Status = DomainTaskStatus.Running,
            Scope = ["src/Review.cs"],
            Acceptance = ["approved"],
            AcceptanceContract = CreateAcceptanceContract("T-REVIEW"),
        };

        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Request = new WorkerRequest
            {
                Task = task,
                Session = new ExecutionSession(task.TaskId, task.Title, "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.UtcNow),
            },
            Session = new ExecutionSession(task.TaskId, task.Title, "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.UtcNow),
            Validation = new ValidationResult
            {
                Passed = true,
            },
            Patch = new PatchSummary(1, 0, 0, true, ["src/Review.cs"]),
            SafetyDecision = SafetyDecision.FromResults(
                task.TaskId,
                SafetyValidationMode.Execution,
                [
                    new SafetyValidatorResult
                    {
                        ValidatorName = "PatchSize",
                        Outcome = SafetyOutcome.NeedsReview,
                        Summary = "Needs review.",
                        Violations =
                        [
                            new SafetyViolation("PATCH_REVIEW_THRESHOLD", "Patch requires review.", "warning", "PatchSize"),
                        ],
                    },
                ]),
        };

        return new PlannerReviewService().Review(task, report);
    }

    public static DomainTaskStatus DecideTransitionForPauseForReview()
    {
        var task = new TaskNode
        {
            TaskId = "T-TRANSITION",
            Title = "Transition task",
            Status = DomainTaskStatus.Running,
            AcceptanceContract = CreateAcceptanceContract("T-TRANSITION"),
        };

        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Request = new WorkerRequest
            {
                Task = task,
                Session = new ExecutionSession(task.TaskId, task.Title, "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.UtcNow),
            },
            Session = new ExecutionSession(task.TaskId, task.Title, "repo", false, "abc123", "NullAiClient", "worktree", DateTimeOffset.UtcNow),
            Validation = new ValidationResult
            {
                Passed = true,
            },
            SafetyDecision = SafetyDecision.FromResults(
                task.TaskId,
                SafetyValidationMode.Execution,
                [
                    new SafetyValidatorResult
                    {
                        ValidatorName = "Architecture",
                        Outcome = SafetyOutcome.NeedsReview,
                        Summary = "Architecture review required.",
                    },
                ]),
        };
        var review = new PlannerReview
        {
            Verdict = PlannerVerdict.PauseForReview,
            Reason = "Architecture review required.",
        };

        return new TaskTransitionPolicy().Decide(task, report, review).NextStatus;
    }

    private sealed class InMemoryTaskGraphRepository : ITaskGraphRepository
    {
        private readonly DomainTaskGraph graph;

        public InMemoryTaskGraphRepository(DomainTaskGraph graph)
        {
            this.graph = graph;
        }

        public DomainTaskGraph Load()
        {
            return graph;
        }

        public void Save(DomainTaskGraph graph)
        {
        }

        public void Upsert(TaskNode task)
        {
        }

        public void UpsertRange(IEnumerable<TaskNode> tasks)
        {
        }

        public T WithWriteLock<T>(Func<T> action)
        {
            return action();
        }
    }

    private static AcceptanceContract CreateAcceptanceContract(string taskId)
    {
        return new AcceptanceContract
        {
            ContractId = $"AC-{taskId}",
            Title = $"Acceptance contract for {taskId}",
            Status = AcceptanceContractLifecycleStatus.Compiled,
            Traceability = new AcceptanceContractTraceability
            {
                SourceTaskId = taskId,
            },
        };
    }

    private sealed class FakeGitClient : Carves.Runtime.Application.Git.IGitClient
    {
        public string TryGetCurrentCommit(string repoRoot)
        {
            return "fake";
        }

        public bool IsRepository(string repoRoot)
        {
            return false;
        }

        public bool HasUncommittedChanges(string repoRoot)
        {
            return false;
        }

        public IReadOnlyList<string> GetUncommittedPaths(string repoRoot)
        {
            return Array.Empty<string>();
        }

        public IReadOnlyList<string> GetUntrackedPaths(string repoRoot)
        {
            return Array.Empty<string>();
        }

        public IReadOnlyList<string> GetChangedPathsSince(string repoRoot, string baseCommit)
        {
            return Array.Empty<string>();
        }

        public bool TryCreateDetachedWorktree(string repoRoot, string worktreePath, string startPoint)
        {
            return false;
        }

        public void TryRemoveWorktree(string repoRoot, string worktreePath)
        {
        }

        public string? TryGetUncommittedDiff(string repoRoot, IReadOnlyList<string>? paths = null)
        {
            return null;
        }

        public string? TryCreateScopedSnapshotCommit(string repoRoot, IReadOnlyList<string> paths, string message)
        {
            return null;
        }
    }
}
