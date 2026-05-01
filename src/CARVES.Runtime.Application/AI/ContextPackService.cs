using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly ICodeGraphQueryService codeGraphQueryService;
    private readonly MemoryService memoryService;
    private readonly FailureSummaryProjectionService failureSummaryProjectionService;
    private readonly ExecutionRunService executionRunService;
    private readonly IRuntimeArtifactRepository? artifactRepository;
    private readonly ExecutionRunReportService executionRunReportService;
    private readonly RuntimeEvidenceStoreService evidenceStoreService;
    private readonly ContextBudgetTelemetryService contextBudgetTelemetryService;
    private readonly RuntimeSurfaceRouteGraphService runtimeSurfaceRouteGraphService;

    public ContextPackService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        ICodeGraphQueryService codeGraphQueryService,
        MemoryService memoryService,
        FailureSummaryProjectionService failureSummaryProjectionService,
        ExecutionRunService executionRunService,
        IRuntimeArtifactRepository? artifactRepository = null)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.codeGraphQueryService = codeGraphQueryService;
        this.memoryService = memoryService;
        this.failureSummaryProjectionService = failureSummaryProjectionService;
        this.executionRunService = executionRunService;
        this.artifactRepository = artifactRepository;
        executionRunReportService = new ExecutionRunReportService(paths);
        evidenceStoreService = new RuntimeEvidenceStoreService(paths);
        contextBudgetTelemetryService = new ContextBudgetTelemetryService(paths);
        runtimeSurfaceRouteGraphService = new RuntimeSurfaceRouteGraphService(paths);
    }

    public ContextPack BuildForTask(TaskNode task, string? model, int? overrideMaxContextTokens = null)
    {
        var draft = BuildTaskDraft(task);
        var pack = ApplyBudget(draft, model, overrideMaxContextTokens);
        Persist(pack, GetTaskPackPath(task.TaskId));
        RecordContextPackSurface(pack, "task_context_pack");
        evidenceStoreService.RecordContextPack(pack, task.CardId, sessionId: null);
        contextBudgetTelemetryService.RecordContextPack(pack, "task_context_built");
        return pack;
    }

    public ContextPack BuildForPlanner(
        RuntimeSessionState session,
        PlannerWakeReason wakeReason,
        string wakeDetail,
        OpportunitySnapshot snapshot,
        IReadOnlyList<Opportunity> selectedOpportunities,
        OpportunityTaskPreviewResult? preview,
        string? model,
        int? overrideMaxContextTokens = null)
    {
        var draft = BuildPlannerDraft(session, wakeReason, wakeDetail, snapshot, selectedOpportunities, preview);
        var pack = ApplyBudget(draft, model, overrideMaxContextTokens);
        Persist(pack, GetPlannerPackPath(draft.PackId));
        RecordContextPackSurface(pack, "planner_context_pack");
        evidenceStoreService.RecordContextPack(pack, cardId: null, sessionId: session.SessionId);
        contextBudgetTelemetryService.RecordContextPack(pack, "planner_context_built");
        return pack;
    }

    public ContextPack? LoadForTask(string taskId)
    {
        var path = GetTaskPackPath(taskId);
        return File.Exists(path) ? JsonSerializer.Deserialize<ContextPack>(File.ReadAllText(path), JsonOptions) : null;
    }

    public string GetTaskPackPath(string taskId)
    {
        return Path.Combine(paths.AiRoot, "runtime", "context-packs", "tasks", $"{taskId}.json");
    }

    public string RenderPrompt(ContextPack pack)
    {
        return pack.PromptInput;
    }

    private void RecordContextPackSurface(ContextPack pack, string producer)
    {
        if (string.IsNullOrWhiteSpace(pack.ArtifactPath))
        {
            return;
        }

        runtimeSurfaceRouteGraphService.RecordSurface(
            pack.ArtifactPath,
            producer,
            "candidate_context_surface",
            pack.PromptInput);
        runtimeSurfaceRouteGraphService.RecordRouteEdge(new RuntimeConsumerRouteEdgeRecord
        {
            SurfaceId = pack.ArtifactPath,
            Consumer = producer,
            DeclaredRouteKind = "persist_only",
            ObservedRouteKind = "persist_only",
            ObservedCount = 1,
            SampleCount = 1,
            FrequencyWindow = "7d",
            EvidenceSource = pack.PackId,
            LastSeen = DateTimeOffset.UtcNow,
        });
    }

    private sealed record ContextPackDraft(
        string PackId,
        ContextPackAudience Audience,
        string? TaskId,
        string ArtifactPath,
        string Goal,
        string Task,
        IReadOnlyList<string> Constraints,
        AcceptanceContract? AcceptanceContract,
        TaskGraphLocalProjection LocalTaskGraph,
        IReadOnlyList<ContextPackModuleProjection> RelevantModules,
        ContextPackFacetNarrowing FacetNarrowing,
        IReadOnlyList<ContextPackRecallItem> Recall,
        IReadOnlyList<string> CodeHints,
        IReadOnlyList<ContextPackWindowedRead> WindowedReads,
        ContextPackCompaction Compaction,
        CompactFailureSummary? LastFailureSummary,
        ExecutionHistorySummary? LastRunSummary,
        IReadOnlyList<ContextPackArtifactReference> ExpandableReferences);
}
