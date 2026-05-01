using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Carves.Runtime.IntegrationTests;

public sealed class SWEbenchAdapterSmokeTests
{
    [Fact]
    public void AdapterSmokePath_ProducesPredictionAndFiltersForbiddenRoots()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();
        var targetRepoRoot = Path.Combine(tempRoot.Path, "target-repo");
        Directory.CreateDirectory(targetRepoRoot);

        WriteFile(targetRepoRoot, "app.py", "print('before')\n");
        WriteFile(targetRepoRoot, Path.Combine("artifacts", "generated.txt"), "artifact-before\n");

        RunProcessInDirectory(targetRepoRoot, "git", "init");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.email", "swebench@example.com");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.name", "SWEbench Smoke");
        RunProcessInDirectory(targetRepoRoot, "git", "add", ".");
        RunProcessInDirectory(targetRepoRoot, "git", "commit", "-m", "base");
        var baseCommit = RunProcessInDirectory(targetRepoRoot, "git", "rev-parse", "HEAD").Trim();

        WriteFile(targetRepoRoot, "app.py", "print('after')\n");
        WriteFile(targetRepoRoot, Path.Combine("artifacts", "generated.txt"), "artifact-after\n");

        var problemStatementPath = Path.Combine(tempRoot.Path, "problem.txt");
        File.WriteAllText(problemStatementPath, "Fix app.py output.", System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "adapter-artifacts");
        var scriptPath = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_swebench_adapter.py");
        var adapterOutput = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scriptPath,
            "--instance-id",
            "demo__repo-0001",
            "--repo-root",
            targetRepoRoot,
            "--base-commit",
            baseCommit,
            "--problem-statement-file",
            problemStatementPath,
            "--run-id",
            "swebench-smoke-001",
            "--artifact-root",
            artifactRoot,
            "--model-name-or-path",
            "carves-smoke");

        var runRoot = Path.Combine(artifactRoot, "swebench-smoke-001");
        var predictionsPath = Path.Combine(runRoot, "predictions.jsonl");
        var patchPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "patch.diff");
        var predictionRowPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "prediction.row.json");
        var manifestPath = Path.Combine(runRoot, "manifest.json");
        var cardPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "card.md");
        var taskProjectionPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "taskgraph-projection.json");
        var resultPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "result.json");
        var contextProjectionPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "context_projection.json");
        var contextBudgetPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "context_budget.json");
        var memoryReadSetPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "memory_read_set.json");
        var codegraphReadSetPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "codegraph_read_set.json");
        var excludedContextPath = Path.Combine(runRoot, "instances", "demo__repo-0001", "excluded_context.json");

        Assert.True(File.Exists(predictionsPath));
        Assert.True(File.Exists(patchPath));
        Assert.True(File.Exists(predictionRowPath));
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(cardPath));
        Assert.True(File.Exists(taskProjectionPath));
        Assert.True(File.Exists(resultPath));
        Assert.True(File.Exists(contextProjectionPath));
        Assert.True(File.Exists(contextBudgetPath));
        Assert.True(File.Exists(memoryReadSetPath));
        Assert.True(File.Exists(codegraphReadSetPath));
        Assert.True(File.Exists(excludedContextPath));

        using var manifestDocument = JsonDocument.Parse(adapterOutput);
        Assert.Equal("swebench-adapter-run-manifest.v1", manifestDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("swebench-smoke-001", manifestDocument.RootElement.GetProperty("run_id").GetString());

        var predictionLine = File.ReadAllText(predictionsPath).Trim();
        using var predictionDocument = JsonDocument.Parse(predictionLine);
        Assert.Equal("demo__repo-0001", predictionDocument.RootElement.GetProperty("instance_id").GetString());
        Assert.Equal("carves-smoke", predictionDocument.RootElement.GetProperty("model_name_or_path").GetString());

        var modelPatch = predictionDocument.RootElement.GetProperty("model_patch").GetString() ?? string.Empty;
        Assert.Contains("app.py", modelPatch, StringComparison.Ordinal);
        Assert.DoesNotContain("artifacts/generated.txt", modelPatch, StringComparison.Ordinal);
        Assert.DoesNotContain(".ai/", modelPatch, StringComparison.Ordinal);
        Assert.DoesNotContain(".carves/", modelPatch, StringComparison.Ordinal);
        Assert.DoesNotContain(".carves-platform/", modelPatch, StringComparison.Ordinal);

        using var contextProjection = JsonDocument.Parse(File.ReadAllText(contextProjectionPath));
        Assert.Equal("swebench-context-projection.v1", contextProjection.RootElement.GetProperty("schema_version").GetString());
        Assert.True(contextProjection.RootElement.GetProperty("solver_forbidden_inputs_confirmed").GetBoolean());
        Assert.Equal("not_observed", contextProjection.RootElement.GetProperty("memory_read_set").GetProperty("observation_state").GetString());
        Assert.Equal("not_observed", contextProjection.RootElement.GetProperty("codegraph_read_set").GetProperty("observation_state").GetString());
        Assert.Equal("observed", contextProjection.RootElement.GetProperty("docs_read_set").GetProperty("observation_state").GetString());
        Assert.Contains(
            contextProjection.RootElement.GetProperty("excluded_sources").EnumerateArray().Select(item => item.GetProperty("source_ref").GetString()),
            item => string.Equals(item, ".ai/memory/", StringComparison.Ordinal));
        Assert.Contains(
            contextProjection.RootElement.GetProperty("selection_reasons").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "bounded_benchmark_projection", StringComparison.Ordinal));

        using var contextBudget = JsonDocument.Parse(File.ReadAllText(contextBudgetPath));
        Assert.Equal("swebench-context-budget.v1", contextBudget.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("not_observed", contextBudget.RootElement.GetProperty("observation_state").GetString());

        using var resultDocument = JsonDocument.Parse(File.ReadAllText(resultPath));
        Assert.Equal("succeeded", resultDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("none", resultDocument.RootElement.GetProperty("failure_stage").GetString());
        Assert.Equal(predictionRowPath, resultDocument.RootElement.GetProperty("prediction_row_path").GetString());
        Assert.Equal(predictionsPath, resultDocument.RootElement.GetProperty("variant_predictions_path").GetString());
        Assert.True(resultDocument.RootElement.GetProperty("evidence_presence").GetProperty("context_projection").GetBoolean());
        Assert.Empty(resultDocument.RootElement.GetProperty("failure_log_paths").EnumerateArray());
        Assert.True(resultDocument.RootElement.GetProperty("adapter_validity").GetProperty("prediction_written").GetBoolean());
        Assert.False(resultDocument.RootElement.GetProperty("adapter_validity").GetProperty("patch_empty").GetBoolean());
        Assert.Equal(0, resultDocument.RootElement.GetProperty("adapter_validity").GetProperty("forbidden_root_leakage_count").GetInt32());
        Assert.Equal("not_run", resultDocument.RootElement.GetProperty("harness_resolution").GetProperty("status").GetString());
        Assert.Equal("adapter_default", resultDocument.RootElement.GetProperty("harness_resolution").GetProperty("reported_by").GetString());
    }

    [Fact]
    public void BenchmarkContracts_AreMachineReadable()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var runContractPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_RUN_CONTRACT_V1.json");
        var resultSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_RESULT_SCHEMA_V1.json");
        var projectionSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_CONTEXT_PROJECTION_SCHEMA_V1.json");
        var budgetSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_CONTEXT_BUDGET_SCHEMA_V1.json");
        var variantContractPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_VARIANT_CONTRACT_V1.json");
        var instanceManifestSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_INSTANCE_MANIFEST_SCHEMA_V1.json");
        var ablationRunSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_ABLATION_RUN_SCHEMA_V1.json");
        var harnessResultSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_HARNESS_RESULT_SCHEMA_V1.json");
        var metricsSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SWEBENCH_METRICS_SCHEMA_V1.json");
        var sequentialRunContractPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SEQUENTIAL_RUN_CONTRACT_V1.json");
        var memoryReuseMetricsSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "MEMORY_REUSE_METRICS_V1.json");
        var memoryMaturityScorecardSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "MEMORY_MATURITY_SCORECARD_V1.json");
        var truthRootAuditSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SEQUENTIAL_TRUTH_ROOT_NO_WRITE_AUDIT_V1.json");
        var sequentialHarnessReadinessSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "benchmarks", "SEQUENTIAL_HARNESS_READINESS_V1.json");
        var memoryUpdateTaskIngressSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "memory-gate", "BENCHMARK_MEMORY_UPDATE_TASK_INGRESS_SCHEMA_V1.json");
        var evidenceAggregationSchemaPath = Path.Combine(runtimeRepoRoot, "docs", "runtime", "memory-gate", "BENCHMARK_EVIDENCE_AGGREGATION_SCHEMA_V1.json");

        using var runContract = JsonDocument.Parse(File.ReadAllText(runContractPath));
        using var resultSchema = JsonDocument.Parse(File.ReadAllText(resultSchemaPath));
        using var projectionSchema = JsonDocument.Parse(File.ReadAllText(projectionSchemaPath));
        using var budgetSchema = JsonDocument.Parse(File.ReadAllText(budgetSchemaPath));
        using var variantContract = JsonDocument.Parse(File.ReadAllText(variantContractPath));
        using var instanceManifestSchema = JsonDocument.Parse(File.ReadAllText(instanceManifestSchemaPath));
        using var ablationRunSchema = JsonDocument.Parse(File.ReadAllText(ablationRunSchemaPath));
        using var harnessResultSchema = JsonDocument.Parse(File.ReadAllText(harnessResultSchemaPath));
        using var metricsSchema = JsonDocument.Parse(File.ReadAllText(metricsSchemaPath));
        using var sequentialRunContract = JsonDocument.Parse(File.ReadAllText(sequentialRunContractPath));
        using var memoryReuseMetricsSchema = JsonDocument.Parse(File.ReadAllText(memoryReuseMetricsSchemaPath));
        using var memoryMaturityScorecardSchema = JsonDocument.Parse(File.ReadAllText(memoryMaturityScorecardSchemaPath));
        using var truthRootAuditSchema = JsonDocument.Parse(File.ReadAllText(truthRootAuditSchemaPath));
        using var sequentialHarnessReadinessSchema = JsonDocument.Parse(File.ReadAllText(sequentialHarnessReadinessSchemaPath));
        using var memoryUpdateTaskIngressSchema = JsonDocument.Parse(File.ReadAllText(memoryUpdateTaskIngressSchemaPath));
        using var evidenceAggregationSchema = JsonDocument.Parse(File.ReadAllText(evidenceAggregationSchemaPath));

        Assert.Equal("swebench-run-contract-v1", runContract.RootElement.GetProperty("contract_id").GetString());
        Assert.Contains(
            runContract.RootElement.GetProperty("forbidden_runtime_truth_writes").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".ai/memory/", StringComparison.Ordinal));
        Assert.Contains(
            runContract.RootElement.GetProperty("forbidden_patch_roots").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".carves-platform/", StringComparison.Ordinal));

        Assert.Equal("SWE-bench Adapter Result", resultSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("object", resultSchema.RootElement.GetProperty("type").GetString());
        Assert.Contains(
            resultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "adapter_validity", StringComparison.Ordinal));
        Assert.Contains(
            resultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "harness_resolution", StringComparison.Ordinal));
        Assert.Contains(
            resultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "failure_stage", StringComparison.Ordinal));
        Assert.Contains(
            resultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "prediction_row_path", StringComparison.Ordinal));
        Assert.Contains(
            resultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "variant_predictions_path", StringComparison.Ordinal));
        Assert.Contains(
            resultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "evidence_presence", StringComparison.Ordinal));
        Assert.Contains(
            resultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "failure_log_paths", StringComparison.Ordinal));
        Assert.Contains(
            resultSchema.RootElement.GetProperty("properties").GetProperty("carves_evidence").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "context_projection", StringComparison.Ordinal));
        Assert.Equal("SWE-bench Context Projection", projectionSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("SWE-bench Context Budget", budgetSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("swebench-variant-contract-v1", variantContract.RootElement.GetProperty("contract_id").GetString());
        Assert.Equal("SWE-bench Instance Manifest", instanceManifestSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("SWE-bench Ablation Run", ablationRunSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("SWE-bench Harness Result", harnessResultSchema.RootElement.GetProperty("title").GetString());
        Assert.Contains(
            harnessResultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "matrix_status", StringComparison.Ordinal));
        Assert.Contains(
            harnessResultSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "resolved_rate", StringComparison.Ordinal));
        Assert.Contains(
            ablationRunSchema.RootElement.GetProperty("properties").GetProperty("matrix").GetProperty("items").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "prediction_row_path", StringComparison.Ordinal));
        Assert.Contains(
            ablationRunSchema.RootElement.GetProperty("properties").GetProperty("matrix").GetProperty("items").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "variant_predictions_path", StringComparison.Ordinal));
        Assert.Contains(
            ablationRunSchema.RootElement.GetProperty("properties").GetProperty("matrix").GetProperty("items").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "variant_summary_path", StringComparison.Ordinal));
        Assert.Contains(
            metricsSchema.RootElement.GetProperty("properties").GetProperty("variant_metrics").GetProperty("items").GetProperty("properties").GetProperty("adapter_validity").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "failure_log_coverage_rate", StringComparison.Ordinal));
        Assert.Contains(
            metricsSchema.RootElement.GetProperty("properties").GetProperty("variant_metrics").GetProperty("items").GetProperty("properties").GetProperty("projection_quality").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "projection_completeness_rate_all_cells", StringComparison.Ordinal));
        Assert.Contains(
            metricsSchema.RootElement.GetProperty("properties").GetProperty("variant_metrics").GetProperty("items").GetProperty("properties").GetProperty("projection_quality").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "projection_completeness_rate_started_cells", StringComparison.Ordinal));
        Assert.Contains(
            metricsSchema.RootElement.GetProperty("properties").GetProperty("variant_metrics").GetProperty("items").GetProperty("properties").GetProperty("harness_resolution").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "matrix_status", StringComparison.Ordinal));
        Assert.Contains(
            metricsSchema.RootElement.GetProperty("properties").GetProperty("variant_metrics").GetProperty("items").GetProperty("properties").GetProperty("harness_resolution").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "resolved_rate", StringComparison.Ordinal));
        Assert.Equal("SWE-bench Metrics", metricsSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("sequential-run-contract.v1", sequentialRunContract.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetString());
        Assert.Equal("Sequential Memory Reuse Metrics", memoryReuseMetricsSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("Memory Maturity Scorecard", memoryMaturityScorecardSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("Sequential Truth Root No Write Audit", truthRootAuditSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("Sequential Harness Readiness", sequentialHarnessReadinessSchema.RootElement.GetProperty("title").GetString());
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "claim_gates", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "truth_root_audit", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "harness_readiness", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("claim_gates").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "benchmark_uplift_claim", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("claim_gates").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "claim_scope", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("harness_readiness").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "status", StringComparison.Ordinal));
        Assert.Equal("Benchmark Memory Update Task Ingress", memoryUpdateTaskIngressSchema.RootElement.GetProperty("title").GetString());
        Assert.Contains(
            memoryUpdateTaskIngressSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "proposal_id", StringComparison.Ordinal));
        Assert.Contains(
            memoryUpdateTaskIngressSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "sufficiency_assessment_id", StringComparison.Ordinal));
        Assert.Contains(
            memoryUpdateTaskIngressSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "host_task_ingress_status", StringComparison.Ordinal));
        Assert.Contains(
            memoryUpdateTaskIngressSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "required_boundary_gates", StringComparison.Ordinal));
        Assert.Equal("benchmark-memory-update-task-ingress.v1", memoryUpdateTaskIngressSchema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetString());
        Assert.False(memoryUpdateTaskIngressSchema.RootElement.GetProperty("properties").GetProperty("truth_write_authorized").GetProperty("const").GetBoolean());
        Assert.False(memoryUpdateTaskIngressSchema.RootElement.GetProperty("properties").GetProperty("benchmark_uplift_claim_authorized").GetProperty("const").GetBoolean());
        Assert.Equal("null", memoryUpdateTaskIngressSchema.RootElement.GetProperty("properties").GetProperty("target_memory_path").GetProperty("type").GetString());
        Assert.Contains(
            memoryUpdateTaskIngressSchema.RootElement.GetProperty("properties").GetProperty("host_task_ingress_status").GetProperty("enum").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "task_draft_allowed", StringComparison.Ordinal));
        Assert.Contains(
            memoryUpdateTaskIngressSchema.RootElement.GetProperty("properties").GetProperty("host_task_ingress_status").GetProperty("enum").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "task_approved", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("harness_readiness").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "harness_python", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("harness_readiness").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "docker_api_candidates", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("truth_root_audit").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "proof_status", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("truth_root_audit").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "audit_root_kind", StringComparison.Ordinal));
        Assert.Contains(
            memoryReuseMetricsSchema.RootElement.GetProperty("properties").GetProperty("truth_root_audit").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "blocking_root_count", StringComparison.Ordinal));
        Assert.Contains(
            memoryMaturityScorecardSchema.RootElement.GetProperty("properties").GetProperty("memory_truth_boundary").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "truth_write_authorized", StringComparison.Ordinal));
        Assert.Contains(
            memoryMaturityScorecardSchema.RootElement.GetProperty("properties").GetProperty("measured_memory_reuse").GetProperty("required").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "claim_scope", StringComparison.Ordinal));
        Assert.Equal("Benchmark Evidence Aggregation", evidenceAggregationSchema.RootElement.GetProperty("title").GetString());
        Assert.Equal("benchmark-evidence-aggregation.v1", evidenceAggregationSchema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetString());
        Assert.False(evidenceAggregationSchema.RootElement.GetProperty("properties").GetProperty("truth_write_authorized").GetProperty("const").GetBoolean());
        Assert.False(evidenceAggregationSchema.RootElement.GetProperty("properties").GetProperty("benchmark_uplift_claim_authorized").GetProperty("const").GetBoolean());
        Assert.Equal("null", evidenceAggregationSchema.RootElement.GetProperty("properties").GetProperty("target_memory_path").GetProperty("type").GetString());
        Assert.Equal(
            "per_cell_same_base_required",
            evidenceAggregationSchema.RootElement.GetProperty("properties").GetProperty("aggregation_scope").GetProperty("properties").GetProperty("base_commit_policy").GetProperty("const").GetString());
        Assert.Equal(
            "not_a_single_sequence",
            evidenceAggregationSchema.RootElement.GetProperty("properties").GetProperty("aggregation_scope").GetProperty("properties").GetProperty("sequence_policy").GetProperty("const").GetString());
    }

    [Fact]
    public void MemoryUpdateTaskIngressBuilder_MapsL2EvidenceToTaskDraftAllowed()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "phase6-task-ingress-001");
        Directory.CreateDirectory(Path.Combine(runRoot, "proposals"));
        Directory.CreateDirectory(Path.Combine(runRoot, "evidence-sufficiency"));

        var proposalPath = Path.Combine(runRoot, "proposals", "memory_update_proposals.jsonl");
        File.WriteAllText(
            proposalPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-proposal.v1",
                proposal_id = "memproposal-sequential",
                reason_codes = new[] { "worker_claim_low_trust" },
                evidence_refs = new[] { "/tmp/proposal-evidence.json" },
                truth_write_authorized = false,
                target_memory_path = (string?)null,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var sufficiencyPath = Path.Combine(runRoot, "evidence-sufficiency", "memory_update_evidence_sufficiency.jsonl");
        File.WriteAllText(
            sufficiencyPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-evidence-sufficiency.v1",
                assessment_id = "memevidence-sequential",
                proposal_id = "memproposal-sequential",
                evidence_level = "L2_measured_small_sample",
                reason_codes = new[] { "review_approval_not_promotion" },
                evidence_refs = new[] { "/tmp/sufficiency-evidence.json" },
                truth_write_authorized = false,
                target_memory_path = (string?)null,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var scriptPath = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_update_task_ingress.py");
        var output = RunProcessInDirectory(runtimeRepoRoot, python, scriptPath, "--phase5-run-root", runRoot);

        using var outputDocument = JsonDocument.Parse(output);
        var taskIngressPath = outputDocument.RootElement.GetProperty("output").GetString();
        Assert.NotNull(taskIngressPath);
        Assert.True(File.Exists(taskIngressPath));

        var row = File.ReadAllText(taskIngressPath!, System.Text.Encoding.UTF8).Trim();
        using var ingressDocument = JsonDocument.Parse(row);
        Assert.Equal("benchmark-memory-update-task-ingress.v1", ingressDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memproposal-sequential", ingressDocument.RootElement.GetProperty("proposal_id").GetString());
        Assert.Equal("memevidence-sequential", ingressDocument.RootElement.GetProperty("sufficiency_assessment_id").GetString());
        Assert.Equal("L2_measured_small_sample", ingressDocument.RootElement.GetProperty("evidence_level").GetString());
        Assert.True(ingressDocument.RootElement.GetProperty("ingress_eligible").GetBoolean());
        Assert.Equal("task_draft_allowed", ingressDocument.RootElement.GetProperty("host_task_ingress_status").GetString());
        Assert.Equal("create_memory_update_task_draft", ingressDocument.RootElement.GetProperty("requested_host_action").GetString());
        Assert.Equal("create_host_memory_update_task_draft", ingressDocument.RootElement.GetProperty("next_action").GetString());
        Assert.False(ingressDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.False(ingressDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());
    }

    [Fact]
    public void MemoryUpdateTaskDraftBuilder_MapsTaskDraftAllowedIngressToHostDraftPayloads()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "phase6-task-draft-001");
        Directory.CreateDirectory(Path.Combine(runRoot, "proposals"));
        Directory.CreateDirectory(Path.Combine(runRoot, "evidence-sufficiency"));
        Directory.CreateDirectory(Path.Combine(runRoot, "task-ingress"));

        var proposalPath = Path.Combine(runRoot, "proposals", "memory_update_proposals.jsonl");
        File.WriteAllText(
            proposalPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-proposal.v1",
                proposal_id = "memproposal-sequential",
                reason_codes = new[] { "worker_claim_low_trust" },
                evidence_refs = new[] { "/tmp/proposal-evidence.json" },
                truth_write_authorized = false,
                target_memory_path = (string?)null,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var sufficiencyPath = Path.Combine(runRoot, "evidence-sufficiency", "memory_update_evidence_sufficiency.jsonl");
        File.WriteAllText(
            sufficiencyPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-evidence-sufficiency.v1",
                assessment_id = "memevidence-sequential",
                proposal_id = "memproposal-sequential",
                evidence_level = "L2_measured_small_sample",
                reason_codes = new[] { "review_approval_not_promotion" },
                evidence_refs = new[] { "/tmp/sufficiency-evidence.json" },
                truth_write_authorized = false,
                target_memory_path = (string?)null,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var ingressPath = Path.Combine(runRoot, "task-ingress", "memory_update_task_ingress.jsonl");
        File.WriteAllText(
            ingressPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-task-ingress.v1",
                ingress_id = "memingress-sequential",
                proposal_id = "memproposal-sequential",
                sufficiency_assessment_id = "memevidence-sequential",
                evidence_level = "L2_measured_small_sample",
                ingress_eligible = true,
                host_task_ingress_status = "task_draft_allowed",
                requested_host_action = "create_memory_update_task_draft",
                next_action = "create_host_memory_update_task_draft",
                reason_codes = new[] { "worker_claim_low_trust", "review_approval_not_promotion" },
                evidence_refs = new[] { "/tmp/ingress-evidence.json" },
                truth_write_authorized = false,
                target_memory_path = (string?)null,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var scriptPath = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_update_task_draft.py");
        var output = RunProcessInDirectory(runtimeRepoRoot, python, scriptPath, "--phase5-run-root", runRoot);

        using var outputDocument = JsonDocument.Parse(output);
        var outputRoot = outputDocument.RootElement.GetProperty("output_root").GetString();
        Assert.NotNull(outputRoot);

        var payloadRoot = Path.Combine(outputRoot!, "memingress-sequential");
        var metadataPath = Path.Combine(payloadRoot, "memory_update_task_draft_payload.json");
        var cardPayloadPath = Path.Combine(payloadRoot, "memory_update_card_create.json");
        var taskgraphPayloadPath = Path.Combine(payloadRoot, "memory_update_taskgraph_draft.json");

        Assert.True(File.Exists(metadataPath));
        Assert.True(File.Exists(cardPayloadPath));
        Assert.True(File.Exists(taskgraphPayloadPath));

        using var metadataDocument = JsonDocument.Parse(File.ReadAllText(metadataPath, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-update-task-draft-payload.v1", metadataDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memproposal-sequential", metadataDocument.RootElement.GetProperty("proposal_id").GetString());
        Assert.Equal("memevidence-sequential", metadataDocument.RootElement.GetProperty("sufficiency_assessment_id").GetString());
        Assert.Equal("memingress-sequential", metadataDocument.RootElement.GetProperty("ingress_id").GetString());
        Assert.False(metadataDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.False(metadataDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());
        Assert.Equal(JsonValueKind.Null, metadataDocument.RootElement.GetProperty("target_memory_path").ValueKind);

        using var cardPayloadDocument = JsonDocument.Parse(File.ReadAllText(cardPayloadPath, System.Text.Encoding.UTF8));
        Assert.Equal("CARD-MEMORY-UPDATE-MEMPROPOSAL-SEQUENTIAL", cardPayloadDocument.RootElement.GetProperty("card_id").GetString());
        Assert.Contains(
            cardPayloadDocument.RootElement.GetProperty("constraints").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "Do not directly write .ai/memory/.", StringComparison.Ordinal));

        using var taskgraphPayloadDocument = JsonDocument.Parse(File.ReadAllText(taskgraphPayloadPath, System.Text.Encoding.UTF8));
        Assert.Equal("TG-CARD-MEMORY-UPDATE-MEMPROPOSAL-SEQUENTIAL-001", taskgraphPayloadDocument.RootElement.GetProperty("draft_id").GetString());
        Assert.Equal("CARD-MEMORY-UPDATE-MEMPROPOSAL-SEQUENTIAL", taskgraphPayloadDocument.RootElement.GetProperty("card_id").GetString());
        var task = taskgraphPayloadDocument.RootElement.GetProperty("tasks")[0];
        Assert.Equal("T-CARD-MEMORY-UPDATE-MEMPROPOSAL-SEQUENTIAL-001", task.GetProperty("task_id").GetString());
        Assert.Equal("execution", task.GetProperty("task_type").GetString());
        Assert.Contains(
            task.GetProperty("constraints").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "Do not directly write .ai/memory/.", StringComparison.Ordinal));
    }

    [Fact]
    public void MemoryPromotionGateIngressBuilder_MapsWritebackReviewToPromotionReviewRequired()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "phase7a-promotion-gate-001");
        Directory.CreateDirectory(Path.Combine(runRoot, "execution"));
        Directory.CreateDirectory(Path.Combine(runRoot, "writeback-review"));

        var executionCandidatePath = Path.Combine(runRoot, "execution", "memory_update_execution_candidate.json");
        File.WriteAllText(
            executionCandidatePath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-execution-candidate.v1",
                execution_candidate_id = "memexec-candidate-sequential-001",
                execution_id = "memexec-sequential-001",
                proposal_id = "memproposal-sequential",
                evidence_level = "L2_measured_small_sample",
                writeback_ready = false,
                target_memory_path = (string?)null,
                reason_codes = new[] { "worker_claim_low_trust", "review_approval_not_promotion" },
                execution_authorized = true,
                truth_write_authorized = false,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var writebackReviewPath = Path.Combine(runRoot, "writeback-review", "memory_update_writeback_review.json");
        File.WriteAllText(
            writebackReviewPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-writeback-review.v1",
                writeback_review_id = "memwriteback-review-sequential-001",
                proposal_id = "memproposal-sequential",
                execution_id = "memexec-sequential-001",
                execution_candidate_id = "memexec-candidate-sequential-001",
                phase5_run_id = "phase7a-promotion-gate-001",
                evidence_level = "L2_measured_small_sample",
                writeback_review_status = "completed",
                decision = "route",
                reason_codes = new[] { "memory_write_requires_promotion_gate", "review_approval_not_promotion", "worker_claim_low_trust" },
                current_posture = "promotion_gate_required",
                requested_host_action = "route_memory_update_through_promotion_gate",
                next_action = "create_separate_memory_promotion_gate_line",
                execution_authorized = true,
                truth_write_authorized = false,
                benchmark_uplift_claim_authorized = false,
                target_memory_path = (string?)null,
                forbidden_roots = new[] { ".ai/memory/", ".ai/tasks/", ".ai/artifacts/reviews/", ".carves-platform/" },
            }) + "\n",
            System.Text.Encoding.UTF8);

        var writebackValidationPath = Path.Combine(runRoot, "writeback-review", "writeback_review_validation.json");
        File.WriteAllText(
            writebackValidationPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-writeback-review-validation.v1",
                writeback_review_id = "memwriteback-review-sequential-001",
                overall_status = "passed",
            }) + "\n",
            System.Text.Encoding.UTF8);

        var reviewEvidencePath = Path.Combine(tempRoot.Path, "REVEVI-T-CARD-MEMORY-UPDATE-WRITEBACK-REVIEW-001-001-001.json");
        File.WriteAllText(
            reviewEvidencePath,
            JsonSerializer.Serialize(new
            {
                schema_version = "runtime-evidence-record.v1",
                evidence_id = "REVEVI-T-CARD-MEMORY-UPDATE-WRITEBACK-REVIEW-001-001-001",
                kind = "review",
                task_id = "T-CARD-MEMORY-UPDATE-WRITEBACK-REVIEW-001-001",
            }) + "\n",
            System.Text.Encoding.UTF8);

        var scriptPath = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_promotion_gate_ingress.py");
        var output = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scriptPath,
            "--phase5-run-root",
            runRoot,
            "--review-evidence-path",
            reviewEvidencePath);

        using var outputDocument = JsonDocument.Parse(output);
        var ingressPath = outputDocument.RootElement.GetProperty("output").GetString();
        var validationPath = outputDocument.RootElement.GetProperty("validation").GetString();
        Assert.NotNull(ingressPath);
        Assert.NotNull(validationPath);
        Assert.True(File.Exists(ingressPath));
        Assert.True(File.Exists(validationPath));

        using var ingressDocument = JsonDocument.Parse(File.ReadAllText(ingressPath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-promotion-gate-ingress.v1", ingressDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("mempromotion-ingress-sequential-001", ingressDocument.RootElement.GetProperty("promotion_gate_ingress_id").GetString());
        Assert.Equal("memproposal-sequential", ingressDocument.RootElement.GetProperty("proposal_id").GetString());
        Assert.Equal("memexec-sequential-001", ingressDocument.RootElement.GetProperty("execution_id").GetString());
        Assert.Equal("memexec-candidate-sequential-001", ingressDocument.RootElement.GetProperty("execution_candidate_id").GetString());
        Assert.Equal("memwriteback-review-sequential-001", ingressDocument.RootElement.GetProperty("writeback_review_id").GetString());
        Assert.Equal("REVEVI-T-CARD-MEMORY-UPDATE-WRITEBACK-REVIEW-001-001-001", ingressDocument.RootElement.GetProperty("review_evidence_id").GetString());
        Assert.Equal("review", ingressDocument.RootElement.GetProperty("review_evidence_kind").GetString());
        Assert.Equal("promotion_gate_required", ingressDocument.RootElement.GetProperty("current_posture").GetString());
        Assert.True(ingressDocument.RootElement.GetProperty("promotion_ingress_eligible").GetBoolean());
        Assert.Equal("promotion_review_required", ingressDocument.RootElement.GetProperty("host_promotion_ingress_status").GetString());
        Assert.Equal("review_memory_promotion_target_path", ingressDocument.RootElement.GetProperty("requested_host_action").GetString());
        Assert.Equal("create_memory_promotion_review_line", ingressDocument.RootElement.GetProperty("next_action").GetString());
        Assert.False(ingressDocument.RootElement.GetProperty("promotion_input_ready").GetBoolean());
        Assert.False(ingressDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.False(ingressDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ingressDocument.RootElement.GetProperty("target_memory_path").ValueKind);
        Assert.Equal("memory promote", ingressDocument.RootElement.GetProperty("promotion_entrypoint").GetProperty("command").GetString());
        Assert.Equal("--from-evidence", ingressDocument.RootElement.GetProperty("promotion_entrypoint").GetProperty("required_option").GetString());

        using var validationDocument = JsonDocument.Parse(File.ReadAllText(validationPath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-promotion-gate-ingress-validation.v1", validationDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("passed", validationDocument.RootElement.GetProperty("overall_status").GetString());
    }

    [Fact]
    public void MemoryPromotionReviewBuilder_AuthorizesPatternTargetPathWithoutWriteback()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "phase7b-promotion-review-001");
        Directory.CreateDirectory(Path.Combine(runRoot, "promotion-gate"));
        Directory.CreateDirectory(Path.Combine(runRoot, "execution"));

        var ingressPath = Path.Combine(runRoot, "promotion-gate", "memory_promotion_gate_ingress.json");
        File.WriteAllText(
            ingressPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-promotion-gate-ingress.v1",
                promotion_gate_ingress_id = "mempromotion-ingress-sequential-001",
                proposal_id = "memproposal-sequential",
                execution_id = "memexec-sequential-001",
                execution_candidate_id = "memexec-candidate-sequential-001",
                writeback_review_id = "memwriteback-review-sequential-001",
                review_evidence_id = "REVEVI-T-CARD-MEMORY-UPDATE-WRITEBACK-REVIEW-001-001-001",
                evidence_level = "L2_measured_small_sample",
                current_posture = "promotion_gate_required",
                host_promotion_ingress_status = "promotion_review_required",
                requested_host_action = "review_memory_promotion_target_path",
                next_action = "create_memory_promotion_review_line",
                promotion_input_ready = false,
                truth_write_authorized = false,
                target_memory_path = (string?)null,
                benchmark_uplift_claim_authorized = false,
                reason_codes = new[] { "memory_write_requires_promotion_gate", "review_approval_not_promotion", "worker_claim_low_trust" },
                evidence_refs = new[] { "artifacts/bench/memory-maturity/phase7b-promotion-review-001/writeback-review/memory_update_writeback_review.json" },
                source = new
                {
                    benchmark = "swebench",
                    phase5_run_id = "phase7b-promotion-review-001",
                    promotion_gate_contract_path = "docs/runtime/memory-gate/BENCHMARK_MEMORY_PROMOTION_GATE_INGRESS_CONTRACT_V1.md",
                },
                forbidden_roots = new[] { ".ai/memory/", ".ai/tasks/", ".ai/artifacts/reviews/", ".carves-platform/" },
            }) + "\n",
            System.Text.Encoding.UTF8);

        var executionCandidatePath = Path.Combine(runRoot, "execution", "memory_update_execution_candidate.json");
        File.WriteAllText(
            executionCandidatePath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-update-execution-candidate.v1",
                execution_candidate_id = "memexec-candidate-sequential-001",
                execution_id = "memexec-sequential-001",
                proposal_id = "memproposal-sequential",
                candidate_type = "execution_pattern_update",
                candidate_memory_scope = "execution",
                evidence_level = "L2_measured_small_sample",
                writeback_ready = false,
                target_memory_path = (string?)null,
                reason_codes = new[] { "worker_claim_low_trust", "memory_write_requires_promotion_gate" },
                execution_authorized = true,
                truth_write_authorized = false,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var scriptPath = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_promotion_review.py");
        var output = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scriptPath,
            "--phase5-run-root",
            runRoot);

        using var outputDocument = JsonDocument.Parse(output);
        var reviewPath = outputDocument.RootElement.GetProperty("output").GetString();
        var validationPath = outputDocument.RootElement.GetProperty("validation").GetString();
        Assert.NotNull(reviewPath);
        Assert.NotNull(validationPath);
        Assert.True(File.Exists(reviewPath));
        Assert.True(File.Exists(validationPath));

        using var reviewDocument = JsonDocument.Parse(File.ReadAllText(reviewPath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-promotion-review.v1", reviewDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("mempromotion-review-sequential-001", reviewDocument.RootElement.GetProperty("promotion_review_id").GetString());
        Assert.Equal("mempromotion-ingress-sequential-001", reviewDocument.RootElement.GetProperty("promotion_gate_ingress_id").GetString());
        Assert.Equal("promotion_input_ready", reviewDocument.RootElement.GetProperty("current_posture").GetString());
        Assert.Equal("completed", reviewDocument.RootElement.GetProperty("promotion_review_status").GetString());
        Assert.Equal("authorize_target_path", reviewDocument.RootElement.GetProperty("decision").GetString());
        Assert.Equal("patterns", reviewDocument.RootElement.GetProperty("target_memory_category").GetString());
        Assert.Equal(".ai/memory/patterns/benchmark_memory_update_promotion_gate.md", reviewDocument.RootElement.GetProperty("target_memory_path").GetString());
        Assert.Equal("promotion_input_ready", reviewDocument.RootElement.GetProperty("host_promotion_ingress_status").GetString());
        Assert.Equal("prepare_host_memory_promotion", reviewDocument.RootElement.GetProperty("requested_host_action").GetString());
        Assert.Equal("create_host_memory_promotion_line", reviewDocument.RootElement.GetProperty("next_action").GetString());
        Assert.True(reviewDocument.RootElement.GetProperty("promotion_input_ready").GetBoolean());
        Assert.False(reviewDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.False(reviewDocument.RootElement.GetProperty("canonical_promotion_authorized").GetBoolean());
        Assert.False(reviewDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());
        Assert.Equal("patterns", reviewDocument.RootElement.GetProperty("approved_promotion_input").GetProperty("category").GetString());
        Assert.Equal(".ai/memory/patterns/benchmark_memory_update_promotion_gate.md", reviewDocument.RootElement.GetProperty("approved_promotion_input").GetProperty("target_memory_path").GetString());
        Assert.False(reviewDocument.RootElement.GetProperty("approved_promotion_input").GetProperty("canonical").GetBoolean());

        using var validationDocument = JsonDocument.Parse(File.ReadAllText(validationPath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-promotion-review-validation.v1", validationDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("passed", validationDocument.RootElement.GetProperty("overall_status").GetString());
    }

    [Fact]
    public void MemoryPatternMarkdownDraftBuilder_RendersContractBoundDraftWithoutWriteback()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "phase10e-pattern-draft-001");
        Directory.CreateDirectory(runRoot);

        var canonicalFactPath = Path.Combine(tempRoot.Path, "MEMFACT-003.json");
        File.WriteAllText(
            canonicalFactPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "temporal-memory-fact.v1",
                fact_id = "MEMFACT-003",
                tier = "canonical_memory",
                status = "active",
                category = "patterns",
                title = "Benchmark-derived memory updates require promotion gate after writeback review",
                summary = "Sequential official-harness benchmark lines may reach execution and writeback review at L2_measured_small_sample, but durable Runtime memory promotion still requires a separate promotion gate with explicit target-path authorization.",
                statement = "Treat benchmark-derived memory update lines from the sequential official-harness lane as promotion-gated after writeback review. Completed execution and completed writeback review may prepare a governed candidate, but they do not authorize direct .ai/memory/ mutation, canonical promotion, or benchmark uplift claims. Any later durable memory promotion must stay Host-routed through memory promote --from-evidence with an explicitly authorized target path.",
                scope = "execution",
                task_scope = "T-CARD-MEMORY-UPDATE-WRITEBACK-REVIEW-001-001",
                target_memory_path = ".ai/memory/patterns/benchmark_memory_update_promotion_gate.md",
                source_evidence_ids = new[] { "REVEVI-T-CARD-MEMORY-UPDATE-WRITEBACK-REVIEW-001-001-001" },
                source_candidate_id = "MEMCAND-002",
                source_fact_id = "MEMFACT-002",
                promotion_record_id = "MEMPROM-003",
                proposed_by = "operator",
                promoted_by = "operator",
                confidence = 0.8,
                supersedes = new[] { "MEMFACT-002", "MEMFACT-001" },
            }) + "\n",
            System.Text.Encoding.UTF8);

        var contractSchemaPath = Path.Combine(tempRoot.Path, "BENCHMARK_MEMORY_PATTERN_MARKDOWN_SCHEMA_V1.json");
        File.WriteAllText(
            contractSchemaPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-pattern-markdown.v1",
                contract_id = "pattern-markdown-contract-benchmark-memory-update.v1",
                canonical_fact_id = "MEMFACT-003",
                canonical_promotion_record_id = "MEMPROM-003",
                category = "patterns",
                target_memory_path = ".ai/memory/patterns/benchmark_memory_update_promotion_gate.md",
                required_sections = new[] { "Title", "Rule", "Why", "Boundaries", "Source Lineage", "Non-Claims" },
                required_non_claims = new[]
                {
                    "benchmark_uplift_not_proven",
                    "no_direct_memory_write_authority_from_benchmark_lane",
                    "provisional_line_not_canonical",
                    "no_extra_doctrine_outside_canonical_fact",
                },
                current_posture = "pattern_markdown_draft_required",
                next_allowed_step = "build_pattern_markdown_draft",
                durable_markdown_write_authorized = false,
                benchmark_uplift_claim_authorized = false,
            }) + "\n",
            System.Text.Encoding.UTF8);

        var scriptPath = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_pattern_markdown_draft.py");
        var output = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scriptPath,
            "--phase5-run-root",
            runRoot,
            "--canonical-fact-input",
            canonicalFactPath,
            "--contract-schema-input",
            contractSchemaPath);

        using var outputDocument = JsonDocument.Parse(output);
        var markdownDraftPath = outputDocument.RootElement.GetProperty("markdown_draft").GetString();
        var draftMetadataPath = outputDocument.RootElement.GetProperty("draft_metadata").GetString();
        var validationPath = outputDocument.RootElement.GetProperty("validation").GetString();
        Assert.NotNull(markdownDraftPath);
        Assert.NotNull(draftMetadataPath);
        Assert.NotNull(validationPath);
        Assert.True(File.Exists(markdownDraftPath));
        Assert.True(File.Exists(draftMetadataPath));
        Assert.True(File.Exists(validationPath));

        var markdownDraft = File.ReadAllText(markdownDraftPath!, System.Text.Encoding.UTF8);
        Assert.Contains("## Title", markdownDraft);
        Assert.Contains("## Rule", markdownDraft);
        Assert.Contains("## Why", markdownDraft);
        Assert.Contains("## Boundaries", markdownDraft);
        Assert.Contains("## Source Lineage", markdownDraft);
        Assert.Contains("## Non-Claims", markdownDraft);
        Assert.Contains("does not prove benchmark uplift", markdownDraft);
        Assert.Contains("does not authorize direct `.ai/memory/` writes", markdownDraft);

        using var draftMetadataDocument = JsonDocument.Parse(File.ReadAllText(draftMetadataPath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-pattern-markdown-draft.v1", draftMetadataDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("MEMFACT-003", draftMetadataDocument.RootElement.GetProperty("canonical_fact_id").GetString());
        Assert.Equal("MEMPROM-003", draftMetadataDocument.RootElement.GetProperty("canonical_promotion_record_id").GetString());
        Assert.Equal(".ai/memory/patterns/benchmark_memory_update_promotion_gate.md", draftMetadataDocument.RootElement.GetProperty("target_memory_path").GetString());
        Assert.Equal("pattern_markdown_review_required", draftMetadataDocument.RootElement.GetProperty("current_posture").GetString());
        Assert.Equal("review_pattern_markdown_draft", draftMetadataDocument.RootElement.GetProperty("next_allowed_step").GetString());
        Assert.False(draftMetadataDocument.RootElement.GetProperty("durable_markdown_write_authorized").GetBoolean());
        Assert.False(draftMetadataDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());

        using var validationDocument = JsonDocument.Parse(File.ReadAllText(validationPath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-pattern-markdown-draft-validation.v1", validationDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("passed", validationDocument.RootElement.GetProperty("overall_status").GetString());
    }

    [Fact]
    public void MemoryPatternWritebackRouteBuilder_DefinesHostWritebackRouteWithoutWritingMarkdown()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "phase10g-pattern-writeback-route-001");
        Directory.CreateDirectory(runRoot);

        var draftMetadataPath = Path.Combine(tempRoot.Path, "memory_pattern_markdown_draft.json");
        File.WriteAllText(
            draftMetadataPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-memory-pattern-markdown-draft.v1",
                draft_id = "mempattern-draft-003",
                canonical_fact_id = "MEMFACT-003",
                canonical_promotion_record_id = "MEMPROM-003",
                category = "patterns",
                target_memory_path = ".ai/memory/patterns/benchmark_memory_update_promotion_gate.md",
                draft_markdown_artifact_path = "artifacts/bench/memory-maturity/phase10g-pattern-writeback-route-001/pattern-markdown/benchmark_memory_update_promotion_gate.draft.md",
                required_sections = new[] { "Title", "Rule", "Why", "Boundaries", "Source Lineage", "Non-Claims" },
                required_non_claims = new[]
                {
                    "benchmark_uplift_not_proven",
                    "no_direct_memory_write_authority_from_benchmark_lane",
                    "provisional_line_not_canonical",
                    "no_extra_doctrine_outside_canonical_fact",
                },
                current_posture = "pattern_markdown_review_required",
                next_allowed_step = "review_pattern_markdown_draft",
                durable_markdown_write_authorized = false,
                benchmark_uplift_claim_authorized = false,
                source = new
                {
                    benchmark = "swebench",
                    phase5_run_id = "phase10g-pattern-writeback-route-001",
                    contract_doc_path = "docs/runtime/memory-gate/BENCHMARK_MEMORY_PATTERN_MARKDOWN_CONTRACT_V1.md",
                    canonical_fact_path = ".ai/evidence/facts/MEMFACT-003.json",
                },
            }) + "\n",
            System.Text.Encoding.UTF8);

        var canonicalFactPath = Path.Combine(tempRoot.Path, "MEMFACT-003.json");
        File.WriteAllText(
            canonicalFactPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "temporal-memory-fact.v1",
                fact_id = "MEMFACT-003",
                tier = "canonical_memory",
                status = "active",
                category = "patterns",
                target_memory_path = ".ai/memory/patterns/benchmark_memory_update_promotion_gate.md",
            }) + "\n",
            System.Text.Encoding.UTF8);

        var promotionRecordPath = Path.Combine(tempRoot.Path, "MEMPROM-003.json");
        File.WriteAllText(
            promotionRecordPath,
            JsonSerializer.Serialize(new
            {
                promotion_id = "MEMPROM-003",
                result_fact_id = "MEMFACT-003",
            }) + "\n",
            System.Text.Encoding.UTF8);

        var scriptPath = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_pattern_writeback_route.py");
        var output = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scriptPath,
            "--phase5-run-root",
            runRoot,
            "--draft-metadata-input",
            draftMetadataPath,
            "--canonical-fact-input",
            canonicalFactPath,
            "--canonical-promotion-record-input",
            promotionRecordPath);

        using var outputDocument = JsonDocument.Parse(output);
        var routePath = outputDocument.RootElement.GetProperty("output").GetString();
        var validationPath = outputDocument.RootElement.GetProperty("validation").GetString();
        Assert.NotNull(routePath);
        Assert.NotNull(validationPath);
        Assert.True(File.Exists(routePath));
        Assert.True(File.Exists(validationPath));

        using var routeDocument = JsonDocument.Parse(File.ReadAllText(routePath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-pattern-writeback-route.v1", routeDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("mempattern-writeback-route-003", routeDocument.RootElement.GetProperty("writeback_route_id").GetString());
        Assert.Equal("mempattern-draft-003", routeDocument.RootElement.GetProperty("draft_id").GetString());
        Assert.Equal("MEMFACT-003", routeDocument.RootElement.GetProperty("canonical_fact_id").GetString());
        Assert.Equal("MEMPROM-003", routeDocument.RootElement.GetProperty("canonical_promotion_record_id").GetString());
        Assert.Equal(".ai/memory/patterns/benchmark_memory_update_promotion_gate.md", routeDocument.RootElement.GetProperty("target_memory_path").GetString());
        Assert.Equal("completed", routeDocument.RootElement.GetProperty("route_status").GetString());
        Assert.Equal("durable_markdown_writeback_input_ready", routeDocument.RootElement.GetProperty("current_posture").GetString());
        Assert.Equal("prepare_host_pattern_markdown_writeback", routeDocument.RootElement.GetProperty("requested_host_action").GetString());
        Assert.Equal("create_host_pattern_markdown_writeback_line", routeDocument.RootElement.GetProperty("next_action").GetString());
        Assert.True(routeDocument.RootElement.GetProperty("durable_markdown_writeback_input_ready").GetBoolean());
        Assert.False(routeDocument.RootElement.GetProperty("durable_markdown_write_authorized").GetBoolean());
        Assert.False(routeDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());

        using var validationDocument = JsonDocument.Parse(File.ReadAllText(validationPath!, System.Text.Encoding.UTF8));
        Assert.Equal("benchmark-memory-pattern-writeback-route-validation.v1", validationDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("passed", validationDocument.RootElement.GetProperty("overall_status").GetString());
    }

    [Fact]
    public void DeterministicAblationLane_ProducesVariantArtifactsAndMetrics()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();
        var targetRepoRoot = Path.Combine(tempRoot.Path, "target-repo");
        Directory.CreateDirectory(targetRepoRoot);

        WriteFile(targetRepoRoot, "app.py", "print('before')\n");
        RunProcessInDirectory(targetRepoRoot, "git", "init");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.email", "swebench@example.com");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.name", "SWEbench Ablation");
        RunProcessInDirectory(targetRepoRoot, "git", "add", ".");
        RunProcessInDirectory(targetRepoRoot, "git", "commit", "-m", "base");
        var baseCommit = RunProcessInDirectory(targetRepoRoot, "git", "rev-parse", "HEAD").Trim();

        var problemStatementPath = Path.Combine(tempRoot.Path, "problem.txt");
        File.WriteAllText(problemStatementPath, "Fix app.py output.", System.Text.Encoding.UTF8);

        var mutateCommand = $"{python} -c \"from pathlib import Path; Path('app.py').write_text(\\\"print('after')\\\\n\\\", encoding='utf-8')\"";
        var manifestPath = Path.Combine(tempRoot.Path, "instance-manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "swebench-instance-manifest.v1",
                dataset = "SWE-bench_Lite",
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__repo-0002",
                        repo_root = targetRepoRoot,
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "deterministic_ablation_fixture" },
                    },
                    new
                    {
                        instance_id = "demo__repo-0003",
                        repo_root = targetRepoRoot,
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "deterministic_ablation_fixture_second" },
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "ablation-artifacts");
        var runAblationScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_ablation_matrix.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runAblationScript,
            "--instance-manifest",
            manifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "swebench-ablation-001",
            "--model-name-or-path",
            "carves-ablation",
            "--variant-id",
            "worker-baseline",
            "--variant-id",
            "codegraph-readonly",
            "--variant-id",
            "memory-readonly");

        var runRoot = Path.Combine(artifactRoot, "swebench-ablation-001");
        var ablationManifestPath = Path.Combine(runRoot, "ablation_manifest.json");
        var summarizeScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "summarize_results.py");
        var metricsPath = Path.Combine(runRoot, "metrics.json");
        RunProcessInDirectory(runtimeRepoRoot, python, summarizeScript, "--ablation-manifest", ablationManifestPath, "--output", metricsPath);

        var compareScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "compare_variants.py");
        var comparisonPath = Path.Combine(runRoot, "comparison.md");
        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--metrics-json", metricsPath, "--output", comparisonPath);

        Assert.True(File.Exists(ablationManifestPath));
        Assert.True(File.Exists(metricsPath));
        Assert.True(File.Exists(comparisonPath));

        using var ablationManifest = JsonDocument.Parse(File.ReadAllText(ablationManifestPath));
        Assert.Equal("swebench-ablation-run.v1", ablationManifest.RootElement.GetProperty("schema_version").GetString());
        Assert.All(
            ablationManifest.RootElement.GetProperty("matrix").EnumerateArray().Select(item => item.GetProperty("fresh_workspace").GetBoolean()),
            value => Assert.True(value));

        var workerResultPath = Path.Combine(runRoot, "variants", "worker-baseline", "instances", "demo__repo-0002", "result.json");
        var workerSecondResultPath = Path.Combine(runRoot, "variants", "worker-baseline", "instances", "demo__repo-0003", "result.json");
        var codegraphResultPath = Path.Combine(runRoot, "variants", "codegraph-readonly", "instances", "demo__repo-0002", "result.json");
        var memoryResultPath = Path.Combine(runRoot, "variants", "memory-readonly", "instances", "demo__repo-0002", "result.json");
        var workerPredictionRowOnePath = Path.Combine(runRoot, "variants", "worker-baseline", "instances", "demo__repo-0002", "prediction.row.json");
        var workerPredictionRowTwoPath = Path.Combine(runRoot, "variants", "worker-baseline", "instances", "demo__repo-0003", "prediction.row.json");
        var workerPredictionsPath = Path.Combine(runRoot, "variants", "worker-baseline", "predictions.jsonl");
        var workerVariantSummaryPath = Path.Combine(runRoot, "variants", "worker-baseline", "variant_summary.json");
        Assert.True(File.Exists(workerResultPath));
        Assert.True(File.Exists(workerSecondResultPath));
        Assert.True(File.Exists(codegraphResultPath));
        Assert.True(File.Exists(memoryResultPath));
        Assert.True(File.Exists(workerPredictionRowOnePath));
        Assert.True(File.Exists(workerPredictionRowTwoPath));
        Assert.True(File.Exists(workerPredictionsPath));
        Assert.True(File.Exists(workerVariantSummaryPath));

        var predictionLines = File.ReadAllLines(workerPredictionsPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Equal(2, predictionLines.Length);
        using var firstPrediction = JsonDocument.Parse(predictionLines[0]);
        using var secondPrediction = JsonDocument.Parse(predictionLines[1]);
        Assert.Equal("demo__repo-0002", firstPrediction.RootElement.GetProperty("instance_id").GetString());
        Assert.Equal("demo__repo-0003", secondPrediction.RootElement.GetProperty("instance_id").GetString());

        using var workerVariantSummary = JsonDocument.Parse(File.ReadAllText(workerVariantSummaryPath));
        Assert.Equal(2, workerVariantSummary.RootElement.GetProperty("expected_instance_count").GetInt32());
        Assert.Equal(2, workerVariantSummary.RootElement.GetProperty("actual_prediction_rows").GetInt32());
        Assert.True(workerVariantSummary.RootElement.GetProperty("fan_in_complete").GetBoolean());

        using var metrics = JsonDocument.Parse(File.ReadAllText(metricsPath));
        Assert.Equal("swebench-metrics.v1", metrics.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal(3, metrics.RootElement.GetProperty("variant_metrics").GetArrayLength());

        var markdown = File.ReadAllText(comparisonPath);
        Assert.Contains("Official harness resolution is still `not_run`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void DeterministicAblationLane_WorkspacePrepFailureDoesNotCountAsCompleteProjection()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();
        var missingRepoRoot = Path.Combine(tempRoot.Path, "missing-target-repo");
        var problemStatementPath = Path.Combine(tempRoot.Path, "problem.txt");
        File.WriteAllText(problemStatementPath, "Trigger workspace prep failure.", System.Text.Encoding.UTF8);

        var manifestPath = Path.Combine(tempRoot.Path, "failure-instance-manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "swebench-instance-manifest.v1",
                dataset = "SWE-bench_Lite",
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__repo-failure",
                        repo_root = missingRepoRoot,
                        base_commit = "HEAD",
                        problem_statement_file = problemStatementPath,
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = Array.Empty<string>(),
                        selection_reasons = new[] { "workspace_prep_failure_fixture" },
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "ablation-failure-artifacts");
        var runAblationScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_ablation_matrix.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runAblationScript,
            "--instance-manifest",
            manifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "swebench-ablation-failure-001",
            "--model-name-or-path",
            "carves-ablation",
            "--variant-id",
            "worker-baseline");

        var runRoot = Path.Combine(artifactRoot, "swebench-ablation-failure-001");
        var ablationManifestPath = Path.Combine(runRoot, "ablation_manifest.json");
        var summarizeScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "summarize_results.py");
        var metricsPath = Path.Combine(runRoot, "metrics.json");
        RunProcessInDirectory(runtimeRepoRoot, python, summarizeScript, "--ablation-manifest", ablationManifestPath, "--output", metricsPath);

        var resultPath = Path.Combine(runRoot, "variants", "worker-baseline", "instances", "demo__repo-failure", "result.json");
        var workspacePrepLogPath = Path.Combine(runRoot, "variants", "worker-baseline", "instances", "demo__repo-failure", "logs", "workspace-prep.log");
        Assert.True(File.Exists(resultPath));
        Assert.True(File.Exists(workspacePrepLogPath));

        using var resultDocument = JsonDocument.Parse(File.ReadAllText(resultPath));
        Assert.Equal("failed", resultDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("workspace_prep", resultDocument.RootElement.GetProperty("failure_stage").GetString());
        Assert.False(resultDocument.RootElement.GetProperty("evidence_presence").GetProperty("context_projection").GetBoolean());
        Assert.False(resultDocument.RootElement.GetProperty("evidence_presence").GetProperty("prediction_row").GetBoolean());
        Assert.Contains(
            resultDocument.RootElement.GetProperty("failure_log_paths").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, workspacePrepLogPath, StringComparison.Ordinal));

        using var metrics = JsonDocument.Parse(File.ReadAllText(metricsPath));
        var variantMetrics = metrics.RootElement.GetProperty("variant_metrics").EnumerateArray().Single();
        Assert.Equal(1, variantMetrics.GetProperty("adapter_validity").GetProperty("workspace_prep_error_count").GetInt32());
        Assert.Equal(1, variantMetrics.GetProperty("adapter_validity").GetProperty("failure_stage_counts").GetProperty("workspace_prep").GetInt32());
        Assert.Equal(0.0, variantMetrics.GetProperty("projection_quality").GetProperty("projection_completeness_rate_all_cells").GetDouble());
        Assert.Equal(0, variantMetrics.GetProperty("projection_quality").GetProperty("complete_projection_cells").GetInt32());
    }

    [Fact]
    public void DeterministicAblationLane_HarnessClaimGateUsesMetricsAsPrimaryTruth()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();
        var targetRepoRoot = Path.Combine(tempRoot.Path, "target-repo");
        Directory.CreateDirectory(targetRepoRoot);

        WriteFile(targetRepoRoot, "app.py", "print('before')\n");
        RunProcessInDirectory(targetRepoRoot, "git", "init");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.email", "swebench@example.com");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.name", "SWEbench Harness");
        RunProcessInDirectory(targetRepoRoot, "git", "add", ".");
        RunProcessInDirectory(targetRepoRoot, "git", "commit", "-m", "base");
        var baseCommit = RunProcessInDirectory(targetRepoRoot, "git", "rev-parse", "HEAD").Trim();

        var problemStatementPath = Path.Combine(tempRoot.Path, "problem.txt");
        File.WriteAllText(problemStatementPath, "Fix app.py output.", System.Text.Encoding.UTF8);
        var mutateCommand = $"{python} -c \"from pathlib import Path; Path('app.py').write_text(\\\"print('after')\\\\n\\\", encoding='utf-8')\"";

        var manifestPath = Path.Combine(tempRoot.Path, "instance-manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "swebench-instance-manifest.v1",
                dataset = "SWE-bench_Lite",
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__repo-claim",
                        repo_root = targetRepoRoot,
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "claim_gate_fixture" },
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var runAblationScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_ablation_matrix.py");
        var evaluateMatrixScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_ablation_matrix.py");
        var summarizeScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "summarize_results.py");
        var compareScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "compare_variants.py");

        var completedArtifactRoot = Path.Combine(tempRoot.Path, "completed-artifacts");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runAblationScript,
            "--instance-manifest",
            manifestPath,
            "--artifact-root",
            completedArtifactRoot,
            "--run-id",
            "swebench-claim-completed",
            "--model-name-or-path",
            "carves-ablation",
            "--variant-id",
            "worker-baseline",
            "--variant-id",
            "memory-readonly");

        var completedNormalizedRoot = Path.Combine(tempRoot.Path, "completed-normalized");
        Directory.CreateDirectory(completedNormalizedRoot);
        File.WriteAllText(
            Path.Combine(completedNormalizedRoot, "worker-baseline.json"),
            JsonSerializer.Serialize(new
            {
                matrix_status = "completed",
                per_instance = new object[]
                {
                    new { instance_id = "demo__repo-claim", status = "resolved", raw_ref = "worker/raw", details = (string?)null }
                }
            }),
            System.Text.Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(completedNormalizedRoot, "memory-readonly.json"),
            JsonSerializer.Serialize(new
            {
                matrix_status = "completed",
                per_instance = new object[]
                {
                    new { instance_id = "demo__repo-claim", status = "unresolved", raw_ref = "memory/raw", details = (string?)null }
                }
            }),
            System.Text.Encoding.UTF8);

        var completedRunRoot = Path.Combine(completedArtifactRoot, "swebench-claim-completed");
        var completedManifestPath = Path.Combine(completedRunRoot, "ablation_manifest.json");
        var completedMetricsPath = Path.Combine(completedRunRoot, "metrics.json");
        var completedComparisonPath = Path.Combine(completedRunRoot, "comparison.md");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateMatrixScript,
            "--ablation-manifest",
            completedManifestPath,
            "--mode",
            "print",
            "--normalized-harness-root",
            completedNormalizedRoot);
        RunProcessInDirectory(runtimeRepoRoot, python, summarizeScript, "--ablation-manifest", completedManifestPath, "--output", completedMetricsPath);
        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--metrics-json", completedMetricsPath, "--output", completedComparisonPath);

        var completedHarnessResultPath = Path.Combine(completedRunRoot, "variants", "worker-baseline", "harness_result.json");
        var completedResultPath = Path.Combine(completedRunRoot, "variants", "worker-baseline", "instances", "demo__repo-claim", "result.json");
        Assert.True(File.Exists(completedHarnessResultPath));
        Assert.True(File.Exists(completedResultPath));

        using var completedHarness = JsonDocument.Parse(File.ReadAllText(completedHarnessResultPath));
        Assert.Equal("completed", completedHarness.RootElement.GetProperty("matrix_status").GetString());
        Assert.Equal(1.0, completedHarness.RootElement.GetProperty("resolved_rate").GetDouble());

        using var completedResult = JsonDocument.Parse(File.ReadAllText(completedResultPath));
        Assert.Equal("official_harness", completedResult.RootElement.GetProperty("harness_resolution").GetProperty("reported_by").GetString());
        Assert.Equal("resolved", completedResult.RootElement.GetProperty("harness_resolution").GetProperty("status").GetString());

        using var completedMetrics = JsonDocument.Parse(File.ReadAllText(completedMetricsPath));
        var completedVariantMetrics = completedMetrics.RootElement.GetProperty("variant_metrics").EnumerateArray().ToArray();
        var completedWorkerMetrics = completedVariantMetrics.Single(item => item.GetProperty("variant_id").GetString() == "worker-baseline");
        Assert.Equal("completed", completedWorkerMetrics.GetProperty("harness_resolution").GetProperty("matrix_status").GetString());
        Assert.Equal(1.0, completedWorkerMetrics.GetProperty("harness_resolution").GetProperty("resolved_rate").GetDouble());

        var completedMarkdown = File.ReadAllText(completedComparisonPath);
        Assert.Contains("All compared variants have completed official harness matrix data.", completedMarkdown, StringComparison.Ordinal);
        Assert.Contains("machine-readable metrics as the source of truth", completedMarkdown, StringComparison.Ordinal);

        var partialArtifactRoot = Path.Combine(tempRoot.Path, "partial-artifacts");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runAblationScript,
            "--instance-manifest",
            manifestPath,
            "--artifact-root",
            partialArtifactRoot,
            "--run-id",
            "swebench-claim-partial",
            "--model-name-or-path",
            "carves-ablation",
            "--variant-id",
            "worker-baseline",
            "--variant-id",
            "memory-readonly");

        var partialNormalizedRoot = Path.Combine(tempRoot.Path, "partial-normalized");
        Directory.CreateDirectory(partialNormalizedRoot);
        File.WriteAllText(
            Path.Combine(partialNormalizedRoot, "worker-baseline.json"),
            JsonSerializer.Serialize(new
            {
                matrix_status = "completed",
                per_instance = new object[]
                {
                    new { instance_id = "demo__repo-claim", status = "resolved", raw_ref = "worker/raw", details = (string?)null }
                }
            }),
            System.Text.Encoding.UTF8);

        var partialRunRoot = Path.Combine(partialArtifactRoot, "swebench-claim-partial");
        var partialManifestPath = Path.Combine(partialRunRoot, "ablation_manifest.json");
        var partialMetricsPath = Path.Combine(partialRunRoot, "metrics.json");
        var partialComparisonPath = Path.Combine(partialRunRoot, "comparison.md");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateMatrixScript,
            "--ablation-manifest",
            partialManifestPath,
            "--mode",
            "print",
            "--normalized-harness-root",
            partialNormalizedRoot);
        RunProcessInDirectory(runtimeRepoRoot, python, summarizeScript, "--ablation-manifest", partialManifestPath, "--output", partialMetricsPath);
        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--metrics-json", partialMetricsPath, "--output", partialComparisonPath);

        using var partialMetrics = JsonDocument.Parse(File.ReadAllText(partialMetricsPath));
        var partialVariantMetrics = partialMetrics.RootElement.GetProperty("variant_metrics").EnumerateArray().ToArray();
        var partialMemoryMetrics = partialVariantMetrics.Single(item => item.GetProperty("variant_id").GetString() == "memory-readonly");
        Assert.Equal("not_run", partialMemoryMetrics.GetProperty("harness_resolution").GetProperty("matrix_status").GetString());
        Assert.Equal(JsonValueKind.Null, partialMemoryMetrics.GetProperty("harness_resolution").GetProperty("resolved_rate").ValueKind);

        var partialMarkdown = File.ReadAllText(partialComparisonPath);
        Assert.Contains("must not claim measured benchmark uplift", partialMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("All compared variants have completed official harness matrix data.", partialMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void BenchmarkMemoryGenerator_ProducesArtifactLocalCandidatesDriftAndGateDecisions()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();
        var targetRepoRoot = Path.Combine(tempRoot.Path, "target-repo");
        Directory.CreateDirectory(targetRepoRoot);

        WriteFile(targetRepoRoot, "app.py", "print('before')\n");
        RunProcessInDirectory(targetRepoRoot, "git", "init");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.email", "swebench@example.com");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.name", "SWEbench Memory");
        RunProcessInDirectory(targetRepoRoot, "git", "add", ".");
        RunProcessInDirectory(targetRepoRoot, "git", "commit", "-m", "base");
        var baseCommit = RunProcessInDirectory(targetRepoRoot, "git", "rev-parse", "HEAD").Trim();

        var problemStatementPath = Path.Combine(tempRoot.Path, "problem.txt");
        File.WriteAllText(problemStatementPath, "Fix app.py output.", System.Text.Encoding.UTF8);
        var mutateCommand = $"{python} -c \"from pathlib import Path; Path('app.py').write_text(\\\"print('after')\\\\n\\\", encoding='utf-8')\"";

        var manifestPath = Path.Combine(tempRoot.Path, "memory-instance-manifest.json");
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "swebench-instance-manifest.v1",
                dataset = "SWE-bench_Lite",
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__repo-memory",
                        repo_root = targetRepoRoot,
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "phase4_memory_generator_fixture" },
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "memory-artifacts");
        var runRoot = Path.Combine(artifactRoot, "swebench-memory-001");
        var runAblationScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_ablation_matrix.py");
        var summarizeScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "summarize_results.py");
        var generateCandidatesScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_candidates.py");
        var generateDriftScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_drift_report.py");
        var inspectCandidatesScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "inspect_memory_candidates.py");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runAblationScript,
            "--instance-manifest",
            manifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "swebench-memory-001",
            "--model-name-or-path",
            "carves-memory",
            "--variant-id",
            "codegraph-readonly");

        var ablationManifestPath = Path.Combine(runRoot, "ablation_manifest.json");
        var metricsPath = Path.Combine(runRoot, "metrics.json");
        RunProcessInDirectory(runtimeRepoRoot, python, summarizeScript, "--ablation-manifest", ablationManifestPath, "--output", metricsPath);
        RunProcessInDirectory(runtimeRepoRoot, python, generateCandidatesScript, "--run-root", runRoot);
        RunProcessInDirectory(runtimeRepoRoot, python, generateDriftScript, "--run-root", runRoot);
        var inspectJson = RunProcessInDirectory(runtimeRepoRoot, python, inspectCandidatesScript, "--run-root", runRoot, "--json");

        var memoryRoot = Path.Combine(runRoot, "memory");
        var candidatesPath = Path.Combine(memoryRoot, "memory_candidates.jsonl");
        var decisionsPath = Path.Combine(memoryRoot, "memory_gate_decisions.jsonl");
        var driftPath = Path.Combine(memoryRoot, "memory_drift_report.json");
        var summaryPath = Path.Combine(memoryRoot, "memory_candidate_summary.md");

        Assert.True(File.Exists(candidatesPath));
        Assert.True(File.Exists(decisionsPath));
        Assert.True(File.Exists(driftPath));
        Assert.True(File.Exists(summaryPath));

        var candidateLines = File.ReadAllLines(candidatesPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.NotEmpty(candidateLines);
        using var firstCandidate = JsonDocument.Parse(candidateLines[0]);
        Assert.Equal("benchmark-memory-candidate.v1", firstCandidate.RootElement.GetProperty("schema_version").GetString());
        Assert.False(firstCandidate.RootElement.GetProperty("direct_truth_write_allowed").GetBoolean());
        Assert.True(firstCandidate.RootElement.GetProperty("requires_human_review").GetBoolean());
        Assert.Equal(JsonValueKind.Null, firstCandidate.RootElement.GetProperty("target_memory_path").ValueKind);
        Assert.Equal("swebench", firstCandidate.RootElement.GetProperty("source").GetProperty("benchmark").GetString());
        Assert.Contains(
            firstCandidate.RootElement.GetProperty("evidence_refs").EnumerateArray().Select(item => item.GetString()),
            item => item is not null && item.EndsWith("metrics.json", StringComparison.Ordinal));
        Assert.Contains(
            firstCandidate.RootElement.GetProperty("reason_codes").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "worker_claim_low_trust", StringComparison.Ordinal));

        var decisionLines = File.ReadAllLines(decisionsPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.NotEmpty(decisionLines);
        using var firstDecision = JsonDocument.Parse(decisionLines[0]);
        Assert.Equal("benchmark-memory-gate-decision.v1", firstDecision.RootElement.GetProperty("schema_version").GetString());
        Assert.False(firstDecision.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.True(firstDecision.RootElement.GetProperty("requires_human_review").GetBoolean());
        Assert.Equal(JsonValueKind.Null, firstDecision.RootElement.GetProperty("target_memory_path").ValueKind);

        using var driftReport = JsonDocument.Parse(File.ReadAllText(driftPath));
        Assert.Equal("benchmark-memory-drift-report.v1", driftReport.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("available", driftReport.RootElement.GetProperty("comparison_basis").GetString());
        Assert.Equal("successful_comparison_found", driftReport.RootElement.GetProperty("comparison_basis_reason").GetString());
        var driftFindings = driftReport.RootElement.GetProperty("drift_findings").EnumerateArray().ToArray();
        Assert.NotEmpty(driftFindings);
        Assert.Contains(
            driftFindings.Select(item => item.GetProperty("kind").GetString()),
            item => string.Equals(item, "missing_memory", StringComparison.Ordinal));
        var firstFinding = driftFindings[0];
        Assert.False(firstFinding.GetProperty("direct_truth_write_allowed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, firstFinding.GetProperty("candidate_ref").ValueKind);

        var summaryMarkdown = File.ReadAllText(summaryPath);
        Assert.Contains("Candidates are not Runtime memory truth.", summaryMarkdown, StringComparison.Ordinal);
        Assert.Contains("memory_gate_decisions.jsonl", summaryMarkdown, StringComparison.Ordinal);

        using var inspectDocument = JsonDocument.Parse(inspectJson);
        Assert.Equal("swebench-memory-001", inspectDocument.RootElement.GetProperty("run_id").GetString());
        Assert.True(inspectDocument.RootElement.GetProperty("summary_exists").GetBoolean());
        Assert.False(inspectDocument.RootElement.GetProperty("candidates_are_memory_truth").GetBoolean());
    }

    [Fact]
    public void BenchmarkMemoryGenerator_RejectsOutputsOutsideRunMemoryRoot()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "swebench", "swebench-memory-guard");
        Directory.CreateDirectory(runRoot);

        var generateCandidatesScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_candidates.py");
        var generateDriftScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_drift_report.py");

        var outsideCandidatePath = Path.Combine(tempRoot.Path, "outside-candidates.jsonl");
        var outsideDecisionPath = Path.Combine(tempRoot.Path, "outside-decisions.jsonl");
        var outsideSummaryPath = Path.Combine(tempRoot.Path, "outside-summary.md");
        var outsideDriftPath = Path.Combine(tempRoot.Path, "outside-drift.json");

        var candidateFailure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            generateCandidatesScript,
            "--run-root",
            runRoot,
            "--output",
            outsideCandidatePath);
        Assert.Contains("--output must stay under", candidateFailure, StringComparison.Ordinal);

        var decisionFailure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            generateCandidatesScript,
            "--run-root",
            runRoot,
            "--gate-decisions-output",
            outsideDecisionPath);
        Assert.Contains("--gate-decisions-output must stay under", decisionFailure, StringComparison.Ordinal);

        var summaryFailure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            generateCandidatesScript,
            "--run-root",
            runRoot,
            "--summary-output",
            outsideSummaryPath);
        Assert.Contains("--summary-output must stay under", summaryFailure, StringComparison.Ordinal);

        var driftFailure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            generateDriftScript,
            "--run-root",
            runRoot,
            "--output",
            outsideDriftPath);
        Assert.Contains("--output must stay under", driftFailure, StringComparison.Ordinal);
    }

    [Fact]
    public void BenchmarkMemoryGenerator_NoSuccessDriftReportMarksComparisonBasisUnavailable()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "swebench", "swebench-memory-nosuccess");
        var instanceRoot = Path.Combine(runRoot, "variants", "worker-baseline", "instances", "demo__repo-fail");
        Directory.CreateDirectory(instanceRoot);

        var resultPath = Path.Combine(instanceRoot, "result.json");
        File.WriteAllText(
            resultPath,
            JsonSerializer.Serialize(new
            {
                run_id = "swebench-memory-nosuccess",
                variant = "worker-baseline",
                instance_id = "demo__repo-fail",
                status = "failed",
                carves_evidence = new { }
            }),
            System.Text.Encoding.UTF8);

        var generateDriftScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_drift_report.py");
        RunProcessInDirectory(runtimeRepoRoot, python, generateDriftScript, "--run-root", runRoot);

        var driftPath = Path.Combine(runRoot, "memory", "memory_drift_report.json");
        Assert.True(File.Exists(driftPath));

        using var driftReport = JsonDocument.Parse(File.ReadAllText(driftPath));
        Assert.Equal("not_available", driftReport.RootElement.GetProperty("comparison_basis").GetString());
        Assert.Equal("no_successful_comparison", driftReport.RootElement.GetProperty("comparison_basis_reason").GetString());
        Assert.Equal(JsonValueKind.Null, driftReport.RootElement.GetProperty("source_artifacts").GetProperty("phase3_result_path").ValueKind);
        Assert.Equal(JsonValueKind.Null, driftReport.RootElement.GetProperty("source_artifacts").GetProperty("context_projection_path").ValueKind);
        Assert.Equal(JsonValueKind.Null, driftReport.RootElement.GetProperty("source_artifacts").GetProperty("codegraph_read_set_path").ValueKind);
        Assert.Equal(JsonValueKind.Null, driftReport.RootElement.GetProperty("source_artifacts").GetProperty("memory_read_set_path").ValueKind);
        Assert.Empty(driftReport.RootElement.GetProperty("drift_findings").EnumerateArray());
    }

    [Fact]
    public void WarmRunProjectionBuilder_ProducesBoundedProjectionAndValidation()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var phase4RunRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "swebench", "phase4-run-001");
        var phase4MemoryRoot = Path.Combine(phase4RunRoot, "memory");
        Directory.CreateDirectory(phase4MemoryRoot);

        var evidenceRoot = Path.Combine(phase4RunRoot, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        var reviewRoot = Path.Combine(phase4RunRoot, "reviews");
        Directory.CreateDirectory(reviewRoot);

        var approvedEvidencePath = Path.Combine(evidenceRoot, "approved-evidence.json");
        var rejectedEvidencePath = Path.Combine(evidenceRoot, "rejected-evidence.json");
        var pendingEvidencePath = Path.Combine(evidenceRoot, "pending-evidence.json");
        var approvedReviewRef = Path.Combine(reviewRoot, "approved-review.json");
        File.WriteAllText(approvedEvidencePath, "{\"ok\":true}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(rejectedEvidencePath, "{\"ok\":true}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(pendingEvidencePath, "{\"ok\":true}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(approvedReviewRef, "{\"approved\":true}\n", System.Text.Encoding.UTF8);

        var candidatesPath = Path.Combine(phase4MemoryRoot, "memory_candidates.jsonl");
        File.WriteAllLines(
            candidatesPath,
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-candidate.v1",
                    candidate_id = "memcand-approved",
                    source = new
                    {
                        benchmark = "swebench",
                        run_id = "phase4-run-001",
                        variant_id = "worker-baseline",
                        instance_id = "demo__approved"
                    },
                    candidate_type = "architecture_rule",
                    claim = "Approved candidate claim.",
                    evidence_refs = new[] { approvedEvidencePath },
                    evidence_presence_required = true,
                    proposed_memory_scope = "architecture",
                    target_memory_path = (string?)null,
                    direct_truth_write_allowed = false,
                    requires_human_review = true,
                    confidence = "high",
                    risk = "low",
                    reason_codes = new[] { "worker_claim_low_trust" }
                }),
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-candidate.v1",
                    candidate_id = "memcand-rejected",
                    source = new
                    {
                        benchmark = "swebench",
                        run_id = "phase4-run-001",
                        variant_id = "worker-baseline",
                        instance_id = "demo__rejected"
                    },
                    candidate_type = "module_fact",
                    claim = "Rejected candidate claim.",
                    evidence_refs = new[] { rejectedEvidencePath },
                    evidence_presence_required = true,
                    proposed_memory_scope = "module",
                    target_memory_path = (string?)null,
                    direct_truth_write_allowed = false,
                    requires_human_review = true,
                    confidence = "medium",
                    risk = "medium",
                    reason_codes = new[] { "worker_claim_low_trust" }
                }),
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-candidate.v1",
                    candidate_id = "memcand-pending",
                    source = new
                    {
                        benchmark = "swebench",
                        run_id = "phase4-run-001",
                        variant_id = "worker-baseline",
                        instance_id = "demo__pending"
                    },
                    candidate_type = "context_selection_rule",
                    claim = "Pending candidate claim.",
                    evidence_refs = new[] { pendingEvidencePath },
                    evidence_presence_required = true,
                    proposed_memory_scope = "execution",
                    target_memory_path = (string?)null,
                    direct_truth_write_allowed = false,
                    requires_human_review = true,
                    confidence = "medium",
                    risk = "low",
                    reason_codes = new[] { "worker_claim_low_trust" }
                })
            },
            System.Text.Encoding.UTF8);

        var decisionsPath = Path.Combine(phase4MemoryRoot, "memory_gate_decisions.jsonl");
        File.WriteAllLines(
            decisionsPath,
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-gate-decision.v1",
                    decision_id = "memgate-approved",
                    candidate_id = "memcand-approved",
                    decision = "promote",
                    reason_codes = new[] { "worker_claim_low_trust" },
                    evidence_refs = new[] { approvedEvidencePath },
                    requires_human_review = true,
                    allowed_next_action = "create_memory_update_task",
                    truth_write_authorized = false,
                    target_memory_path = (string?)null
                }),
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-gate-decision.v1",
                    decision_id = "memgate-rejected",
                    candidate_id = "memcand-rejected",
                    decision = "reject",
                    reason_codes = new[] { "worker_claim_low_trust" },
                    evidence_refs = new[] { rejectedEvidencePath },
                    requires_human_review = true,
                    allowed_next_action = "reject_candidate",
                    truth_write_authorized = false,
                    target_memory_path = (string?)null
                }),
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-gate-decision.v1",
                    decision_id = "memgate-pending",
                    candidate_id = "memcand-pending",
                    decision = "promote",
                    reason_codes = new[] { "worker_claim_low_trust" },
                    evidence_refs = new[] { pendingEvidencePath },
                    requires_human_review = true,
                    allowed_next_action = "create_memory_update_task",
                    truth_write_authorized = false,
                    target_memory_path = (string?)null
                })
            },
            System.Text.Encoding.UTF8);

        var reviewApprovalsPath = Path.Combine(phase4MemoryRoot, "review_approvals.jsonl");
        File.WriteAllLines(
            reviewApprovalsPath,
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    candidate_id = "memcand-approved",
                    gate_decision_id = "memgate-approved",
                    review_approval_status = "approved",
                    review_approval_ref = approvedReviewRef
                }),
                JsonSerializer.Serialize(new
                {
                    candidate_id = "memcand-rejected",
                    gate_decision_id = "memgate-rejected",
                    review_approval_status = "approved",
                    review_approval_ref = approvedReviewRef
                }),
                JsonSerializer.Serialize(new
                {
                    candidate_id = "memcand-pending",
                    gate_decision_id = "memgate-pending",
                    review_approval_status = "pending",
                    review_approval_ref = (string?)null
                })
            },
            System.Text.Encoding.UTF8);

        var warmRunRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "memory-run-001");
        var buildScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_warm_run_projection.py");
        var validateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "validate_warm_run_projection.py");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            buildScript,
            "--phase4-run-root",
            phase4RunRoot,
            "--warm-run-root",
            warmRunRoot,
            "--repo",
            "demo/repo",
            "--sequence-id",
            "sequence-001",
            "--review-approvals-jsonl",
            reviewApprovalsPath);

        var validationOutput = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            validateScript,
            "--warm-run-root",
            warmRunRoot);

        var projectionRoot = Path.Combine(warmRunRoot, "warm-run", "projection");
        var approvedLedgerPath = Path.Combine(projectionRoot, "approved_candidate_ledger.jsonl");
        var excludedPath = Path.Combine(projectionRoot, "excluded_candidates.jsonl");
        var projectionPath = Path.Combine(projectionRoot, "warm_run_projection.json");
        var validationPath = Path.Combine(projectionRoot, "projection_validation.json");

        Assert.True(File.Exists(approvedLedgerPath));
        Assert.True(File.Exists(excludedPath));
        Assert.True(File.Exists(projectionPath));
        Assert.True(File.Exists(validationPath));

        var approvedLines = File.ReadAllLines(approvedLedgerPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Single(approvedLines);
        using var approvedLedgerDoc = JsonDocument.Parse(approvedLines[0]);
        Assert.Equal("benchmark-approved-candidate-consumption.v1", approvedLedgerDoc.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("approved", approvedLedgerDoc.RootElement.GetProperty("consumption_status").GetString());
        Assert.True(approvedLedgerDoc.RootElement.GetProperty("warm_run_eligible").GetBoolean());
        Assert.False(approvedLedgerDoc.RootElement.GetProperty("direct_truth_write_allowed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, approvedLedgerDoc.RootElement.GetProperty("target_memory_path").ValueKind);

        var excludedLines = File.ReadAllLines(excludedPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Equal(2, excludedLines.Length);

        using var projectionDocument = JsonDocument.Parse(File.ReadAllText(projectionPath));
        Assert.Equal("warm-run-projection.v1", projectionDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.False(projectionDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.Equal(JsonValueKind.Null, projectionDocument.RootElement.GetProperty("memory_truth_path").ValueKind);
        Assert.Equal("demo/repo", projectionDocument.RootElement.GetProperty("projection_scope").GetProperty("repo").GetString());
        Assert.Equal("sequence-001", projectionDocument.RootElement.GetProperty("projection_scope").GetProperty("sequence_id").GetString());
        var sourceCandidates = projectionDocument.RootElement.GetProperty("source_candidates").EnumerateArray().ToArray();
        Assert.Single(sourceCandidates);
        Assert.Equal("memcand-approved", sourceCandidates[0].GetProperty("candidate_id").GetString());
        Assert.Equal("approved", sourceCandidates[0].GetProperty("consumption_status").GetString());
        Assert.Contains(
            projectionDocument.RootElement.GetProperty("forbidden_roots").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".ai/memory/", StringComparison.Ordinal));

        using var validationDocument = JsonDocument.Parse(validationOutput);
        Assert.True(validationDocument.RootElement.GetProperty("valid").GetBoolean());
        Assert.True(validationDocument.RootElement.GetProperty("all_evidence_refs_present").GetBoolean());
        Assert.Empty(validationDocument.RootElement.GetProperty("missing_evidence_refs").EnumerateArray());
        Assert.Empty(validationDocument.RootElement.GetProperty("forbidden_root_targets_detected").EnumerateArray());
        Assert.Equal(1, validationDocument.RootElement.GetProperty("approved_candidate_count").GetInt32());
        Assert.Equal(2, validationDocument.RootElement.GetProperty("excluded_candidate_count").GetInt32());
    }

    [Fact]
    public void WarmRunProjectionBuilder_RejectsWarmRunRootOutsideMemoryMaturityArtifacts()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var phase4RunRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "swebench", "phase4-run-guard");
        var phase4MemoryRoot = Path.Combine(phase4RunRoot, "memory");
        Directory.CreateDirectory(phase4MemoryRoot);

        var evidencePath = Path.Combine(phase4RunRoot, "evidence.json");
        File.WriteAllText(evidencePath, "{\"ok\":true}\n", System.Text.Encoding.UTF8);
        var reviewPath = Path.Combine(phase4RunRoot, "review.json");
        File.WriteAllText(reviewPath, "{\"approved\":true}\n", System.Text.Encoding.UTF8);

        File.WriteAllLines(
            Path.Combine(phase4MemoryRoot, "memory_candidates.jsonl"),
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-candidate.v1",
                    candidate_id = "memcand-guard",
                    source = new { benchmark = "swebench", run_id = "phase4-run-guard", variant_id = "worker-baseline", instance_id = "demo__guard" },
                    candidate_type = "module_fact",
                    claim = "Guard candidate.",
                    evidence_refs = new[] { evidencePath },
                    evidence_presence_required = true,
                    proposed_memory_scope = "module",
                    target_memory_path = (string?)null,
                    direct_truth_write_allowed = false,
                    requires_human_review = true,
                    confidence = "medium",
                    risk = "low",
                    reason_codes = new[] { "worker_claim_low_trust" }
                })
            },
            System.Text.Encoding.UTF8);

        File.WriteAllLines(
            Path.Combine(phase4MemoryRoot, "memory_gate_decisions.jsonl"),
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-gate-decision.v1",
                    decision_id = "memgate-guard",
                    candidate_id = "memcand-guard",
                    decision = "promote",
                    reason_codes = new[] { "worker_claim_low_trust" },
                    evidence_refs = new[] { evidencePath },
                    requires_human_review = true,
                    allowed_next_action = "create_memory_update_task",
                    truth_write_authorized = false,
                    target_memory_path = (string?)null
                })
            },
            System.Text.Encoding.UTF8);

        var reviewApprovalsPath = Path.Combine(phase4MemoryRoot, "review_approvals.jsonl");
        File.WriteAllLines(
            reviewApprovalsPath,
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    candidate_id = "memcand-guard",
                    gate_decision_id = "memgate-guard",
                    review_approval_status = "approved",
                    review_approval_ref = reviewPath
                })
            },
            System.Text.Encoding.UTF8);

        var buildScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_warm_run_projection.py");
        var invalidWarmRunRoot = Path.Combine(tempRoot.Path, "outside-memory-maturity");

        var failure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            buildScript,
            "--phase4-run-root",
            phase4RunRoot,
            "--warm-run-root",
            invalidWarmRunRoot,
            "--repo",
            "demo/repo",
            "--sequence-id",
            "sequence-guard",
            "--review-approvals-jsonl",
            reviewApprovalsPath);

        Assert.Contains("--warm-run-root must stay under artifacts/bench/memory-maturity/<run-id>", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void WarmRunConsumptionReceipt_ProducesReceiptAndNoWriteProof()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var phase4RunRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "swebench", "phase4-run-receipt");
        var phase4MemoryRoot = Path.Combine(phase4RunRoot, "memory");
        Directory.CreateDirectory(phase4MemoryRoot);

        var evidenceRoot = Path.Combine(phase4RunRoot, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        var reviewRoot = Path.Combine(phase4RunRoot, "reviews");
        Directory.CreateDirectory(reviewRoot);

        var evidencePath = Path.Combine(evidenceRoot, "approved-evidence.json");
        var reviewRef = Path.Combine(reviewRoot, "approved-review.json");
        File.WriteAllText(evidencePath, "{\"ok\":true}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(reviewRef, "{\"approved\":true}\n", System.Text.Encoding.UTF8);

        File.WriteAllLines(
            Path.Combine(phase4MemoryRoot, "memory_candidates.jsonl"),
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-candidate.v1",
                    candidate_id = "memcand-receipt",
                    source = new { benchmark = "swebench", run_id = "phase4-run-receipt", variant_id = "worker-baseline", instance_id = "demo__receipt" },
                    candidate_type = "execution_pattern",
                    claim = "Receipt candidate claim for projection consumption.",
                    evidence_refs = new[] { evidencePath },
                    evidence_presence_required = true,
                    proposed_memory_scope = "execution",
                    target_memory_path = (string?)null,
                    direct_truth_write_allowed = false,
                    requires_human_review = true,
                    confidence = "medium",
                    risk = "low",
                    reason_codes = new[] { "worker_claim_low_trust" }
                })
            },
            System.Text.Encoding.UTF8);

        File.WriteAllLines(
            Path.Combine(phase4MemoryRoot, "memory_gate_decisions.jsonl"),
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-gate-decision.v1",
                    decision_id = "memgate-receipt",
                    candidate_id = "memcand-receipt",
                    decision = "promote",
                    reason_codes = new[] { "worker_claim_low_trust" },
                    evidence_refs = new[] { evidencePath },
                    requires_human_review = true,
                    allowed_next_action = "create_memory_update_task",
                    truth_write_authorized = false,
                    target_memory_path = (string?)null
                })
            },
            System.Text.Encoding.UTF8);

        var reviewApprovalsPath = Path.Combine(phase4MemoryRoot, "review_approvals.jsonl");
        File.WriteAllLines(
            reviewApprovalsPath,
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    candidate_id = "memcand-receipt",
                    gate_decision_id = "memgate-receipt",
                    review_approval_status = "approved",
                    review_approval_ref = reviewRef
                })
            },
            System.Text.Encoding.UTF8);

        var warmRunRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "memory-run-receipt");
        var buildScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_warm_run_projection.py");
        var validateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "validate_warm_run_projection.py");
        var recordScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "record_warm_run_consumption.py");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            buildScript,
            "--phase4-run-root",
            phase4RunRoot,
            "--warm-run-root",
            warmRunRoot,
            "--repo",
            "demo/repo",
            "--sequence-id",
            "sequence-receipt",
            "--review-approvals-jsonl",
            reviewApprovalsPath);

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            validateScript,
            "--warm-run-root",
            warmRunRoot);

        var receiptOutput = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            recordScript,
            "--warm-run-root",
            warmRunRoot,
            "--instance-id",
            "demo__receipt",
            "--consume-all-approved",
            "--context-pack-ref",
            "context-pack://warm-run/demo__receipt",
            "--context-pack-ref",
            "projection://warm-run/demo__receipt");

        var instanceRoot = Path.Combine(warmRunRoot, "warm-run", "instances", "demo__receipt");
        var receiptPath = Path.Combine(instanceRoot, "projection_consumption_receipt.json");
        var noWriteReportPath = Path.Combine(instanceRoot, "truth_root_no_write_report.json");

        Assert.True(File.Exists(receiptPath));
        Assert.True(File.Exists(noWriteReportPath));

        using var receiptDocument = JsonDocument.Parse(File.ReadAllText(receiptPath));
        Assert.Equal("warm-run-consumption-receipt.v1", receiptDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("demo__receipt", receiptDocument.RootElement.GetProperty("instance_id").GetString());
        Assert.False(receiptDocument.RootElement.GetProperty("truth_write_attempted").GetBoolean());
        Assert.Equal(0, receiptDocument.RootElement.GetProperty("truth_write_paths").GetArrayLength());
        Assert.Contains(
            receiptDocument.RootElement.GetProperty("consumed_candidate_ids").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "memcand-receipt", StringComparison.Ordinal));
        Assert.Contains(
            receiptDocument.RootElement.GetProperty("context_pack_refs").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "context-pack://warm-run/demo__receipt", StringComparison.Ordinal));

        using var noWriteDocument = JsonDocument.Parse(File.ReadAllText(noWriteReportPath));
        Assert.Equal("passed", noWriteDocument.RootElement.GetProperty("proof_status").GetString());
        Assert.False(noWriteDocument.RootElement.GetProperty("truth_write_attempted").GetBoolean());
        Assert.Equal(0, noWriteDocument.RootElement.GetProperty("truth_write_paths").GetArrayLength());
        Assert.Equal(0, noWriteDocument.RootElement.GetProperty("forbidden_root_targets_detected").GetArrayLength());
        Assert.Contains(
            noWriteDocument.RootElement.GetProperty("checked_forbidden_roots").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".ai/memory/", StringComparison.Ordinal));

        using var receiptOutputDocument = JsonDocument.Parse(receiptOutput);
        Assert.Equal(receiptPath, receiptOutputDocument.RootElement.GetProperty("receipt_path").GetString());
        Assert.Equal(noWriteReportPath, receiptOutputDocument.RootElement.GetProperty("no_write_report_path").GetString());
        Assert.False(receiptOutputDocument.RootElement.GetProperty("truth_write_attempted").GetBoolean());
    }

    [Fact]
    public void WarmRunConsumptionReceipt_RejectsWarmRunRootOutsideMemoryMaturityArtifacts()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var recordScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "record_warm_run_consumption.py");
        var invalidWarmRunRoot = Path.Combine(tempRoot.Path, "outside-memory-maturity");

        var failure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            recordScript,
            "--warm-run-root",
            invalidWarmRunRoot,
            "--instance-id",
            "demo__invalid",
            "--consume-all-approved");

        Assert.Contains("--warm-run-root must stay under artifacts/bench/memory-maturity/<run-id>", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryUpdateProposalGenerator_ProducesBoundedProposalAndSufficiencyArtifacts()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "memory-update-run-001");
        var projectionRoot = Path.Combine(runRoot, "warm-run", "projection");
        var instanceRoot = Path.Combine(runRoot, "warm-run", "instances", "demo__sequence");
        Directory.CreateDirectory(projectionRoot);
        Directory.CreateDirectory(instanceRoot);

        var reviewRef = Path.Combine(runRoot, "reviews", "approved-review.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reviewRef)!);
        File.WriteAllText(reviewRef, "{\"approved\":true}\n", System.Text.Encoding.UTF8);

        var evidencePath = Path.Combine(runRoot, "evidence", "approved-evidence.json");
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        File.WriteAllText(evidencePath, "{\"ok\":true}\n", System.Text.Encoding.UTF8);

        var approvedLedgerPath = Path.Combine(projectionRoot, "approved_candidate_ledger.jsonl");
        File.WriteAllLines(
            approvedLedgerPath,
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-approved-candidate-consumption.v1",
                    consumption_id = "memconsume-sequential",
                    candidate_id = "memcand-sequential",
                    gate_decision_id = "memgate-sequential",
                    source = new
                    {
                        benchmark = "swebench",
                        run_id = "phase4-seq-official-harness-minimal-001",
                        variant_id = "worker-baseline",
                        instance_id = "demo__sequence"
                    },
                    gate_decision = "promote",
                    review_approval_status = "approved",
                    review_approval_ref = reviewRef,
                    consumption_status = "approved",
                    warm_run_eligible = true,
                    evidence_refs = new[] { evidencePath, reviewRef },
                    reason_codes = new[] { "worker_claim_low_trust" },
                    direct_truth_write_allowed = false,
                    target_memory_path = (string?)null
                })
            },
            System.Text.Encoding.UTF8);

        var projectionPath = Path.Combine(projectionRoot, "warm_run_projection.json");
        File.WriteAllText(
            projectionPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "warm-run-projection.v1",
                projection_id = "warmproj-memory-update-001",
                source_phase4_run_id = "phase4-seq-official-harness-minimal-001",
                source_candidates = new object[]
                {
                    new
                    {
                        candidate_id = "memcand-sequential",
                        gate_decision_id = "memgate-sequential",
                        consumption_id = "memconsume-sequential",
                        review_approval_ref = reviewRef,
                        consumption_status = "approved",
                        claim = "Approved sequential candidate claim.",
                        projected_as = "execution_pattern",
                        evidence_refs = new[] { evidencePath, reviewRef }
                    }
                },
                projection_scope = new
                {
                    benchmark = "swebench",
                    repo = "demo/repo",
                    sequence_id = "sequence-memory-update-001"
                },
                truth_write_authorized = false,
                memory_truth_path = (string?)null,
                forbidden_roots = new[] { ".ai/memory/", ".ai/tasks/", ".ai/artifacts/reviews/", ".carves-platform/" }
            }),
            System.Text.Encoding.UTF8);

        var receiptPath = Path.Combine(instanceRoot, "projection_consumption_receipt.json");
        File.WriteAllText(
            receiptPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "warm-run-consumption-receipt.v1",
                run_id = "memory-update-run-001",
                sequence_id = "sequence-memory-update-001",
                instance_id = "demo__sequence",
                projection_id = "warmproj-memory-update-001",
                consumed_candidate_ids = new[] { "memcand-sequential" },
                excluded_candidate_ids = Array.Empty<string>(),
                context_pack_refs = new[] { "context-pack://warm-run/demo__sequence" },
                estimated_projection_tokens = 12,
                forbidden_inputs_observed = false,
                truth_write_attempted = false,
                truth_write_paths = Array.Empty<string>()
            }),
            System.Text.Encoding.UTF8);

        var metricsPath = Path.Combine(runRoot, "memory_reuse_metrics.json");
        File.WriteAllText(
            metricsPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "memory-reuse-metrics.v1",
                run_id = "memory-update-run-001",
                sequence_id = "sequence-memory-update-001",
                projection_reuse = new
                {
                    warm_projection_available = true,
                    approved_candidate_count = 1,
                    consumed_candidate_count = 1,
                    excluded_candidate_count = 0,
                    projection_token_estimate = 12,
                    truth_root_no_write_pass_rate = 1.0,
                    warm_receipt_coverage_rate = 1.0
                },
                cold_warm_comparison = new
                {
                    same_repo_confirmed = true,
                    same_sequence_confirmed = true,
                    same_base_commit_confirmed = true,
                    same_provider_profile_confirmed = true,
                    same_budget_confirmed = true,
                    same_harness_policy_confirmed = true,
                    only_warm_projection_diff = true
                },
                lane_metrics = new
                {
                    cold_run = new { status = "completed" },
                    warm_run = new { status = "completed" }
                },
                harness_resolution = new
                {
                    status = "complete",
                    cold_resolved_rate = 0.0,
                    warm_resolved_rate = 0.0,
                    resolved_rate_uplift = 0.0
                },
                harness_readiness = new
                {
                    status = "ready",
                    reason_code = "none",
                    docker_api_endpoint = "unix:///var/run/docker.sock",
                    docker_api_status = "available",
                    docker_api_candidates = new[] { "unix:///var/run/docker.sock" },
                    docker_host = "unix:///var/run/docker.sock",
                    docker_probe_mode = "api",
                    harness_python = "/usr/bin/python3",
                    harness_module = "swebench.harness.run_evaluation",
                    module_status = "available",
                    pip_status = "available"
                },
                truth_root_audit = new
                {
                    proof_status = "passed",
                    verdict = "passed",
                    audit_root_kind = "isolated_workspace_root",
                    changed_root_count = 0,
                    blocking_root_count = 0,
                    review_required_root_count = 0,
                    changed_path_count = 0,
                    platform_state_change_count = 0
                },
                claim_gates = new
                {
                    benchmark_uplift_claim = false,
                    uplift_claim_allowed = false,
                    claim_scope = "smoke_only",
                    reason = "sample_size_below_threshold",
                    sample_size = 1,
                    sample_size_threshold = 3
                }
            }),
            System.Text.Encoding.UTF8);

        var scorecardPath = Path.Combine(runRoot, "memory_maturity_scorecard.json");
        File.WriteAllText(scorecardPath, "{\"schema_version\":\"memory-maturity-scorecard.v1\"}\n", System.Text.Encoding.UTF8);

        var truthAuditPath = Path.Combine(runRoot, "truth_root_no_write_report.json");
        File.WriteAllText(
            truthAuditPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "truth-root-no-write-report.v1",
                proof_status = "passed"
            }),
            System.Text.Encoding.UTF8);

        var generatorScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_update_proposals.py");
        var output = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            generatorScript,
            "--phase5-run-root",
            runRoot);

        var proposalsPath = Path.Combine(runRoot, "proposals", "memory_update_proposals.jsonl");
        var sufficiencyPath = Path.Combine(runRoot, "evidence-sufficiency", "memory_update_evidence_sufficiency.jsonl");
        var summaryPath = Path.Combine(runRoot, "proposals", "memory_update_proposal_summary.md");

        Assert.True(File.Exists(proposalsPath));
        Assert.True(File.Exists(sufficiencyPath));
        Assert.True(File.Exists(summaryPath));

        var proposalLines = File.ReadAllLines(proposalsPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Single(proposalLines);
        using var proposalDocument = JsonDocument.Parse(proposalLines[0]);
        Assert.Equal("benchmark-memory-update-proposal.v1", proposalDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("proposal_only", proposalDocument.RootElement.GetProperty("proposal_status").GetString());
        Assert.False(proposalDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.False(proposalDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());
        Assert.Equal(JsonValueKind.Null, proposalDocument.RootElement.GetProperty("target_memory_path").ValueKind);
        Assert.Equal("request_more_evidence", proposalDocument.RootElement.GetProperty("allowed_next_action").GetString());

        var sufficiencyLines = File.ReadAllLines(sufficiencyPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Single(sufficiencyLines);
        using var sufficiencyDocument = JsonDocument.Parse(sufficiencyLines[0]);
        Assert.Equal("benchmark-memory-update-evidence-sufficiency.v1", sufficiencyDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("L1_smoke_measurement", sufficiencyDocument.RootElement.GetProperty("evidence_level").GetString());
        Assert.Equal("review_required", sufficiencyDocument.RootElement.GetProperty("next_action").GetString());
        Assert.False(sufficiencyDocument.RootElement.GetProperty("task_proposal_allowed").GetBoolean());
        Assert.Equal("proposal_only", sufficiencyDocument.RootElement.GetProperty("host_memory_update_task_status").GetString());
        Assert.False(sufficiencyDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.False(sufficiencyDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());
        Assert.Equal(JsonValueKind.Null, sufficiencyDocument.RootElement.GetProperty("target_memory_path").ValueKind);

        using var outputDocument = JsonDocument.Parse(output);
        Assert.Equal(proposalsPath, outputDocument.RootElement.GetProperty("proposal_output").GetString());
        Assert.Equal(sufficiencyPath, outputDocument.RootElement.GetProperty("sufficiency_output").GetString());
        Assert.Equal(summaryPath, outputDocument.RootElement.GetProperty("summary_output").GetString());
    }

    [Fact]
    public void MemoryUpdateProposalGenerator_RejectsProposalOutputOutsideBoundedRoots()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "memory-update-run-guard");
        Directory.CreateDirectory(Path.Combine(runRoot, "warm-run", "projection"));
        Directory.CreateDirectory(Path.Combine(runRoot, "warm-run", "instances", "demo__sequence"));
        File.WriteAllText(Path.Combine(runRoot, "memory_reuse_metrics.json"), "{\"claim_gates\":{\"claim_scope\":\"smoke_only\",\"sample_size\":1,\"sample_size_threshold\":3},\"harness_resolution\":{\"status\":\"complete\",\"resolved_rate_uplift\":0.0},\"truth_root_audit\":{\"proof_status\":\"passed\"},\"cold_warm_comparison\":{\"same_repo_confirmed\":true,\"same_sequence_confirmed\":true,\"same_base_commit_confirmed\":true,\"same_provider_profile_confirmed\":true,\"same_budget_confirmed\":true,\"same_harness_policy_confirmed\":true,\"only_warm_projection_diff\":true}}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(runRoot, "memory_maturity_scorecard.json"), "{}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(runRoot, "truth_root_no_write_report.json"), "{\"proof_status\":\"passed\"}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(runRoot, "warm-run", "projection", "warm_run_projection.json"), "{\"projection_id\":\"warmproj-guard\",\"source_candidates\":[{\"candidate_id\":\"memcand-guard\",\"gate_decision_id\":\"memgate-guard\",\"claim\":\"Guard claim\",\"projected_as\":\"execution_pattern\",\"evidence_refs\":[]}]} \n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(runRoot, "warm-run", "projection", "approved_candidate_ledger.jsonl"), "{\"candidate_id\":\"memcand-guard\",\"gate_decision_id\":\"memgate-guard\",\"consumption_id\":\"memconsume-guard\",\"review_approval_ref\":\"/tmp/review.json\",\"reason_codes\":[\"worker_claim_low_trust\"],\"evidence_refs\":[],\"consumption_status\":\"approved\"}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(runRoot, "warm-run", "instances", "demo__sequence", "projection_consumption_receipt.json"), "{\"consumed_candidate_ids\":[\"memcand-guard\"]}\n", System.Text.Encoding.UTF8);

        var generatorScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_update_proposals.py");
        var invalidOutput = Path.Combine(tempRoot.Path, "outside-proposals.jsonl");

        var failure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            generatorScript,
            "--phase5-run-root",
            runRoot,
            "--proposal-output",
            invalidOutput);

        Assert.Contains("--proposal-output must stay under", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void MemoryUpdateProposalGenerator_UsesValidatedAggregationAsSupplementarySufficiencyEvidence()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var runRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "memory-update-run-aggregation");
        var projectionRoot = Path.Combine(runRoot, "warm-run", "projection");
        var instanceRoot = Path.Combine(runRoot, "warm-run", "instances", "demo__sequence");
        Directory.CreateDirectory(projectionRoot);
        Directory.CreateDirectory(instanceRoot);

        var evidencePath = Path.Combine(tempRoot.Path, "artifacts", "bench", "swebench", "phase4-memory-update-aggregation", "evidence", "approved-evidence.json");
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        File.WriteAllText(evidencePath, "{\"status\":\"approved\"}\n", System.Text.Encoding.UTF8);

        var reviewRef = Path.Combine(tempRoot.Path, "artifacts", "bench", "swebench", "phase4-memory-update-aggregation", "reviews", "approved-review.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reviewRef)!);
        File.WriteAllText(reviewRef, "{\"status\":\"approved\"}\n", System.Text.Encoding.UTF8);

        var approvedLedgerPath = Path.Combine(projectionRoot, "approved_candidate_ledger.jsonl");
        File.WriteAllText(
            approvedLedgerPath,
            JsonSerializer.Serialize(new
            {
                candidate_id = "memcand-sequential",
                gate_decision_id = "memgate-sequential",
                consumption_id = "memconsume-sequential",
                review_approval_ref = reviewRef,
                reason_codes = new[] { "worker_claim_low_trust" },
                evidence_refs = new[] { evidencePath, reviewRef },
                consumption_status = "approved"
            }) + "\n",
            System.Text.Encoding.UTF8);

        var projectionPath = Path.Combine(projectionRoot, "warm_run_projection.json");
        File.WriteAllText(
            projectionPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "warm-run-projection.v1",
                projection_id = "warmproj-memory-update-aggregation",
                source_phase4_run_id = "phase4-seq-official-harness-minimal-001",
                source_candidates = new object[]
                {
                    new
                    {
                        candidate_id = "memcand-sequential",
                        gate_decision_id = "memgate-sequential",
                        consumption_id = "memconsume-sequential",
                        review_approval_ref = reviewRef,
                        consumption_status = "approved",
                        claim = "Approved sequential candidate claim.",
                        projected_as = "execution_pattern",
                        evidence_refs = new[] { evidencePath, reviewRef }
                    }
                },
                projection_scope = new
                {
                    benchmark = "swebench",
                    repo = "astropy/astropy",
                    sequence_id = "sequence-memory-update-aggregation"
                },
                truth_write_authorized = false,
                memory_truth_path = (string?)null,
                forbidden_roots = new[] { ".ai/memory/", ".ai/tasks/", ".ai/artifacts/reviews/", ".carves-platform/" }
            }),
            System.Text.Encoding.UTF8);

        var receiptPath = Path.Combine(instanceRoot, "projection_consumption_receipt.json");
        File.WriteAllText(
            receiptPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "warm-run-consumption-receipt.v1",
                run_id = "memory-update-run-aggregation",
                sequence_id = "sequence-memory-update-aggregation",
                instance_id = "demo__sequence",
                projection_id = "warmproj-memory-update-aggregation",
                consumed_candidate_ids = new[] { "memcand-sequential" },
                excluded_candidate_ids = Array.Empty<string>(),
                context_pack_refs = new[] { "context-pack://warm-run/demo__sequence" },
                estimated_projection_tokens = 12,
                forbidden_inputs_observed = false,
                truth_write_attempted = false,
                truth_write_paths = Array.Empty<string>()
            }),
            System.Text.Encoding.UTF8);

        var metricsPath = Path.Combine(runRoot, "memory_reuse_metrics.json");
        File.WriteAllText(
            metricsPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "memory-reuse-metrics.v1",
                run_id = "memory-update-run-aggregation",
                sequence_id = "sequence-memory-update-aggregation",
                projection_reuse = new
                {
                    warm_projection_available = true,
                    approved_candidate_count = 1,
                    consumed_candidate_count = 1,
                    excluded_candidate_count = 0,
                    projection_token_estimate = 12,
                    truth_root_no_write_pass_rate = 1.0,
                    warm_receipt_coverage_rate = 1.0
                },
                cold_warm_comparison = new
                {
                    same_repo_confirmed = true,
                    same_sequence_confirmed = true,
                    same_base_commit_confirmed = true,
                    same_provider_profile_confirmed = true,
                    same_budget_confirmed = true,
                    same_harness_policy_confirmed = true,
                    only_warm_projection_diff = true
                },
                harness_resolution = new
                {
                    status = "complete",
                    cold_resolved_rate = 0.0,
                    warm_resolved_rate = 0.0,
                    resolved_rate_uplift = 0.0
                },
                truth_root_audit = new
                {
                    proof_status = "passed"
                },
                claim_gates = new
                {
                    benchmark_uplift_claim = false,
                    uplift_claim_allowed = false,
                    claim_scope = "smoke_only",
                    reason = "sample_size_below_threshold",
                    sample_size = 1,
                    sample_size_threshold = 3
                }
            }),
            System.Text.Encoding.UTF8);

        var scorecardPath = Path.Combine(runRoot, "memory_maturity_scorecard.json");
        File.WriteAllText(scorecardPath, "{\"schema_version\":\"memory-maturity-scorecard.v1\"}\n", System.Text.Encoding.UTF8);

        var truthAuditPath = Path.Combine(runRoot, "truth_root_no_write_report.json");
        File.WriteAllText(
            truthAuditPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "truth-root-no-write-report.v1",
                proof_status = "passed"
            }),
            System.Text.Encoding.UTF8);

        var aggregationRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "aggregation-run-002");
        var aggregationDir = Path.Combine(aggregationRoot, "aggregation");
        Directory.CreateDirectory(aggregationDir);

        var aggregationPath = Path.Combine(aggregationDir, "evidence_aggregation.json");
        File.WriteAllText(
            aggregationPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "benchmark-evidence-aggregation.v1",
                aggregation_id = "benchagg-aggregation-run-002",
                aggregation_type = "multi_run_evidence_expansion",
                source_phase = "phase6",
                lineage = new
                {
                    candidate_family_id = "memcand-family-astropy",
                    projection_family_id = "warmproj-family-astropy",
                    memory_update_proposal_id = (string?)null
                },
                aggregation_scope = new
                {
                    benchmark = "swebench_lite",
                    repo_policy = "same_repo",
                    base_commit_policy = "per_cell_same_base_required",
                    sequence_policy = "not_a_single_sequence"
                },
                cells = new object[]
                {
                    new
                    {
                        cell_id = "cell-memory-update-run-aggregation",
                        run_root = runRoot,
                        instance_id = "astropy__astropy-12907",
                        repo = "astropy/astropy",
                        base_commit = "d16bfe05a744909de4b27f5875fe0d4ed41ce607",
                        cold_harness_status = "completed",
                        warm_harness_status = "completed",
                        truth_root_audit_status = "passed",
                        fairness_status = "passed",
                        cold_resolved = false,
                        warm_resolved = false,
                        resolved_rate_delta = 0.0,
                        claim_scope = "smoke_only",
                        evidence_refs = new[] { metricsPath, scorecardPath, truthAuditPath }
                    },
                    new
                    {
                        cell_id = "cell-memory-update-run-aggregation-002",
                        run_root = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "cell-run-002"),
                        instance_id = "astropy__astropy-14182",
                        repo = "astropy/astropy",
                        base_commit = "abc123",
                        cold_harness_status = "completed",
                        warm_harness_status = "completed",
                        truth_root_audit_status = "passed",
                        fairness_status = "passed",
                        cold_resolved = false,
                        warm_resolved = false,
                        resolved_rate_delta = 0.5,
                        claim_scope = "small_sample",
                        evidence_refs = new[] { metricsPath }
                    },
                    new
                    {
                        cell_id = "cell-memory-update-run-aggregation-003",
                        run_root = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "cell-run-003"),
                        instance_id = "astropy__astropy-14995",
                        repo = "astropy/astropy",
                        base_commit = "def456",
                        cold_harness_status = "completed",
                        warm_harness_status = "completed",
                        truth_root_audit_status = "passed",
                        fairness_status = "passed",
                        cold_resolved = false,
                        warm_resolved = true,
                        resolved_rate_delta = 0.5,
                        claim_scope = "small_sample",
                        evidence_refs = new[] { metricsPath }
                    }
                },
                aggregate_result = new
                {
                    cell_count = 3,
                    completed_cell_count = 3,
                    truth_root_audit_pass_count = 3,
                    fairness_pass_count = 3,
                    resolved_rate_delta_mean = 0.3333333333,
                    benchmark_uplift_claim_allowed = false,
                    memory_truth_writeback_allowed = false
                },
                limits = new
                {
                    not_a_leaderboard_claim = true,
                    not_memory_truth = true,
                    direct_truth_write_allowed = false
                },
                requires_human_review = true,
                truth_write_authorized = false,
                target_memory_path = (string?)null,
                benchmark_uplift_claim_authorized = false,
                forbidden_roots = new[] { ".ai/memory/", ".ai/tasks/", ".ai/artifacts/reviews/", ".carves-platform/" }
            }),
            System.Text.Encoding.UTF8);

        var validationPath = Path.Combine(aggregationDir, "evidence_aggregation_validation.json");
        File.WriteAllText(
            validationPath,
            JsonSerializer.Serialize(new
            {
                valid = true,
                all_evidence_refs_present = true,
                missing_evidence_refs = Array.Empty<string>(),
                included_cell_count = 3,
                excluded_cell_count = 0,
                not_a_single_sequence_confirmed = true,
                per_cell_same_base_policy_confirmed = true,
                forbidden_root_targets_detected = Array.Empty<string>()
            }),
            System.Text.Encoding.UTF8);

        var generatorScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "generate_memory_update_proposals.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            generatorScript,
            "--phase5-run-root",
            runRoot,
            "--aggregation-root",
            aggregationRoot);

        var proposalsPath = Path.Combine(runRoot, "proposals", "memory_update_proposals.jsonl");
        var sufficiencyPath = Path.Combine(runRoot, "evidence-sufficiency", "memory_update_evidence_sufficiency.jsonl");

        var proposalLines = File.ReadAllLines(proposalsPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Single(proposalLines);
        using var proposalDocument = JsonDocument.Parse(proposalLines[0]);
        Assert.Equal("create_governed_memory_update_task", proposalDocument.RootElement.GetProperty("allowed_next_action").GetString());
        Assert.Contains(
            aggregationPath,
            proposalDocument.RootElement.GetProperty("evidence_refs").EnumerateArray().Select(static item => item.GetString()).Where(static value => value is not null)!);
        Assert.Contains(
            validationPath,
            proposalDocument.RootElement.GetProperty("evidence_refs").EnumerateArray().Select(static item => item.GetString()).Where(static value => value is not null)!);

        var sufficiencyLines = File.ReadAllLines(sufficiencyPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Single(sufficiencyLines);
        using var sufficiencyDocument = JsonDocument.Parse(sufficiencyLines[0]);
        Assert.Equal("L2_measured_small_sample", sufficiencyDocument.RootElement.GetProperty("evidence_level").GetString());
        Assert.Equal("human_review", sufficiencyDocument.RootElement.GetProperty("next_action").GetString());
        Assert.True(sufficiencyDocument.RootElement.GetProperty("task_proposal_allowed").GetBoolean());
        Assert.Equal(3, sufficiencyDocument.RootElement.GetProperty("evidence_posture").GetProperty("sample_size").GetInt32());
        Assert.Equal(0.3333333333, sufficiencyDocument.RootElement.GetProperty("evidence_posture").GetProperty("resolved_rate_uplift").GetDouble(), 6);
        Assert.Contains(
            aggregationPath,
            sufficiencyDocument.RootElement.GetProperty("evidence_refs").EnumerateArray().Select(static item => item.GetString()).Where(static value => value is not null)!);
        Assert.Contains(
            validationPath,
            sufficiencyDocument.RootElement.GetProperty("evidence_refs").EnumerateArray().Select(static item => item.GetString()).Where(static value => value is not null)!);
    }

    [Fact]
    public void EvidenceAggregationBuilder_ProducesBoundedAggregationArtifacts()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);

        var includedRunRoot = Path.Combine(artifactRoot, "cell-run-001");
        Directory.CreateDirectory(Path.Combine(includedRunRoot, "cold-run"));
        Directory.CreateDirectory(Path.Combine(includedRunRoot, "warm-run"));
        File.WriteAllText(
            Path.Combine(includedRunRoot, "sequence_manifest.json"),
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-cell-001",
                dataset = "SWE-bench_Lite",
                repo = "astropy/astropy",
                instances = new object[]
                {
                    new
                    {
                        instance_id = "astropy__astropy-12907",
                        base_commit = "abc123"
                    }
                }
            }),
            System.Text.Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(includedRunRoot, "memory_reuse_metrics.json"),
            JsonSerializer.Serialize(new
            {
                harness_resolution = new
                {
                    cold_resolved_rate = 0.0,
                    warm_resolved_rate = 1.0,
                    resolved_rate_uplift = 1.0
                },
                claim_gates = new
                {
                    claim_scope = "smoke_only"
                },
                cold_warm_comparison = new
                {
                    same_repo_confirmed = true,
                    same_sequence_confirmed = true,
                    same_base_commit_confirmed = true,
                    same_provider_profile_confirmed = true,
                    same_budget_confirmed = true,
                    same_harness_policy_confirmed = true,
                    only_warm_projection_diff = true
                }
            }),
            System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(includedRunRoot, "memory_maturity_scorecard.json"), "{}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(includedRunRoot, "truth_root_no_write_report.json"), "{\"proof_status\":\"passed\"}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(includedRunRoot, "cold-run", "harness_result.json"), "{\"matrix_status\":\"completed\"}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(includedRunRoot, "warm-run", "harness_result.json"), "{\"matrix_status\":\"completed\"}\n", System.Text.Encoding.UTF8);

        var excludedRunRoot = Path.Combine(artifactRoot, "cell-run-002");
        Directory.CreateDirectory(Path.Combine(excludedRunRoot, "cold-run"));
        Directory.CreateDirectory(Path.Combine(excludedRunRoot, "warm-run"));
        File.WriteAllText(
            Path.Combine(excludedRunRoot, "sequence_manifest.json"),
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-cell-002",
                dataset = "SWE-bench_Lite",
                repo = "astropy/astropy",
                instances = new object[]
                {
                    new
                    {
                        instance_id = "astropy__astropy-14182",
                        base_commit = "def456"
                    }
                }
            }),
            System.Text.Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(excludedRunRoot, "memory_reuse_metrics.json"),
            JsonSerializer.Serialize(new
            {
                harness_resolution = new
                {
                    cold_resolved_rate = 0.0,
                    warm_resolved_rate = 0.0,
                    resolved_rate_uplift = 0.0
                },
                claim_gates = new
                {
                    claim_scope = "smoke_only"
                },
                cold_warm_comparison = new
                {
                    same_repo_confirmed = true,
                    same_sequence_confirmed = true,
                    same_base_commit_confirmed = false,
                    same_provider_profile_confirmed = true,
                    same_budget_confirmed = true,
                    same_harness_policy_confirmed = true,
                    only_warm_projection_diff = true
                }
            }),
            System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(excludedRunRoot, "memory_maturity_scorecard.json"), "{}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(excludedRunRoot, "truth_root_no_write_report.json"), "{\"proof_status\":\"passed\"}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(excludedRunRoot, "cold-run", "harness_result.json"), "{\"matrix_status\":\"completed\"}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(Path.Combine(excludedRunRoot, "warm-run", "harness_result.json"), "{\"matrix_status\":\"completed\"}\n", System.Text.Encoding.UTF8);

        var aggregationRoot = Path.Combine(artifactRoot, "aggregation-run-001");
        Directory.CreateDirectory(aggregationRoot);

        var buildScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_evidence_aggregation.py");
        var validateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "validate_evidence_aggregation.py");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            buildScript,
            "--aggregation-root",
            aggregationRoot,
            "--cell-run-root",
            includedRunRoot,
            "--cell-run-root",
            excludedRunRoot,
            "--candidate-family-id",
            "memcand-family-astropy",
            "--projection-family-id",
            "warmproj-family-astropy",
            "--memory-update-proposal-id",
            "memproposal-astropy-family");

        var validationOutput = RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            validateScript,
            "--aggregation-root",
            aggregationRoot);

        var aggregationDir = Path.Combine(aggregationRoot, "aggregation");
        var aggregationPath = Path.Combine(aggregationDir, "evidence_aggregation.json");
        var includedPath = Path.Combine(aggregationDir, "included_cells.jsonl");
        var excludedPath = Path.Combine(aggregationDir, "excluded_cells.jsonl");
        var validationPath = Path.Combine(aggregationDir, "evidence_aggregation_validation.json");

        Assert.True(File.Exists(aggregationPath));
        Assert.True(File.Exists(includedPath));
        Assert.True(File.Exists(excludedPath));
        Assert.True(File.Exists(validationPath));

        using var aggregationDocument = JsonDocument.Parse(File.ReadAllText(aggregationPath));
        Assert.Equal("benchmark-evidence-aggregation.v1", aggregationDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("multi_run_evidence_expansion", aggregationDocument.RootElement.GetProperty("aggregation_type").GetString());
        Assert.Equal(
            "per_cell_same_base_required",
            aggregationDocument.RootElement.GetProperty("aggregation_scope").GetProperty("base_commit_policy").GetString());
        Assert.Equal(
            "not_a_single_sequence",
            aggregationDocument.RootElement.GetProperty("aggregation_scope").GetProperty("sequence_policy").GetString());
        Assert.False(aggregationDocument.RootElement.GetProperty("truth_write_authorized").GetBoolean());
        Assert.False(aggregationDocument.RootElement.GetProperty("benchmark_uplift_claim_authorized").GetBoolean());
        Assert.Equal(JsonValueKind.Null, aggregationDocument.RootElement.GetProperty("target_memory_path").ValueKind);
        Assert.Equal(1, aggregationDocument.RootElement.GetProperty("cells").GetArrayLength());
        Assert.Equal(1, aggregationDocument.RootElement.GetProperty("aggregate_result").GetProperty("cell_count").GetInt32());
        Assert.Equal(1.0, aggregationDocument.RootElement.GetProperty("aggregate_result").GetProperty("resolved_rate_delta_mean").GetDouble());

        var includedLines = File.ReadAllLines(includedPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        var excludedLines = File.ReadAllLines(excludedPath).Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        Assert.Single(includedLines);
        Assert.Single(excludedLines);

        using var excludedDocument = JsonDocument.Parse(excludedLines[0]);
        Assert.Equal("fairness_not_passed", excludedDocument.RootElement.GetProperty("reason_code").GetString());

        using var validationDocument = JsonDocument.Parse(validationOutput);
        Assert.True(validationDocument.RootElement.GetProperty("valid").GetBoolean());
        Assert.True(validationDocument.RootElement.GetProperty("all_evidence_refs_present").GetBoolean());
        Assert.True(validationDocument.RootElement.GetProperty("not_a_single_sequence_confirmed").GetBoolean());
        Assert.True(validationDocument.RootElement.GetProperty("per_cell_same_base_policy_confirmed").GetBoolean());
        Assert.Equal(1, validationDocument.RootElement.GetProperty("included_cell_count").GetInt32());
        Assert.Equal(1, validationDocument.RootElement.GetProperty("excluded_cell_count").GetInt32());
        Assert.Empty(validationDocument.RootElement.GetProperty("forbidden_root_targets_detected").EnumerateArray());
    }

    [Fact]
    public void EvidenceAggregationBuilder_RejectsAggregationRootOutsideMemoryMaturityArtifacts()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var buildScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_evidence_aggregation.py");
        var invalidAggregationRoot = Path.Combine(tempRoot.Path, "outside-aggregation");
        var validCellRunRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity", "cell-run-guard");
        Directory.CreateDirectory(validCellRunRoot);

        var failure = RunProcessExpectFailureInDirectory(
            runtimeRepoRoot,
            python,
            buildScript,
            "--aggregation-root",
            invalidAggregationRoot,
            "--cell-run-root",
            validCellRunRoot,
            "--candidate-family-id",
            "memcand-family-guard",
            "--projection-family-id",
            "warmproj-family-guard");

        Assert.Contains("--aggregation-root must stay under artifacts/bench/memory-maturity/<run-id>", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialRunner_ProducesColdWarmArtifactsAndWarmConsumptionReceipts()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-001");

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-001",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 128 },
                harness_policy = new { mode = "not_run" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-001",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_fixture_first" }
                    },
                    new
                    {
                        instance_id = "demo__sequence-002",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_fixture_second" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-run-001",
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-run-001");
        var normalizedManifestPath = Path.Combine(runRoot, "sequence_manifest.json");
        var coldManifestPath = Path.Combine(runRoot, "cold-run", "manifest.json");
        var warmManifestPath = Path.Combine(runRoot, "warm-run", "manifest.json");
        var coldPredictionsPath = Path.Combine(runRoot, "cold-run", "predictions.jsonl");
        var warmPredictionsPath = Path.Combine(runRoot, "warm-run", "predictions.jsonl");
        var warmReceiptPath = Path.Combine(runRoot, "warm-run", "instances", "demo__sequence-001", "projection_consumption_receipt.json");
        var noWriteReportPath = Path.Combine(runRoot, "warm-run", "instances", "demo__sequence-001", "truth_root_no_write_report.json");
        var runLevelAuditPath = Path.Combine(runRoot, "truth_root_no_write_report.json");
        var beforeSnapshotPath = Path.Combine(runRoot, "truth-root-snapshot-before.json");
        var afterSnapshotPath = Path.Combine(runRoot, "truth-root-snapshot-after.json");
        var warmContextProjectionPath = Path.Combine(runRoot, "warm-run", "instances", "demo__sequence-001", "context_projection.json");
        var coldContextProjectionPath = Path.Combine(runRoot, "cold-run", "instances", "demo__sequence-001", "context_projection.json");

        Assert.True(File.Exists(normalizedManifestPath));
        Assert.True(File.Exists(coldManifestPath));
        Assert.True(File.Exists(warmManifestPath));
        Assert.True(File.Exists(coldPredictionsPath));
        Assert.True(File.Exists(warmPredictionsPath));
        Assert.True(File.Exists(warmReceiptPath));
        Assert.True(File.Exists(noWriteReportPath));
        Assert.True(File.Exists(runLevelAuditPath));
        Assert.True(File.Exists(beforeSnapshotPath));
        Assert.True(File.Exists(afterSnapshotPath));
        Assert.True(File.Exists(warmContextProjectionPath));
        Assert.True(File.Exists(coldContextProjectionPath));

        Assert.Equal(2, File.ReadAllLines(coldPredictionsPath).Count(line => !string.IsNullOrWhiteSpace(line)));
        Assert.Equal(2, File.ReadAllLines(warmPredictionsPath).Count(line => !string.IsNullOrWhiteSpace(line)));

        using var warmManifest = JsonDocument.Parse(File.ReadAllText(warmManifestPath));
        Assert.True(warmManifest.RootElement.GetProperty("reads_warm_run_projection").GetBoolean());

        using var warmContextProjection = JsonDocument.Parse(File.ReadAllText(warmContextProjectionPath));
        Assert.Contains(
            warmContextProjection.RootElement.GetProperty("included_sources").EnumerateArray().Select(item => item.GetProperty("source_kind").GetString()),
            item => string.Equals(item, "warm_run_projection", StringComparison.Ordinal));
        Assert.Contains(
            warmContextProjection.RootElement.GetProperty("selection_reasons").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, "approved_warm_run_projection", StringComparison.Ordinal));

        using var coldContextProjection = JsonDocument.Parse(File.ReadAllText(coldContextProjectionPath));
        Assert.DoesNotContain(
            coldContextProjection.RootElement.GetProperty("included_sources").EnumerateArray().Select(item => item.GetProperty("source_kind").GetString()),
            item => string.Equals(item, "warm_run_projection", StringComparison.Ordinal));

        using var receiptDocument = JsonDocument.Parse(File.ReadAllText(warmReceiptPath));
        Assert.False(receiptDocument.RootElement.GetProperty("truth_write_attempted").GetBoolean());
        Assert.NotEqual(0, receiptDocument.RootElement.GetProperty("consumed_candidate_ids").GetArrayLength());

        using var noWriteDocument = JsonDocument.Parse(File.ReadAllText(noWriteReportPath));
        Assert.Equal("passed", noWriteDocument.RootElement.GetProperty("proof_status").GetString());

        using var runLevelAuditDocument = JsonDocument.Parse(File.ReadAllText(runLevelAuditPath));
        Assert.Equal("passed", runLevelAuditDocument.RootElement.GetProperty("proof_status").GetString());
        Assert.Equal("isolated_workspace_root", runLevelAuditDocument.RootElement.GetProperty("audit_root_kind").GetString());
        Assert.Equal(0, runLevelAuditDocument.RootElement.GetProperty("changed_root_count").GetInt32());
        Assert.Equal(0, runLevelAuditDocument.RootElement.GetProperty("blocking_root_count").GetInt32());
        Assert.Equal(0, runLevelAuditDocument.RootElement.GetProperty("review_required_root_count").GetInt32());
        Assert.Equal(0, runLevelAuditDocument.RootElement.GetProperty("changed_path_count").GetInt32());
        Assert.Equal(0, runLevelAuditDocument.RootElement.GetProperty("platform_state_change_count").GetInt32());
    }

    [Fact]
    public void SequentialComparatorAndScorecard_BlockUpliftClaimsWithoutHarness()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-compare");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-compare-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-compare",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "not_run" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-compare",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_compare_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-compare-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-compare-run");
        var metricsPath = Path.Combine(runRoot, "memory_reuse_metrics.json");
        var scorecardPath = Path.Combine(runRoot, "memory_maturity_scorecard.json");
        var reportPath = Path.Combine(runRoot, "memory_maturity_report.md");

        var compareScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "compare_cold_warm_runs.py");
        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--run-root", runRoot, "--output", metricsPath);

        var scorecardScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_maturity_scorecard.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scorecardScript,
            "--metrics-json",
            metricsPath,
            "--output-json",
            scorecardPath,
            "--output-report",
            reportPath);

        Assert.True(File.Exists(metricsPath));
        Assert.True(File.Exists(scorecardPath));
        Assert.True(File.Exists(reportPath));

        using var metricsDocument = JsonDocument.Parse(File.ReadAllText(metricsPath));
        Assert.Equal("memory-reuse-metrics.v1", metricsDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.True(metricsDocument.RootElement.GetProperty("projection_reuse").GetProperty("warm_projection_available").GetBoolean());
        Assert.True(metricsDocument.RootElement.GetProperty("cold_warm_comparison").GetProperty("same_sequence_confirmed").GetBoolean());
        Assert.True(metricsDocument.RootElement.GetProperty("cold_warm_comparison").GetProperty("only_warm_projection_diff").GetBoolean());
        Assert.Equal("not_run", metricsDocument.RootElement.GetProperty("harness_resolution").GetProperty("status").GetString());
        Assert.False(metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("uplift_claim_allowed").GetBoolean());
        Assert.Equal("not_observed", metricsDocument.RootElement.GetProperty("harness_resolution").GetProperty("resolved_rate_uplift").GetString());
        Assert.Equal("passed", metricsDocument.RootElement.GetProperty("truth_root_audit").GetProperty("proof_status").GetString());

        using var scorecardDocument = JsonDocument.Parse(File.ReadAllText(scorecardPath));
        Assert.Equal("memory-maturity-scorecard.v1", scorecardDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.True(scorecardDocument.RootElement.GetProperty("bounded_projection_readiness").GetProperty("projection_bridge_ready").GetBoolean());
        Assert.True(scorecardDocument.RootElement.GetProperty("bounded_projection_readiness").GetProperty("sequential_fairness_validated").GetBoolean());
        Assert.Equal("not_observed", scorecardDocument.RootElement.GetProperty("measured_memory_reuse").GetProperty("observation_state").GetString());
        Assert.False(scorecardDocument.RootElement.GetProperty("memory_truth_boundary").GetProperty("truth_write_authorized").GetBoolean());

        var reportText = File.ReadAllText(reportPath);
        Assert.Contains("measured benchmark uplift remains blocked", reportText, StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialHarnessImport_RecordsSmallSampleEvidenceWithoutUnlockingUpliftClaim()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-harness");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-harness-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-harness",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "print" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-harness",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_harness_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-harness-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-harness-run");
        var normalizedHarnessRoot = Path.Combine(tempRoot.Path, "normalized-sequential-harness");
        Directory.CreateDirectory(normalizedHarnessRoot);
        WriteSequentialNormalizedHarnessResult(normalizedHarnessRoot, "cold-run", "demo__sequence-harness", "unresolved");
        WriteSequentialNormalizedHarnessResult(normalizedHarnessRoot, "warm-run", "demo__sequence-harness", "resolved");

        var evaluateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_sequential_runs.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateScript,
            "--run-root",
            runRoot,
            "--mode",
            "print",
            "--normalized-harness-root",
            normalizedHarnessRoot);

        var metricsPath = Path.Combine(runRoot, "memory_reuse_metrics.json");
        var scorecardPath = Path.Combine(runRoot, "memory_maturity_scorecard.json");
        var reportPath = Path.Combine(runRoot, "memory_maturity_report.md");
        var compareScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "compare_cold_warm_runs.py");
        var scorecardScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_maturity_scorecard.py");

        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--run-root", runRoot, "--output", metricsPath);
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scorecardScript,
            "--metrics-json",
            metricsPath,
            "--output-json",
            scorecardPath,
            "--output-report",
            reportPath);

        var coldHarnessResultPath = Path.Combine(runRoot, "cold-run", "harness_result.json");
        var warmHarnessResultPath = Path.Combine(runRoot, "warm-run", "harness_result.json");
        var warmResultPath = Path.Combine(runRoot, "warm-run", "instances", "demo__sequence-harness", "result.json");
        Assert.True(File.Exists(coldHarnessResultPath));
        Assert.True(File.Exists(warmHarnessResultPath));
        Assert.True(File.Exists(warmResultPath));

        using var metricsDocument = JsonDocument.Parse(File.ReadAllText(metricsPath));
        Assert.Equal("complete", metricsDocument.RootElement.GetProperty("harness_resolution").GetProperty("status").GetString());
        Assert.Equal(0.0, metricsDocument.RootElement.GetProperty("harness_resolution").GetProperty("cold_resolved_rate").GetDouble());
        Assert.Equal(1.0, metricsDocument.RootElement.GetProperty("harness_resolution").GetProperty("warm_resolved_rate").GetDouble());
        Assert.Equal(1.0, metricsDocument.RootElement.GetProperty("harness_resolution").GetProperty("resolved_rate_uplift").GetDouble());
        Assert.False(metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("benchmark_uplift_claim").GetBoolean());
        Assert.False(metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("uplift_claim_allowed").GetBoolean());
        Assert.Equal("smoke_only", metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("claim_scope").GetString());
        Assert.Equal(
            "sample_size_below_threshold",
            metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("reason").GetString());
        Assert.Equal("passed", metricsDocument.RootElement.GetProperty("truth_root_audit").GetProperty("proof_status").GetString());

        using var warmResultDocument = JsonDocument.Parse(File.ReadAllText(warmResultPath));
        Assert.Equal("official_harness", warmResultDocument.RootElement.GetProperty("harness_resolution").GetProperty("reported_by").GetString());
        Assert.Equal("resolved", warmResultDocument.RootElement.GetProperty("harness_resolution").GetProperty("status").GetString());

        using var scorecardDocument = JsonDocument.Parse(File.ReadAllText(scorecardPath));
        Assert.Equal("observed", scorecardDocument.RootElement.GetProperty("measured_memory_reuse").GetProperty("observation_state").GetString());
        Assert.False(scorecardDocument.RootElement.GetProperty("measured_memory_reuse").GetProperty("benchmark_uplift_claim").GetBoolean());
        Assert.False(scorecardDocument.RootElement.GetProperty("measured_memory_reuse").GetProperty("uplift_claim_allowed").GetBoolean());
        Assert.Equal("smoke_only", scorecardDocument.RootElement.GetProperty("measured_memory_reuse").GetProperty("claim_scope").GetString());

        var reportText = File.ReadAllText(reportPath);
        Assert.Contains("## Projection Reuse", reportText, StringComparison.Ordinal);
        Assert.Contains("## Adapter Correctness", reportText, StringComparison.Ordinal);
        Assert.Contains("## Official Resolved-Rate Evidence", reportText, StringComparison.Ordinal);
        Assert.Contains("## Claim Gate", reportText, StringComparison.Ordinal);
        Assert.Contains("## Truth-Root Audit", reportText, StringComparison.Ordinal);
        Assert.Contains("small-sample evidence only", reportText, StringComparison.Ordinal);
        Assert.Contains("sample_size_below_threshold", reportText, StringComparison.Ordinal);
        Assert.Contains("Machine-readable metrics remain the primary truth", reportText, StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialClaimGate_MarksInvalidWhenFairnessBreaks()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-invalid");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-invalid-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-invalid",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "print" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-invalid",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_invalid_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-invalid-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-invalid-run");
        var normalizedHarnessRoot = Path.Combine(tempRoot.Path, "normalized-sequential-invalid");
        Directory.CreateDirectory(normalizedHarnessRoot);
        WriteSequentialNormalizedHarnessResult(normalizedHarnessRoot, "cold-run", "demo__sequence-invalid", "unresolved");
        WriteSequentialNormalizedHarnessResult(normalizedHarnessRoot, "warm-run", "demo__sequence-invalid", "resolved");

        var evaluateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_sequential_runs.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateScript,
            "--run-root",
            runRoot,
            "--mode",
            "print",
            "--normalized-harness-root",
            normalizedHarnessRoot);

        var warmManifestPath = Path.Combine(runRoot, "warm-run", "manifest.json");
        using (var warmManifestDocument = JsonDocument.Parse(File.ReadAllText(warmManifestPath)))
        {
            var warmManifest = JsonSerializer.Deserialize<Dictionary<string, object?>>(warmManifestDocument.RootElement.GetRawText())!;
            warmManifest["provider_profile"] = "different-profile";
            File.WriteAllText(warmManifestPath, JsonSerializer.Serialize(warmManifest), System.Text.Encoding.UTF8);
        }

        var metricsPath = Path.Combine(runRoot, "memory_reuse_metrics.json");
        var scorecardPath = Path.Combine(runRoot, "memory_maturity_scorecard.json");
        var reportPath = Path.Combine(runRoot, "memory_maturity_report.md");
        var compareScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "compare_cold_warm_runs.py");
        var scorecardScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_memory_maturity_scorecard.py");

        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--run-root", runRoot, "--output", metricsPath);
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            scorecardScript,
            "--metrics-json",
            metricsPath,
            "--output-json",
            scorecardPath,
            "--output-report",
            reportPath);

        using var metricsDocument = JsonDocument.Parse(File.ReadAllText(metricsPath));
        Assert.False(metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("benchmark_uplift_claim").GetBoolean());
        Assert.False(metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("uplift_claim_allowed").GetBoolean());
        Assert.Equal("invalid", metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("claim_scope").GetString());
        Assert.Equal("fairness_gate_failed", metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("reason").GetString());
        Assert.Equal("passed", metricsDocument.RootElement.GetProperty("truth_root_audit").GetProperty("proof_status").GetString());

        using var scorecardDocument = JsonDocument.Parse(File.ReadAllText(scorecardPath));
        Assert.Equal("invalid", scorecardDocument.RootElement.GetProperty("measured_memory_reuse").GetProperty("claim_scope").GetString());

        var reportText = File.ReadAllText(reportPath);
        Assert.Contains("fairness_gate_failed", reportText, StringComparison.Ordinal);
        Assert.Contains("Benchmark uplift claims remain blocked", reportText, StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialHarnessReadiness_UsesExplicitHarnessInterpreter()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();
        using var fakeDockerApi = new FakeDockerApiServer(tempRoot.Path);

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-harness-ready");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);
        var harnessPython = WriteFakeHarnessPython(tempRoot.Path, "fake-harness-python");

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-harness-ready-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-harness-ready",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "run" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-harness-ready",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_harness_ready_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-harness-ready-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-harness-ready-run");
        var evaluateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_sequential_runs.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateScript,
            "--run-root",
            runRoot,
            "--mode",
            "run",
            "--harness-python",
            harnessPython,
            "--docker-host",
            fakeDockerApi.DockerHost);

        var readinessPath = Path.Combine(runRoot, "harness_readiness.json");
        var coldHarnessResultPath = Path.Combine(runRoot, "cold-run", "harness_result.json");
        Assert.True(File.Exists(readinessPath));
        Assert.True(File.Exists(coldHarnessResultPath));

        using var readinessDocument = JsonDocument.Parse(File.ReadAllText(readinessPath));
        Assert.Equal("ready", readinessDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("none", readinessDocument.RootElement.GetProperty("reason_code").GetString());
        Assert.Equal(harnessPython, readinessDocument.RootElement.GetProperty("harness_python").GetString());
        Assert.Equal("swebench.harness.run_evaluation", readinessDocument.RootElement.GetProperty("harness_module").GetString());
        Assert.Equal(fakeDockerApi.DockerHost, readinessDocument.RootElement.GetProperty("docker_host").GetString());
        Assert.Equal(fakeDockerApi.DockerHost, readinessDocument.RootElement.GetProperty("docker_api_endpoint").GetString());
        Assert.Contains(
            readinessDocument.RootElement.GetProperty("docker_api_candidates").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, fakeDockerApi.DockerHost, StringComparison.Ordinal));
        Assert.Equal("explicit_host", readinessDocument.RootElement.GetProperty("docker_probe_mode").GetString());
        Assert.Equal("available", readinessDocument.RootElement.GetProperty("pip_status").GetString());
        Assert.Equal("available", readinessDocument.RootElement.GetProperty("module_status").GetString());
        Assert.Equal("available", readinessDocument.RootElement.GetProperty("docker_api_status").GetString());

        using var harnessResultDocument = JsonDocument.Parse(File.ReadAllText(coldHarnessResultPath));
        Assert.Equal("partial", harnessResultDocument.RootElement.GetProperty("matrix_status").GetString());
        Assert.Equal("official_harness", harnessResultDocument.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public void SequentialHarnessReadiness_UsesEnvDockerHostWhenExplicitHostIsAbsent()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();
        using var fakeDockerApi = new FakeDockerApiServer(tempRoot.Path);

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-harness-env-host");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);
        var harnessPython = WriteFakeHarnessPython(tempRoot.Path, "fake-harness-env-python");

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-harness-env-host-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-harness-env-host",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "run" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-harness-env-host",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_harness_env_host_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-harness-env-host-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-harness-env-host-run");
        var evaluateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_sequential_runs.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            new Dictionary<string, string?> { ["DOCKER_HOST"] = fakeDockerApi.DockerHost },
            python,
            evaluateScript,
            "--run-root",
            runRoot,
            "--mode",
            "run",
            "--harness-python",
            harnessPython);

        var readinessPath = Path.Combine(runRoot, "harness_readiness.json");
        Assert.True(File.Exists(readinessPath));

        using var readinessDocument = JsonDocument.Parse(File.ReadAllText(readinessPath));
        Assert.Equal("ready", readinessDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("none", readinessDocument.RootElement.GetProperty("reason_code").GetString());
        Assert.Null(readinessDocument.RootElement.GetProperty("docker_host").GetString());
        Assert.Equal(fakeDockerApi.DockerHost, readinessDocument.RootElement.GetProperty("docker_api_endpoint").GetString());
        Assert.Equal("env_host", readinessDocument.RootElement.GetProperty("docker_probe_mode").GetString());
        Assert.Contains(
            readinessDocument.RootElement.GetProperty("docker_api_candidates").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, fakeDockerApi.DockerHost, StringComparison.Ordinal));
        Assert.Equal("available", readinessDocument.RootElement.GetProperty("docker_api_status").GetString());
    }

    [Fact]
    public void SequentialHarnessReadiness_ClassifiesBlockedHarnessInfra()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-harness-blocked");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-harness-blocked-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-harness-blocked",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "run" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-harness-blocked",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_harness_blocked_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-harness-blocked-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-harness-blocked-run");
        var evaluateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_sequential_runs.py");
        var missingHarnessPython = Path.Combine(tempRoot.Path, "missing-harness-python");
        var missingDockerHost = $"unix://{Path.Combine(tempRoot.Path, "missing-docker.sock")}";
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateScript,
            "--run-root",
            runRoot,
            "--mode",
            "run",
            "--harness-python",
            missingHarnessPython,
            "--docker-host",
            missingDockerHost);

        var metricsPath = Path.Combine(runRoot, "memory_reuse_metrics.json");
        var compareScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "compare_cold_warm_runs.py");
        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--run-root", runRoot, "--output", metricsPath);

        var readinessPath = Path.Combine(runRoot, "harness_readiness.json");
        var coldHarnessResultPath = Path.Combine(runRoot, "cold-run", "harness_result.json");
        Assert.True(File.Exists(readinessPath));
        Assert.True(File.Exists(coldHarnessResultPath));

        using var readinessDocument = JsonDocument.Parse(File.ReadAllText(readinessPath));
        Assert.Equal("blocked_harness_infra", readinessDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("harness_python_unavailable", readinessDocument.RootElement.GetProperty("reason_code").GetString());
        Assert.Equal("unavailable", readinessDocument.RootElement.GetProperty("pip_status").GetString());
        Assert.Equal("unavailable", readinessDocument.RootElement.GetProperty("module_status").GetString());
        Assert.Equal(missingDockerHost, readinessDocument.RootElement.GetProperty("docker_host").GetString());
        Assert.Equal(missingDockerHost, readinessDocument.RootElement.GetProperty("docker_api_endpoint").GetString());
        Assert.Equal("explicit_host", readinessDocument.RootElement.GetProperty("docker_probe_mode").GetString());
        Assert.Equal("unavailable", readinessDocument.RootElement.GetProperty("docker_api_status").GetString());

        using var harnessResultDocument = JsonDocument.Parse(File.ReadAllText(coldHarnessResultPath));
        Assert.Equal("failed", harnessResultDocument.RootElement.GetProperty("matrix_status").GetString());
        Assert.Equal("not_available", harnessResultDocument.RootElement.GetProperty("source").GetString());
        Assert.Contains("reason_code=harness_python_unavailable", harnessResultDocument.RootElement.GetProperty("details").GetString(), StringComparison.Ordinal);

        using var metricsDocument = JsonDocument.Parse(File.ReadAllText(metricsPath));
        Assert.Equal("blocked_harness_infra", metricsDocument.RootElement.GetProperty("harness_readiness").GetProperty("status").GetString());
        Assert.Equal("harness_python_unavailable", metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("reason").GetString());
        Assert.False(metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("benchmark_uplift_claim").GetBoolean());
        Assert.False(metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("uplift_claim_allowed").GetBoolean());
    }

    [Fact]
    public void SequentialHarnessReadiness_ClassifiesMissingDockerAsBlockedHarnessInfra()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-harness-missing-docker");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-harness-missing-docker-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-harness-missing-docker",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "run" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-harness-missing-docker",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_harness_missing_docker_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-harness-missing-docker-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-harness-missing-docker-run");
        var evaluateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_sequential_runs.py");
        var fakeHarnessPython = WriteFakeHarnessPython(tempRoot.Path, "fake-harness-ready.py");
        var missingDockerHost = $"unix://{Path.Combine(tempRoot.Path, "missing-docker.sock")}";
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateScript,
            "--run-root",
            runRoot,
            "--mode",
            "run",
            "--harness-python",
            fakeHarnessPython,
            "--docker-host",
            missingDockerHost);

        var readinessPath = Path.Combine(runRoot, "harness_readiness.json");
        var coldHarnessResultPath = Path.Combine(runRoot, "cold-run", "harness_result.json");
        Assert.True(File.Exists(readinessPath));
        Assert.True(File.Exists(coldHarnessResultPath));

        using var readinessDocument = JsonDocument.Parse(File.ReadAllText(readinessPath));
        Assert.Equal("blocked_harness_infra", readinessDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("docker_api_unavailable", readinessDocument.RootElement.GetProperty("reason_code").GetString());
        Assert.Equal("available", readinessDocument.RootElement.GetProperty("pip_status").GetString());
        Assert.Equal("available", readinessDocument.RootElement.GetProperty("module_status").GetString());
        Assert.Equal(missingDockerHost, readinessDocument.RootElement.GetProperty("docker_host").GetString());
        Assert.Equal(missingDockerHost, readinessDocument.RootElement.GetProperty("docker_api_endpoint").GetString());
        Assert.Equal("unavailable", readinessDocument.RootElement.GetProperty("docker_api_status").GetString());

        using var harnessResultDocument = JsonDocument.Parse(File.ReadAllText(coldHarnessResultPath));
        Assert.Equal("failed", harnessResultDocument.RootElement.GetProperty("matrix_status").GetString());
        Assert.Contains("reason_code=docker_api_unavailable", harnessResultDocument.RootElement.GetProperty("details").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialHarnessReadiness_ClassifiesUnsupportedDockerHost()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand) = CreateSequentialTargetRepoFixture(python, tempRoot.Path);
        var projectionRoot = PrepareApprovedWarmRunProjectionFixture(runtimeRepoRoot, python, tempRoot.Path, "sequence-harness-unsupported-docker");
        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(auditRoot);

        var sequenceManifestPath = Path.Combine(tempRoot.Path, "sequence-harness-unsupported-docker-manifest.json");
        File.WriteAllText(
            sequenceManifestPath,
            JsonSerializer.Serialize(new
            {
                schema_version = "sequential-memory-run-manifest.v1",
                sequence_id = "sequence-harness-unsupported-docker",
                dataset = "SWE-bench_Lite",
                repo = "demo/repo",
                repo_root = targetRepoRoot,
                model_name_or_path = "carves-sequential",
                provider_profile = "workspace_build_test",
                tool_budget = new { max_tool_calls = 4 },
                token_budget = new { max_context_tokens = 64000, estimated_used_tokens = 96 },
                harness_policy = new { mode = "run" },
                instances = new object[]
                {
                    new
                    {
                        instance_id = "demo__sequence-harness-unsupported-docker",
                        base_commit = baseCommit,
                        problem_statement_file = problemStatementPath,
                        pre_export_command = mutateCommand,
                        memory_reads = new[] { "memory/module-summary" },
                        codegraph_reads = new[] { "codegraph/bounded-impact" },
                        docs_reads = Array.Empty<string>(),
                        excluded_sources = new[] { "full_codegraph_dump" },
                        selection_reasons = new[] { "sequence_harness_unsupported_docker_fixture" }
                    }
                }
            }),
            System.Text.Encoding.UTF8);

        var artifactRoot = Path.Combine(tempRoot.Path, "artifacts", "bench", "memory-maturity");
        Directory.CreateDirectory(artifactRoot);
        var runnerScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "run_sequential_eval.py");
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            runnerScript,
            "--sequence-manifest",
            sequenceManifestPath,
            "--artifact-root",
            artifactRoot,
            "--run-id",
            "sequential-harness-unsupported-docker-run",
            "--audit-root",
            auditRoot,
            "--approved-projection-root",
            projectionRoot);

        var runRoot = Path.Combine(artifactRoot, "sequential-harness-unsupported-docker-run");
        var evaluateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "evaluate_sequential_runs.py");
        var fakeHarnessPython = WriteFakeHarnessPython(tempRoot.Path, "fake-harness-unsupported-docker.py");
        var unsupportedDockerHost = "http://127.0.0.1:2375";
        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            evaluateScript,
            "--run-root",
            runRoot,
            "--mode",
            "run",
            "--harness-python",
            fakeHarnessPython,
            "--docker-host",
            unsupportedDockerHost);

        var readinessPath = Path.Combine(runRoot, "harness_readiness.json");
        var metricsPath = Path.Combine(runRoot, "memory_reuse_metrics.json");
        var compareScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "compare_cold_warm_runs.py");
        RunProcessInDirectory(runtimeRepoRoot, python, compareScript, "--run-root", runRoot, "--output", metricsPath);

        using var readinessDocument = JsonDocument.Parse(File.ReadAllText(readinessPath));
        Assert.Equal("blocked_harness_infra", readinessDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("unsupported_docker_host", readinessDocument.RootElement.GetProperty("reason_code").GetString());
        Assert.Equal("unsupported_host", readinessDocument.RootElement.GetProperty("docker_api_status").GetString());
        Assert.Equal(unsupportedDockerHost, readinessDocument.RootElement.GetProperty("docker_api_endpoint").GetString());

        using var metricsDocument = JsonDocument.Parse(File.ReadAllText(metricsPath));
        Assert.Equal("unsupported_docker_host", metricsDocument.RootElement.GetProperty("claim_gates").GetProperty("reason").GetString());
    }

    [Fact]
    public void SequentialTruthRootAudit_BlocksWhenAiTruthRootsChange()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var auditRoot = Path.Combine(tempRoot.Path, "audit-root");
        Directory.CreateDirectory(Path.Combine(auditRoot, ".ai", "memory"));

        var auditScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "audit_sequential_truth_roots.py");
        var beforeSnapshotPath = Path.Combine(tempRoot.Path, "before-snapshot.json");
        var afterSnapshotPath = Path.Combine(tempRoot.Path, "after-snapshot.json");
        var reportPath = Path.Combine(tempRoot.Path, "truth-root-no-write-report.json");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            auditScript,
            "snapshot",
            "--audit-root",
            auditRoot,
            "--audit-root-kind",
            "explicit_custom_root",
            "--snapshot-output",
            beforeSnapshotPath);

        WriteFile(auditRoot, Path.Combine(".ai", "memory", "snapshot.md"), "mutated\n");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            auditScript,
            "compare",
            "--audit-root",
            auditRoot,
            "--audit-root-kind",
            "explicit_custom_root",
            "--before-snapshot",
            beforeSnapshotPath,
            "--after-snapshot-output",
            afterSnapshotPath,
            "--report-output",
            reportPath);

        Assert.True(File.Exists(afterSnapshotPath));
        Assert.True(File.Exists(reportPath));

        using var reportDocument = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal("blocked", reportDocument.RootElement.GetProperty("proof_status").GetString());
        Assert.Equal("blocked", reportDocument.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("explicit_custom_root", reportDocument.RootElement.GetProperty("audit_root_kind").GetString());
        Assert.Equal(1, reportDocument.RootElement.GetProperty("changed_root_count").GetInt32());
        Assert.Equal(1, reportDocument.RootElement.GetProperty("blocking_root_count").GetInt32());
        Assert.Equal(0, reportDocument.RootElement.GetProperty("review_required_root_count").GetInt32());
        Assert.Equal(1, reportDocument.RootElement.GetProperty("changed_path_count").GetInt32());
        Assert.Equal(0, reportDocument.RootElement.GetProperty("platform_state_change_count").GetInt32());
        Assert.Contains(
            reportDocument.RootElement.GetProperty("changed_roots").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".ai/memory/", StringComparison.Ordinal));
        Assert.Contains(
            reportDocument.RootElement.GetProperty("blocked_roots").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".ai/memory/", StringComparison.Ordinal));
        Assert.Contains(
            reportDocument.RootElement.GetProperty("added_paths").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".ai/memory/snapshot.md", StringComparison.Ordinal));
    }

    [Fact]
    public void SequentialTruthRootAudit_RecordsPlatformStateLedgerForHostRuntimeRoot()
    {
        var runtimeRepoRoot = FindRuntimeRepoRoot();
        var python = ResolvePythonInterpreter();
        using var tempRoot = new TempDirectory();

        var auditRoot = Path.Combine(tempRoot.Path, "host-audit-root");
        Directory.CreateDirectory(Path.Combine(auditRoot, ".carves-platform", "runtime-state"));

        var auditScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "audit_sequential_truth_roots.py");
        var beforeSnapshotPath = Path.Combine(tempRoot.Path, "before-snapshot.json");
        var afterSnapshotPath = Path.Combine(tempRoot.Path, "after-snapshot.json");
        var reportPath = Path.Combine(tempRoot.Path, "truth-root-no-write-report.json");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            auditScript,
            "snapshot",
            "--audit-root",
            auditRoot,
            "--audit-root-kind",
            "host_runtime_root",
            "--snapshot-output",
            beforeSnapshotPath);

        WriteFile(auditRoot, Path.Combine(".carves-platform", "runtime-state", "state.json"), "{ \"tick\": 1 }\n");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            auditScript,
            "compare",
            "--audit-root",
            auditRoot,
            "--audit-root-kind",
            "host_runtime_root",
            "--before-snapshot",
            beforeSnapshotPath,
            "--after-snapshot-output",
            afterSnapshotPath,
            "--report-output",
            reportPath);

        using var reportDocument = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal("review_required", reportDocument.RootElement.GetProperty("proof_status").GetString());
        Assert.Equal("review_required", reportDocument.RootElement.GetProperty("verdict").GetString());
        Assert.Equal("host_runtime_root", reportDocument.RootElement.GetProperty("audit_root_kind").GetString());
        Assert.Equal(0, reportDocument.RootElement.GetProperty("blocking_root_count").GetInt32());
        Assert.Equal(1, reportDocument.RootElement.GetProperty("review_required_root_count").GetInt32());
        Assert.Equal(1, reportDocument.RootElement.GetProperty("platform_state_change_count").GetInt32());
        Assert.Contains(
            reportDocument.RootElement.GetProperty("review_required_roots").EnumerateArray().Select(item => item.GetString()),
            item => string.Equals(item, ".carves-platform/", StringComparison.Ordinal));
        Assert.Contains(
            reportDocument.RootElement.GetProperty("platform_state_change_ledger").EnumerateArray().Select(item => item.GetProperty("path").GetString()),
            item => string.Equals(item, ".carves-platform/runtime-state/state.json", StringComparison.Ordinal));
    }

    private static string FindRuntimeRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(current.FullName, "CARVES.Runtime.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate runtime repository root.");
    }

    private static string ResolvePythonInterpreter()
    {
        foreach (var candidate in new[] { "python3", "python" })
        {
            try
            {
                RunProcess(candidate, "--version");
                return candidate;
            }
            catch
            {
                // try next candidate
            }
        }

        throw new InvalidOperationException("Python interpreter not found.");
    }

    private static string RunProcess(string fileName, params string[] args)
        => RunProcess(fileName, args, null);

    private static string RunProcessInDirectory(string workingDirectory, string fileName, params string[] args)
        => RunProcess(fileName, args, workingDirectory);

    private static string RunProcessInDirectory(
        string workingDirectory,
        IReadOnlyDictionary<string, string?> environmentVariables,
        string fileName,
        params string[] args)
        => RunProcess(fileName, args, workingDirectory, environmentVariables);

    private static string RunProcessExpectFailureInDirectory(string workingDirectory, string fileName, params string[] args)
    {
        try
        {
            RunProcess(fileName, args, workingDirectory);
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }

        throw new InvalidOperationException($"Command unexpectedly succeeded: {fileName} {string.Join(' ', args)}");
    }

    private static string RunProcess(string fileName, string[] args, string? workingDirectory, IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                if (pair.Value is null)
                {
                    process.StartInfo.Environment.Remove(pair.Key);
                }
                else
                {
                    process.StartInfo.Environment[pair.Key] = pair.Value;
                }
            }
        }

        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Command failed: {fileName} {string.Join(' ', args)}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        return stdout;
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, System.Text.Encoding.UTF8);
    }

    private static (string TargetRepoRoot, string BaseCommit, string ProblemStatementPath, string MutateCommand) CreateSequentialTargetRepoFixture(string python, string tempRootPath)
    {
        var targetRepoRoot = Path.Combine(tempRootPath, "target-repo");
        Directory.CreateDirectory(targetRepoRoot);

        WriteFile(targetRepoRoot, "app.py", "print('before')\n");
        RunProcessInDirectory(targetRepoRoot, "git", "init");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.email", "swebench@example.com");
        RunProcessInDirectory(targetRepoRoot, "git", "config", "user.name", "Sequential Smoke");
        RunProcessInDirectory(targetRepoRoot, "git", "add", ".");
        RunProcessInDirectory(targetRepoRoot, "git", "commit", "-m", "base");
        var baseCommit = RunProcessInDirectory(targetRepoRoot, "git", "rev-parse", "HEAD").Trim();

        var problemStatementPath = Path.Combine(tempRootPath, "problem.txt");
        File.WriteAllText(problemStatementPath, "Fix app.py output.", System.Text.Encoding.UTF8);
        var mutateCommand = $"{python} -c \"from pathlib import Path; Path('app.py').write_text(\\\"print('after')\\\\n\\\", encoding='utf-8')\"";
        return (targetRepoRoot, baseCommit, problemStatementPath, mutateCommand);
    }

    private static string PrepareApprovedWarmRunProjectionFixture(string runtimeRepoRoot, string python, string tempRootPath, string sequenceId)
    {
        var phase4RunRoot = Path.Combine(tempRootPath, "artifacts", "bench", "swebench", $"phase4-{sequenceId}");
        var phase4MemoryRoot = Path.Combine(phase4RunRoot, "memory");
        Directory.CreateDirectory(phase4MemoryRoot);

        var evidenceRoot = Path.Combine(phase4RunRoot, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        var reviewRoot = Path.Combine(phase4RunRoot, "reviews");
        Directory.CreateDirectory(reviewRoot);

        var evidencePath = Path.Combine(evidenceRoot, "approved-evidence.json");
        var reviewRef = Path.Combine(reviewRoot, "approved-review.json");
        File.WriteAllText(evidencePath, "{\"ok\":true}\n", System.Text.Encoding.UTF8);
        File.WriteAllText(reviewRef, "{\"approved\":true}\n", System.Text.Encoding.UTF8);

        File.WriteAllLines(
            Path.Combine(phase4MemoryRoot, "memory_candidates.jsonl"),
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-candidate.v1",
                    candidate_id = "memcand-sequential",
                    source = new { benchmark = "swebench", run_id = $"phase4-{sequenceId}", variant_id = "worker-baseline", instance_id = "demo__sequence" },
                    candidate_type = "execution_pattern",
                    claim = "Approved sequential candidate claim.",
                    evidence_refs = new[] { evidencePath },
                    evidence_presence_required = true,
                    proposed_memory_scope = "execution",
                    target_memory_path = (string?)null,
                    direct_truth_write_allowed = false,
                    requires_human_review = true,
                    confidence = "medium",
                    risk = "low",
                    reason_codes = new[] { "worker_claim_low_trust" }
                })
            },
            System.Text.Encoding.UTF8);

        File.WriteAllLines(
            Path.Combine(phase4MemoryRoot, "memory_gate_decisions.jsonl"),
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    schema_version = "benchmark-memory-gate-decision.v1",
                    decision_id = "memgate-sequential",
                    candidate_id = "memcand-sequential",
                    decision = "promote",
                    reason_codes = new[] { "worker_claim_low_trust" },
                    evidence_refs = new[] { evidencePath },
                    requires_human_review = true,
                    allowed_next_action = "create_memory_update_task",
                    truth_write_authorized = false,
                    target_memory_path = (string?)null
                })
            },
            System.Text.Encoding.UTF8);

        var reviewApprovalsPath = Path.Combine(phase4MemoryRoot, "review_approvals.jsonl");
        File.WriteAllLines(
            reviewApprovalsPath,
            new[]
            {
                JsonSerializer.Serialize(new
                {
                    candidate_id = "memcand-sequential",
                    gate_decision_id = "memgate-sequential",
                    review_approval_status = "approved",
                    review_approval_ref = reviewRef
                })
            },
            System.Text.Encoding.UTF8);

        var warmRunRoot = Path.Combine(tempRootPath, "artifacts", "bench", "memory-maturity", $"memory-{sequenceId}");
        var buildScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "build_warm_run_projection.py");
        var validateScript = Path.Combine(runtimeRepoRoot, "tools", "benchmarks", "swebench", "validate_warm_run_projection.py");

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            buildScript,
            "--phase4-run-root",
            phase4RunRoot,
            "--warm-run-root",
            warmRunRoot,
            "--repo",
            "demo/repo",
            "--sequence-id",
            sequenceId,
            "--review-approvals-jsonl",
            reviewApprovalsPath);

        RunProcessInDirectory(
            runtimeRepoRoot,
            python,
            validateScript,
            "--warm-run-root",
            warmRunRoot);

        return Path.Combine(warmRunRoot, "warm-run", "projection");
    }

    private static void WriteSequentialNormalizedHarnessResult(string normalizedRoot, string lane, string instanceId, string status)
    {
        File.WriteAllText(
            Path.Combine(normalizedRoot, $"{lane}.json"),
            JsonSerializer.Serialize(new
            {
                matrix_status = "completed",
                per_instance = new object[]
                {
                    new
                    {
                        instance_id = instanceId,
                        status,
                        raw_ref = $"{lane}/raw",
                        details = (string?)null
                    }
                }
            }),
            System.Text.Encoding.UTF8);
    }

    private static string WriteFakeHarnessPython(string tempRootPath, string fileName)
    {
        var scriptPath = Path.Combine(tempRootPath, fileName);
        File.WriteAllText(
            scriptPath,
            "#!/usr/bin/env sh\n" +
            "if [ \"$1\" = \"-m\" ] && [ \"$3\" = \"--help\" ]; then\n" +
            "  echo 'fake harness help'\n" +
            "  exit 0\n" +
            "fi\n" +
            "if [ \"$1\" = \"-m\" ] && [ \"$2\" = \"pip\" ] && [ \"$3\" = \"--version\" ]; then\n" +
            "  echo 'pip 99.0'\n" +
            "  exit 0\n" +
            "fi\n" +
            "exit 0\n",
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        RunProcess("chmod", "+x", scriptPath);
        return scriptPath;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "carves-swebench", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class FakeDockerApiServer : IDisposable
    {
        private readonly Socket listener;
        private readonly Thread thread;
        private volatile bool disposed;

        public FakeDockerApiServer(string tempRootPath)
        {
            SocketPath = Path.Combine(tempRootPath, $"fake-docker-{Guid.NewGuid():N}.sock");
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            listener.Listen(4);
            thread = new Thread(ServeLoop)
            {
                IsBackground = true
            };
            thread.Start();
        }

        public string SocketPath { get; }

        public string DockerHost => $"unix://{SocketPath}";

        public void Dispose()
        {
            disposed = true;
            try
            {
                listener.Dispose();
            }
            catch
            {
                // ignored
            }

            thread.Join(millisecondsTimeout: 1000);
            if (File.Exists(SocketPath))
            {
                File.Delete(SocketPath);
            }
        }

        private void ServeLoop()
        {
            while (!disposed)
            {
                try
                {
                    using var client = listener.Accept();
                    client.ReceiveTimeout = 1000;
                    var buffer = new byte[1024];
                    try
                    {
                        _ = client.Receive(buffer);
                    }
                    catch
                    {
                        // ignored
                    }

                    var body = "OK";
                    var response =
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: text/plain\r\n" +
                        $"Content-Length: {Encoding.ASCII.GetByteCount(body)}\r\n" +
                        "Connection: close\r\n\r\n" +
                        body;
                    client.Send(Encoding.ASCII.GetBytes(response));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (disposed)
                    {
                        break;
                    }
                }
            }
        }
    }
}
