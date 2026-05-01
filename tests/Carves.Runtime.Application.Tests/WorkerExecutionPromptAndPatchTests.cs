using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Processes;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Safety;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class WorkerExecutionPromptAndPatchTests
{
    [Fact]
    public void WorkerAiRequestFactory_InstructsWorkerToFinishWithoutConfirmation()
    {
        var factory = new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low");
        var task = new TaskNode
        {
            TaskId = "T-WORKER-INSTRUCTIONS",
            Title = "Assess provider evidence",
            Description = "Produce a bounded assessment.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = [".ai/runtime/sustainability"],
            Acceptance = ["assessment is produced"],
        };
        var contextPack = new ContextPack
        {
            Goal = "Produce the assessment.",
            Task = "Assess provider evidence.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-instructions",
            Allowed = true,
            SelectedBackendId = "openai_api",
            SelectedProviderId = "openai",
            SelectedModelId = "gpt-4.1",
            Summary = "Selected openai backend.",
            RouteSource = "active_profile_fallback",
            RoutingIntent = "reasoning_summary",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "openai_api",
            validationCommands: [],
            selection);

        Assert.Contains("Do not ask for confirmation", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("return the final assessment directly instead of a plan", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Keep assessment-only responses compact", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Host-governed initialization and task packet assembly are already satisfied", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not spend startup budget re-reading broad repo entry or governance documents as a ritual", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not emit a `CARVES.AI initialization report`, `Agent bootstrap sources`, or any other bootstrap restatement inside delegated execution.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Re-open `README.md`, `AGENTS.md`, `.ai/memory/architecture/*`, `.ai/PROJECT_BOUNDARY.md`, or `.ai/STATE.md` only when the task scope, execution packet, or a concrete escalation trigger explicitly requires them.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("The shell is Windows PowerShell.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not use bash-only edit syntax such as `ApplyPatch <<'PATCH'`", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("use PowerShell-native commands like `Get-Content -Raw`", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("prefer a pattern like `$path = 'relative/file.cs'; @'...content...'@ | Set-Content -Path $path`", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not use ambiguous PowerShell alias/filter shorthand such as `?` or incomplete `Where-Object`", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("prefer direct `rg <pattern> <path>` over `rg --files ... | ? ...`", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Formal build/test validation is executed by CARVES after the worker returns.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not run `dotnet restore`, `dotnet build`, or `dotnet test` as routine verification", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("do not use `git status` or `git diff` as routine patch verification", request.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Use `python3` only for bounded file edits, not for repository inspection.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not build helper scripts that bulk-print task truth or dump multiple source files to stdout as preflight.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("read at most one or two directly relevant files per command", request.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("after roughly four to six targeted shell reads", request.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not concatenate broad `sed -n`/`cat` readbacks across multiple changed files", request.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("For implementation tasks, do not stop at a plan, file inventory, or intent recap once the target edit surface is clear.", request.Instructions, StringComparison.Ordinal);
        Assert.Equal(900, request.MaxOutputTokens);
    }

    [Fact]
    public void WorkerAiRequestFactory_UsesBashShellGuidanceForLocalCodexCliOnPosix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var factory = new WorkerAiRequestFactory(500, 30, "codex-cli", "low");
        var task = new TaskNode
        {
            TaskId = "T-CODEX-CLI-SHELL-GUIDANCE",
            Title = "Assess provider evidence",
            Description = "Produce a bounded assessment.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["docs/runtime/"],
            Acceptance = ["assessment is produced"],
        };
        var contextPack = new ContextPack
        {
            Goal = "Produce the assessment.",
            Task = "Assess provider evidence.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-instructions",
            Allowed = true,
            SelectedBackendId = "codex_cli",
            SelectedProviderId = "codex",
            SelectedModelId = "codex-cli",
            Summary = "Selected codex cli backend.",
            RouteSource = "active_profile_no_match",
            RoutingIntent = "code_small",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "codex_cli",
            validationCommands: [],
            selection);

        Assert.Contains("gives you a POSIX/bash shell", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not use PowerShell-only commands such as `Get-Content`", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("prefer `rg`, `sed -n`, `cat`, `head`, and `tail`", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("adapt it to bash instead of retrying the same PowerShell pattern", request.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("The shell is Windows PowerShell.", request.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain("use PowerShell-native commands", request.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerAiRequestFactory_AppliesWorkerWrapperCanaryOnlyForExplicitAllowlist()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.CanaryEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "worker",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.instructions",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        var factory = new WorkerAiRequestFactory(
            500,
            30,
            "gpt-5-mini",
            "low",
            new RuntimeTokenWorkerWrapperCanaryService(name => env.TryGetValue(name, out var value) ? value : null));
        var task = new TaskNode
        {
            TaskId = "T-WORKER-CANARY-FACTORY",
            Title = "Assess provider evidence",
            Description = "Produce a bounded assessment.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = [".ai/runtime/sustainability"],
            Acceptance = ["assessment is produced"],
        };
        var contextPack = new ContextPack
        {
            Goal = "Produce the assessment.",
            Task = "Assess provider evidence.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-canary",
            Allowed = true,
            SelectedBackendId = "openai_api",
            SelectedProviderId = "openai",
            SelectedModelId = "gpt-4.1",
            Summary = "Selected openai backend.",
            RouteSource = "active_profile_fallback",
            RoutingIntent = "patch_draft",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "openai_api",
            validationCommands: [],
            selection);

        Assert.Contains("Hard boundaries", request.Instructions, StringComparison.Ordinal);
        Assert.Equal("true", request.Metadata["worker_wrapper_canary_candidate_applied"]);
        Assert.Equal("false", request.Metadata["worker_wrapper_main_path_default_enabled"]);
        Assert.Equal("active_canary", request.Metadata["worker_wrapper_decision_mode"]);
        Assert.Equal("candidate_applied", request.Metadata["worker_wrapper_canary_decision_reason"]);
        Assert.Equal(RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion, request.Metadata["worker_wrapper_canary_candidate_version"]);
        Assert.Equal(RuntimeTokenWorkerWrapperCanaryService.FallbackVersion, request.Metadata["worker_wrapper_canary_fallback_version"]);
    }

    [Fact]
    public void WorkerAiRequestFactory_UsesLimitedMainPathDefaultInsideFrozenScope()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.MainPathDefaultEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "worker",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.instructions",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        var factory = new WorkerAiRequestFactory(
            500,
            30,
            "gpt-5-mini",
            "low",
            new RuntimeTokenWorkerWrapperCanaryService(name => env.TryGetValue(name, out var value) ? value : null));
        var task = new TaskNode
        {
            TaskId = "T-WORKER-MAIN-PATH-DEFAULT",
            Title = "Use frozen main-path default",
            Description = "Use the worker wrapper candidate as the scoped main-path default.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = [".ai/runtime/sustainability"],
            Acceptance = ["assessment is produced"],
        };
        var contextPack = new ContextPack
        {
            Goal = "Produce the assessment.",
            Task = "Assess provider evidence.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-main-path-default",
            Allowed = true,
            SelectedBackendId = "null_worker",
            SelectedProviderId = "local",
            SelectedModelId = "none",
            Summary = "Selected null_worker backend.",
            RouteSource = "active_profile_fallback",
            RoutingIntent = "patch_draft",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "null_worker",
            validationCommands: [],
            selection);

        Assert.Contains("Hard boundaries", request.Instructions, StringComparison.Ordinal);
        Assert.Equal("true", request.Metadata["worker_wrapper_main_path_default_enabled"]);
        Assert.Equal("true", request.Metadata["worker_wrapper_canary_candidate_applied"]);
        Assert.Equal("limited_main_path_default", request.Metadata["worker_wrapper_decision_mode"]);
        Assert.Equal("main_path_default", request.Metadata["worker_wrapper_canary_decision_reason"]);
        Assert.Equal(RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion, request.Metadata["worker_wrapper_canary_candidate_version"]);
        Assert.Equal(RuntimeTokenWorkerWrapperCanaryService.FallbackVersion, request.Metadata["worker_wrapper_canary_fallback_version"]);
    }

    [Fact]
    public void WorkerAiRequestFactory_EmbedsValidationCommandsAsRuntimeOwnedContract()
    {
        var factory = new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low");
        var task = new TaskNode
        {
            TaskId = "T-WORKER-VALIDATION-CONTRACT",
            Title = "Add focused unit coverage",
            Description = "Apply a narrow unit-test update.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["tests/Ordering.UnitTests/"],
            Acceptance =
            [
                "focused unit coverage is added",
                "Ordering.UnitTests passes for the added coverage",
            ],
        };
        var contextPack = new ContextPack
        {
            Goal = "Add the focused unit coverage.",
            Task = "Update the unit tests only.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-validation-contract",
            Allowed = true,
            SelectedBackendId = "codex_cli",
            SelectedProviderId = "codex",
            SelectedModelId = "gpt-5-codex",
            Summary = "Selected codex cli backend.",
            RouteSource = "active_profile_no_match",
            RoutingIntent = "patch_draft",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "codex_cli",
            validationCommands:
            [
                ["dotnet", "test", "tests/Ordering.UnitTests/Ordering.UnitTests.csproj", "--no-restore"],
            ],
            selection);

        Assert.Contains("Formal validation commands (executed by CARVES after you finish; do not run them yourself):", request.Input, StringComparison.Ordinal);
        Assert.Contains("- dotnet test tests/Ordering.UnitTests/Ordering.UnitTests.csproj --no-restore", request.Input, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerAiRequestFactory_EmbedsClosureContractAsWorkerInputAndEnvelopeSegment()
    {
        var factory = new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low");
        var task = new TaskNode
        {
            TaskId = "T-WORKER-CLOSURE-CONTRACT",
            Title = "Apply governed worker slice",
            Description = "Carry closure contract details into the worker request.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs"],
            Acceptance = ["worker request includes the closure contract"],
        };
        var contextPack = new ContextPack
        {
            Goal = "Apply the governed worker slice.",
            Task = "Carry closure contract details into the worker request.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-closure-contract",
            Allowed = true,
            SelectedBackendId = "codex_cli",
            SelectedProviderId = "codex",
            SelectedModelId = "gpt-5-codex",
            Summary = "Selected codex cli backend.",
            RouteSource = "active_profile_no_match",
            RoutingIntent = "patch_draft",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "codex_cli",
            validationCommands: [],
            selection);

        Assert.Contains("Result closure contract (Host verifies this after the worker returns; your completion claim is not truth):", request.Input, StringComparison.Ordinal);
        Assert.Contains("Worker execution packet (Host-issued execution contract; this is not a queue or truth root):", request.Input, StringComparison.Ordinal);
        Assert.Contains("WEP-T-WORKER-CLOSURE-CONTRACT-v1", request.Input, StringComparison.Ordinal);
        Assert.Contains("generic_review_closure_v1", request.Input, StringComparison.Ordinal);
        Assert.Contains("patch_scope_recorded", request.Input, StringComparison.Ordinal);
        Assert.Contains("focused_required_validation", request.Input, StringComparison.Ordinal);
        Assert.Contains("contract_items_satisfied", request.Input, StringComparison.Ordinal);
        Assert.Contains("Host checks required fields, worker-claimable contract items, allowed files, and forbidden vocabulary", request.Input, StringComparison.Ordinal);
        Assert.Equal("WEP-T-WORKER-CLOSURE-CONTRACT-v1", request.WorkerExecutionPacket.PacketId);
        Assert.Equal("EP-T-WORKER-CLOSURE-CONTRACT-v1", request.WorkerExecutionPacket.SourceExecutionPacketId);
        Assert.Contains("patch_scope_recorded", request.WorkerExecutionPacket.RequiredContractMatrix);
        Assert.Contains("contract_items_satisfied", request.WorkerExecutionPacket.CompletionClaimSchema.Fields);
        Assert.False(request.WorkerExecutionPacket.CompletionClaimSchema.ClaimIsTruth);
        Assert.Equal(".ai/execution/T-WORKER-CLOSURE-CONTRACT/result.json", request.WorkerExecutionPacket.ResultSubmission.CandidateResultChannel);
        Assert.Equal("task ingest-result T-WORKER-CLOSURE-CONTRACT", request.WorkerExecutionPacket.ResultSubmission.HostIngestCommand);
        Assert.False(request.WorkerExecutionPacket.WritesTruthRoots);
        Assert.Equal("worker-execution-packet.v1", request.Metadata["worker_execution_packet_schema"]);
        Assert.Equal("WEP-T-WORKER-CLOSURE-CONTRACT-v1", request.Metadata["worker_execution_packet_id"]);
        Assert.Equal("EP-T-WORKER-CLOSURE-CONTRACT-v1", request.Metadata["worker_execution_packet_source_execution_packet_id"]);
        Assert.Contains("patch_scope_recorded", request.Metadata["worker_execution_packet_required_contract_matrix"], StringComparison.Ordinal);
        Assert.Contains("targeted tests", request.Metadata["worker_execution_packet_required_validation"], StringComparison.Ordinal);
        Assert.Contains("focused_required_validation", request.Metadata["worker_execution_packet_required_validation_gates"], StringComparison.Ordinal);
        Assert.Contains(".ai/execution/T-WORKER-CLOSURE-CONTRACT/result.json", request.Metadata["worker_execution_packet_evidence_required"], StringComparison.Ordinal);
        Assert.Contains("contract_items_satisfied", request.Metadata["worker_execution_packet_completion_claim_fields"], StringComparison.Ordinal);
        Assert.Equal("true", request.Metadata["worker_execution_packet_completion_claim_required"]);
        Assert.Equal("false", request.Metadata["worker_execution_packet_claim_is_truth"]);
        Assert.Equal("true", request.Metadata["worker_execution_packet_host_validation_required"]);
        Assert.Equal(".ai/execution/T-WORKER-CLOSURE-CONTRACT/result.json", request.Metadata["worker_execution_packet_result_channel"]);
        Assert.Equal("task ingest-result T-WORKER-CLOSURE-CONTRACT", request.Metadata["worker_execution_packet_host_ingest_command"]);
        Assert.Equal("true", request.Metadata["worker_execution_packet_candidate_only"]);
        Assert.Equal("true", request.Metadata["worker_execution_packet_review_bundle_required"]);
        Assert.Equal("true", request.Metadata["worker_execution_packet_submitted_by_host_or_adapter"]);
        Assert.Equal("false", request.Metadata["worker_execution_packet_writes_truth_roots"]);
        Assert.Equal("false", request.Metadata["worker_execution_packet_creates_task_queue"]);
        Assert.Equal("result_closure_protocol_v1", request.Metadata["closure_contract_protocol_id"]);
        Assert.Equal("generic_review_closure_v1", request.Metadata["closure_contract_profile_id"]);
        Assert.Contains("patch_scope_recorded", request.Metadata["closure_contract_required_checks"], StringComparison.Ordinal);
        Assert.Contains("focused_required_validation", request.Metadata["closure_contract_required_validation_gates"], StringComparison.Ordinal);
        Assert.Equal("true", request.Metadata["closure_contract_completion_claim_required"]);
        Assert.Contains("contract_items_satisfied", request.Metadata["closure_contract_completion_claim_fields"], StringComparison.Ordinal);
        var draft = Assert.IsType<LlmRequestEnvelopeDraft>(request.RequestEnvelopeDraft);
        var workerPacketSegment = Assert.Single(draft.Segments, segment => segment.SegmentId == "worker_execution_packet");
        Assert.Equal("$.input.worker_execution_packet", workerPacketSegment.PayloadPath);
        Assert.Contains("not a queue or truth root", workerPacketSegment.Content, StringComparison.Ordinal);
        var closureSegment = Assert.Single(draft.Segments, segment => segment.SegmentId == "closure_contract");
        Assert.Equal("$.input.closure_contract", closureSegment.PayloadPath);
        Assert.Contains("your completion claim is not truth", closureSegment.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerAiRequestFactory_EmbedsAcceptanceContractAsFirstClassWorkerInput()
    {
        var factory = new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low");
        var task = new TaskNode
        {
            TaskId = "T-WORKER-ACCEPTANCE-CONTRACT",
            Title = "Apply governed worker slice",
            Description = "Carry acceptance contract details into the worker request.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs"],
            Acceptance = ["worker request includes the acceptance contract summary"],
        };
        var contextPack = new ContextPack
        {
            Goal = "Apply the governed worker slice.",
            Task = "Carry acceptance contract details into the worker request.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-acceptance-contract",
            Allowed = true,
            SelectedBackendId = "codex_cli",
            SelectedProviderId = "codex",
            SelectedModelId = "gpt-5-codex",
            Summary = "Selected codex cli backend.",
            RouteSource = "active_profile_no_match",
            RoutingIntent = "patch_draft",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(
                task.TaskId,
                new AcceptanceContract
                {
                    ContractId = "AC-T-WORKER-ACCEPTANCE-CONTRACT",
                    Title = "Worker request contract",
                    Status = AcceptanceContractLifecycleStatus.Compiled,
                    Intent = new AcceptanceContractIntent
                    {
                        Goal = "Make acceptance contract semantics explicit to the worker.",
                        BusinessValue = "Prevent worker execution from drifting from human intent.",
                    },
                    NonGoals = ["Do not hide evidence requirements inside review-only truth."],
                    EvidenceRequired =
                    [
                        new AcceptanceContractEvidenceRequirement { Type = "result_commit" },
                    ],
                    HumanReview = new AcceptanceContractHumanReviewPolicy
                    {
                        Required = true,
                        ProvisionalAllowed = true,
                    },
                }),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "codex_cli",
            validationCommands: [],
            selection);

        Assert.Contains("Acceptance contract:", request.Input, StringComparison.Ordinal);
        Assert.Contains("AC-T-WORKER-ACCEPTANCE-CONTRACT", request.Input, StringComparison.Ordinal);
        Assert.Contains("result_commit", request.Input, StringComparison.Ordinal);
        Assert.Contains("provisional_allowed=yes", request.Input, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerAiRequestFactory_AttachesRequestEnvelopeDraftWithContextPackSections()
    {
        var factory = new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low");
        var task = new TaskNode
        {
            TaskId = "T-WORKER-REQUEST-ENVELOPE",
            Title = "Carry request attribution context",
            Description = "Ensure the worker request carries section-level envelope attribution.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs"],
            Acceptance = ["worker request carries an attribution draft"],
        };
        const string promptInput = "Context Pack\n\nGoal:\nAlpha\n\nTask:\nBeta";
        var goalStart = promptInput.IndexOf("Goal:", StringComparison.Ordinal);
        var taskStart = promptInput.IndexOf("\n\nTask:", StringComparison.Ordinal) + 2;
        var contextPack = new ContextPack
        {
            PackId = "CP-WORKER-001",
            Goal = "Alpha",
            Task = "Beta",
            Constraints = ["Stay in scope."],
            PromptInput = promptInput,
            PromptSections =
            [
                new RenderedPromptSection
                {
                    SectionId = "goal",
                    SectionKind = "goal",
                    SourceItemId = "goal",
                    StartChar = goalStart,
                    EndChar = taskStart - 2,
                },
                new RenderedPromptSection
                {
                    SectionId = "task",
                    SectionKind = "task",
                    SourceItemId = "task",
                    StartChar = taskStart,
                    EndChar = promptInput.Length,
                },
            ],
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-envelope",
            Allowed = true,
            SelectedBackendId = "codex_cli",
            SelectedProviderId = "codex",
            SelectedModelId = "gpt-5-codex",
            Summary = "Selected codex cli backend.",
            RouteSource = "active_profile_no_match",
            RoutingIntent = "patch_draft",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "codex_cli",
            validationCommands: [],
            selection);

        var draft = Assert.IsType<LlmRequestEnvelopeDraft>(request.RequestEnvelopeDraft);
        Assert.Equal("worker", draft.RequestKind);
        Assert.Equal("CP-WORKER-001", draft.PackId);
        Assert.Contains(draft.Segments, segment => segment.SegmentId == "context_pack");
        var goalSegment = Assert.Single(draft.Segments, segment => segment.SegmentId == "context_pack:goal");
        Assert.Equal("goal", goalSegment.SegmentKind);
        Assert.Equal("goal", goalSegment.SourceItemId);
        Assert.Contains("Goal:", goalSegment.Content, StringComparison.Ordinal);
        var taskSegment = Assert.Single(draft.Segments, segment => segment.SegmentId == "context_pack:task");
        Assert.Contains("Task:", taskSegment.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerAiRequestFactory_EmbedsPatchBudgetAndStopContract()
    {
        var factory = new WorkerAiRequestFactory(500, 30, "gpt-5-mini", "low");
        var task = new TaskNode
        {
            TaskId = "T-WORKER-PATCH-BUDGET",
            Title = "Apply a bounded source update",
            Description = "Apply a bounded source update without overrunning the declared patch budget.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs"],
            Acceptance = ["the bounded update stays inside the declared patch budget"],
        };
        var contextPack = new ContextPack
        {
            Goal = "Apply the bounded source update.",
            Task = "Stay within the declared patch budget.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-patch-budget",
            Allowed = true,
            SelectedBackendId = "codex_cli",
            SelectedProviderId = "codex",
            SelectedModelId = "gpt-5-codex",
            Summary = "Selected codex cli backend.",
            RouteSource = "active_profile_no_match",
            RoutingIntent = "patch_draft",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "codex_cli",
            validationCommands: [],
            selection);

        Assert.Contains("Execution patch budget (authoritative; do not exceed it):", request.Input, StringComparison.Ordinal);
        Assert.Contains("- Max files changed: 4", request.Input, StringComparison.Ordinal);
        Assert.Contains("- Max lines changed: 200", request.Input, StringComparison.Ordinal);
        Assert.Contains("Stop conditions: requires_new_card_or_taskgraph", request.Input, StringComparison.Ordinal);
        Assert.Contains("Stay within the declared execution budget", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("If the requested change appears likely to exceed that patch budget, do not force a large one-pass patch.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("must be split or narrowed before further editing", request.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerAiRequestFactory_RequiresConcreteSourceGroundingForArchivedEvidenceTasks()
    {
        var factory = new WorkerAiRequestFactory(500, 30, "gpt-4.1", "low");
        var task = new TaskNode
        {
            TaskId = "T-WORKER-GROUNDED",
            Title = "Draft bounded provider evidence follow-up proposals",
            Description = "Draft proposals from archived provider evidence.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = [".ai/runtime/sustainability"],
            Acceptance =
            [
                "the follow-up proposals are explicitly derived from the assessed provider_evidence set",
                "each proposal remains bounded and references its archived evidence source",
            ],
        };
        var contextPack = new ContextPack
        {
            Goal = "Draft grounded proposals.",
            Task = "Use the provided evidence set only.",
            Constraints = ["Stay in scope."],
            PromptInput = "Context Pack",
        };
        var selection = new WorkerSelectionDecision
        {
            RepoId = "repo-worker-grounded",
            Allowed = true,
            SelectedBackendId = "openai_api",
            SelectedProviderId = "openai",
            SelectedModelId = "gpt-4.1",
            Summary = "Selected openai backend.",
            RouteSource = "active_profile_fallback",
            RoutingIntent = "reasoning_summary",
        };

        var request = factory.Create(
            task,
            contextPack,
            CreatePacket(task.TaskId),
            $".ai/runtime/execution-packets/{task.TaskId}.json",
            WorkerExecutionProfile.UntrustedDefault,
            "repo",
            "worktree",
            "abc123",
            false,
            "openai_api",
            validationCommands: [],
            selection);

        Assert.Contains("Use only task IDs, artifact paths, and evidence identifiers that are explicitly present in the provided context.", request.Instructions, StringComparison.Ordinal);
        Assert.Contains("Do not invent, rename, or substitute source identifiers", request.Instructions, StringComparison.Ordinal);
    }

    private static ExecutionPacket CreatePacket(string taskId, AcceptanceContract? acceptanceContract = null)
    {
        return new ExecutionPacket
        {
            PacketId = $"EP-{taskId}-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-TEST",
                TaskId = taskId,
                TaskRevision = 1,
            },
            Goal = "test packet",
            PlannerIntent = PlannerIntent.Execution,
            AcceptanceContract = acceptanceContract,
            Context = new ExecutionPacketContext
            {
                AssemblyOrder = ["Architecture", "RelevantModules", "CurrentTaskFiles"],
                MemoryBundleRefs = [".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md"],
                CodegraphQueries = ["module:test"],
                RelevantFiles = ["README.md"],
            },
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = ["src/"],
                ReadOnlyRoots = ["docs/"],
                TruthRoots = ["carves://truth/tasks"],
                RepoMirrorRoots = [".ai/"],
            },
            Budgets = new ExecutionPacketBudgets
            {
                MaxFilesChanged = 4,
                MaxLinesChanged = 200,
                MaxShellCommands = 6,
            },
            ClosureContract = new ExecutionPacketClosureContract
            {
                ContractMatrixProfileId = "generic_review_closure_v1",
                Summary = "Worker candidate must include patch scope, validation, safety, and scope hygiene evidence before Host review can allow writeback.",
                RequiredContractChecks =
                [
                    "review_artifact_present",
                    "validation_recorded",
                    "safety_recorded",
                    "patch_scope_recorded",
                    "scope_hygiene",
                ],
                RequiredValidationGates = ["focused_required_validation"],
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
                EvidenceSurfaces = [$"inspect execution-packet {taskId}"],
            },
            WorkerExecutionPacket = new WorkerExecutionPacket
            {
                PacketId = $"WEP-{taskId}-v1",
                SourceExecutionPacketId = $"EP-{taskId}-v1",
                TaskId = taskId,
                Goal = "test packet",
                AllowedFiles = ["src/", "tests/"],
                AllowedActions = ["read", "edit", "carves.submit_result"],
                RequiredContractMatrix =
                [
                    "review_artifact_present",
                    "validation_recorded",
                    "safety_recorded",
                    "patch_scope_recorded",
                    "scope_hygiene",
                ],
                RequiredValidation = ["targeted tests"],
                RequiredValidationGates = ["focused_required_validation"],
                EvidenceRequired =
                [
                    $"inspect execution-packet {taskId}",
                    $".ai/execution/{taskId}/result.json",
                    $".ai/artifacts/worker-executions/{taskId}.json",
                ],
                CompletionClaimSchema = new WorkerCompletionClaimSchema
                {
                    Required = true,
                    Fields =
                    [
                        "changed_files",
                        "contract_items_satisfied",
                        "tests_run",
                        "evidence_paths",
                        "known_limitations",
                        "next_recommendation",
                    ],
                    ClaimIsTruth = false,
                    HostValidationRequired = true,
                },
                ResultSubmission = new WorkerResultSubmissionContract
                {
                    CandidateResultChannel = $".ai/execution/{taskId}/result.json",
                    HostIngestCommand = $"task ingest-result {taskId}",
                    CandidateOnly = true,
                    ReviewBundleRequired = true,
                    SubmittedByHostOrAdapter = true,
                    WorkerDirectTruthWriteAllowed = false,
                },
                GrantsLifecycleTruthAuthority = false,
                GrantsTruthWriteAuthority = false,
                CreatesTaskQueue = false,
                WritesTruthRoots = false,
            },
            RequiredValidation = ["targeted tests"],
            StableEvidenceSurfaces = [$"inspect execution-packet {taskId}"],
            WorkerAllowedActions = ["read", "edit", "submit_result"],
            PlannerOnlyActions = ["carves.review_task", "carves.sync_state"],
            StopConditions = ["requires_new_card_or_taskgraph"],
        };
    }

    [Fact]
    public void WorkerService_DoesNotTreatTaskScopeAsTouchedPathsWhenWorkerReportsNoWrites()
    {
        using var workspace = new TemporaryWorkspace();
        var processRunner = new RecordingProcessRunner();
        var workerAdapter = new ReadOnlyAssessmentWorkerAdapter();
        var artifacts = new RecordingRuntimeArtifactRepository();
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var worker = new WorkerService(
            TestSystemConfigFactory.Create(["src", "tests"]),
            SafetyRules.CreateDefault(),
            ModuleDependencyMap.Empty,
            new WorkerAdapterRegistry([workerAdapter], workerAdapter),
            processRunner,
            new StubWorktreeManager(),
            new SafetyService(SafetyValidatorCatalog.CreateDefault()),
            artifacts,
            CreateBoundaryService(),
            CreatePermissionOrchestrationService(artifacts),
            new RuntimeIncidentTimelineService(new InMemoryRuntimeIncidentTimelineRepository(), operatorOsEventStream),
            actorSessionService,
            operatorOsEventStream,
            new ExecutionEvidenceRecorder(workspace.Paths));

        var task = new TaskNode
        {
            TaskId = "T-READONLY-ASSESSMENT",
            Title = "Assess archived provider evidence set",
            Description = "Return a bounded assessment without changing files.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = [".ai/runtime/sustainability", ".ai/artifacts/provider", ".ai/tasks/cards"],
            Acceptance = ["assessment is returned without file writes"],
        };
        var executionRequest = new WorkerExecutionRequest
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Instructions = "Return the final assessment directly.",
            Input = "Assess the archived provider evidence set.",
            RepoRoot = workspace.RootPath,
            WorktreeRoot = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId),
            BaseCommit = "abc123",
            BackendHint = workerAdapter.BackendId,
            Profile = WorkerExecutionProfile.UntrustedDefault,
            AllowedFiles = task.Scope,
        };
        var request = new WorkerRequest
        {
            Task = task,
            Session = new ExecutionSession(task.TaskId, task.Title, workspace.RootPath, false, "abc123", workerAdapter.AdapterId, executionRequest.WorktreeRoot, DateTimeOffset.UtcNow),
            ValidationCommands = [],
            ExecutionRequest = executionRequest,
            Selection = new WorkerSelectionDecision
            {
                RepoId = "repo-readonly-assessment",
                Allowed = true,
                SelectedBackendId = workerAdapter.BackendId,
                SelectedProviderId = workerAdapter.ProviderId,
                Summary = "Selected readonly assessment worker.",
                RouteSource = "active_profile_fallback",
                Profile = WorkerExecutionProfile.UntrustedDefault,
                RoutingIntent = "reasoning_summary",
            },
        };

        var report = worker.Execute(request);

        Assert.Equal(SafetyOutcome.Allow, report.SafetyDecision.Outcome);
        Assert.Empty(report.Patch.Paths);
        Assert.Equal(0, report.Patch.FilesChanged);
        Assert.True(report.WorkerExecution.Succeeded);
    }

    [Fact]
    public void WorkerService_RejectsRemoteApiPatchDraftThatDidNotMaterializeWrites()
    {
        using var workspace = new TemporaryWorkspace();
        var processRunner = new RecordingProcessRunner();
        var workerAdapter = new ProseOnlyPatchDraftWorkerAdapter();
        var artifacts = new RecordingRuntimeArtifactRepository();
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var worker = new WorkerService(
            TestSystemConfigFactory.Create(["src", "tests"]),
            SafetyRules.CreateDefault(),
            ModuleDependencyMap.Empty,
            new WorkerAdapterRegistry([workerAdapter], workerAdapter),
            processRunner,
            new StubWorktreeManager(),
            new SafetyService(SafetyValidatorCatalog.CreateDefault()),
            artifacts,
            CreateBoundaryService(),
            CreatePermissionOrchestrationService(artifacts),
            new RuntimeIncidentTimelineService(new InMemoryRuntimeIncidentTimelineRepository(), operatorOsEventStream),
            actorSessionService,
            operatorOsEventStream,
            new ExecutionEvidenceRecorder(workspace.Paths));

        var task = new TaskNode
        {
            TaskId = "T-REMOTEAPI-PATCH-DRAFT",
            Title = "Add missing response assertions",
            Description = "Update the functional test to assert round-trip item fields.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["tests/Ordering.FunctionalTests/OrderingApiTests.cs"],
            Acceptance = ["response assertions are added"],
        };
        var executionRequest = new WorkerExecutionRequest
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Instructions = "Apply the scoped test edits and submit the result.",
            Input = "Add the missing response assertions.",
            RepoRoot = workspace.RootPath,
            WorktreeRoot = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId),
            BaseCommit = "abc123",
            BackendHint = workerAdapter.BackendId,
            Profile = WorkerExecutionProfile.UntrustedDefault,
            AllowedFiles = task.Scope,
            RoutingIntent = "patch_draft",
            Packet = CreatePacket(task.TaskId),
        };
        var request = new WorkerRequest
        {
            Task = task,
            Session = new ExecutionSession(task.TaskId, task.Title, workspace.RootPath, false, "abc123", workerAdapter.AdapterId, executionRequest.WorktreeRoot, DateTimeOffset.UtcNow),
            ValidationCommands = [],
            ExecutionRequest = executionRequest,
            Selection = new WorkerSelectionDecision
            {
                RepoId = "repo-remoteapi-patch-draft",
                Allowed = true,
                SelectedBackendId = workerAdapter.BackendId,
                SelectedProviderId = workerAdapter.ProviderId,
                Summary = "Selected prose-only patch-draft worker.",
                RouteSource = "active_profile_fallback",
                Profile = WorkerExecutionProfile.UntrustedDefault,
                RoutingIntent = "patch_draft",
            },
        };

        var report = worker.Execute(request);

        Assert.False(report.WorkerExecution.Succeeded);
        Assert.Equal(WorkerExecutionStatus.Failed, report.WorkerExecution.Status);
        Assert.Equal(WorkerFailureKind.InvalidOutput, report.WorkerExecution.FailureKind);
        Assert.Equal(WorkerFailureLayer.Protocol, report.WorkerExecution.FailureLayer);
        Assert.False(report.WorkerExecution.Retryable);
        Assert.False(report.Validation.Passed);
        Assert.Contains("did not materialize changed files", report.WorkerExecution.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkerService_RetainsSuccessfulChangedFileWorktreeUntilReviewWriteback()
    {
        using var workspace = new TemporaryWorkspace();
        var processRunner = new RecordingProcessRunner();
        var workerAdapter = new SuccessfulWriteWorkerAdapter();
        var artifacts = new RecordingRuntimeArtifactRepository();
        var operatorEvents = new InMemoryOperatorOsEventRepository();
        var operatorOsEventStream = new OperatorOsEventStreamService(operatorEvents);
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var incidents = new InMemoryRuntimeIncidentTimelineRepository();
        var worktreeManager = new RecordingCleanupWorktreeManager();
        var worker = new WorkerService(
            TestSystemConfigFactory.Create(["src", "tests"]),
            SafetyRules.CreateDefault(),
            ModuleDependencyMap.Empty,
            new WorkerAdapterRegistry([workerAdapter], workerAdapter),
            processRunner,
            worktreeManager,
            new SafetyService(SafetyValidatorCatalog.CreateDefault()),
            artifacts,
            CreateBoundaryService(),
            CreatePermissionOrchestrationService(artifacts),
            new RuntimeIncidentTimelineService(incidents, operatorOsEventStream),
            actorSessionService,
            operatorOsEventStream,
            new ExecutionEvidenceRecorder(workspace.Paths));

        var task = new TaskNode
        {
            TaskId = "T-RETAIN-WORKTREE",
            Title = "Add bounded unit-test coverage",
            Description = "Write a scoped test file.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs"],
            Acceptance = ["scoped test file is produced"],
        };
        var executionRequest = new WorkerExecutionRequest
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Instructions = "Write the bounded test file.",
            Input = "Context Pack",
            RepoRoot = workspace.RootPath,
            WorktreeRoot = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId),
            BaseCommit = "abc123",
            BackendHint = workerAdapter.BackendId,
            Profile = WorkerExecutionProfile.UntrustedDefault,
            AllowedFiles = task.Scope,
            Packet = CreatePacket(task.TaskId),
        };
        var request = new WorkerRequest
        {
            Task = task,
            Session = new ExecutionSession(task.TaskId, task.Title, workspace.RootPath, false, "abc123", workerAdapter.AdapterId, executionRequest.WorktreeRoot, DateTimeOffset.UtcNow),
            ValidationCommands = [],
            ExecutionRequest = executionRequest,
            Selection = new WorkerSelectionDecision
            {
                RepoId = "repo-retain-worktree",
                Allowed = true,
                SelectedBackendId = workerAdapter.BackendId,
                SelectedProviderId = workerAdapter.ProviderId,
                Summary = "Selected successful write worker.",
                RouteSource = "active_profile_no_match",
                Profile = WorkerExecutionProfile.UntrustedDefault,
                RoutingIntent = "patch_draft",
            },
        };

        var report = worker.Execute(request);

        Assert.True(report.Validation.Passed);
        Assert.True(report.SafetyDecision.Allowed);
        Assert.Equal(["tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs"], report.Patch.Paths);
        Assert.Equal("present", report.WorkerExecution.CompletionClaim.Status);
        Assert.Contains("contract_items_satisfied", report.WorkerExecution.CompletionClaim.PresentFields);
        Assert.Equal(["tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs"], report.WorkerExecution.CompletionClaim.ChangedFiles);
        Assert.Empty(worktreeManager.CleanupCalls);
        Assert.Contains(incidents.Load(), incident => incident.IncidentType == RuntimeIncidentType.WorkerStarted);
        Assert.Contains(incidents.Load(), incident => incident.IncidentType == RuntimeIncidentType.WorkerCompleted);
        Assert.Contains(operatorEvents.Load().Entries, entry => entry.EventKind == OperatorOsEventKind.TaskStarted);
        Assert.Contains(operatorEvents.Load().Entries, entry => entry.EventKind == OperatorOsEventKind.WorkerSpawned);
    }

    [Fact]
    public void WorkerService_AppendsWorkerExecutionAuditReadModelWhenConfigured()
    {
        using var workspace = new TemporaryWorkspace();
        var workerAdapter = new SuccessfulWriteWorkerAdapter();
        var artifacts = new RecordingRuntimeArtifactRepository();
        var auditReadModel = new RecordingWorkerExecutionAuditReadModel();
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var worker = new WorkerService(
            TestSystemConfigFactory.Create(["src", "tests"]),
            SafetyRules.CreateDefault(),
            ModuleDependencyMap.Empty,
            new WorkerAdapterRegistry([workerAdapter], workerAdapter),
            new RecordingProcessRunner(),
            new RecordingCleanupWorktreeManager(),
            new SafetyService(SafetyValidatorCatalog.CreateDefault()),
            artifacts,
            CreateBoundaryService(),
            CreatePermissionOrchestrationService(artifacts),
            new RuntimeIncidentTimelineService(new InMemoryRuntimeIncidentTimelineRepository(), operatorOsEventStream),
            actorSessionService,
            operatorOsEventStream,
            new ExecutionEvidenceRecorder(workspace.Paths),
            auditReadModel);

        var task = new TaskNode
        {
            TaskId = "T-AUDIT-SIDECAR",
            Title = "Add bounded unit-test coverage",
            Description = "Write a scoped test file.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = ["tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs"],
            Acceptance = ["scoped test file is produced"],
        };
        var executionRequest = new WorkerExecutionRequest
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Instructions = "Write the bounded test file.",
            Input = "Context Pack",
            RepoRoot = workspace.RootPath,
            WorktreeRoot = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId),
            BaseCommit = "abc123",
            BackendHint = workerAdapter.BackendId,
            Profile = WorkerExecutionProfile.UntrustedDefault,
            AllowedFiles = task.Scope,
            Packet = CreatePacket(task.TaskId),
        };
        var request = new WorkerRequest
        {
            Task = task,
            Session = new ExecutionSession(task.TaskId, task.Title, workspace.RootPath, false, "abc123", workerAdapter.AdapterId, executionRequest.WorktreeRoot, DateTimeOffset.UtcNow),
            ValidationCommands = [],
            ExecutionRequest = executionRequest,
            Selection = new WorkerSelectionDecision
            {
                RepoId = "repo-audit-sidecar",
                Allowed = true,
                SelectedBackendId = workerAdapter.BackendId,
                SelectedProviderId = workerAdapter.ProviderId,
                Summary = "Selected successful write worker.",
                RouteSource = "active_profile_no_match",
                Profile = WorkerExecutionProfile.UntrustedDefault,
                RoutingIntent = "patch_draft",
            },
        };

        var report = worker.Execute(request);

        Assert.True(report.WorkerExecution.Succeeded);
        var entry = Assert.Single(auditReadModel.Entries);
        Assert.Equal("T-AUDIT-SIDECAR", entry.TaskId);
        Assert.Equal(report.WorkerExecution.RunId, entry.RunId);
        Assert.Equal("completed", entry.EventType);
        Assert.Equal("Succeeded", entry.Status);
        Assert.Equal("Allow", entry.SafetyOutcome);
        Assert.True(entry.SafetyAllowed);
        Assert.Equal(1, entry.ChangedFilesCount);
        Assert.Contains(report.Validation.Evidence, evidence => evidence.Contains("safety layer worker_execution_boundary: phase=pre_execution", StringComparison.Ordinal));
        Assert.Contains(report.Validation.Evidence, evidence => evidence.Contains("safety layer change_observation: phase=post_execution_observation", StringComparison.Ordinal));
        Assert.Contains(report.Validation.Evidence, evidence => evidence.Contains("safety layer safety_service: phase=post_execution", StringComparison.Ordinal));
    }

    [Fact]
    public void WorkerService_DoesNotFailWhenWorkerExecutionAuditReadModelAppendFails()
    {
        using var workspace = new TemporaryWorkspace();
        var workerAdapter = new ReadOnlyAssessmentWorkerAdapter();
        var artifacts = new RecordingRuntimeArtifactRepository();
        var operatorEvents = new InMemoryOperatorOsEventRepository();
        var operatorOsEventStream = new OperatorOsEventStreamService(operatorEvents);
        var actorSessionService = new ActorSessionService(new InMemoryActorSessionRepository(), operatorOsEventStream);
        var incidents = new InMemoryRuntimeIncidentTimelineRepository();
        var worker = new WorkerService(
            TestSystemConfigFactory.Create(["src", "tests"]),
            SafetyRules.CreateDefault(),
            ModuleDependencyMap.Empty,
            new WorkerAdapterRegistry([workerAdapter], workerAdapter),
            new RecordingProcessRunner(),
            new StubWorktreeManager(),
            new SafetyService(SafetyValidatorCatalog.CreateDefault()),
            artifacts,
            CreateBoundaryService(),
            CreatePermissionOrchestrationService(artifacts),
            new RuntimeIncidentTimelineService(incidents, operatorOsEventStream),
            actorSessionService,
            operatorOsEventStream,
            new ExecutionEvidenceRecorder(workspace.Paths),
            new ThrowingWorkerExecutionAuditReadModel());

        var task = new TaskNode
        {
            TaskId = "T-AUDIT-SIDECAR-FAILS",
            Title = "Assess archived provider evidence set",
            Description = "Return a bounded assessment without changing files.",
            TaskType = TaskType.Execution,
            Status = DomainTaskStatus.Pending,
            Scope = [".ai/runtime/sustainability", ".ai/artifacts/provider", ".ai/tasks/cards"],
            Acceptance = ["assessment is returned without file writes"],
        };
        var executionRequest = new WorkerExecutionRequest
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Instructions = "Return the final assessment directly.",
            Input = "Assess the archived provider evidence set.",
            RepoRoot = workspace.RootPath,
            WorktreeRoot = Path.Combine(workspace.RootPath, ".carves-worktrees", task.TaskId),
            BaseCommit = "abc123",
            BackendHint = workerAdapter.BackendId,
            Profile = WorkerExecutionProfile.UntrustedDefault,
            AllowedFiles = task.Scope,
        };
        var request = new WorkerRequest
        {
            Task = task,
            Session = new ExecutionSession(task.TaskId, task.Title, workspace.RootPath, false, "abc123", workerAdapter.AdapterId, executionRequest.WorktreeRoot, DateTimeOffset.UtcNow),
            ValidationCommands = [],
            ExecutionRequest = executionRequest,
            Selection = new WorkerSelectionDecision
            {
                RepoId = "repo-audit-sidecar-fails",
                Allowed = true,
                SelectedBackendId = workerAdapter.BackendId,
                SelectedProviderId = workerAdapter.ProviderId,
                Summary = "Selected readonly assessment worker.",
                RouteSource = "active_profile_fallback",
                Profile = WorkerExecutionProfile.UntrustedDefault,
                RoutingIntent = "reasoning_summary",
            },
        };

        var report = worker.Execute(request);

        Assert.True(report.WorkerExecution.Succeeded);
        Assert.Equal(SafetyOutcome.Allow, report.SafetyDecision.Outcome);
        Assert.Empty(report.Patch.Paths);
        Assert.Contains(incidents.Load(), incident =>
            incident.IncidentType == RuntimeIncidentType.AuditSidecarFailed
            && incident.ReasonCode == "worker_execution_audit_sidecar_append_failed"
            && incident.TaskId == "T-AUDIT-SIDECAR-FAILS"
            && incident.ReferenceId == report.WorkerExecution.RunId
            && incident.Summary.Contains("sidecar append failed", StringComparison.Ordinal));
        Assert.Contains(operatorEvents.Load().Entries, entry =>
            entry.EventKind == OperatorOsEventKind.IncidentDetected
            && entry.ReasonCode == "worker_execution_audit_sidecar_append_failed"
            && entry.TaskId == "T-AUDIT-SIDECAR-FAILS");
    }

    private static WorkerExecutionBoundaryService CreateBoundaryService()
    {
        return new WorkerExecutionBoundaryService(
            repoRoot: "repo",
            new RepoRegistryService(new InMemoryRepoRegistryRepository()),
            new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository()));
    }

    private static WorkerPermissionOrchestrationService CreatePermissionOrchestrationService(IRuntimeArtifactRepository artifacts)
    {
        var repoRegistry = new RepoRegistryService(new InMemoryRepoRegistryRepository());
        var governance = new PlatformGovernanceService(new InMemoryPlatformGovernanceRepository());
        var operatorOsEventStream = new OperatorOsEventStreamService(new InMemoryOperatorOsEventRepository());
        var taskGraph = new Carves.Runtime.Domain.Tasks.TaskGraph();
        taskGraph.AddOrReplace(new TaskNode
        {
            TaskId = "T-READONLY-ASSESSMENT",
            Title = "Read-only assessment task",
            Status = DomainTaskStatus.Pending,
            Scope = [".ai/runtime/sustainability", ".ai/artifacts/provider", ".ai/tasks/cards"],
            Acceptance = ["safe"],
        });
        var taskGraphService = new TaskGraphService(new InMemoryTaskGraphRepository(taskGraph), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var sessionRepository = new InMemoryRuntimeSessionRepository();
        var markdownSync = new NoOpMarkdownSyncService();

        return new WorkerPermissionOrchestrationService(
            "repo",
            new WorkerPermissionInterpreter(),
            new ApprovalPolicyEngine("repo", repoRegistry, governance),
            artifacts,
            new InMemoryWorkerPermissionAuditRepository(),
            governance,
            repoRegistry,
            taskGraphService,
            sessionRepository,
            markdownSync,
            new RuntimeIncidentTimelineService(new InMemoryRuntimeIncidentTimelineRepository(), operatorOsEventStream),
            new PlannerWakeBridgeService("repo", sessionRepository, markdownSync, taskGraphService, operatorOsEventStream));
    }

    private sealed class RecordingWorkerExecutionAuditReadModel : IWorkerExecutionAuditReadModel
    {
        public List<WorkerExecutionAuditEntry> Entries { get; } = new();

        public string StoragePath => "memory://worker-execution-audit";

        public bool StorageExists => Entries.Count > 0;

        public void AppendExecution(WorkerExecutionAuditEntry entry)
        {
            Entries.Add(entry);
        }

        public IReadOnlyList<WorkerExecutionAuditEntry> QueryRecent(int limit)
        {
            return Entries.TakeLast(limit).Reverse().ToArray();
        }

        public WorkerExecutionAuditSummary GetSummary()
        {
            return new WorkerExecutionAuditSummary
            {
                TotalExecutions = Entries.Count,
                SucceededExecutions = Entries.Count(entry => string.Equals(entry.Status, "Succeeded", StringComparison.Ordinal)),
                FailedExecutions = Entries.Count(entry => string.Equals(entry.Status, "Failed", StringComparison.Ordinal)),
                BlockedExecutions = Entries.Count(entry => string.Equals(entry.Status, "Blocked", StringComparison.Ordinal)),
                SkippedExecutions = Entries.Count(entry => string.Equals(entry.Status, "Skipped", StringComparison.Ordinal)),
                ApprovalWaitExecutions = Entries.Count(entry => string.Equals(entry.Status, "ApprovalWait", StringComparison.Ordinal)),
                SafetyBlockedExecutions = Entries.Count(entry => !entry.SafetyAllowed),
                PermissionRequestCount = Entries.Sum(entry => entry.PermissionRequestCount),
                ChangedFilesCount = Entries.Sum(entry => entry.ChangedFilesCount),
                LatestTaskId = Entries.LastOrDefault()?.TaskId,
                LatestOccurrenceUtc = Entries.LastOrDefault()?.OccurredAtUtc,
            };
        }
    }

    private sealed class ThrowingWorkerExecutionAuditReadModel : IWorkerExecutionAuditReadModel
    {
        public string StoragePath => "memory://throwing-worker-execution-audit";

        public bool StorageExists => true;

        public void AppendExecution(WorkerExecutionAuditEntry entry)
        {
            throw new IOException("sidecar append failed");
        }

        public IReadOnlyList<WorkerExecutionAuditEntry> QueryRecent(int limit)
        {
            return [];
        }

        public WorkerExecutionAuditSummary GetSummary()
        {
            return new WorkerExecutionAuditSummary();
        }
    }

    private sealed class RecordingProcessRunner : IProcessRunner
    {
        public ProcessExecutionResult Run(IReadOnlyList<string> command, string workingDirectory)
        {
            return new ProcessExecutionResult(0, string.Empty, string.Empty);
        }
    }

    private sealed class ReadOnlyAssessmentWorkerAdapter : IWorkerAdapter
    {
        public string AdapterId => "ReadOnlyAssessmentWorkerAdapter";

        public string BackendId => "openai_api";

        public string ProviderId => "openai";

        public bool IsConfigured => true;

        public bool IsRealAdapter => true;

        public string SelectionReason => "Read-only test adapter.";

        public WorkerProviderCapabilities GetCapabilities()
        {
            return new WorkerProviderCapabilities
            {
                SupportsExecution = true,
                SupportsEventStream = true,
                SupportsHealthProbe = true,
            };
        }

        public WorkerBackendHealthSummary CheckHealth()
        {
            return new WorkerBackendHealthSummary
            {
                State = WorkerBackendHealthState.Healthy,
                Summary = "healthy",
            };
        }

        public WorkerRunControlResult Cancel(string runId, string reason)
        {
            return new WorkerRunControlResult
            {
                BackendId = BackendId,
                RunId = runId,
                Supported = false,
                Succeeded = false,
                Summary = "not supported",
            };
        }

        public WorkerExecutionResult Execute(WorkerExecutionRequest request)
        {
            return new WorkerExecutionResult
            {
                TaskId = request.TaskId,
                BackendId = BackendId,
                ProviderId = ProviderId,
                AdapterId = AdapterId,
                AdapterReason = SelectionReason,
                ProfileId = request.Profile.ProfileId,
                TrustedProfile = request.Profile.Trusted,
                Status = WorkerExecutionStatus.Succeeded,
                Configured = true,
                Model = "gpt-4.1",
                Summary = "Archived provider evidence assessed without file changes.",
                RequestPreview = request.Input,
                RequestHash = "hash",
                ChangedFiles = Array.Empty<string>(),
                CommandTrace =
                [
                    new CommandExecutionRecord(["remote-api", "openai", "responses_api"], 0, "assessment complete", string.Empty, false, request.WorktreeRoot, "remote_api", DateTimeOffset.UtcNow),
                ],
            };
        }
    }

    private sealed class ProseOnlyPatchDraftWorkerAdapter : IWorkerAdapter
    {
        public string AdapterId => "ProseOnlyPatchDraftWorkerAdapter";

        public string BackendId => "gemini_api";

        public string ProviderId => "gemini";

        public bool IsConfigured => true;

        public bool IsRealAdapter => true;

        public string SelectionReason => "Prose-only remote API patch-draft test adapter.";

        public WorkerProviderCapabilities GetCapabilities()
        {
            return new WorkerProviderCapabilities
            {
                SupportsExecution = true,
                SupportsHealthProbe = true,
                SupportsNetworkAccess = true,
            };
        }

        public WorkerBackendHealthSummary CheckHealth()
        {
            return new WorkerBackendHealthSummary
            {
                State = WorkerBackendHealthState.Healthy,
                Summary = "healthy",
            };
        }

        public WorkerRunControlResult Cancel(string runId, string reason)
        {
            return new WorkerRunControlResult
            {
                BackendId = BackendId,
                RunId = runId,
                Supported = false,
                Succeeded = false,
                Summary = "not supported",
            };
        }

        public WorkerExecutionResult Execute(WorkerExecutionRequest request)
        {
            return new WorkerExecutionResult
            {
                TaskId = request.TaskId,
                BackendId = BackendId,
                ProviderId = ProviderId,
                AdapterId = AdapterId,
                AdapterReason = SelectionReason,
                ProtocolFamily = "gemini_native",
                RequestFamily = "generate_content",
                ProfileId = request.Profile.ProfileId,
                TrustedProfile = request.Profile.Trusted,
                Status = WorkerExecutionStatus.Succeeded,
                Configured = true,
                Model = "gemini-2.5-pro",
                Summary = "Narrative patch draft completed without materialized writes.",
                Rationale = "I updated the test, built the solution, and ran the targeted tests.",
                RequestPreview = request.Input,
                RequestHash = "hash",
                ChangedFiles = Array.Empty<string>(),
                CommandTrace =
                [
                    new CommandExecutionRecord(["remote-api", "gemini", "generate_content"], 0, "narrative only", string.Empty, false, request.WorktreeRoot, "remote_api", DateTimeOffset.UtcNow),
                ],
            };
        }
    }

    private sealed class StubWorktreeManager : IWorktreeManager
    {
        public string ResolveWorktreeRoot(SystemConfig systemConfig, string repoRoot)
        {
            return Path.Combine(repoRoot, ".carves-worktrees");
        }

        public string PrepareWorktree(SystemConfig systemConfig, string repoRoot, string taskId, string? startPoint)
        {
            return Path.Combine(repoRoot, ".carves-worktrees", taskId);
        }

        public void CleanupWorktree(string worktreePath)
        {
        }
    }

    private sealed class RecordingCleanupWorktreeManager : IWorktreeManager
    {
        public List<string> CleanupCalls { get; } = new();

        public string ResolveWorktreeRoot(SystemConfig systemConfig, string repoRoot)
        {
            return Path.Combine(repoRoot, ".carves-worktrees");
        }

        public string PrepareWorktree(SystemConfig systemConfig, string repoRoot, string taskId, string? startPoint)
        {
            return Path.Combine(repoRoot, ".carves-worktrees", taskId);
        }

        public void CleanupWorktree(string worktreePath)
        {
            CleanupCalls.Add(worktreePath);
        }
    }

    private sealed class SuccessfulWriteWorkerAdapter : IWorkerAdapter
    {
        public string AdapterId => "SuccessfulWriteWorkerAdapter";

        public string BackendId => "codex_cli";

        public string ProviderId => "codex";

        public bool IsConfigured => true;

        public bool IsRealAdapter => false;

        public string SelectionReason => "Successful write test adapter.";

        public WorkerProviderCapabilities GetCapabilities()
        {
            return new WorkerProviderCapabilities
            {
                SupportsExecution = true,
                SupportsHealthProbe = true,
            };
        }

        public WorkerBackendHealthSummary CheckHealth()
        {
            return new WorkerBackendHealthSummary
            {
                State = WorkerBackendHealthState.Healthy,
                Summary = "healthy",
            };
        }

        public WorkerRunControlResult Cancel(string runId, string reason)
        {
            return new WorkerRunControlResult
            {
                BackendId = BackendId,
                RunId = runId,
                Supported = false,
                Succeeded = false,
                Summary = "not supported",
            };
        }

        public WorkerExecutionResult Execute(WorkerExecutionRequest request)
        {
            return new WorkerExecutionResult
            {
                TaskId = request.TaskId,
                BackendId = BackendId,
                ProviderId = ProviderId,
                AdapterId = AdapterId,
                AdapterReason = SelectionReason,
                ProtocolFamily = "local_cli",
                RequestFamily = "delegated_worker_launch",
                ProfileId = request.Profile.ProfileId,
                TrustedProfile = request.Profile.Trusted,
                Status = WorkerExecutionStatus.Succeeded,
                Configured = true,
                Model = "gpt-5-codex",
                Summary = "Created the bounded test file.",
                Rationale = string.Join(
                    Environment.NewLine,
                    "- changed_files: tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs",
                    "- contract_items_satisfied: patch_scope_recorded; scope_hygiene; validation_recorded",
                    "- tests_run: host focused validation pending",
                    "- evidence_paths: .ai/artifacts/worker-executions/T-RETAIN-WORKTREE.json",
                    "- known_limitations: none",
                    "- next_recommendation: submit for Host review"),
                RequestPreview = request.Input,
                RequestHash = "hash",
                ChangedFiles = ["tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs"],
                CommandTrace =
                [
                    new CommandExecutionRecord(["powershell", "-Command", "Set-Content tests/Ordering.UnitTests/Application/CreateOrderDraftCommandHandlerTest.cs"], 0, string.Empty, string.Empty, false, request.WorktreeRoot, "write", DateTimeOffset.UtcNow),
                ],
            };
        }
    }

    private sealed class RecordingRuntimeArtifactRepository : IRuntimeArtifactRepository
    {
        public void SaveWorkerArtifact(TaskRunArtifact artifact)
        {
        }

        public TaskRunArtifact? TryLoadWorkerArtifact(string taskId)
        {
            return null;
        }

        public void SaveWorkerExecutionArtifact(WorkerExecutionArtifact artifact)
        {
        }

        public WorkerExecutionArtifact? TryLoadWorkerExecutionArtifact(string taskId)
        {
            return null;
        }

        public void SaveWorkerPermissionArtifact(WorkerPermissionArtifact artifact)
        {
        }

        public WorkerPermissionArtifact? TryLoadWorkerPermissionArtifact(string taskId)
        {
            return null;
        }

        public IReadOnlyList<WorkerPermissionArtifact> LoadWorkerPermissionArtifacts()
        {
            return Array.Empty<WorkerPermissionArtifact>();
        }

        public void SaveProviderArtifact(AiExecutionArtifact artifact)
        {
        }

        public AiExecutionArtifact? TryLoadProviderArtifact(string taskId)
        {
            return null;
        }

        public void SavePlannerProposalArtifact(Carves.Runtime.Domain.Planning.PlannerProposalEnvelope artifact)
        {
        }

        public Carves.Runtime.Domain.Planning.PlannerProposalEnvelope? TryLoadPlannerProposalArtifact(string proposalId)
        {
            return null;
        }

        public void SaveSafetyArtifact(SafetyArtifact artifact)
        {
        }

        public SafetyArtifact? TryLoadSafetyArtifact(string taskId)
        {
            return null;
        }

        public void SavePlannerReviewArtifact(PlannerReviewArtifact artifact)
        {
        }

        public PlannerReviewArtifact? TryLoadPlannerReviewArtifact(string taskId)
        {
            return null;
        }

        public void SaveMergeCandidateArtifact(MergeCandidateArtifact artifact)
        {
        }

        public void SaveRuntimeFailureArtifact(RuntimeFailureRecord artifact)
        {
        }

        public RuntimeFailureRecord? TryLoadLatestRuntimeFailure()
        {
            return null;
        }

        public void SaveRuntimePackAdmissionArtifact(RuntimePackAdmissionArtifact artifact)
        {
        }

        public RuntimePackAdmissionArtifact? TryLoadCurrentRuntimePackAdmissionArtifact()
        {
            return null;
        }

        public void SaveRuntimePackSelectionArtifact(RuntimePackSelectionArtifact artifact)
        {
        }

        public RuntimePackSelectionArtifact? TryLoadCurrentRuntimePackSelectionArtifact()
        {
            return null;
        }
    }

    private sealed class InMemoryRepoRegistryRepository : IRepoRegistryRepository
    {
        private RepoRegistry registry = new();

        public RepoRegistry Load()
        {
            return registry;
        }

        public void Save(RepoRegistry registry)
        {
            this.registry = registry;
        }
    }

    private sealed class InMemoryPlatformGovernanceRepository : IPlatformGovernanceRepository
    {
        private PlatformGovernanceSnapshot snapshot = new();
        private IReadOnlyList<GovernanceEvent> events = Array.Empty<GovernanceEvent>();

        public PlatformGovernanceSnapshot Load()
        {
            return snapshot;
        }

        public IReadOnlyList<GovernanceEvent> LoadEvents()
        {
            return events;
        }

        public void Save(PlatformGovernanceSnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        public void SaveEvents(IReadOnlyList<GovernanceEvent> events)
        {
            this.events = events;
        }
    }

    private sealed class InMemoryRuntimeIncidentTimelineRepository : IRuntimeIncidentTimelineRepository
    {
        private IReadOnlyList<RuntimeIncidentRecord> records = Array.Empty<RuntimeIncidentRecord>();

        public IReadOnlyList<RuntimeIncidentRecord> Load()
        {
            return records;
        }

        public void Save(IReadOnlyList<RuntimeIncidentRecord> records)
        {
            this.records = records;
        }
    }
}
