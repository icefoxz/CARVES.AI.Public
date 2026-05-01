using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Application.Memory;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Evidence;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Memory;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Memory;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class ContextPackServiceTests
{
    [Fact]
    public void BuildForTask_ProjectsLocalTruthIntoDeterministicContextPack()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/tasks/cards/CARD-CTX.md", """
# CARD-CTX

## Title
Context pack

## Goal
Project compact task context.
""");
        workspace.WriteFile(".ai/memory/modules/resultenvelope.md", "# ResultEnvelope\nCompact summary.");
        workspace.WriteFile(".ai/memory/project/runtime_context.md", """
# Runtime Context

ResultEnvelope changes should preserve schema handling and keep result ingestion deterministic.
""");

        var task = new TaskNode
        {
            TaskId = "T-CTX-001",
            CardId = "CARD-CTX",
            Title = "Project compact context",
            Description = "Project compact task context for worker execution.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/ResultEnvelope.cs", "src/ResultIngestionService.cs"],
            Acceptance = ["context pack exists"],
            Constraints = ["keep projection local"],
            Dependencies = ["T-CTX-DEP-001"],
            LastWorkerDetailRef = ".ai/artifacts/worker-executions/T-CTX-001.json",
            LastProviderDetailRef = ".ai/artifacts/provider/T-CTX-001.json",
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-CTX-001",
                Title = "Context pack contract",
                Status = AcceptanceContractLifecycleStatus.Compiled,
                Intent = new AcceptanceContractIntent
                {
                    Goal = "Carry acceptance contract summary into worker-facing context.",
                    BusinessValue = "Workers should see governed intent before patching.",
                },
                AcceptanceExamples =
                [
                    new AcceptanceContractExample
                    {
                        Given = "Runtime truth already contains a scoped task",
                        When = "The worker context pack is built",
                        Then = "The acceptance contract summary is visible in prompt input",
                    },
                ],
                NonGoals = ["Do not widen runtime boundary."],
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "result_commit" },
                ],
                HumanReview = new AcceptanceContractHumanReviewPolicy
                {
                    Required = true,
                    ProvisionalAllowed = true,
                },
            },
        };
        var dependency = new TaskNode
        {
            TaskId = "T-CTX-DEP-001",
            Title = "Dependency task",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Completed,
            LastWorkerSummary = "Dependency summary captured from completed execution.",
        };
        var blocker = new TaskNode
        {
            TaskId = "T-CTX-BLOCK-001",
            Title = "Blocked by current task",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Dependencies = ["T-CTX-001"],
        };
        var graphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task, dependency, blocker], ["CARD-CTX"])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var run = executionRunService.PrepareRunForDispatch(task);
        var resultPath = workspace.WriteFile(".ai/execution/T-CTX-001/result.json", JsonSerializer.Serialize(new ResultEnvelope
        {
            TaskId = task.TaskId,
            ExecutionRunId = run.RunId,
            ExecutionEvidencePath = ".ai/artifacts/worker-executions/RUN-T-CTX-001-001/evidence.json",
            Status = "failed",
            Validation = new ResultEnvelopeValidation
            {
                Build = "failed",
                Tests = "not_run",
            },
        }));
        workspace.WriteFile(".ai/artifacts/worker-executions/RUN-T-CTX-001-001/evidence.json", JsonSerializer.Serialize(new ExecutionEvidence
        {
            RunId = run.RunId,
            TaskId = task.TaskId,
            BuildOutputRef = ".ai/artifacts/worker-executions/RUN-T-CTX-001-001/build.log",
            TestOutputRef = ".ai/artifacts/worker-executions/RUN-T-CTX-001-001/test.log",
            CommandLogRef = ".ai/artifacts/worker-executions/RUN-T-CTX-001-001/command.log",
            PatchRef = ".ai/artifacts/worker-executions/RUN-T-CTX-001-001/patch.diff",
        }));
        var completedRun = executionRunService.FailRun(run, resultPath);
        new ExecutionRunReportService(workspace.Paths).Persist(
            completedRun,
            new ResultEnvelope
            {
                TaskId = task.TaskId,
                ExecutionRunId = completedRun.RunId,
                Status = "failed",
                Validation = new ResultEnvelopeValidation
                {
                    Build = "failed",
                    Tests = "not_run",
                },
                Telemetry = new ExecutionTelemetry
                {
                    FilesChanged = 2,
                    LinesChanged = 12,
                },
            },
            failure: null);

        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        failureRepository.Append(new FailureReport
        {
            Id = "FAIL-CTX-001",
            TaskId = task.TaskId,
            CardId = task.CardId,
            Failure = new FailureDetails
            {
                Type = FailureType.BuildFailure,
                Message = "Missing schemaVersion handling",
            },
            Attribution = new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.9,
            },
            Result = new FailureResultSummary
            {
                Status = "failed",
                StopReason = "build_failure",
            },
            InputSummary = new FailureInputSummary
            {
                FilesInvolved = ["src/ResultEnvelope.cs"],
            },
        });

        var packService = new ContextPackService(
            workspace.Paths,
            graphService,
            new StaticCodeGraphQueryService(new CodeGraphScopeAnalysis(
                task.Scope,
                ["ResultEnvelope", "ResultIngestionService"],
                ["src/ResultEnvelope.cs", "src/ResultIngestionService.cs"],
                [],
                ["Execution"],
                ["ResultEnvelope: Result contract type", "src/ResultEnvelope.cs: Result schema handling"]),
                [
                    new Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry("module:ResultEnvelope", "ResultEnvelope", "src/ResultEnvelope", "Result envelope module summary.", Array.Empty<string>(), ["Execution"]),
                    new Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry("module:ResultIngestionService", "ResultIngestionService", "src/ResultIngestionService", "Result ingestion module summary.", Array.Empty<string>(), ["Execution"]),
                ]),
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new FailureSummaryProjectionService(workspace.Paths, new FailureContextService(failureRepository), executionRunService),
            executionRunService);

        var pack = packService.BuildForTask(task, "gpt-5-mini");
        var evidence = new RuntimeEvidenceStoreService(workspace.Paths).ListForTask(task.TaskId, RuntimeEvidenceKind.ContextPack);
        var telemetry = new ContextBudgetTelemetryService(workspace.Paths).ListRecent();

        Assert.True(File.Exists(Path.Combine(workspace.Paths.AiRoot, "runtime", "context-packs", "tasks", "T-CTX-001.json")));
        Assert.NotEmpty(evidence);
        Assert.NotEmpty(telemetry);
        Assert.Equal(RuntimeEvidenceKind.ContextPack, evidence[0].Kind);
        Assert.Equal("context_pack_build", telemetry[0].OperationKind);
        Assert.Equal(task.TaskId, telemetry[0].TaskId);
        Assert.Equal(pack.PackId, telemetry[0].PackId);
        Assert.Equal(pack.Audience.ToString(), telemetry[0].Audience);
        Assert.Equal(pack.ArtifactPath, telemetry[0].ArtifactPath);
        Assert.Equal(pack.FacetNarrowing.Phase, telemetry[0].FacetPhase);
        Assert.Contains("goal", telemetry[0].IncludedItemIds);
        Assert.Contains("task", telemetry[0].IncludedItemIds);
        Assert.Contains("acceptance_contract:AC-T-CTX-001", telemetry[0].IncludedItemIds);
        Assert.Contains("dependency:T-CTX-DEP-001", telemetry[0].IncludedItemIds);
        Assert.Contains("blocker:T-CTX-BLOCK-001", telemetry[0].IncludedItemIds);
        Assert.Contains("module:ResultEnvelope", telemetry[0].IncludedItemIds);
        Assert.Contains("recall:module_memory:.ai/memory/modules/resultenvelope.md", telemetry[0].IncludedItemIds);
        Assert.Contains(telemetry[0].ExpandableReferences, reference => reference.Kind == "worker_execution" && reference.Path == ".ai/artifacts/worker-executions/T-CTX-001.json");
        Assert.Contains(telemetry[0].SourcePaths, path => path == ".ai/runtime/context-packs/tasks/T-CTX-001.json");
        Assert.Contains(telemetry[0].SourcePaths, path => path == ".ai/memory/modules/resultenvelope.md");
        Assert.Contains(".ai/runtime/context-packs/tasks/T-CTX-001.json", evidence[0].ArtifactPaths);
        Assert.Equal("AC-T-CTX-001", pack.AcceptanceContract?.ContractId);
        Assert.Equal("T-CTX-DEP-001", Assert.Single(pack.LocalTaskGraph.Dependencies).TaskId);
        Assert.Equal("Dependency summary captured from completed execution.", Assert.Single(pack.LocalTaskGraph.Dependencies).Summary);
        Assert.Equal("T-CTX-BLOCK-001", Assert.Single(pack.LocalTaskGraph.Blockers).TaskId);
        Assert.Equal(2, pack.RelevantModules.Count);
        Assert.NotEmpty(pack.PromptSections);
        var goalSection = Assert.Single(pack.PromptSections, section => section.SectionId == "goal");
        Assert.True(goalSection.EndChar > goalSection.StartChar);
        Assert.Contains("Goal:", pack.PromptInput[goalSection.StartChar..goalSection.EndChar], StringComparison.Ordinal);
        Assert.Contains(pack.PromptSections, section => section.SectionId == "acceptance_contract");
        Assert.Contains(pack.PromptSections, section => section.SectionId == "windowed_reads");
        Assert.Equal("BuildFailure", pack.LastFailureSummary!.FailureType);
        Assert.Equal("semantic", pack.LastFailureSummary.FailureLane);
        Assert.Equal("failed", pack.LastFailureSummary.BuildStatus);
        Assert.Contains(".ai/artifacts/worker-executions/RUN-T-CTX-001-001/build.log", pack.LastFailureSummary.ArtifactReferences);
        Assert.Contains(pack.RelevantModules, module => string.Equals(module.Module, "ResultEnvelope", StringComparison.Ordinal) && string.Equals(module.Summary, "Result envelope module summary.", StringComparison.Ordinal));
        Assert.Contains("Local task graph:", pack.PromptInput);
        Assert.Contains("Acceptance contract:", pack.PromptInput);
        Assert.Contains("Do not widen runtime boundary.", pack.PromptInput);
        Assert.Contains("result_commit", pack.PromptInput);
        Assert.Contains("summary: Dependency summary captured from completed execution.", pack.PromptInput);
        Assert.Contains("Relevant modules:", pack.PromptInput);
        Assert.Equal("worker", pack.Budget.ProfileId);
        Assert.Equal(ContextBudgetPolicyResolver.EstimatorVersion, pack.Budget.EstimatorVersion);
        Assert.Equal(pack.ExpandableReferences.Count, pack.Budget.FullDocBlockedCount);
        Assert.Contains(ContextBudgetReasonCodes.FullDocBlocked, pack.Budget.BudgetViolationReasonCodes);
        Assert.Contains(ContextBudgetReasonCodes.EvidenceRequired, pack.Budget.BudgetViolationReasonCodes);
        Assert.Equal("task_context_build", pack.FacetNarrowing.Phase);
        Assert.Contains("ResultEnvelope", pack.FacetNarrowing.Modules);
        Assert.NotEmpty(pack.Recall);
        Assert.Contains(pack.Recall, item => item.Kind == "module_memory" && item.Source == ".ai/memory/modules/resultenvelope.md");
        Assert.Contains("Bounded recall:", pack.PromptInput);
        Assert.NotEmpty(pack.Budget.LargestContributors);
        Assert.Contains("module_memory:.ai/memory/modules/resultenvelope.md", pack.Budget.TopSources);
        Assert.Contains("module:ResultEnvelope", pack.Budget.TopSources);
        Assert.Contains(pack.ExpandableReferences, reference => reference.Kind == "worker_execution" && reference.Path == ".ai/artifacts/worker-executions/T-CTX-001.json");
        Assert.Contains(pack.ExpandableReferences, reference => reference.Kind == "provider_execution" && reference.Path == ".ai/artifacts/provider/T-CTX-001.json");
    }

    [Fact]
    public void BuildForTask_BuildsFacetNarrowedRecallExcerptsWithProvenance()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/memory/modules/resultingestionservice.md", """
# ResultIngestionService

Historical diary text that is not directly about the active task.

Result ingestion schema changes must preserve the schema migration gate and keep deterministic result writeback on the main execution path.
""");
        var task = new TaskNode
        {
            TaskId = "T-CTX-RECALL-001",
            Title = "Tighten result ingestion schema gate",
            Description = "Protect the result ingestion schema migration gate without widening runtime context.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/ResultIngestionService.cs"],
            Acceptance = ["schema gate preserved"],
            Constraints = ["stay prompt-safe"],
        };
        var graphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task], [])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var packService = new ContextPackService(
            workspace.Paths,
            graphService,
            new StaticCodeGraphQueryService(new CodeGraphScopeAnalysis(
                task.Scope,
                ["ResultIngestionService"],
                ["src/ResultIngestionService.cs"],
                [],
                [],
                ["ResultIngestionService: schema migration gate for result writeback"])),
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new FailureSummaryProjectionService(workspace.Paths, new FailureContextService(new JsonFailureReportRepository(workspace.Paths)), executionRunService),
            executionRunService);

        var pack = packService.BuildForTask(task, "gpt-5-mini");

        var recall = Assert.Single(pack.Recall, item => item.Kind == "module_memory");
        Assert.Equal(".ai/memory/modules/resultingestionservice.md", recall.Source);
        Assert.Contains("schema migration gate", recall.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Historical diary text", recall.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(recall.TokenEstimate > 0);
        Assert.Contains("ResultIngestionService", pack.FacetNarrowing.Modules);
    }

    [Fact]
    public void BuildForTask_ConsumesSelectedProjectUnderstandingRecipeIntoBoundedContextShaping()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/MyApi/Controllers/WeatherController.cs", "namespace MyApi.Controllers; public sealed class WeatherController {}");
        workspace.WriteFile("src/MyApi/Program.cs", "var builder = WebApplication.CreateBuilder(args);");
        workspace.WriteFile("src/MyApi/MyApi.csproj", "<Project Sdk=\"Microsoft.NET.Sdk.Web\"></Project>");
        workspace.WriteFile("tests/MyApi/WeatherControllerTests.cs", "public sealed class WeatherControllerTests {}");
        workspace.WriteFile("artifacts/runtime-pack-v1-dotnet-webapi.json", """
{
  "schemaVersion": "carves.pack.v1",
  "packId": "carves.firstparty.dotnet-webapi",
  "packVersion": "0.1.0",
  "name": "CARVES First-Party .NET Web API",
  "publisher": {
    "name": "CARVES",
    "trustLevel": "first_party"
  },
  "license": {
    "expression": "MIT"
  },
  "compatibility": {
    "carvesRuntime": ">=0.4.0",
    "frameworkHints": ["aspnetcore", "dotnet"],
    "repoSignals": ["*.csproj", "Program.cs"]
  },
  "capabilityKinds": ["project_understanding_recipe"],
  "requestedPermissions": {
    "readPaths": ["src/**", "tests/**", "*.csproj", "Program.cs"],
    "network": false,
    "env": false,
    "secrets": false,
    "truthWrite": false
  },
  "recipes": {
    "projectUnderstandingRecipes": [
      {
        "id": "dotnet-webapi-project-understanding",
        "description": "Prioritize source, project, and entry files for bounded context shaping.",
        "frameworkHints": ["aspnetcore", "dotnet"],
        "repoSignals": ["*.csproj", "Program.cs"],
        "includeGlobs": ["src/**/*.cs", "tests/**/*.cs", "*.csproj", "Program.cs"],
        "excludeGlobs": ["**/bin/**", "**/obj/**", ".git/**", ".ai/**", ".carves-platform/**"],
        "priorityRules": [
          { "id": "dotnet-src-priority", "glob": "src/**/*.cs", "weight": 90 },
          { "id": "dotnet-project-priority", "glob": "*.csproj", "weight": 85 }
        ]
      }
    ],
    "verificationRecipes": [],
    "reviewRubrics": []
  }
}
""");

        var task = new TaskNode
        {
            TaskId = "T-CTX-PACK-001",
            Title = "Shape context with selected pack",
            Description = "Bound context shaping through selected project understanding recipe.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/MyApi/Controllers/WeatherController.cs"],
            Acceptance = ["project understanding rules consumed"],
            Constraints = ["stay bounded"],
        };
        var graphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task], [])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        artifactRepository.SaveRuntimePackSelectionArtifact(new RuntimePackSelectionArtifact
        {
            SelectionId = "packsel-dotnet-webapi",
            PackId = "carves.firstparty.dotnet-webapi",
            PackVersion = "0.1.0",
            Channel = "stable",
            RuntimeStandardVersion = "0.4.0",
            PackArtifactPath = ".ai/artifacts/packs/dotnet-webapi.json",
            RuntimePackAttributionPath = ".ai/artifacts/packs/dotnet-webapi.attribution.json",
            ArtifactRef = ".ai/artifacts/packs/dotnet-webapi.json",
            ExecutionProfiles = new RuntimePackAdmissionProfileSelection
            {
                PolicyPreset = "core-default",
                GatePreset = "strict",
                ValidatorProfile = "default-validator",
                EnvironmentProfile = "workspace",
                RoutingProfile = "connected-lanes",
            },
            AdmissionSource = new RuntimePackAdmissionSource
            {
                AssignmentMode = "overlay_assignment",
                AssignmentRef = "artifacts/runtime-pack-v1-dotnet-webapi.json",
            },
            AdmissionCapturedAt = DateTimeOffset.UtcNow,
            SelectionMode = "manual_local_assignment",
            SelectionReason = "Selected for context shaping coverage.",
            Summary = "Selected declarative pack for context shaping coverage.",
            ChecksPassed = ["selection is derived from admitted current evidence"],
        });

        var executionRunService = new ExecutionRunService(workspace.Paths, artifactRepository);
        var packService = new ContextPackService(
            workspace.Paths,
            graphService,
            new StaticCodeGraphQueryService(new CodeGraphScopeAnalysis(
                task.Scope,
                ["WeatherController"],
                ["src/MyApi/Controllers/WeatherController.cs"],
                [],
                [],
                ["WeatherController: API endpoint controller"])),
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new FailureSummaryProjectionService(workspace.Paths, new FailureContextService(new JsonFailureReportRepository(workspace.Paths)), executionRunService),
            executionRunService,
            artifactRepository);

        var pack = packService.BuildForTask(task, "gpt-5-mini");

        Assert.Contains("runtime_pack:carves.firstparty.dotnet-webapi@0.1.0(stable)", pack.Constraints);
        Assert.Contains(pack.CodeHints, item => item.Contains("runtime pack current: carves.firstparty.dotnet-webapi@0.1.0 (stable)", StringComparison.Ordinal));
        Assert.Contains(pack.CodeHints, item => item.Contains("aspnetcore", StringComparison.Ordinal));
        Assert.Contains("src/MyApi/Program.cs", pack.FacetNarrowing.ScopeFiles);
        Assert.Contains("src/MyApi/MyApi.csproj", pack.FacetNarrowing.ScopeFiles);
        Assert.Contains("runtime_pack_project_understanding", pack.FacetNarrowing.ArtifactTypes);
        Assert.Contains(pack.ExpandableReferences, reference => reference.Kind == "runtime_pack_manifest" && reference.Path == "artifacts/runtime-pack-v1-dotnet-webapi.json");
        Assert.Contains("runtime pack current: carves.firstparty.dotnet-webapi@0.1.0 (stable)", pack.PromptInput, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildForTask_EnforcesBudgetAndTrimsDeterministically()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-CTX-TRIM-001",
            Title = "Trim context pack",
            Description = new string('x', 1200),
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"],
            Acceptance = ["budget enforced"],
            Constraints = ["stay small"],
        };
        var graphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task], [])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var failureRepository = new JsonFailureReportRepository(workspace.Paths);
        failureRepository.Append(new FailureReport
        {
            Id = "FAIL-CTX-TRIM",
            TaskId = task.TaskId,
            Failure = new FailureDetails
            {
                Type = FailureType.TestRegression,
                Message = new string('f', 400),
            },
            Attribution = new FailureAttribution
            {
                Layer = FailureAttributionLayer.Worker,
                Confidence = 0.9,
            },
            Result = new FailureResultSummary
            {
                Status = "failed",
                StopReason = "test_failure",
            },
        });
        var packService = new ContextPackService(
            workspace.Paths,
            graphService,
            new StaticCodeGraphQueryService(new CodeGraphScopeAnalysis(
                task.Scope,
                ["Alpha", "Beta", "Gamma", "Delta", "Epsilon"],
                ["src/Alpha.cs", "src/Beta.cs", "src/Gamma.cs"],
                [],
                [],
                [
                    "Alpha: alpha summary",
                    "Beta: beta summary",
                    "Gamma: gamma summary",
                    "Delta: delta summary",
                    "Epsilon: epsilon summary",
                    "src/Alpha.cs: alpha file"
                ])),
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new FailureSummaryProjectionService(workspace.Paths, new FailureContextService(failureRepository), executionRunService),
            executionRunService);

        var first = packService.BuildForTask(task, "gpt-5-mini", overrideMaxContextTokens: 200);
        var second = packService.BuildForTask(task, "gpt-5-mini", overrideMaxContextTokens: 200);
        var telemetry = new ContextBudgetTelemetryService(workspace.Paths).ListRecent();

        Assert.True(
            first.Budget.UsedTokens <= first.Budget.MaxContextTokens
            || string.Equals(first.Budget.BudgetPosture, ContextBudgetPostures.DegradedContext, StringComparison.Ordinal));
        Assert.NotEmpty(first.Trimmed);
        Assert.True(
            string.Equals(first.Budget.BudgetPosture, ContextBudgetPostures.HardCapEnforced, StringComparison.Ordinal)
            || string.Equals(first.Budget.BudgetPosture, ContextBudgetPostures.DegradedContext, StringComparison.Ordinal));
        Assert.Equal(first.Trimmed.Count, first.Budget.TruncatedItemsCount);
        Assert.Equal(first.Budget.FixedTokensEstimate + first.Budget.DynamicTokensEstimate, first.Budget.TotalContextTokensEstimate);
        Assert.True(first.Budget.DynamicTokensEstimate >= 0);
        Assert.Contains(ContextBudgetReasonCodes.TaskComplexity, first.Budget.BudgetViolationReasonCodes);
        Assert.Contains(ContextBudgetReasonCodes.EstimatorUncertain, first.Budget.BudgetViolationReasonCodes);
        Assert.Equal(first.PromptInput, second.PromptInput);
        Assert.Equal(first.Trimmed.Select(item => item.Key), second.Trimmed.Select(item => item.Key));
        Assert.Equal(first.Budget.BudgetPosture, second.Budget.BudgetPosture);
        Assert.NotEmpty(telemetry);
        Assert.NotEmpty(telemetry[0].TrimmedItems);
        Assert.Equal(second.Trimmed.Select(item => item.Key), telemetry[0].TrimmedItems.Select(item => item.ItemId));
        Assert.All(telemetry[0].TrimmedItems, item => Assert.False(string.IsNullOrWhiteSpace(item.Reason)));
    }

    [Fact]
    public void BuildForTask_ProjectsWindowedReadsAndCompactionForLargeScopeFiles()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/LargeContextFile.cs", string.Join(Environment.NewLine, Enumerable.Range(1, 220).Select(index => $"public string Value{index:D3} => \"Line{index:D3}\";")));
        var task = new TaskNode
        {
            TaskId = "T-CTX-WINDOW-001",
            Title = "Window large context file",
            Description = "Project bounded large-file reads into context-pack truth.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/LargeContextFile.cs"],
            Acceptance = ["windowed reads exist"],
            Constraints = ["stay preflight and deterministic"],
        };
        var graphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task], [])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var packService = new ContextPackService(
            workspace.Paths,
            graphService,
            new StaticCodeGraphQueryService(new CodeGraphScopeAnalysis(
                task.Scope,
                ["LargeContextModule"],
                ["src/LargeContextFile.cs"],
                [],
                [],
                ["LargeContextModule: windowed file should be bounded before execution"])),
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new FailureSummaryProjectionService(workspace.Paths, new FailureContextService(new JsonFailureReportRepository(workspace.Paths)), executionRunService),
            executionRunService);

        var pack = packService.BuildForTask(task, "gpt-5-mini", overrideMaxContextTokens: 280);

        var window = Assert.Single(pack.WindowedReads);
        Assert.Equal("src/LargeContextFile.cs", window.Path);
        Assert.Equal(1, window.StartLine);
        Assert.Equal(80, window.EndLine);
        Assert.Equal(220, window.TotalLines);
        Assert.True(window.Truncated);
        Assert.Equal("bounded_scope_windowing", pack.Compaction.Strategy);
        Assert.Equal(1, pack.Compaction.CandidateFileCount);
        Assert.Equal(1, pack.Compaction.RelevantFileCount);
        Assert.Equal(1, pack.Compaction.WindowedReadCount);
        Assert.Equal(0, pack.Compaction.OmittedFileCount);
        Assert.Contains("Windowed file reads:", pack.PromptInput, StringComparison.Ordinal);
        Assert.Contains("src/LargeContextFile.cs: lines 1-80 of 220", pack.PromptInput, StringComparison.Ordinal);
        var telemetry = Assert.Single(new ContextBudgetTelemetryService(workspace.Paths).ListRecent());
        Assert.Contains("windowed_read:src/LargeContextFile.cs:1-80", telemetry.IncludedItemIds);
        Assert.Contains("src/LargeContextFile.cs", telemetry.SourcePaths);
    }

    [Fact]
    public void RecordSearch_RemainsCompatibleWithoutContextPackFactFields()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ContextBudgetTelemetryService(workspace.Paths);

        var telemetry = service.RecordSearch(
            operationKind: "bounded_memory_search",
            profileId: "worker",
            queryText: "schema handling",
            budgetTokens: 100,
            usedTokens: 42,
            resultCount: 2,
            droppedItemsCount: 0,
            topSources: [".ai/memory/modules/resultenvelope.md"],
            outcome: "search_completed");

        Assert.Equal("bounded_memory_search", telemetry.OperationKind);
        Assert.Null(telemetry.PackId);
        Assert.Null(telemetry.Audience);
        Assert.Null(telemetry.ArtifactPath);
        Assert.Null(telemetry.FacetPhase);
        Assert.Empty(telemetry.IncludedItemIds);
        Assert.Empty(telemetry.TrimmedItems);
        Assert.Empty(telemetry.ExpandableReferences);
        Assert.Empty(telemetry.SourcePaths);
    }

    [Fact]
    public void ContextBudgetTelemetryRecord_DoesNotAddSubjectiveOptimizationFields()
    {
        var forbiddenTerms = new[]
        {
            "usefulness",
            "judgment",
            "recommended",
            "recommendation",
            "quality",
            "confidence",
            "auto_optimize",
            "autooptimization",
            "keep",
            "remove",
        };
        var propertyNames = typeof(ContextBudgetTelemetryRecord)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var propertyName in propertyNames)
        {
            foreach (var forbiddenTerm in forbiddenTerms)
            {
                Assert.DoesNotContain(forbiddenTerm, propertyName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void BuildForTask_IncludesProjectedActiveFactsAndExcludesInvalidatedFacts()
    {
        using var workspace = new TemporaryWorkspace();
        var repoScope = $"repo:{Path.GetFileName(workspace.RootPath)}";
        var promotionService = new RuntimeMemoryPromotionService(workspace.Paths);

        var activeCandidate = promotionService.StageCandidate(
            category: "project",
            title: "Active memory filter excludes invalidated facts.",
            summary: "Capture the active read rule for Batch A.",
            statement: "Active memory filter excludes invalidated facts from context assembly.",
            scope: repoScope,
            proposer: "ContextPackService",
            sourceEvidenceIds: ["CTXEVI-ACTIVE-001"],
            confidence: 0.93);
        var activeAudit = promotionService.RecordCandidateAudit(activeCandidate.CandidateId, MemoryPromotionAuditDecision.Approved, "review-task", "Stage active fact.");
        var activeFact = promotionService.PromoteCandidateToProvisional(activeCandidate.CandidateId, activeAudit.AuditId, "memory-review");

        var staleCandidate = promotionService.StageCandidate(
            category: "project",
            title: "Old filter included invalidated facts.",
            summary: "Capture a stale rule for exclusion coverage.",
            statement: "Old context assembly allowed invalidated facts into active reads.",
            scope: repoScope,
            proposer: "ContextPackService",
            sourceEvidenceIds: ["CTXEVI-STALE-001"],
            confidence: 0.71);
        var staleAudit = promotionService.RecordCandidateAudit(staleCandidate.CandidateId, MemoryPromotionAuditDecision.Approved, "review-task", "Stage stale fact.");
        var staleFact = promotionService.PromoteCandidateToProvisional(staleCandidate.CandidateId, staleAudit.AuditId, "memory-review");
        var invalidateAudit = promotionService.RecordFactAudit(staleFact.FactId, MemoryPromotionAuditDecision.Approved, "review-task", "Invalidate stale fact.");
        var invalidatedFact = promotionService.InvalidateFact(staleFact.FactId, invalidateAudit.AuditId, "memory-review", "No longer valid.");

        var task = new TaskNode
        {
            TaskId = "T-CTX-ACTIVE-001",
            Title = "Protect active memory filter",
            Description = "Exclude invalidated facts from context reads.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["src/ContextPackService.cs"],
            Acceptance = ["active memory filter excludes invalidated facts"],
            Constraints = ["stay query-time only"],
        };
        var graphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph([task], [])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var executionRunService = new ExecutionRunService(workspace.Paths);
        var packService = new ContextPackService(
            workspace.Paths,
            graphService,
            new StaticCodeGraphQueryService(new CodeGraphScopeAnalysis(
                task.Scope,
                ["ContextPackService"],
                ["src/ContextPackService.cs"],
                [],
                [],
                ["ContextPackService: active memory filter protects context assembly"])),
            new MemoryService(new FileMemoryRepository(workspace.Paths), new ExecutionContextBuilder()),
            new FailureSummaryProjectionService(workspace.Paths, new FailureContextService(new JsonFailureReportRepository(workspace.Paths)), executionRunService),
            executionRunService);

        var pack = packService.BuildForTask(task, "gpt-5-mini");

        Assert.Contains(pack.Recall, item => item.Source == $".ai/evidence/facts/{activeFact.FactId}.json");
        Assert.DoesNotContain(pack.Recall, item => item.Source == $".ai/evidence/facts/{invalidatedFact.FactId}.json");
        Assert.Contains(pack.Recall, item => item.Text.Contains("excludes invalidated facts", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(pack.Recall, item => item.Text.Contains("allowed invalidated facts into active reads", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StaticCodeGraphQueryService : ICodeGraphQueryService
    {
        private readonly CodeGraphScopeAnalysis scopeAnalysis;
        private readonly IReadOnlyList<Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry> moduleSummaries;

        public StaticCodeGraphQueryService(
            CodeGraphScopeAnalysis scopeAnalysis,
            IReadOnlyList<Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry>? moduleSummaries = null)
        {
            this.scopeAnalysis = scopeAnalysis;
            this.moduleSummaries = moduleSummaries ?? Array.Empty<Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry>();
        }

        public Carves.Runtime.Domain.CodeGraph.CodeGraphManifest LoadManifest()
        {
            return new Carves.Runtime.Domain.CodeGraph.CodeGraphManifest();
        }

        public IReadOnlyList<Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry> LoadModuleSummaries()
        {
            return moduleSummaries;
        }

        public Carves.Runtime.Domain.CodeGraph.CodeGraphIndex LoadIndex()
        {
            return new Carves.Runtime.Domain.CodeGraph.CodeGraphIndex();
        }

        public CodeGraphScopeAnalysis AnalyzeScope(IEnumerable<string> scopeEntries)
        {
            return scopeAnalysis;
        }

        public CodeGraphImpactAnalysis AnalyzeImpact(IEnumerable<string> scopeEntries)
        {
            return CodeGraphImpactAnalysis.Empty;
        }
    }
}
