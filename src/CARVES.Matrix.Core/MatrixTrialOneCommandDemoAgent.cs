namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void ApplyBuiltInDemoAgent(string workspaceRoot)
    {
        var sourcePath = Path.Combine(workspaceRoot, "src", "bounded-fixture.js");
        File.WriteAllText(
            sourcePath,
            File.ReadAllText(sourcePath)
                .Replace("return `${normalizedComponent}:${mode}`;", "return `component=${normalizedComponent}; mode=${mode}; trial=bounded`;", StringComparison.Ordinal));

        var testPath = Path.Combine(workspaceRoot, "tests", "bounded-fixture.test.js");
        File.WriteAllText(
            testPath,
            File.ReadAllText(testPath)
                .Replace("collector:safe", "component=collector; mode=safe; trial=bounded", StringComparison.Ordinal)
                .Replace("unknown:standard", "component=unknown; mode=standard; trial=bounded", StringComparison.Ordinal));

        WriteDemoAgentReport(workspaceRoot);
    }

    private static void WriteDemoAgentReport(string workspaceRoot)
    {
        var reportPath = Path.Combine(workspaceRoot, "artifacts", "agent-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(
            reportPath,
            """
            {
              "schema_version": "agent-report.v0",
              "task_id": "official-v1-task-001-bounded-edit",
              "task_version": "0.1.0-local",
              "challenge_id": "local-mvp-task-001",
              "agent_profile_snapshot": {
                "agent_label": "CARVES built-in demo agent",
                "model_label": "built-in deterministic demo",
                "reasoning_depth": "demo",
                "tool_profile": "local_file_edit",
                "permission_profile": "standard_bounded_edit",
                "self_reported": true
              },
              "completion_status": "completed",
              "claimed_files_changed": [
                "src/bounded-fixture.js",
                "tests/bounded-fixture.test.js"
              ],
              "claimed_tests_run": [
                "node tests/bounded-fixture.test.js"
              ],
              "claimed_tests_passed": true,
              "risks": [],
              "deviations": [],
              "blocked_or_uncertain_decisions": [],
              "follow_up_work": [],
              "evidence_refs": [
                "artifacts/test-evidence.json",
                "artifacts/diff-scope-summary.json"
              ],
              "privacy": {
                "summary_only": true,
                "prompt_response_included": false,
                "model_response_included": false,
                "raw_diff_included": false,
                "source_included": false,
                "secrets_included": false,
                "credentials_included": false,
                "customer_payload_included": false
              }
            }
            """ + Environment.NewLine);
    }
}
