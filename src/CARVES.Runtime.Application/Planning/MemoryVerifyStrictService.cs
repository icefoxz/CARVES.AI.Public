using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Planning;

public sealed record MemoryVerifyCheckResult(
    string CheckId,
    string Severity,
    string Status,
    string Summary);

public sealed record MemoryVerifyMetrics(
    int FailurePolicyCount,
    int SourceTrustRuleCount,
    int RecoveryCaseCount,
    int DriftCaseCount,
    int CriticalFailureCount,
    int WarningCount);

public sealed record MemoryVerifyResult(
    string SchemaVersion,
    string PolicyVersion,
    string Mode,
    string VerifyCommand,
    bool Passed,
    int ExitCode,
    IReadOnlyList<MemoryVerifyCheckResult> Checks,
    MemoryVerifyMetrics Metrics);

public sealed class MemoryVerifyStrictService
{
    private readonly string repoRoot;

    public MemoryVerifyStrictService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public IReadOnlyList<string> GetRequiredArtifacts()
    {
        return
        [
            "docs/runtime/memory-gate/ACTIVE_MEMORY_READ_CONTRACT_V1.md",
            "docs/runtime/memory-gate/COMMAND_FAILURE_MODE_MATRIX_V1.json",
            "docs/runtime/memory-gate/MEMORY_FAILURE_MODE_POLICY_V1.json",
            "docs/runtime/memory-gate/MEMORY_SOURCE_TRUST_CLASSIFICATION_V1.json",
            "docs/runtime/memory-gate/MEMORY_POISONING_BOUNDARIES_V1.json",
            "docs/runtime/memory-gate/MEMORY_RECOVERY_PROTOCOL_V1.json",
            "docs/runtime/memory-gate/MEMORY_RECOVERY_DRILLS_V1.json",
            "docs/runtime/memory-gate/MEMORY_DRIFT_SEVERITY_V1.json",
            "docs/runtime/memory-gate/MEMORY_M0_RELEASE_GATE_V1.json",
        ];
    }

    public IReadOnlyList<string> GetCriticalChecks()
    {
        return
        [
            "active_read_contract_present",
            "failure_policy_artifacts_present",
            "source_trust_artifacts_present",
            "recovery_artifacts_present",
            "drift_policy_artifact_present",
            "verify_command_matrix_entry",
            "critical_drift_blockers_present",
            "release_gate_artifact_present",
        ];
    }

    public MemoryVerifyResult RunStrict()
    {
        var checks = new List<MemoryVerifyCheckResult>();
        var failurePolicyService = new MemoryFailureModePolicyService();
        var sourceTrustService = new MemorySourceTrustPolicyService();
        var recoveryService = new MemoryRecoveryProtocolService();
        var driftService = new MemoryDriftSeverityPolicyService();

        AddFileCheck(checks, "active_read_contract_present", "critical", "docs/runtime/memory-gate/ACTIVE_MEMORY_READ_CONTRACT_V1.md");
        AddFileCheck(checks, "failure_policy_artifacts_present", "critical", "docs/runtime/memory-gate/MEMORY_FAILURE_MODE_POLICY_V1.json", "docs/runtime/memory-gate/COMMAND_FAILURE_MODE_MATRIX_V1.json");
        AddFileCheck(checks, "source_trust_artifacts_present", "critical", "docs/runtime/memory-gate/MEMORY_SOURCE_TRUST_CLASSIFICATION_V1.json", "docs/runtime/memory-gate/MEMORY_POISONING_BOUNDARIES_V1.json");
        AddFileCheck(checks, "recovery_artifacts_present", "critical", "docs/runtime/memory-gate/MEMORY_RECOVERY_PROTOCOL_V1.json", "docs/runtime/memory-gate/MEMORY_RECOVERY_DRILLS_V1.json");
        AddFileCheck(checks, "drift_policy_artifact_present", "critical", "docs/runtime/memory-gate/MEMORY_DRIFT_SEVERITY_V1.json");
        AddFileCheck(checks, "release_gate_artifact_present", "critical", "docs/runtime/memory-gate/MEMORY_M0_RELEASE_GATE_V1.json");

        var verifyCommand = failurePolicyService.GetCommandMode("carves memory verify --strict");
        checks.Add(new MemoryVerifyCheckResult(
            "verify_command_matrix_entry",
            "critical",
            verifyCommand.DefaultMode == MemoryFailureMode.Strict
            && verifyCommand.ExitCode == 1
            && string.Equals(verifyCommand.JsonErrorShape, "memory_verify_result.v1", StringComparison.Ordinal)
            && string.Equals(verifyCommand.TelemetryEvent, "memory.drift.blocked", StringComparison.Ordinal)
                ? "pass"
                : "fail",
            "Verify command matrix must expose strict mode, nonzero failure exit, memory_verify_result.v1 JSON, and memory.drift.blocked telemetry."));

        var criticalDriftPass =
            driftService.GetEntry("canonical_fact_without_evidence").BlocksRelease
            && driftService.GetEntry("invalidated_fact_retrieved").BlocksRelease
            && driftService.GetEntry("dependency_contradiction").BlocksHighRiskTask
            && driftService.GetEntry("stale_codegraph_high_risk_task").BlocksHighRiskTask;
        checks.Add(new MemoryVerifyCheckResult(
            "critical_drift_blockers_present",
            "critical",
            criticalDriftPass ? "pass" : "fail",
            "Critical and high-risk drift entries must remain blocker-grade for release or high-risk execution."));

        var releaseGate = TryLoadReleaseGateArtifact();
        checks.Add(new MemoryVerifyCheckResult(
            "release_gate_command_matches_runtime",
            "warning",
            releaseGate is { VerifyCommand: "carves memory verify --strict --json" } ? "pass" : "fail",
            "Release gate artifact should point at carves memory verify --strict --json."));

        var criticalFailureCount = checks.Count(check => string.Equals(check.Severity, "critical", StringComparison.Ordinal) && string.Equals(check.Status, "fail", StringComparison.Ordinal));
        var warningCount = checks.Count(check => string.Equals(check.Status, "fail", StringComparison.Ordinal)) - criticalFailureCount;

        return new MemoryVerifyResult(
            SchemaVersion: "memory_verify_result.v1",
            PolicyVersion: "memory-gate.v1",
            Mode: "strict",
            VerifyCommand: "carves memory verify --strict --json",
            Passed: criticalFailureCount == 0,
            ExitCode: criticalFailureCount == 0 ? 0 : 1,
            Checks: checks,
            Metrics: new MemoryVerifyMetrics(
                FailurePolicyCount: failurePolicyService.GetFailurePolicies().Count,
                SourceTrustRuleCount: sourceTrustService.GetRules().Count,
                RecoveryCaseCount: recoveryService.GetRecoveryCases().Count,
                DriftCaseCount: driftService.GetEntries().Count,
                CriticalFailureCount: criticalFailureCount,
                WarningCount: Math.Max(0, warningCount)));
    }

    private void AddFileCheck(List<MemoryVerifyCheckResult> checks, string checkId, string severity, params string[] relativePaths)
    {
        var missing = relativePaths.Where(path => !File.Exists(Path.Combine(repoRoot, path))).ToArray();
        checks.Add(new MemoryVerifyCheckResult(
            checkId,
            severity,
            missing.Length == 0 ? "pass" : "fail",
            missing.Length == 0
                ? $"Required artifacts present: {string.Join(", ", relativePaths)}."
                : $"Missing required artifacts: {string.Join(", ", missing)}."));
    }

    private MemoryReleaseGateArtifact? TryLoadReleaseGateArtifact()
    {
        var path = Path.Combine(repoRoot, "docs", "runtime", "memory-gate", "MEMORY_M0_RELEASE_GATE_V1.json");
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return new MemoryReleaseGateArtifact(
            document.RootElement.GetProperty("schema_version").GetString() ?? string.Empty,
            document.RootElement.GetProperty("policy_version").GetString() ?? string.Empty,
            document.RootElement.GetProperty("verify_command").GetString() ?? string.Empty,
            document.RootElement.GetProperty("required_artifacts").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray(),
            document.RootElement.GetProperty("critical_checks").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
    }

    private sealed record MemoryReleaseGateArtifact(
        string SchemaVersion,
        string PolicyVersion,
        string VerifyCommand,
        IReadOnlyList<string> RequiredArtifacts,
        IReadOnlyList<string> CriticalChecks);
}
