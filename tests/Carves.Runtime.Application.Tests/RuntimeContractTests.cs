using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeContractTests
{
    [Fact]
    public void ContractSchemas_ExistAndRemainVersioned()
    {
        var repoRoot = ResolveRepoRoot();
        var manifest = LoadContractPresenceManifest(repoRoot);

        foreach (var relativePath in manifest.SchemaFiles)
        {
            var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Missing contract schema: {relativePath}");
            var document = JsonNode.Parse(File.ReadAllText(fullPath))!.AsObject();
            Assert.NotNull(document["$schema"]);
            Assert.NotNull(document["title"]);
        }

        foreach (var relativePath in manifest.RequiredFiles)
        {
            var fullPath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Missing required contract file: {relativePath}");
        }
    }

    [Fact]
    public void ControlPlaneConfigRepository_LoadsCarvesCodeStandard()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/config/carves_code_standard.json", """
{
  "version": "0.4",
  "core_loop": "Controller -> Execute -> Recorder -> View",
  "interaction_loop": "View -> Controller -> Execute -> Recorder -> View",
  "authority": {
    "recorder_writable_by": ["Execute"],
    "domain_events_emitted_by": ["Recorder"],
    "view_read_only": true,
    "controller_direct_recorder_write_forbidden": true,
    "actor_direct_recorder_write_forbidden": true
  },
  "applicability": {
    "carves_purpose": "CARVES constrains generated or modified product code.",
    "runtime_purpose": "CARVES.Runtime governs AI planning, execution, safety, and review for that code.",
    "directory_layout_required": false,
    "one_class_per_layer_required": false,
    "refactor_for_purity_alone_forbidden": true
  },
  "ai_friendly_architecture": {
    "core_principle": "Structure first.",
    "domain_oriented_layout_preferred": true,
    "avoid_generic_directories": true,
    "avoid_generic_type_names": true,
    "interfaces_only_at_real_boundaries": true,
    "explicit_state_modeling_required": true,
    "recommended_file_lines_lower_bound": 200,
    "recommended_file_lines_upper_bound": 500,
    "refactor_file_lines_threshold": 800,
    "max_conceptual_jumps": 5
  },
  "physical_splitting": {
    "core_principle": "Logical strict, physical elastic.",
    "logical_layers_strict": true,
    "physical_splitting_elastic": true,
    "split_score_can_split": 3,
    "split_score_should_split": 4,
    "avoid_thin_forwarder_splits": true,
    "avoid_completeness_splits": true,
    "shared_only_for_non_sovereign_support": true,
    "runtime_recommended_independent_modules": ["TaskTransitionPolicy"]
  },
  "extreme_naming": {
    "core_principle": "Naming is architecture contract.",
    "naming_grammar": "<Domain><Qualifier?><RoleSuffix>",
    "domain_must_lead": true,
    "role_suffix_must_be_terminal": true,
    "full_words_required": true,
    "pascal_case_required": true,
    "file_name_matches_primary_type": true,
    "one_concept_one_headword": true,
    "canonical_vocabulary_required": true,
    "application_service_requires_compatibility_annotation": true,
    "engine_system_level_only": true,
    "canonical_architectural_terms": ["Controller", "Actor", "Recorder", "View", "Execute", "Shared"],
    "canonical_execute_terms": ["Procedure", "Module", "Service"],
    "canonical_runtime_terms": ["Planner", "Scheduler", "Worker", "Detector", "Evaluator", "Opportunity"],
    "canonical_platform_terms": ["Registry", "Instance", "Broker", "Gateway", "Lease", "Quota", "Policy", "Event", "Dashboard", "OperatorApi"],
    "canonical_mechanism_terms": ["Command", "Reason", "Decision", "Policy", "Dto"],
    "forbidden_generic_words": ["Manager", "Processor", "Helper"],
    "suggested_analyzer_rules": ["CARVES001", "CARVES201", "CARVES301"],
    "platform_forbidden_aliases": {
      "Directory": "Registry",
      "Hub": "Broker"
    },
    "platform_lint_paths": ["src/CARVES.Runtime.Application/Platform"],
    "platform_lint_allowlist_type_names": ["RuntimeInstanceManager"]
  },
  "dependency_contract": {
    "core_principle": "Dependency direction is architecture law.",
    "dependency_direction_one_way": true,
    "same_layer_coupling_restricted": true,
    "role_classification_precedence": ["attribute", "namespace", "directory", "suffix"],
    "included_edge_kinds": ["constructor", "field", "property", "method_invocation", "object_creation", "implemented_interface", "base_type"],
    "excluded_edge_kinds": ["reflection", "service_locator", "source_generator_wiring", "di_registration", "dynamic_dispatch_beyond_symbol_resolution"],
    "recorder_access_model": "split_reader_writer_ports_preferred",
    "ambiguous_symbol_policy": "emit_info_and_skip_strict_enforcement",
    "allowed_dependency_matrix": {
      "Controller": ["Procedure", "Shared"],
      "View": ["Controller", "RecorderReader", "Shared"],
      "Procedure": ["Module", "Shared"],
      "Module": ["Service", "Actor", "RecorderWriter", "Shared"],
      "Service": ["Service", "Shared"],
      "Actor": ["Shared"],
      "Recorder": ["Shared"],
      "Shared": ["Shared"]
    },
    "forbidden_diagnostic_rules": ["CARVES101", "CARVES102", "CARVES103", "CARVES104", "CARVES105", "CARVES106", "CARVES107", "CARVES108", "CARVES109"],
    "restricted_diagnostic_rules": ["CARVES201", "CARVES202", "CARVES203"],
    "advisory_diagnostic_rules": ["CARVES100", "CARVES301", "CARVES302", "CARVES303"]
  },
  "allowed_edges": ["Controller -> Procedure"],
  "restricted_edges": ["Module -> Module requires explicit reason"],
  "forbidden_edges": ["Controller -> Recorder(write)"],
  "moderation_rules": ["Treat CARVES as interpretation first."],
  "review_questions": ["Where is the authoritative state?"],
  "runtime_questions": ["Which CARVES layers does this runtime task affect?"]
}
""");

        var repository = new FileControlPlaneConfigRepository(workspace.Paths);
        var standard = repository.LoadCarvesCodeStandard();

        Assert.Equal("0.4", standard.Version);
        Assert.Contains("Execute", standard.Authority.RecorderWritableBy);
        Assert.Contains("Controller -> Recorder(write)", standard.ForbiddenEdges);
        Assert.False(standard.Applicability.DirectoryLayoutRequired);
        Assert.True(standard.Applicability.RefactorForPurityAloneForbidden);
        Assert.True(standard.AiFriendlyArchitecture.DomainOrientedLayoutPreferred);
        Assert.Equal(800, standard.AiFriendlyArchitecture.RefactorFileLinesThreshold);
        Assert.True(standard.PhysicalSplitting.PhysicalSplittingElastic);
        Assert.Equal(4, standard.PhysicalSplitting.SplitScoreShouldSplit);
        Assert.Equal("<Domain><Qualifier?><RoleSuffix>", standard.ExtremeNaming.NamingGrammar);
        Assert.True(standard.ExtremeNaming.OneConceptOneHeadword);
        Assert.Contains("Controller", standard.ExtremeNaming.CanonicalArchitecturalTerms);
        Assert.Contains("OperatorApi", standard.ExtremeNaming.CanonicalPlatformTerms);
        Assert.Contains("Manager", standard.ExtremeNaming.ForbiddenGenericWords);
        Assert.Contains("CARVES201", standard.ExtremeNaming.SuggestedAnalyzerRules);
        Assert.Equal("Registry", standard.ExtremeNaming.PlatformForbiddenAliases["Directory"]);
        Assert.Contains("src/CARVES.Runtime.Application/Platform", standard.ExtremeNaming.PlatformLintPaths);
        Assert.Contains("RuntimeInstanceManager", standard.ExtremeNaming.PlatformLintAllowlistTypeNames);
        Assert.True(standard.DependencyContract.DependencyDirectionOneWay);
        Assert.Equal("split_reader_writer_ports_preferred", standard.DependencyContract.RecorderAccessModel);
        Assert.Contains("Procedure", standard.DependencyContract.AllowedDependencyMatrix["Controller"]);
        Assert.Contains("CARVES101", standard.DependencyContract.ForbiddenDiagnosticRules);
        Assert.Contains("Treat CARVES as interpretation first.", standard.ModerationRules);
        Assert.Contains("Which CARVES layers does this runtime task affect?", standard.RuntimeQuestions);
    }

    [Fact]
    public void RuntimeArtifacts_AreWrittenWithVersionedSchemaFields()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new JsonRuntimeArtifactRepository(workspace.Paths);
        var task = new TaskNode
        {
            TaskId = "T-CONTRACT",
            Title = "Contract artifact test",
            Status = DomainTaskStatus.Pending,
            Scope = ["src/Contract.cs"],
            Acceptance = ["artifacts exist"],
        };
        var session = new ExecutionSession(
            task.TaskId,
            task.Title,
            workspace.RootPath,
            false,
            "abc123",
            "NullAiClient",
            Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId),
            DateTimeOffset.UtcNow);
        var request = new WorkerRequest
        {
            Task = task,
            Session = session,
            AiRequest = new AiExecutionRequest(task.TaskId, task.Title, "Follow the task.", "input", 200),
        };
        var report = new TaskRunReport
        {
            TaskId = task.TaskId,
            Request = request,
            Session = session,
            Validation = new ValidationResult
            {
                Passed = true,
                Evidence = ["validation passed"],
            },
            SafetyDecision = SafetyDecision.Allow(task.TaskId),
        };

        repository.SaveWorkerArtifact(new TaskRunArtifact { Report = report });
        repository.SaveWorkerExecutionArtifact(new WorkerExecutionArtifact
        {
            TaskId = task.TaskId,
            Result = new WorkerExecutionResult
            {
                TaskId = task.TaskId,
                BackendId = "codex-sdk",
                ProviderId = "codex",
                AdapterId = "CodexWorkerAdapter",
                Summary = "Worker contract artifact.",
                Status = WorkerExecutionStatus.Succeeded,
            },
        });
        repository.SaveProviderArtifact(new AiExecutionArtifact
        {
            TaskId = task.TaskId,
            Record = AiExecutionRecord.Skipped("null", "none", "dry-run", "preview", "hash"),
        });
        repository.SaveSafetyArtifact(new SafetyArtifact
        {
            Decision = SafetyDecision.Allow(task.TaskId),
        });
        repository.SavePlannerReviewArtifact(new PlannerReviewArtifact
        {
            TaskId = task.TaskId,
            Review = new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Validated.",
                AcceptanceMet = true,
            },
            ResultingStatus = DomainTaskStatus.Completed,
            TransitionReason = "Validation passed.",
            PlannerComment = "Validated.",
            PatchSummary = "files=0; lines=0; paths=(none)",
            ValidationPassed = true,
            ValidationEvidence = ["validation passed"],
            SafetyOutcome = SafetyOutcome.Allow,
            SafetyIssues = [],
            DecisionStatus = ReviewDecisionStatus.Approved,
        });
        repository.SaveMergeCandidateArtifact(new MergeCandidateArtifact
        {
            TaskId = task.TaskId,
            ReviewReason = "Approved by human review.",
            PlannerComment = "Validated.",
            ResultCommit = "abc123",
            PatchSummary = "files=0; lines=0; paths=(none)",
            ValidationPassed = true,
            SafetyOutcome = SafetyOutcome.Allow,
        });

        Assert.Contains("\"schema_version\": 1", File.ReadAllText(Path.Combine(workspace.Paths.WorkerArtifactsRoot, "T-CONTRACT.json")), StringComparison.Ordinal);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(Path.Combine(workspace.Paths.WorkerExecutionArtifactsRoot, "T-CONTRACT.json")), StringComparison.Ordinal);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(Path.Combine(workspace.Paths.ProviderArtifactsRoot, "T-CONTRACT.json")), StringComparison.Ordinal);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(Path.Combine(workspace.Paths.SafetyArtifactsRoot, "T-CONTRACT.json")), StringComparison.Ordinal);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(Path.Combine(workspace.Paths.ReviewArtifactsRoot, "T-CONTRACT.json")), StringComparison.Ordinal);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(Path.Combine(workspace.Paths.MergeArtifactsRoot, "T-CONTRACT.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void TaskGraphRepository_LoadsLegacyTaskNodesWithoutSchemaVersion()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile(".ai/tasks/graph.json", """
{
  "version": 1,
  "updated_at": "2026-03-16T00:00:00Z",
  "cards": ["CARD-LEGACY"],
  "tasks": [
    {
      "task_id": "T-LEGACY",
      "title": "Legacy node",
      "status": "pending",
      "priority": "P1",
      "card_id": "CARD-LEGACY",
      "dependencies": [],
      "node_file": "nodes/T-LEGACY.json"
    }
  ]
}
""");
        workspace.WriteFile(".ai/tasks/nodes/T-LEGACY.json", """
{
  "task_id": "T-LEGACY",
  "title": "Legacy node",
  "description": "Pre-versioned payload.",
  "status": "pending",
  "task_type": "feature",
  "priority": "P1",
  "source": "LEGACY",
  "card_id": "CARD-LEGACY",
  "dependencies": [],
  "scope": ["src/Legacy.cs"],
  "acceptance": ["legacy load works"],
  "constraints": [],
  "validation": {
    "commands": [],
    "checks": [],
    "expected_evidence": []
  },
  "retry_count": 0,
  "capabilities": [],
  "metadata": {},
  "planner_review": {
    "verdict": "continue",
    "reason": "Legacy payload",
    "acceptance_met": false,
    "boundary_preserved": true,
    "scope_drift_detected": false,
    "follow_up_suggestions": []
  }
}
""");

        var repository = new JsonTaskGraphRepository(workspace.Paths);
        var loaded = repository.Load();

        Assert.True(loaded.Tasks.ContainsKey("T-LEGACY"));
        Assert.Equal(TaskType.Execution, loaded.Tasks["T-LEGACY"].TaskType);
        repository.Save(loaded);
        Assert.Contains("\"schema_version\": 1", File.ReadAllText(Path.Combine(workspace.Paths.TaskNodesRoot, "T-LEGACY.json")), StringComparison.Ordinal);
        Assert.Contains("\"task_type\": \"execution\"", File.ReadAllText(Path.Combine(workspace.Paths.TaskNodesRoot, "T-LEGACY.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void RefactoringBacklogRepository_RoundTripsVersionedSnapshot()
    {
        using var workspace = new TemporaryWorkspace();
        var repository = new RefactoringBacklogRepository(workspace.Paths);
        var snapshot = new RefactoringBacklogSnapshot
        {
            Items =
            [
                new RefactoringBacklogItem
                {
                    ItemId = "RB-CONTRACT",
                    Fingerprint = "abc",
                    Kind = "large-file",
                    Path = "src/Contract.cs",
                    Reason = "Large file",
                    Priority = "P3",
                },
            ],
        };

        repository.Save(snapshot);
        var loaded = repository.Load();

        Assert.Equal(1, loaded.Version);
        Assert.Contains("\"version\": 1", File.ReadAllText(Path.Combine(workspace.RootPath, ".ai", "refactoring", "backlog.json")), StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static RuntimeContractPresenceManifest LoadContractPresenceManifest(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "tests", "Carves.Runtime.Application.Tests", "TestData", "runtime-contract-presence.manifest.json");
        var manifest = JsonSerializer.Deserialize<RuntimeContractPresenceManifest>(File.ReadAllText(manifestPath));
        return manifest ?? throw new InvalidOperationException("Runtime contract presence manifest is missing or invalid.");
    }

    private sealed class RuntimeContractPresenceManifest
    {
        public string[] SchemaFiles { get; init; } = [];

        public string[] RequiredFiles { get; init; } = [];
    }
}
