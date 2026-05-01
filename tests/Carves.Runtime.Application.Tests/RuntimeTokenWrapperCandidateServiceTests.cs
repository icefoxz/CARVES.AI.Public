using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenWrapperCandidateServiceTests
{
    private static readonly JsonSerializerOptions RepoJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Persist_BuildsOfflineWrapperCandidateAndReviewBundle()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var resultDate = new DateOnly(2026, 4, 21);
        var task = new TaskNode
        {
            TaskId = "T-PHASE12-W-001",
            Title = "Design wrapper candidate",
            Description = "Build a structural-only offline wrapper candidate.",
            Status = DomainTaskStatus.Completed,
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/AI/"],
            Acceptance =
            [
                "candidate preserves worker execution boundaries",
                "candidate references its archived evidence source",
            ],
            LastWorkerRunId = "RUN-T-PHASE12-W-001",
        };
        new JsonTaskGraphRepository(paths).Save(new DomainTaskGraph([task]));
        WriteExecutionPacket(paths, task.TaskId, new ExecutionPacket
        {
            PacketId = "EP-T-PHASE12-W-001-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-PHASE12-W-001",
                TaskId = task.TaskId,
                TaskRevision = 1,
            },
            Goal = "Build a structural-only wrapper candidate.",
            PlannerIntent = Carves.Runtime.Domain.Planning.PlannerIntent.Execution,
            Scope = task.Scope,
            Context = new ExecutionPacketContext
            {
                ContextPackRef = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
            },
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = task.Scope,
            },
            Budgets = new ExecutionPacketBudgets
            {
                MaxFilesChanged = 4,
                MaxLinesChanged = 120,
                MaxShellCommands = 4,
            },
            StopConditions =
            [
                "stop if the candidate would weaken governance boundaries",
                "stop if token savings require lossy paraphrase",
            ],
        });
        WriteContextPack(paths, task.TaskId, new ContextPack
        {
            PackId = "task-T-PHASE12-W-001",
            ArtifactPath = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
            TaskId = task.TaskId,
            Goal = "Build a structural-only wrapper candidate.",
            Task = "Replay source worker instructions offline.",
            Constraints = ["Stay on the wrapper surface only."],
            PromptInput = "Context Pack\n\nGoal:\nReplay worker wrapper.\n\nTask:\nBuild offline candidate.",
            PromptSections =
            [
                new RenderedPromptSection
                {
                    SectionId = "goal",
                    SectionKind = "goal",
                    SourceItemId = task.TaskId,
                    RendererVersion = "prose_v1",
                    StartChar = 0,
                    EndChar = 24,
                }
            ],
            Budget = new ContextPackBudget
            {
                ProfileId = "worker",
                Model = "gpt-5-mini",
                EstimatorVersion = ContextBudgetPolicyResolver.EstimatorVersion,
                ModelLimitTokens = 16000,
                TargetTokens = 900,
                AdvisoryTokens = 1000,
                HardSafetyTokens = 1300,
                MaxContextTokens = 1000,
                ReservedHeadroomTokens = 300,
                CoreBudgetTokens = 550,
                RelevantBudgetTokens = 450,
                UsedTokens = 120,
                TrimmedTokens = 0,
                FixedTokensEstimate = 120,
                DynamicTokensEstimate = 0,
                TotalContextTokensEstimate = 120,
                BudgetPosture = "balanced",
            },
        });

        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            TrustLineClassification = "recomputed_trusted_for_phase_1_target_decision",
            Phase11WrapperInvariantManifestMayReferenceThisLine = true,
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    SegmentKind = "system",
                    PayloadPath = "$.instructions",
                    SourceComponentPath = "src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs",
                    SourceAnchor = "WorkerAiRequestFactory.BuildInstructions",
                    ShareP95 = 0.316,
                    TokensP95 = 646,
                    RecommendedCandidateStrategy = "dedupe_then_request_kind_slice",
                    Invariants =
                    [
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SCOPE-001",
                            InvariantClass = "scope_boundary",
                            Title = "Governed worker scope boundary",
                        },
                        new RuntimeTokenWrapperPolicyInvariantItem
                        {
                            InvariantId = "WRAP-WORKER-SHELL-003",
                            InvariantClass = "execution_modality",
                            Title = "PowerShell execution modality and edit method",
                        }
                    ],
                }
            ],
        };
        var validator = new RuntimeTokenWrapperOfflineValidatorResult
        {
            ResultDate = resultDate,
            CohortId = manifest.CohortId,
            TrustLineClassification = manifest.TrustLineClassification,
            Phase11WrapperValidatorMayReferenceThisLine = true,
            Phase10Decision = manifest.Phase10Decision,
            Phase10NextTrack = manifest.Phase10NextTrack,
            ValidatorVerdict = "ready_for_phase12_wrapper_candidate_design",
            Phase12WrapperCandidateMayStart = true,
        };
        var recollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = manifest.CohortId,
                RequestKinds = ["worker"],
            },
            TaskIds = [task.TaskId],
            Tasks =
            [
                new RuntimeTokenBaselineWorkerRecollectTaskRecord
                {
                    TaskId = task.TaskId,
                    RunId = "RUN-T-PHASE12-W-001",
                    RequestId = "worker-request-phase12w001",
                    PacketArtifactPath = $".ai/runtime/execution-packets/{task.TaskId}.json",
                    ContextPackArtifactPath = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
                }
            ],
        };

        var config = AiProviderConfig.CreateProviderDefaults("openai", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 500, reasoningEffort: "low");
        var result = RuntimeTokenWrapperCandidateService.Persist(
            paths,
            manifest,
            validator,
            recollect,
            config,
            "TestRepo",
            new StubGitClient(),
            new JsonTaskGraphRepository(paths),
            new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low"),
            resultDate);

        Assert.True(result.Phase12WrapperCandidateMayReferenceThisLine);
        Assert.Equal("worker:system:$.instructions", result.CandidateSurfaceId);
        Assert.Equal("dedupe_then_request_kind_slice", result.CandidateStrategy);
        Assert.True(result.MaterialReductionPass);
        Assert.True(result.SchemaValidityPass);
        Assert.True(result.InvariantCoveragePass);
        Assert.True(result.SemanticPreservationPass);
        Assert.True(result.SaliencePreservationPass);
        Assert.True(result.PriorityPreservationPass);
        Assert.True(result.EnterActiveCanaryReviewBundleReady);
        Assert.False(result.ActiveCanaryApprovalGranted);
        Assert.False(result.RuntimeShadowExecutionAllowed);
        Assert.False(result.MainRendererReplacementAllowed);
        Assert.Single(result.Samples);
        var sample = result.Samples[0];
        Assert.True(sample.TokenDelta > 0);
        Assert.True(sample.ReductionRatio >= 0.20d);
        Assert.True(sample.SourceGroundingIncluded);
        Assert.Contains("Source grounding", result.CandidateTextPreview, StringComparison.Ordinal);
        Assert.All(result.ManualReviewQueue, item =>
        {
            Assert.Equal("ready_for_operator_review_before_canary", item.ReviewStatus);
            Assert.False(item.BlocksPhase12Completion);
            Assert.True(item.BlocksEnterActiveCanary);
        });
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.ReviewBundleMarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.ReviewBundleJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_RejectsUntrustedBaselineLine()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var manifest = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            TrustLineClassification = "recomputed_but_insufficient_data_for_phase_1_target_decision",
            Phase11WrapperInvariantManifestMayReferenceThisLine = true,
            Phase10Decision = "reprioritize_to_wrapper",
            Phase10NextTrack = "wrapper_policy_shadow_offline",
            SurfaceManifests =
            [
                new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
                {
                    ManifestId = "manifest:worker:system:$.instructions",
                    InventoryId = "worker:system:$.instructions",
                    RequestKind = "worker",
                    SegmentKind = "system",
                    PayloadPath = "$.instructions",
                }
            ],
        };
        var validator = new RuntimeTokenWrapperOfflineValidatorResult
        {
            ResultDate = resultDate,
            CohortId = manifest.CohortId,
            TrustLineClassification = manifest.TrustLineClassification,
            Phase11WrapperValidatorMayReferenceThisLine = true,
            Phase10Decision = manifest.Phase10Decision,
            Phase10NextTrack = manifest.Phase10NextTrack,
            ValidatorVerdict = "ready_for_phase12_wrapper_candidate_design",
            Phase12WrapperCandidateMayStart = true,
        };
        var recollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = manifest.CohortId,
                RequestKinds = ["worker"],
            },
            Tasks = [],
        };

        var error = Assert.Throws<InvalidOperationException>(() =>
            RuntimeTokenWrapperCandidateService.Persist(
                workspace.Paths,
                manifest,
                validator,
                recollect,
                AiProviderConfig.CreateProviderDefaults("openai", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 500, reasoningEffort: "low"),
                "TestRepo",
                new StubGitClient(),
                new JsonTaskGraphRepository(workspace.Paths),
                new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low"),
                resultDate));

        Assert.Contains("trusted recomputed baseline line", error.Message, StringComparison.Ordinal);
    }

    private static void WriteExecutionPacket(ControlPlanePaths paths, string taskId, ExecutionPacket packet)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "execution-packets", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(packet, RepoJsonOptions));
    }

    private static void WriteContextPack(ControlPlanePaths paths, string taskId, ContextPack pack)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "context-packs", "tasks", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(pack, RepoJsonOptions));
    }
}
