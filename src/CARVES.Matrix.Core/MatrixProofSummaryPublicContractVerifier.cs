using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool VerifyProofSummaryPublicContract(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root)
    {
        var contract = MatrixProofSummaryPublicContract.Model;
        var consistent = true;
        var proofMode = GetString(root, "proof_mode");
        consistent &= VerifyAllowedSummaryProperties(
            issues,
            summaryRelativePath,
            root,
            string.Empty,
            contract.GetTopLevelFields(proofMode));
        consistent &= VerifyAllowedSummaryObject(
            issues,
            summaryRelativePath,
            root,
            contract.ArtifactManifest);
        consistent &= VerifyAllowedSummaryObject(
            issues,
            summaryRelativePath,
            root,
            contract.ProofCapabilities);
        consistent &= VerifyAllowedSummaryObject(
            issues,
            summaryRelativePath,
            root,
            contract.ProofCapabilityCoverage);
        consistent &= VerifyAllowedSummaryObject(
            issues,
            summaryRelativePath,
            root,
            contract.ProofCapabilityRequirements);
        consistent &= VerifyAllowedSummaryObject(
            issues,
            summaryRelativePath,
            root,
            contract.Privacy);
        consistent &= VerifyAllowedSummaryObject(
            issues,
            summaryRelativePath,
            root,
            contract.PublicClaims);
        consistent &= VerifyAllowedSummaryObject(
            issues,
            summaryRelativePath,
            root,
            contract.TrustChainHardening);
        consistent &= VerifyTrustChainGateContract(issues, summaryRelativePath, root);

        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.NativeMinimalProofMode, StringComparison.Ordinal))
        {
            consistent &= VerifyAllowedSummaryObject(
                issues,
                summaryRelativePath,
                root,
                contract.Native);
            return consistent;
        }

        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.FullReleaseProofMode, StringComparison.Ordinal))
        {
            consistent &= VerifyAllowedSummaryObject(
                issues,
                summaryRelativePath,
                root,
                contract.Project);
            consistent &= VerifyAllowedSummaryObject(
                issues,
                summaryRelativePath,
                root,
                contract.ProjectTrustChainHardening);
            consistent &= VerifyAllowedSummaryObject(
                issues,
                summaryRelativePath,
                root,
                contract.Packaged);
            consistent &= VerifyAllowedSummaryObject(
                issues,
                summaryRelativePath,
                root,
                contract.PackagedTrustChainHardening);
        }

        return consistent;
    }

    private static bool VerifyAllowedSummaryObject(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root,
        MatrixProofSummaryObjectContract contract)
    {
        if (!TryGet(root, out var element, contract.JsonPath.Split('.'))
            || element.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        return VerifyAllowedSummaryProperties(issues, summaryRelativePath, element, contract.JsonPath, contract.FieldNames);
    }

    private static bool VerifyTrustChainGateContract(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root)
    {
        if (!TryGet(root, out var gates, "trust_chain_hardening", "gates")
            || gates.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        var consistent = true;
        foreach (var gate in gates.EnumerateArray())
        {
            if (gate.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            consistent &= VerifyAllowedSummaryProperties(
                issues,
                summaryRelativePath,
                gate,
                MatrixProofSummaryPublicContract.Model.TrustChainGate.JsonPath,
                MatrixProofSummaryPublicContract.Model.TrustChainGate.FieldNames);
        }

        return consistent;
    }

    private static bool VerifyAllowedSummaryProperties(
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement element,
        string jsonPathPrefix,
        IReadOnlyList<string> allowedFields)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var consistent = true;
        foreach (var property in element.EnumerateObject())
        {
            if (allowedFields.Contains(property.Name, StringComparer.Ordinal))
            {
                continue;
            }

            var jsonPath = string.IsNullOrEmpty(jsonPathPrefix)
                ? property.Name
                : $"{jsonPathPrefix}.{property.Name}";
            issues.Add(new MatrixVerifyIssue(
                "summary",
                "matrix_proof_summary",
                summaryRelativePath,
                $"summary_unknown_field:{jsonPath}",
                "known_field",
                jsonPath));
            consistent = false;
        }

        return consistent;
    }
}
