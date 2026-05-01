using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool VerifySummaryTrustChainHardening(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root,
        MatrixArtifactManifestVerificationResult manifestVerification,
        MatrixVerifyRequiredArtifacts requiredArtifacts,
        MatrixVerifyTrialArtifacts trialArtifacts,
        MatrixVerifyShieldEvaluation shieldEvaluation,
        IReadOnlyList<MatrixVerifyIssue> nonSummaryIssues)
    {
        var expected = BuildVerifyTrustChainHardening(
            manifestVerification,
            requiredArtifacts,
            trialArtifacts,
            shieldEvaluation,
            new MatrixVerifyProofSummary(summaryRelativePath, Present: true, Consistent: true),
            nonSummaryIssues);

        var consistent = true;
        consistent &= VerifySummaryField(
            issues,
            summaryRelativePath,
            "trust_chain_hardening.gates_satisfied",
            FormatBool(expected.GatesSatisfied),
            FormatBool(GetBool(root, "trust_chain_hardening", "gates_satisfied")));
        consistent &= VerifySummaryField(
            issues,
            summaryRelativePath,
            "trust_chain_hardening.computed_by",
            expected.ComputedBy,
            GetString(root, "trust_chain_hardening", "computed_by"));

        if (!TryGet(root, out var gatesElement, "trust_chain_hardening", "gates")
            || gatesElement.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new MatrixVerifyIssue("summary", "matrix_proof_summary", summaryRelativePath, "summary_trust_chain_hardening_gates_missing", "trust_chain_hardening.gates", null));
            return false;
        }

        var actualGates = gatesElement.EnumerateArray().ToArray();
        consistent &= VerifySummaryField(
            issues,
            summaryRelativePath,
            "trust_chain_hardening.gate_count",
            expected.Gates.Count.ToString(),
            actualGates.Length.ToString());

        foreach (var expectedGate in expected.Gates)
        {
            var actualGate = FindGate(actualGates, expectedGate.GateId);
            if (!actualGate.HasValue)
            {
                issues.Add(new MatrixVerifyIssue(
                    "summary",
                    "matrix_proof_summary",
                    summaryRelativePath,
                    $"summary_trust_chain_hardening_gate_missing:{expectedGate.GateId}",
                    expectedGate.GateId,
                    null));
                consistent = false;
                continue;
            }

            consistent &= VerifySummaryField(issues, summaryRelativePath, $"trust_chain_hardening.gates.{expectedGate.GateId}.satisfied", FormatBool(expectedGate.Satisfied), FormatBool(GetBool(actualGate.Value, "satisfied")));
            consistent &= VerifySummaryField(issues, summaryRelativePath, $"trust_chain_hardening.gates.{expectedGate.GateId}.reason", expectedGate.Reason, GetString(actualGate.Value, "reason"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, $"trust_chain_hardening.gates.{expectedGate.GateId}.issue_codes", FormatStringArray(expectedGate.IssueCodes), FormatStringArray(GetStringArray(actualGate.Value, "issue_codes")));
            consistent &= VerifySummaryField(issues, summaryRelativePath, $"trust_chain_hardening.gates.{expectedGate.GateId}.reason_codes", FormatStringArray(expectedGate.ReasonCodes), FormatStringArray(GetStringArray(actualGate.Value, "reason_codes")));
        }

        return consistent;
    }

    private static JsonElement? FindGate(IEnumerable<JsonElement> gates, string gateId)
    {
        foreach (var gate in gates)
        {
            if (string.Equals(GetString(gate, "gate_id"), gateId, StringComparison.Ordinal))
            {
                return gate;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement element, params string[] path)
    {
        if (!TryGet(element, out var current, path) || current.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return current.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatStringArray(IEnumerable<string> values)
    {
        return string.Join("|", values.OrderBy(value => value, StringComparer.Ordinal));
    }
}
