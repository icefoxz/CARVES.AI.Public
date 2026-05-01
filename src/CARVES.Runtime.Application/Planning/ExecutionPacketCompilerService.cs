using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public sealed class ExecutionPacketCompilerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly IReadOnlyList<string> ContextAssemblyOrder =
    [
        "Architecture",
        "RelevantModules",
        "CurrentTaskFiles",
    ];

    private const string GenericClosureProfileId = "generic_review_closure_v1";
    private const string NarrativeResultClosureProfileId = "narrative_result_closure_v1";
    private const string RuntimeRecoverableResidueProfileId = "runtime_recoverable_residue_contract_v1";

    private readonly ControlPlanePaths paths;
    private readonly TaskGraph.TaskGraphService taskGraphService;
    private readonly ICodeGraphQueryService codeGraphQueryService;
    private readonly MemoryService memoryService;
    private readonly PlannerIntentRoutingService plannerIntentRoutingService;
    private readonly AuthoritativeTruthStoreService authoritativeTruthStoreService;
    private readonly FormalPlanningExecutionGateService formalPlanningExecutionGateService;
    private readonly ModeExecutionEntryGateService modeExecutionEntryGateService;

    public ExecutionPacketCompilerService(
        ControlPlanePaths paths,
        TaskGraph.TaskGraphService taskGraphService,
        ICodeGraphQueryService codeGraphQueryService,
        MemoryService memoryService,
        PlannerIntentRoutingService plannerIntentRoutingService,
        IControlPlaneLockService? lockService = null,
        FormalPlanningExecutionGateService? formalPlanningExecutionGateService = null)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.codeGraphQueryService = codeGraphQueryService;
        this.memoryService = memoryService;
        this.plannerIntentRoutingService = plannerIntentRoutingService;
        this.authoritativeTruthStoreService = new AuthoritativeTruthStoreService(paths, lockService);
        this.formalPlanningExecutionGateService = formalPlanningExecutionGateService ?? new FormalPlanningExecutionGateService();
        this.modeExecutionEntryGateService = new ModeExecutionEntryGateService(this.formalPlanningExecutionGateService);
    }

    public ExecutionPacket Compile(TaskNode task, MemoryBundle? memoryBundle = null)
    {
        var scopeAnalysis = codeGraphQueryService.AnalyzeScope(task.Scope);
        var bundle = memoryBundle ?? memoryService.BundleForTask(task);
        var intent = plannerIntentRoutingService.Classify(task);
        var packetId = BuildPacketId(task);
        var goal = string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description;
        var scope = task.Scope.Select(NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var closureContract = BuildClosureContract(task);
        var requiredValidation = BuildRequiredValidation(task);
        var stableEvidenceSurfaces = BuildStableEvidenceSurfaces(task.TaskId);
        var workerAllowedActions = BuildWorkerAllowedActions(intent);
        var candidateFiles = scopeAnalysis.Files.Count == 0
            ? scope.Where(static value => !string.IsNullOrWhiteSpace(value)).Take(8).ToArray()
            : scopeAnalysis.Files.Select(NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var boundedRead = BoundedReadProjectionBuilder.Build(paths.RepoRoot, candidateFiles);
        var codegraphQueries = BuildCodegraphQueries(scopeAnalysis, task.Scope);

        return new ExecutionPacket
        {
            PacketId = packetId,
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = task.CardId ?? "CARD-UNKNOWN",
                TaskId = task.TaskId,
                TaskRevision = Math.Max(1, task.RetryCount + 1),
            },
            Goal = goal,
            PlannerIntent = intent,
            Scope = scope,
            NonGoals = task.Constraints.Distinct(StringComparer.Ordinal).ToArray(),
            AcceptanceContract = task.AcceptanceContract,
            Context = new ExecutionPacketContext
            {
                AssemblyOrder = ContextAssemblyOrder.ToArray(),
                MemoryBundleRefs = BuildMemoryBundleRefs(bundle),
                CodegraphQueries = codegraphQueries,
                RelevantFiles = boundedRead.RelevantFiles,
                ContextPackRef = ToRepoRelative(Path.Combine(paths.RuntimeRoot, "context-packs", "tasks", $"{task.TaskId}.json")),
                WindowedReads = boundedRead.WindowedReads
                    .Select(item => new ExecutionPacketWindowedRead
                    {
                        Path = item.Path,
                        TotalLines = item.TotalLines,
                        StartLine = item.StartLine,
                        EndLine = item.EndLine,
                        Reason = item.Reason,
                        Truncated = item.Truncated,
                    })
                    .ToArray(),
                Compaction = new ExecutionPacketContextCompaction
                {
                    Strategy = boundedRead.Strategy,
                    CandidateFileCount = boundedRead.CandidateFileCount,
                    RelevantFileCount = boundedRead.RelevantFiles.Count,
                    WindowedReadCount = boundedRead.WindowedReads.Count,
                    FullReadCount = boundedRead.FullReadCount,
                    OmittedFileCount = boundedRead.OmittedFileCount,
                },
            },
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = task.Scope.Select(NormalizePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                ReadOnlyRoots = BuildReadOnlyRoots(task),
                TruthRoots = ["carves://truth/tasks", "carves://truth/runtime"],
                RepoMirrorRoots = [".ai/"],
            },
            Budgets = BuildBudgets(task),
            ClosureContract = closureContract,
            WorkerExecutionPacket = BuildWorkerExecutionPacket(
                task,
                packetId,
                goal,
                scope,
                closureContract,
                requiredValidation,
                stableEvidenceSurfaces,
                workerAllowedActions),
            RequiredValidation = requiredValidation,
            StableEvidenceSurfaces = stableEvidenceSurfaces,
            WorkerAllowedActions = workerAllowedActions,
            PlannerOnlyActions =
            [
                "carves.create_card",
                "carves.create_taskgraph",
                "carves.issue_packet",
                "carves.review_task",
                "carves.sync_state",
            ],
            StopConditions =
            [
                "predicted_patch_exceeds_budget",
                "requires_split_to_stay_within_patch_budget",
                "needs_host_seam_change",
                "touches_more_than_2_subsystems",
                "requires_new_card_or_taskgraph",
            ],
        };
    }

    public ExecutionPacket CompileAndPersist(TaskNode task, MemoryBundle? memoryBundle = null)
    {
        modeExecutionEntryGateService.EnsureReadyForExecution(task);
        var packet = Compile(task, memoryBundle);
        Persist(task.TaskId, packet);
        return packet;
    }

    public ExecutionPacketSurfaceSnapshot BuildSnapshot(string taskId)
    {
        var graph = taskGraphService.Load();
        if (!graph.Tasks.TryGetValue(taskId, out var task))
        {
            throw new InvalidOperationException($"Task '{taskId}' was not found in task graph truth.");
        }

        var packetPath = GetPacketPath(taskId);
        var authoritativePath = authoritativeTruthStoreService.GetExecutionPacketPath(taskId);
        var persisted = File.Exists(authoritativePath) || File.Exists(packetPath);
        var packetPayload = authoritativeTruthStoreService.ReadAuthoritativeFirst(authoritativePath, packetPath);
        var packet = persisted && !string.IsNullOrWhiteSpace(packetPayload)
            ? JsonSerializer.Deserialize<ExecutionPacket>(packetPayload, JsonOptions) ?? Compile(task)
            : Compile(task);

        return new ExecutionPacketSurfaceSnapshot
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? string.Empty,
            PlannerIntent = packet.PlannerIntent,
            PacketPath = ToRepoRelative(packetPath),
            Persisted = persisted,
            Summary = $"Compiled packet for {task.TaskId} with planner intent {packet.PlannerIntent} and explicit context assembly order.",
            Packet = packet,
        };
    }

    public string GetPacketPath(string taskId)
    {
        return Path.Combine(paths.RuntimeRoot, "execution-packets", $"{taskId}.json");
    }

    public string GetAuthoritativePacketPath(string taskId)
    {
        return authoritativeTruthStoreService.GetExecutionPacketPath(taskId);
    }

    public string GetPacketRepoRelativePath(string taskId)
    {
        return ToRepoRelative(GetPacketPath(taskId));
    }

    private void Persist(string taskId, ExecutionPacket packet)
    {
        var path = GetPacketPath(taskId);
        var authoritativePath = authoritativeTruthStoreService.GetExecutionPacketPath(taskId);
        authoritativeTruthStoreService.WithWriterLease(authoritativePath, "execution-packet-persist", () =>
        {
            authoritativeTruthStoreService.WriteAuthoritativeThenMirror(
                authoritativePath,
                path,
                JsonSerializer.Serialize(packet, JsonOptions),
                writerLockHeld: true);
            return 0;
        });
    }

    private static string BuildPacketId(TaskNode task)
    {
        return $"EP-{task.TaskId}-v{Math.Max(1, task.RetryCount + 1)}";
    }

    private static string[] BuildMemoryBundleRefs(MemoryBundle bundle)
    {
        return bundle.Architecture
            .Concat(bundle.Modules)
            .Select(document => NormalizePath(document.Path))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] BuildCodegraphQueries(CodeGraphScopeAnalysis scopeAnalysis, IReadOnlyList<string> scope)
    {
        var queries = new List<string>();
        queries.AddRange(scopeAnalysis.Modules.Take(5).Select(module => $"module:{module}"));
        queries.AddRange(scopeAnalysis.DependencyModules.Take(5).Select(module => $"dependency:{module}"));

        if (queries.Count == 0)
        {
            queries.AddRange(scope.Take(5).Select(item => $"scope:{NormalizePath(item)}"));
        }

        return queries
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] BuildReadOnlyRoots(TaskNode task)
    {
        if (task.Scope.Any(scope => scope.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)))
        {
            return Array.Empty<string>();
        }

        return ["docs/"];
    }

    private static ExecutionPacketBudgets BuildBudgets(TaskNode task)
    {
        return task.TaskType switch
        {
            TaskType.Review => new ExecutionPacketBudgets { MaxFilesChanged = 0, MaxLinesChanged = 0, MaxShellCommands = 4 },
            TaskType.Planning => new ExecutionPacketBudgets { MaxFilesChanged = 0, MaxLinesChanged = 0, MaxShellCommands = 4 },
            TaskType.Meta => new ExecutionPacketBudgets { MaxFilesChanged = 4, MaxLinesChanged = 200, MaxShellCommands = 8 },
            _ => new ExecutionPacketBudgets
            {
                MaxFilesChanged = Math.Max(4, Math.Min(12, task.Scope.Count * 2)),
                MaxLinesChanged = 400,
                MaxShellCommands = Math.Max(6, task.Validation.Commands.Count + 4),
            },
        };
    }

    private static string[] BuildRequiredValidation(TaskNode task)
    {
        var validations = new List<string>();
        if (task.Validation.Commands.Count > 0)
        {
            validations.AddRange(task.Validation.Commands.Select(command => string.Join(' ', command)));
        }
        else
        {
            validations.Add("targeted tests");
        }

        validations.Add("surface smoke");
        validations.Add("post-change audit");
        return validations.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static ExecutionPacketClosureContract BuildClosureContract(TaskNode task)
    {
        var profileId = ResolveClosureContractProfileId(task);
        string[] requiredContractChecks = profileId switch
        {
            RuntimeRecoverableResidueProfileId =>
            [
                "residue_contract_schema_presence",
                "persistence_readback_wiring",
                "review_surface_projection",
                "severity_vocabulary_invariant",
                "roundtrip_validation_evidence",
                "scope_hygiene",
            ],
            NarrativeResultClosureProfileId =>
            [
                "review_artifact_present",
                "validation_recorded",
                "safety_recorded",
                "completion_claim_recorded",
                "result_channel_recorded",
                "scope_hygiene",
            ],
            _ =>
            [
                "review_artifact_present",
                "validation_recorded",
                "safety_recorded",
                "patch_scope_recorded",
                "scope_hygiene",
            ],
        };
        var requiredValidationGates = new List<string> { "focused_required_validation" };
        if (profileId == RuntimeRecoverableResidueProfileId)
        {
            requiredValidationGates.Add("contract_roundtrip_required");
        }

        if (task.Validation.ExpectedEvidence.Count > 0)
        {
            requiredValidationGates.Add("task_expected_evidence_required");
        }

        return new ExecutionPacketClosureContract
        {
            ContractMatrixProfileId = profileId,
            Summary = profileId switch
            {
                RuntimeRecoverableResidueProfileId => "Worker candidate must close the residue contract through schema, persistence/readback, projection, severity vocabulary, roundtrip validation, and scope hygiene evidence.",
                NarrativeResultClosureProfileId => "Worker candidate may close as a narrative or structured result when routing intent is summary-oriented; Host still requires completion claim, result channel, validation, safety, review, and scope hygiene evidence.",
                _ => "Worker candidate must include patch scope, validation, safety, and scope hygiene evidence before Host review can allow writeback.",
            },
            RequiredContractChecks = requiredContractChecks,
            RequiredValidationGates = requiredValidationGates.Distinct(StringComparer.Ordinal).ToArray(),
            CompletionClaimRequired = true,
            CompletionClaimFields =
            [
                "changed_files",
                "contract_items_satisfied",
                "tests_run",
                "evidence_paths",
                "known_limitations",
                "next_recommendation",
            ],
            ForbiddenVocabulary = profileId == RuntimeRecoverableResidueProfileId
                ? ["low", "medium", "high"]
                : Array.Empty<string>(),
            EvidenceSurfaces = BuildStableEvidenceSurfaces(task.TaskId),
        };
    }

    private static string ResolveClosureContractProfileId(TaskNode task)
    {
        if (IsNarrativeResultRoutingTask(task))
        {
            return NarrativeResultClosureProfileId;
        }

        var candidateText = string.Join(
            " ",
            new[] { task.TaskId, task.CardId ?? string.Empty, task.Title, task.Description }
                .Concat(task.Scope));
        return ContainsAny(
            candidateText,
            "CARD-972",
            "recoverable residue",
            "residue contract",
            "ControlPlaneResidueContract",
            "ControlPlaneLock",
            "ManagedWorkspaceLeaseService",
            "RuntimeProductClosurePilotStatusService")
            ? RuntimeRecoverableResidueProfileId
            : GenericClosureProfileId;
    }

    private static bool IsNarrativeResultRoutingTask(TaskNode task)
    {
        return task.Metadata.TryGetValue("routing_intent", out var routingIntent)
               && routingIntent is "reasoning_summary" or "failure_summary" or "review_summary" or "structured_output";
    }

    private static string[] BuildStableEvidenceSurfaces(string taskId)
    {
        return
        [
            $"inspect execution-packet {taskId}",
            $"api execution-packet {taskId}",
            $"task inspect {taskId}",
        ];
    }

    private static string[] BuildWorkerAllowedActions(Carves.Runtime.Domain.Planning.PlannerIntent intent)
    {
        return intent == Carves.Runtime.Domain.Planning.PlannerIntent.Execution
            ?
            [
                "read",
                "edit",
                "build",
                "test",
                "carves.load_context",
                "carves.query_codegraph",
                "carves.submit_result",
                "carves.request_replan",
            ]
            :
            [
                "read",
                "carves.request_replan",
            ];
    }

    private static WorkerExecutionPacket BuildWorkerExecutionPacket(
        TaskNode task,
        string executionPacketId,
        string goal,
        IReadOnlyList<string> scope,
        ExecutionPacketClosureContract closureContract,
        IReadOnlyList<string> requiredValidation,
        IReadOnlyList<string> stableEvidenceSurfaces,
        IReadOnlyList<string> workerAllowedActions)
    {
        var evidenceRequired = task.Validation.ExpectedEvidence
            .Concat(closureContract.EvidenceSurfaces)
            .Concat(stableEvidenceSurfaces)
            .Concat(
            [
                $".ai/execution/{task.TaskId}/result.json",
                $".ai/artifacts/worker-executions/{task.TaskId}.json",
            ])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new WorkerExecutionPacket
        {
            PacketId = $"WEP-{task.TaskId}-v{Math.Max(1, task.RetryCount + 1)}",
            SourceExecutionPacketId = executionPacketId,
            TaskId = task.TaskId,
            Goal = goal,
            AllowedFiles = scope.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray(),
            AllowedActions = workerAllowedActions.ToArray(),
            RequiredContractMatrix = closureContract.RequiredContractChecks.ToArray(),
            RequiredValidation = requiredValidation.ToArray(),
            RequiredValidationGates = closureContract.RequiredValidationGates.ToArray(),
            EvidenceRequired = evidenceRequired,
            ForbiddenVocabulary = closureContract.ForbiddenVocabulary.ToArray(),
            CompletionClaimSchema = new WorkerCompletionClaimSchema
            {
                Required = closureContract.CompletionClaimRequired,
                Fields = closureContract.CompletionClaimFields.ToArray(),
                ClaimIsTruth = false,
                HostValidationRequired = true,
            },
            ResultSubmission = new WorkerResultSubmissionContract
            {
                CandidateResultChannel = $".ai/execution/{task.TaskId}/result.json",
                HostIngestCommand = $"task ingest-result {task.TaskId}",
                CandidateOnly = true,
                ReviewBundleRequired = true,
                SubmittedByHostOrAdapter = true,
                WorkerDirectTruthWriteAllowed = false,
            },
            GrantsLifecycleTruthAuthority = false,
            GrantsTruthWriteAuthority = false,
            CreatesTaskQueue = false,
            WritesTruthRoots = false,
            Notes =
            [
                "Worker output is a candidate result; Host validation and review decide closure.",
                "Worker must not write CARVES truth roots directly.",
                "Completion claim is a declaration, not lifecycle truth.",
            ],
        };
    }

    private string ToRepoRelative(string path)
    {
        return NormalizePath(Path.GetRelativePath(paths.RepoRoot, path));
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}
