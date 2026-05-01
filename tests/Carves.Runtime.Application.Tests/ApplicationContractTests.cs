using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class ApplicationContractTests
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
    };

    [Fact]
    public void MarkdownProjection_MatchesGoldenFixtures()
    {
        var repoRoot = ResolveRepoRoot();
        var projection = ApplicationBoundarySmoke.BuildProjection();

        Assert.Equal(ReadFixture(repoRoot, "expected_task_queue.md"), projection.TaskQueue);
        Assert.Equal(ReadFixture(repoRoot, "expected_state.md"), projection.State);
        Assert.Equal(ReadFixture(repoRoot, "expected_current_task.md"), projection.CurrentTask);
    }

    [Fact]
    public void MarkdownProjection_StateProjectsAcceptanceContractGapCount()
    {
        var graph = new Carves.Runtime.Domain.Tasks.TaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-MISSING-CONTRACT",
                Title = "Pending task missing acceptance contract",
                Status = DomainTaskStatus.Pending,
                Priority = "P1",
                Scope = ["src/MissingContract.cs"],
                Acceptance = ["blocked until contract is projected"],
            },
        ],
        ["CARD-GAP"]);

        var projection = new MarkdownProjector().Build(graph);

        Assert.Contains("- dispatch-blocking acceptance contract gaps: 1", projection.State, StringComparison.Ordinal);
    }

    [Fact]
    public void PlannerReview_PausesForNeedsReviewOutcome()
    {
        var review = ApplicationBoundarySmoke.ReviewNeedsReviewOutcome();

        Assert.Equal(PlannerVerdict.PauseForReview, review.Verdict);
    }

    [Fact]
    public void TaskTransitionPolicy_MapsPauseForReviewToReview()
    {
        var nextStatus = ApplicationBoundarySmoke.DecideTransitionForPauseForReview();

        Assert.Equal(DomainTaskStatus.Review, nextStatus);
    }

    [Fact]
    public void TaskGraphRepository_RoundTripsTaskNodeSchema()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonTaskGraphRepository(workspace.Paths);
        var graph = new Carves.Runtime.Domain.Tasks.TaskGraph(
        [
            new TaskNode
            {
                TaskId = "T-ROUNDTRIP",
                Title = "Round-trip task",
                Status = DomainTaskStatus.Pending,
                Priority = "P1",
                Scope = ["src/RoundTrip.cs"],
                Acceptance = ["stored"],
                PlannerReview = new PlannerReview
                {
                    Verdict = PlannerVerdict.PauseForReview,
                    Reason = "Waiting on provisional review debt.",
                    DecisionStatus = ReviewDecisionStatus.ProvisionalAccepted,
                    AcceptanceMet = true,
                    BoundaryPreserved = true,
                    DecisionDebt = new ReviewDecisionDebt
                    {
                        Summary = "One bounded follow-up remains.",
                        FollowUpActions = ["Re-run targeted validation once the follow-up slice lands."],
                        RequiresFollowUpReview = true,
                        RecordedAt = new DateTimeOffset(2026, 4, 9, 1, 0, 0, TimeSpan.Zero),
                    },
                },
                AcceptanceContract = new AcceptanceContract
                {
                    ContractId = "AC-T-ROUNDTRIP",
                    Title = "Round-trip contract",
                    Status = AcceptanceContractLifecycleStatus.Compiled,
                    Owner = "planner",
                    CreatedAtUtc = new DateTimeOffset(2026, 4, 9, 0, 0, 0, TimeSpan.Zero),
                    Intent = new AcceptanceContractIntent
                    {
                        Goal = "Round-trip acceptance contract task truth.",
                        BusinessValue = "Preserve structured acceptance semantics in authoritative task storage.",
                    },
                    Checks = new AcceptanceContractChecks
                    {
                        PolicyChecks = ["No second planner"],
                    },
                    Constraints = new AcceptanceContractConstraintSet
                    {
                        MustNot = ["Do not drop acceptance contract on persistence round-trip"],
                    },
                    HumanReview = new AcceptanceContractHumanReviewPolicy(),
                    Traceability = new AcceptanceContractTraceability
                    {
                        SourceTaskId = "T-ROUNDTRIP",
                    },
                },
            },
        ],
        ["CARD-TEST"]);

        repository.Save(graph);
        var loaded = repository.Load();
        var nodeJson = File.ReadAllText(Path.Combine(workspace.Paths.TaskNodesRoot, "T-ROUNDTRIP.json"));
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);

        Assert.True(loaded.Tasks.ContainsKey("T-ROUNDTRIP"));
        Assert.Contains("\"schema_version\": 1", nodeJson, StringComparison.Ordinal);
        Assert.Contains("\"acceptance_contract\"", nodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_status\": \"provisional_accepted\"", nodeJson, StringComparison.Ordinal);
        Assert.Contains("\"decision_debt\"", nodeJson, StringComparison.Ordinal);
        Assert.Equal("AC-T-ROUNDTRIP", loaded.Tasks["T-ROUNDTRIP"].AcceptanceContract?.ContractId);
        Assert.Equal(ReviewDecisionStatus.ProvisionalAccepted, loaded.Tasks["T-ROUNDTRIP"].PlannerReview.DecisionStatus);
        Assert.Equal("One bounded follow-up remains.", loaded.Tasks["T-ROUNDTRIP"].PlannerReview.DecisionDebt?.Summary);
        Assert.True(File.Exists(truthStore.TaskGraphFile));
        Assert.True(File.Exists(truthStore.GetTaskNodePath("T-ROUNDTRIP")));
        Assert.True(truthStore.ExternalToRepo);
    }

    [Fact]
    public void TaskGraphRepository_LoadsLegacyStringScopeLimitAsNonAuthoritativeCompatibility()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonTaskGraphRepository(workspace.Paths);
        var graph = new Carves.Runtime.Domain.Tasks.TaskGraph(
        [
            CreateTaskWithAcceptanceContract(
                "T-LEGACY-SCOPE-LIMIT",
                new AcceptanceContractScopeLimit
                {
                    MaxFilesChanged = 3,
                    MaxLinesChanged = 120,
                }),
        ],
        ["CARD-SCOPE-LIMIT"]);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);

        repository.Save(graph);
        SetAcceptanceContractScopeLimit(
            Path.Combine(workspace.Paths.TaskNodesRoot, "T-LEGACY-SCOPE-LIMIT.json"),
            JsonValue.Create("Runtime public wrapper only"));
        SetAcceptanceContractScopeLimit(
            truthStore.GetTaskNodePath("T-LEGACY-SCOPE-LIMIT"),
            JsonValue.Create("Runtime public wrapper only"));

        var loaded = repository.Load().Tasks["T-LEGACY-SCOPE-LIMIT"];

        Assert.Null(loaded.AcceptanceContract?.Constraints.ScopeLimit);
    }

    [Fact]
    public void TaskGraphRepository_PreservesStructuredScopeLimitCaps()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonTaskGraphRepository(workspace.Paths);
        var graph = new Carves.Runtime.Domain.Tasks.TaskGraph(
        [
            CreateTaskWithAcceptanceContract(
                "T-STRUCTURED-SCOPE-LIMIT",
                new AcceptanceContractScopeLimit
                {
                    MaxFilesChanged = 5,
                    MaxLinesChanged = 250,
                }),
        ],
        ["CARD-SCOPE-LIMIT"]);

        repository.Save(graph);

        var loadedScopeLimit = repository.Load().Tasks["T-STRUCTURED-SCOPE-LIMIT"].AcceptanceContract?.Constraints.ScopeLimit;

        Assert.NotNull(loadedScopeLimit);
        Assert.Equal(5, loadedScopeLimit.MaxFilesChanged);
        Assert.Equal(250, loadedScopeLimit.MaxLinesChanged);
    }

    [Fact]
    public void TaskGraphRepository_LoadsRepoMirrorWhenItIsNewerThanExternalAuthoritative()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonTaskGraphRepository(workspace.Paths);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);
        repository.Save(new Carves.Runtime.Domain.Tasks.TaskGraph(
        [
            CreateTask("T-FRESH", DomainTaskStatus.Pending, "CARD-FRESH"),
        ],
        ["CARD-FRESH"]));

        var mirrorNodePath = Path.Combine(workspace.Paths.TaskNodesRoot, "T-FRESH.json");
        SetGraphSummaryStatus(workspace.Paths.TaskGraphFile, "T-FRESH", "completed");
        SetTaskNodeStatus(mirrorNodePath, "completed");

        var staleTime = DateTime.UtcNow.AddMinutes(-10);
        var freshTime = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(truthStore.TaskGraphFile, staleTime);
        File.SetLastWriteTimeUtc(truthStore.GetTaskNodePath("T-FRESH"), staleTime);
        File.SetLastWriteTimeUtc(workspace.Paths.TaskGraphFile, freshTime);
        File.SetLastWriteTimeUtc(mirrorNodePath, freshTime);

        var loaded = repository.Load();

        Assert.Equal(DomainTaskStatus.Completed, loaded.Tasks["T-FRESH"].Status);
    }

    [Fact]
    public void TaskGraphRepository_LoadsExternalAuthoritativeWhenMirrorIsNotNewer()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonTaskGraphRepository(workspace.Paths);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);
        repository.Save(new Carves.Runtime.Domain.Tasks.TaskGraph(
        [
            CreateTask("T-AUTHORITATIVE", DomainTaskStatus.Pending, "CARD-AUTHORITATIVE"),
        ],
        ["CARD-AUTHORITATIVE"]));

        var mirrorNodePath = Path.Combine(workspace.Paths.TaskNodesRoot, "T-AUTHORITATIVE.json");
        SetGraphSummaryStatus(workspace.Paths.TaskGraphFile, "T-AUTHORITATIVE", "completed");
        SetTaskNodeStatus(mirrorNodePath, "completed");

        var staleMirrorTime = DateTime.UtcNow.AddMinutes(-10);
        var freshAuthoritativeTime = DateTime.UtcNow;
        File.SetLastWriteTimeUtc(workspace.Paths.TaskGraphFile, staleMirrorTime);
        File.SetLastWriteTimeUtc(mirrorNodePath, staleMirrorTime);
        File.SetLastWriteTimeUtc(truthStore.TaskGraphFile, freshAuthoritativeTime);
        File.SetLastWriteTimeUtc(truthStore.GetTaskNodePath("T-AUTHORITATIVE"), freshAuthoritativeTime);

        var loaded = repository.Load();

        Assert.Equal(DomainTaskStatus.Pending, loaded.Tasks["T-AUTHORITATIVE"].Status);
    }

    [Fact]
    public void TaskGraphRepository_UpsertPreservesUnchangedTaskNodePayloads()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonTaskGraphRepository(workspace.Paths);
        var truthStore = new AuthoritativeTruthStoreService(workspace.Paths);
        repository.Save(new Carves.Runtime.Domain.Tasks.TaskGraph(
        [
            CreateTask("T-KEEP", DomainTaskStatus.Pending, "CARD-PARTIAL"),
            CreateTask("T-UPDATE", DomainTaskStatus.Pending, "CARD-PARTIAL"),
        ],
        ["CARD-PARTIAL"]));
        var keepMirrorPath = Path.Combine(workspace.Paths.TaskNodesRoot, "T-KEEP.json");
        var keepAuthoritativePath = truthStore.GetTaskNodePath("T-KEEP");
        RemoveTaskNodeProperty(keepMirrorPath, "acceptance_contract");
        RemoveTaskNodeProperty(keepAuthoritativePath, "acceptance_contract");
        var expectedMirrorPayload = File.ReadAllText(keepMirrorPath);
        var expectedAuthoritativePayload = File.ReadAllText(keepAuthoritativePath);

        repository.Upsert(CreateTask("T-UPDATE", DomainTaskStatus.Completed, "CARD-PARTIAL"));

        Assert.Equal(expectedMirrorPayload, File.ReadAllText(keepMirrorPath));
        Assert.Equal(expectedAuthoritativePayload, File.ReadAllText(keepAuthoritativePath));
        Assert.Equal(DomainTaskStatus.Completed, repository.Load().Tasks["T-UPDATE"].Status);
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static string ReadFixture(string repoRoot, string fileName)
    {
        return File.ReadAllText(Path.Combine(repoRoot, "tests", "Carves.Runtime.Application.Tests", "Fixtures", fileName))
            .ReplaceLineEndings("\n")
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private static TaskNode CreateTask(string taskId, DomainTaskStatus status, string cardId)
    {
        return new TaskNode
        {
            TaskId = taskId,
            Title = taskId,
            Status = status,
            Priority = "P1",
            CardId = cardId,
            Scope = ["src/Test.cs"],
            Acceptance = ["stored"],
        };
    }

    private static TaskNode CreateTaskWithAcceptanceContract(string taskId, AcceptanceContractScopeLimit? scopeLimit)
    {
        return new TaskNode
        {
            TaskId = taskId,
            Title = taskId,
            Status = DomainTaskStatus.Pending,
            Priority = "P1",
            CardId = "CARD-SCOPE-LIMIT",
            Scope = ["src/Test.cs"],
            Acceptance = ["stored"],
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = $"AC-{taskId}",
                Title = $"{taskId} contract",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                Owner = "planner",
                CreatedAtUtc = new DateTimeOffset(2026, 4, 17, 5, 0, 0, TimeSpan.Zero),
                Intent = new AcceptanceContractIntent
                {
                    Goal = "Read scope_limit compatibility.",
                    BusinessValue = "Keep task graph history readable.",
                },
                Constraints = new AcceptanceContractConstraintSet
                {
                    ScopeLimit = scopeLimit,
                },
                HumanReview = new AcceptanceContractHumanReviewPolicy(),
                Traceability = new AcceptanceContractTraceability
                {
                    SourceTaskId = taskId,
                },
            },
        };
    }

    private static void SetGraphSummaryStatus(string graphPath, string taskId, string status)
    {
        var document = JsonNode.Parse(File.ReadAllText(graphPath))!.AsObject();
        var tasks = document["tasks"]!.AsArray();
        foreach (var taskNode in tasks)
        {
            var task = taskNode!.AsObject();
            if (string.Equals(task["task_id"]?.GetValue<string>(), taskId, StringComparison.Ordinal))
            {
                task["status"] = status;
                break;
            }
        }

        File.WriteAllText(graphPath, document.ToJsonString(IndentedJson));
    }

    private static void SetTaskNodeStatus(string nodePath, string status)
    {
        var document = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        document["status"] = status;
        File.WriteAllText(nodePath, document.ToJsonString(IndentedJson));
    }

    private static void RemoveTaskNodeProperty(string nodePath, string propertyName)
    {
        var document = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        document.Remove(propertyName);
        File.WriteAllText(nodePath, document.ToJsonString(IndentedJson));
    }

    private static void SetAcceptanceContractScopeLimit(string nodePath, JsonNode? scopeLimit)
    {
        var document = JsonNode.Parse(File.ReadAllText(nodePath))!.AsObject();
        var acceptanceContract = document["acceptance_contract"]!.AsObject();
        var constraints = acceptanceContract["constraints"]!.AsObject();
        constraints["scope_limit"] = scopeLimit?.DeepClone();
        File.WriteAllText(nodePath, document.ToJsonString(IndentedJson));
    }
}
