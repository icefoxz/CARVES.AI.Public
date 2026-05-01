using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeKernelHostContractTests
{
    [Fact]
    public void RuntimeAgentGovernanceKernel_ProjectsMachineReadableBootstrapTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-governance-kernel");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-governance-kernel");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime agent governance kernel", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves-platform/policies/agent-governance-kernel.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves-platform/runtime-state/", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("operator_review_first", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-agent-governance-kernel", root.GetProperty("surface_id").GetString());
        Assert.Equal(".carves-platform/policies/agent-governance-kernel.json", root.GetProperty("policy_path").GetString());

        var policy = root.GetProperty("policy");
        Assert.Equal(3, policy.GetProperty("policy_version").GetInt32());
        Assert.Contains(policy.GetProperty("path_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "bootstrap_anchor");
        Assert.Contains(policy.GetProperty("mixed_roots").EnumerateArray(), item => item.GetProperty("root_path").GetString() == ".carves-platform/runtime-state/");
        Assert.Equal("UnclassifiedPath", policy.GetProperty("unclassified_default").GetProperty("lifecycle_class").GetString());
        Assert.Equal("runtime-agent-bootstrap-packet", policy.GetProperty("bootstrap_packet_contract").GetProperty("surface_id").GetString());
        Assert.Equal("runtime-agent-bootstrap-receipt", policy.GetProperty("warm_resume_contract").GetProperty("surface_id").GetString());
        Assert.Equal("runtime-agent-task-overlay", policy.GetProperty("task_overlay_contract").GetProperty("surface_id").GetString());
        Assert.Contains(policy.GetProperty("model_profiles").EnumerateArray(), item => item.GetProperty("profile_id").GetString() == "weak");
        Assert.Contains(policy.GetProperty("weak_execution_lanes").EnumerateArray(), item => item.GetProperty("lane_id").GetString() == "weak-model-bounded-execution");
    }

    [Fact]
    public void RuntimeAgentBootstrapReceiptAndWeakLaneSurfaces_ProjectLowContextExecutionTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var bootstrapInspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-bootstrap-packet");
        var bootstrapApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-bootstrap-packet");
        var receiptInspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-bootstrap-receipt");
        var receiptApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-bootstrap-receipt");
        var routingInspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-model-profile-routing");
        var routingApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-model-profile-routing");
        var weakInspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-weak-model-lane");
        var weakApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-weak-model-lane");

        Assert.Equal(0, bootstrapInspect.ExitCode);
        Assert.Equal(0, bootstrapApi.ExitCode);
        Assert.Equal(0, receiptInspect.ExitCode);
        Assert.Equal(0, receiptApi.ExitCode);
        Assert.Equal(0, routingInspect.ExitCode);
        Assert.Equal(0, routingApi.ExitCode);
        Assert.Equal(0, weakInspect.ExitCode);
        Assert.Equal(0, weakApi.ExitCode);
        Assert.Contains("Runtime agent bootstrap packet", bootstrapInspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent bootstrap receipt", receiptInspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-agent-model-profile-routing", routingInspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime weak-model execution lane", weakInspect.StandardOutput, StringComparison.Ordinal);

        using (var document = JsonDocument.Parse(bootstrapApi.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-bootstrap-packet", root.GetProperty("surface_id").GetString());
            Assert.Equal("bootstrap_packet_default", root.GetProperty("packet").GetProperty("startup_mode").GetString());
            Assert.Contains(root.GetProperty("packet").GetProperty("warm_resume_inspect_commands").EnumerateArray(), item => item.GetString() == "inspect runtime-agent-bootstrap-receipt [<receipt-json-path>]");
            var hotPath = root.GetProperty("packet").GetProperty("hot_path_context");
            Assert.Equal("compact_context_does_not_replace_initialization_report", hotPath.GetProperty("governance_boundary").GetString());
            Assert.Contains(hotPath.GetProperty("default_inspect_commands").EnumerateArray(), item => item.GetString() == "inspect runtime-agent-bootstrap-packet");
            Assert.True(hotPath.GetProperty("full_governance_read_triggers").GetArrayLength() > 0);
            var markdownReadPolicy = hotPath.GetProperty("markdown_read_policy");
            Assert.Equal("machine_surface_first", markdownReadPolicy.GetProperty("default_post_initialization_mode").GetString());
            Assert.Contains(markdownReadPolicy.GetProperty("required_initial_sources").EnumerateArray(), item => item.GetString() == "AGENTS.md");
            Assert.Contains(markdownReadPolicy.GetProperty("read_tiers").EnumerateArray(), item => item.GetProperty("tier_id").GetString() == "daily_hot_path");
        }

        using (var document = JsonDocument.Parse(receiptApi.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-bootstrap-receipt", root.GetProperty("surface_id").GetString());
            Assert.Equal("cold_init_required", root.GetProperty("receipt").GetProperty("resume_decision").GetString());
            Assert.Contains(root.GetProperty("receipt").GetProperty("required_receipt_fields").EnumerateArray(), item => item.GetString() == "policy_version");
            Assert.Equal("compact_context_does_not_replace_initialization_report", root.GetProperty("receipt").GetProperty("hot_path_context").GetProperty("governance_boundary").GetString());
            Assert.Equal("receipt_validated_machine_surface_first", root.GetProperty("receipt").GetProperty("hot_path_context").GetProperty("markdown_read_policy").GetProperty("warm_resume_mode").GetString());
        }

        using (var document = JsonDocument.Parse(routingApi.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-model-profile-routing", root.GetProperty("surface_id").GetString());
            Assert.Contains(root.GetProperty("routing").GetProperty("profiles").EnumerateArray(), item => item.GetProperty("profile_id").GetString() == "standard");
        }

        using (var document = JsonDocument.Parse(weakApi.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-weak-model-lane", root.GetProperty("surface_id").GetString());
            Assert.Contains(root.GetProperty("lane_snapshot").GetProperty("lanes").EnumerateArray(), item => item.GetProperty("lane_id").GetString() == "weak-model-bounded-execution");
        }
    }

    [Fact]
    public void RuntimeAgentTaskOverlayAndLoopGuard_ProjectTaskScopedLowContextSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var overlayInspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-task-overlay", "T-CARD-323-003");
        var overlayApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-task-overlay", "T-CARD-323-003");
        var guardInspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-loop-stall-guard", "T-CARD-323-003");
        var guardApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-loop-stall-guard", "T-CARD-323-003");

        Assert.Equal(0, overlayInspect.ExitCode);
        Assert.Equal(0, overlayApi.ExitCode);
        Assert.Equal(0, guardInspect.ExitCode);
        Assert.Equal(0, guardApi.ExitCode);
        Assert.Contains("Runtime agent task overlay", overlayInspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent loop-stall guard", guardInspect.StandardOutput, StringComparison.Ordinal);

        using (var document = JsonDocument.Parse(overlayApi.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-task-overlay", root.GetProperty("surface_id").GetString());
            Assert.Equal("T-CARD-323-003", root.GetProperty("overlay").GetProperty("task_id").GetString());
            Assert.True(root.GetProperty("overlay").GetProperty("scope_files").GetArrayLength() > 0);
            Assert.True(root.GetProperty("overlay").GetProperty("editable_roots").GetArrayLength() > 0);
            Assert.True(root.GetProperty("overlay").GetProperty("validation_context").GetProperty("required_verification").GetArrayLength() > 0);
            Assert.True(root.GetProperty("overlay").GetProperty("planner_review").TryGetProperty("verdict", out _));
            Assert.Equal("task_overlay_first_after_initialization", root.GetProperty("overlay").GetProperty("markdown_read_guidance").GetProperty("default_read_mode").GetString());
            Assert.True(root.GetProperty("overlay").GetProperty("markdown_read_guidance").GetProperty("replacement_surfaces").GetArrayLength() > 0);
        }

        using (var document = JsonDocument.Parse(guardApi.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-loop-stall-guard", root.GetProperty("surface_id").GetString());
            Assert.Equal("T-CARD-323-003", root.GetProperty("guard").GetProperty("task_id").GetString());
            Assert.Contains(root.GetProperty("guard").GetProperty("profile_outcomes").EnumerateArray(), item => item.GetProperty("profile_id").GetString() == "weak");
        }
    }

    [Fact]
    public void RuntimeAgentShortContext_AggregatesLowContextStartupAndTaskPointers()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();
        const string taskId = "T-INTEGRATION-SHORT-CONTEXT";
        sandbox.AddSyntheticPendingTask(taskId, scope: ["README.md"]);

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-agent-short-context", taskId);
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-agent-short-context", taskId);
        var agentInspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "agent", "context", taskId);
        var agentApi = RunProgram("--repo-root", sandbox.RootPath, "--cold", "agent", "context", taskId, "--json");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Equal(0, agentInspect.ExitCode);
        Assert.Equal(0, agentApi.ExitCode);
        Assert.Contains("Runtime agent short context", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Context pack: not_materialized", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime agent short context", agentInspect.StandardOutput, StringComparison.Ordinal);

        using (var document = JsonDocument.Parse(api.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-short-context", root.GetProperty("surface_id").GetString());
            Assert.Equal(taskId, root.GetProperty("requested_task_id").GetString());
            Assert.Equal(taskId, root.GetProperty("resolved_task_id").GetString());
            Assert.Equal("explicit", root.GetProperty("task_resolution_source").GetString());
            Assert.True(root.GetProperty("short_context_ready").GetBoolean());
            Assert.Equal("runtime-agent-thread-start", root.GetProperty("thread_start").GetProperty("surface_id").GetString());
            Assert.Equal("runtime-agent-bootstrap-packet", root.GetProperty("bootstrap").GetProperty("surface_id").GetString());
            Assert.Equal("selected", root.GetProperty("task").GetProperty("state").GetString());
            Assert.Equal(taskId, root.GetProperty("task").GetProperty("task_id").GetString());
            Assert.True(root.GetProperty("task").GetProperty("scope_file_count").GetInt32() > 0);
            Assert.Equal("not_materialized", root.GetProperty("context_pack").GetProperty("state").GetString());
            var markdownBudget = root.GetProperty("markdown_budget");
            Assert.Equal("runtime-markdown-read-path-budget", markdownBudget.GetProperty("surface_id").GetString());
            Assert.Equal(0, markdownBudget.GetProperty("post_initialization_default_tokens").GetInt32());
            Assert.True(markdownBudget.GetProperty("deferred_markdown_tokens").GetInt32() > 0);
            Assert.Contains(root.GetProperty("primary_commands").EnumerateArray(), command =>
                command.GetProperty("command").GetString() == "carves agent context --json"
                && command.GetProperty("surface_id").GetString() == "runtime-agent-short-context");
            Assert.Contains(root.GetProperty("primary_commands").EnumerateArray(), command =>
                command.GetProperty("command").GetString() == $"carves api runtime-markdown-read-path-budget {taskId}"
                && command.GetProperty("surface_id").GetString() == "runtime-markdown-read-path-budget");
            Assert.Contains(root.GetProperty("detail_refs").EnumerateArray(), command =>
                command.GetProperty("command").GetString() == "carves api runtime-agent-bootstrap-packet");
            Assert.Contains(root.GetProperty("detail_refs").EnumerateArray(), command =>
                command.GetProperty("command").GetString() == $"carves api runtime-agent-task-overlay {taskId}");
            Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item =>
                item.GetString()!.Contains("does not initialize, plan, approve, execute", StringComparison.Ordinal));
        }

        using (var document = JsonDocument.Parse(agentApi.StandardOutput))
        {
            var root = document.RootElement;
            Assert.Equal("runtime-agent-short-context", root.GetProperty("surface_id").GetString());
            Assert.Equal(taskId, root.GetProperty("resolved_task_id").GetString());
        }
    }

    [Fact]
    public void RuntimeMarkdownReadPathBudget_ProjectsDeferredGeneratedViewsAndShortContextReadPath()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-markdown-read-path-budget");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-markdown-read-path-budget");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime Markdown read-path budget", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Generated Markdown views", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/TASK_QUEUE.md", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-markdown-read-path-budget", root.GetProperty("surface_id").GetString());
        Assert.Equal("N/A", root.GetProperty("requested_task_id").GetString());
        Assert.Equal(0, root.GetProperty("post_initialization_default").GetProperty("estimated_default_markdown_tokens").GetInt32());
        Assert.True(root.GetProperty("generated_markdown_views").GetProperty("deferred_markdown_tokens").GetInt32() > 0);
        Assert.Contains(root.GetProperty("deferred_markdown_sources").EnumerateArray(), item =>
            item.GetString()!.Contains(".ai/TASK_QUEUE.md", StringComparison.Ordinal));
        Assert.Contains(root.GetProperty("default_machine_read_path").EnumerateArray(), item =>
            item.GetString() == "carves agent context --json");
        Assert.Contains(root.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("path").GetString() == ".ai/TASK_QUEUE.md"
            && item.GetProperty("read_action").GetString() == "defer_after_initialization"
            && item.GetProperty("over_single_file_budget").GetBoolean());
        Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item =>
            item.GetString()!.Contains("does not read large Markdown bodies", StringComparison.Ordinal));
    }

    [Fact]
    public void RuntimeCodeUnderstandingEngine_ProjectsCodegraphFirstExtractionTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-code-understanding-engine");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-code-understanding-engine");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime code-understanding engine", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves-platform/policies/code-understanding-engine.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("tree-sitter", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("ast-grep", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("D:/Projects/CARVES/scip-master", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-code-understanding-engine", root.GetProperty("surface_id").GetString());
        Assert.Equal(".carves-platform/policies/code-understanding-engine.json", root.GetProperty("policy_path").GetString());

        var policy = root.GetProperty("policy");
        Assert.Equal(".ai/codegraph/", policy.GetProperty("strengthens_truth_root").GetString());
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "syntax_substrate");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "structured_query_and_rewrite");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "semantic_index_protocol");
        Assert.Contains(policy.GetProperty("precision_tiers").EnumerateArray(), item => item.GetProperty("tier_id").GetString() == "search_grade");
        Assert.Contains(policy.GetProperty("precision_tiers").EnumerateArray(), item => item.GetProperty("tier_id").GetString() == "impact_grade");
        Assert.Contains(policy.GetProperty("precision_tiers").EnumerateArray(), item => item.GetProperty("tier_id").GetString() == "governance_grade");
        Assert.Contains(policy.GetProperty("semantic_path_pilots").EnumerateArray(), item => item.GetProperty("pilot_id").GetString() == "bounded_csharp_semantic_path");
        Assert.Contains(policy.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "local_scip_master_is_not_semantic_index_protocol");
        Assert.Contains(policy.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "codegraph_scope_analysis");
        Assert.Contains(policy.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "bounded_csharp_semantic_path_pilot");
    }

    [Fact]
    public void RuntimeMinimalWorkerBaseline_ProjectsThinWorkerTruthWithoutReplacingHostGovernance()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-minimal-worker-baseline");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-minimal-worker-baseline");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime minimal-worker baseline", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves-platform/policies/minimal-worker-baseline.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("query | execute_actions", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("task run <task-id>", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("weak-model-bounded-execution", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-minimal-worker-baseline", root.GetProperty("surface_id").GetString());
        Assert.Equal(".carves-platform/policies/minimal-worker-baseline.json", root.GetProperty("policy_path").GetString());

        var policy = root.GetProperty("policy");
        Assert.Contains(policy.GetProperty("linear_loop").GetProperty("loop_phases").EnumerateArray(), item => item.GetString() == "query");
        Assert.Contains(policy.GetProperty("linear_loop").GetProperty("loop_phases").EnumerateArray(), item => item.GetString() == "execute_actions");
        Assert.Equal("weak", policy.GetProperty("weak_lane").GetProperty("model_profile_id").GetString());
        Assert.Contains(policy.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "host_taskgraph_and_writeback_remain_stronger");
    }

    [Fact]
    public void RuntimeDurableExecutionSemantics_ProjectsCheckpointResumeAndInterruptTruthWithoutReplacingTaskGraph()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-durable-execution-semantics");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-durable-execution-semantics");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime durable-execution semantics", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves-platform/policies/durable-execution-semantics.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("checkpoint_semantics", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("resume_semantics", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("taskgraph_replacement => rejected", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-durable-execution-semantics", root.GetProperty("surface_id").GetString());
        Assert.Equal(".carves-platform/policies/durable-execution-semantics.json", root.GetProperty("policy_path").GetString());

        var policy = root.GetProperty("policy");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "checkpoint_semantics");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "resume_semantics");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "human_interrupt_points");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "state_inspection_surfaces");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "execution_memory_separation");
        Assert.Contains(policy.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "taskgraph_replacement_is_rejected");
        Assert.Contains(policy.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "resume_gate_and_runtime_controls");
        Assert.Contains(policy.GetProperty("readiness_map").EnumerateArray(), item => item.GetProperty("semantic_id").GetString() == "taskgraph_replacement" && item.GetProperty("readiness").GetString() == "rejected");
    }

    [Fact]
    public void RuntimeRepoAuthoredGateLoop_ProjectsRepoChecksAndWorkflowQualificationWithoutPrOnlyLockIn()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-repo-authored-gate-loop");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-repo-authored-gate-loop");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime repo-authored gate loop", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves-platform/policies/repo-authored-gate-loop.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".continue/checks/", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("github_only_worldview => rejected", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("markdown_prompt_truth_owner => rejected", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-repo-authored-gate-loop", root.GetProperty("surface_id").GetString());
        Assert.Equal(".carves-platform/policies/repo-authored-gate-loop.json", root.GetProperty("policy_path").GetString());

        var policy = root.GetProperty("policy");
        Assert.Equal(".carves-platform/policies/repo-authored-gate-loop.json", policy.GetProperty("definition_kernel").GetProperty("machine_truth_path").GetString());
        Assert.Contains(policy.GetProperty("definition_kernel").GetProperty("projection_paths").EnumerateArray(), item => item.GetString() == ".continue/checks/");
        Assert.Contains(policy.GetProperty("definition_kernel").GetProperty("projection_paths").EnumerateArray(), item => item.GetString() == ".agents/checks/");
        Assert.Contains(policy.GetProperty("workflow_projections").EnumerateArray(), item => item.GetProperty("projection_id").GetString() == "pull_request_ci_projection");
        Assert.Contains(policy.GetProperty("workflow_projections").EnumerateArray(), item => item.GetProperty("projection_id").GetString() == "branch_review_projection");
        Assert.Contains(policy.GetProperty("workflow_projections").EnumerateArray(), item => item.GetProperty("projection_id").GetString() == "local_operator_projection");
        Assert.Contains(policy.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "markdown_prompt_files_remain_projection_only");
        Assert.Contains(policy.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "workflow_projections_are_not_pr_only");
        Assert.Contains(policy.GetProperty("readiness_map").EnumerateArray(), item => item.GetProperty("semantic_id").GetString() == "github_only_worldview" && item.GetProperty("readiness").GetString() == "rejected");
        Assert.Contains(policy.GetProperty("readiness_map").EnumerateArray(), item => item.GetProperty("semantic_id").GetString() == "markdown_prompt_truth_owner" && item.GetProperty("readiness").GetString() == "rejected");
    }

    [Fact]
    public void RuntimeGitNativeCodingLoop_ProjectsRepoMapPatchAndEvidenceLoopWithoutReplacingHostGovernance()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-git-native-coding-loop");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-git-native-coding-loop");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime git-native coding loop", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".carves-platform/policies/git-native-coding-loop.json", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("repo_map_projection", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("patch_first_interaction", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("lint_test_evidence_loop", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("host_governance_replacement => rejected", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-git-native-coding-loop", root.GetProperty("surface_id").GetString());
        Assert.Equal(".carves-platform/policies/git-native-coding-loop.json", root.GetProperty("policy_path").GetString());

        var policy = root.GetProperty("policy");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "repo_map_projection");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "patch_first_interaction");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "git_native_commit_evidence_loop");
        Assert.Contains(policy.GetProperty("concern_families").EnumerateArray(), item => item.GetProperty("family_id").GetString() == "lint_test_evidence_loop");
        Assert.Contains(policy.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "repo_map_remains_projection_not_codegraph_truth");
        Assert.Contains(policy.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "host_governance_replacement_is_rejected");
        Assert.Contains(policy.GetProperty("readiness_map").EnumerateArray(), item => item.GetProperty("semantic_id").GetString() == "repo_map_as_codegraph_truth" && item.GetProperty("readiness").GetString() == "rejected");
        Assert.Contains(policy.GetProperty("readiness_map").EnumerateArray(), item => item.GetProperty("semantic_id").GetString() == "git_commit_as_truth_writeback" && item.GetProperty("readiness").GetString() == "rejected");
        Assert.Contains(policy.GetProperty("readiness_map").EnumerateArray(), item => item.GetProperty("semantic_id").GetString() == "host_governance_replacement" && item.GetProperty("readiness").GetString() == "rejected");
    }

    [Fact]
    public void RuntimeContextKernel_ProjectsBoundedReadContext()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-context-kernel");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-context-kernel");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime context kernel", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("context_narrowing_preflight_first", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("windowed_read_projection", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect execution-packet <task-id>", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-context-kernel", root.GetProperty("surface_id").GetString());
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "context_pack_projection");
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "windowed_read_projection");
        Assert.Contains(root.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "task_context_pack");
    }

    [Fact]
    public void RuntimeKnowledgeKernel_ProjectsMemoryRootsAndAuditBoundary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-knowledge-kernel");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-knowledge-kernel");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime knowledge kernel", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/memory/inbox/", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("promotion_requires_audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/evidence/facts/", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-knowledge-kernel", root.GetProperty("surface_id").GetString());
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "knowledge_inbox");
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "temporal_fact_truth");
        Assert.Contains(root.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "temporal_facts_preserve_validity_windows");
        Assert.Contains(root.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "temporal_fact_ledger");
        Assert.Contains(root.GetProperty("boundary_rules").EnumerateArray(), item => item.GetProperty("rule_id").GetString() == "code_facts_live_in_codegraph");
    }

    [Fact]
    public void RuntimeDomainGraphKernel_ProjectsCodegraphFirstStructureTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-domain-graph-kernel");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-domain-graph-kernel");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime domain_graph kernel", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("structure_facts_are_codegraph_first", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(".ai/codegraph/symbols/", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-domain-graph-kernel", root.GetProperty("surface_id").GetString());
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "codegraph_structure_roots");
        Assert.Contains(root.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "scope_analysis");
    }

    [Fact]
    public void RuntimeExecutionKernel_ProjectsActorAndWorkspaceTruth()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-execution-kernel");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-execution-kernel");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime execution kernel", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("one_execution_truth_spine", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime inspect <repo-id>", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-execution-kernel", root.GetProperty("surface_id").GetString());
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "actor_runtime_live_state");
        Assert.Contains(root.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "workspace_runtime_lifecycle");
    }

    [Fact]
    public void RuntimeArtifactPolicyKernel_ProjectsEvidenceAndPolicyBoundary()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-artifact-policy-kernel");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-artifact-policy-kernel");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime artifact_policy kernel", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("policy_bundle_governs_gate_decisions", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("policy inspect", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("inspect runtime-export-profiles", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-artifact-policy-kernel", root.GetProperty("surface_id").GetString());
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "artifact_bundle_truth");
        Assert.Contains(root.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "policy_bundle_surface");
        Assert.Contains(root.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "runtime_export_profiles_surface");
    }

    [Fact]
    public void RuntimeKernelStructure_DocumentsStableMaintainerShape()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var readme = File.ReadAllText(Path.Combine(repoRoot, "README.md"));
        var docsIndex = File.ReadAllText(Path.Combine(repoRoot, "docs", "INDEX.md"));
        var structureDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "runtime", "runtime-kernel-structure.md"));

        Assert.Contains("docs/runtime/runtime-kernel-structure.md", readme, StringComparison.Ordinal);
        Assert.Contains("runtime/runtime-kernel-structure.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Maintainer-first read order", structureDoc, StringComparison.Ordinal);
        Assert.Contains("Compatibility and projection layers", structureDoc, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeKernelUpgradeQualification_ProjectsProofPathsAndGoNoGo()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-kernel-upgrade-qualification");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-kernel-upgrade-qualification");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime qualification kernel", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("go_requires_execution_and_knowledge_paths", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("No-go if a second control plane", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-kernel-upgrade-qualification", root.GetProperty("surface_id").GetString());
        Assert.Contains(root.GetProperty("truth_roots").EnumerateArray(), item => item.GetProperty("root_id").GetString() == "structure_freeze_proof_path");
        Assert.Contains(root.GetProperty("governed_read_paths").EnumerateArray(), item => item.GetProperty("path_id").GetString() == "upgrade_qualification_surface");
    }

    private static ProgramRunResult RunProgram(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
