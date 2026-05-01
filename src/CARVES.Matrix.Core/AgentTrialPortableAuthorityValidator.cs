using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

internal sealed record AgentTrialPortableAuthorityValidation(
    bool Present,
    bool Verified,
    string? PackMetadataPath,
    string? ChallengeMetadataPath,
    string? ExpectedTaskContractSha256,
    string? ExpectedInstructionPackSha256,
    IReadOnlyList<string> MissingRequiredArtifacts,
    IReadOnlyList<string> FailureReasons)
{
    public static AgentTrialPortableAuthorityValidation NotPresent { get; } = new(
        Present: false,
        Verified: true,
        PackMetadataPath: null,
        ChallengeMetadataPath: null,
        ExpectedTaskContractSha256: null,
        ExpectedInstructionPackSha256: null,
        MissingRequiredArtifacts: [],
        FailureReasons: []);

    public string? FirstFailureReason => FailureReasons.FirstOrDefault();
}

internal static class AgentTrialPortableAuthorityValidator
{
    private const string AuthorityDirectoryName = ".carves-pack";

    public static AgentTrialPortableAuthorityValidation Validate(string workspaceRoot, TimeSpan timeout)
    {
        var authorityRoot = ResolveAuthorityRoot(workspaceRoot);
        if (authorityRoot is null)
        {
            return AgentTrialPortableAuthorityValidation.NotPresent;
        }

        var missing = new List<string>();
        var reasons = new List<string>();
        var packPath = Require(authorityRoot, "authority/pack.json", "portable_authority_pack", missing, reasons);
        var challengePath = Require(authorityRoot, "authority/challenge.json", "portable_authority_challenge", missing, reasons);
        var taskAuthorityPath = Require(authorityRoot, "authority/task-contract.json", "portable_authority_task_contract", missing, reasons);
        var instructionAuthorityPath = Require(authorityRoot, "authority/instruction-pack.json", "portable_authority_instruction_pack", missing, reasons);
        Require(authorityRoot, "pack-manifest.json", "portable_pack_manifest", missing, reasons);
        Require(authorityRoot, "scorer/scoring-contract.json", "portable_scoring_contract", missing, reasons);
        var baselinePath = Require(authorityRoot, "baseline-manifest.json", "portable_baseline_manifest", missing, reasons);
        var expectedTaskPath = Require(authorityRoot, "expected/task-contract.json", "portable_expected_task_contract", missing, reasons);
        var expectedInstructionPath = Require(authorityRoot, "expected/instruction-pack.json", "portable_expected_instruction_pack", missing, reasons);

        var expectedTaskSha256 = ReadExpectedSha256(expectedTaskPath, "portable_expected_task_contract_invalid", reasons);
        var expectedInstructionSha256 = ReadExpectedSha256(expectedInstructionPath, "portable_expected_instruction_pack_invalid", reasons);
        ComparePinnedFile(taskAuthorityPath, expectedTaskSha256, "portable_authority_task_contract_hash_mismatch", reasons);
        ComparePinnedFile(
            instructionAuthorityPath,
            expectedInstructionSha256,
            "portable_authority_instruction_pack_hash_mismatch",
            reasons);
        CompareWorkspaceFile(
            Path.Combine(workspaceRoot, ".carves", "trial", "task-contract.json"),
            expectedTaskSha256,
            "portable_task_contract_missing",
            "portable_task_contract_hash_mismatch",
            "portable_task_contract",
            missing,
            reasons);
        CompareWorkspaceFile(
            Path.Combine(workspaceRoot, ".carves", "trial", "instruction-pack.json"),
            expectedInstructionSha256,
            "portable_instruction_pack_missing",
            "portable_instruction_pack_hash_mismatch",
            "portable_instruction_pack",
            missing,
            reasons);

        AgentTrialPortableBaselineValidator.Validate(workspaceRoot, baselinePath, timeout, missing, reasons);

        return new AgentTrialPortableAuthorityValidation(
            Present: true,
            Verified: reasons.Count == 0,
            packPath,
            challengePath,
            expectedTaskSha256,
            expectedInstructionSha256,
            missing.Distinct(StringComparer.Ordinal).ToArray(),
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string? ResolveAuthorityRoot(string workspaceRoot)
    {
        var workspace = new DirectoryInfo(Path.GetFullPath(workspaceRoot));
        var parent = workspace.Parent;
        if (parent is null)
        {
            return null;
        }

        var authorityRoot = Path.Combine(parent.FullName, AuthorityDirectoryName);
        return Directory.Exists(authorityRoot) ? authorityRoot : null;
    }

    private static string? Require(
        string authorityRoot,
        string relativePath,
        string artifactName,
        List<string> missing,
        List<string> reasons)
    {
        var path = Path.Combine(authorityRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            return path;
        }

        missing.Add(artifactName);
        reasons.Add(artifactName + "_missing");
        return null;
    }

    private static string? ReadExpectedSha256(string? path, string reason, List<string> reasons)
    {
        if (path is null)
        {
            return null;
        }

        try
        {
            var root = AgentTrialLocalJson.ReadObject(path);
            return NormalizeSha256(root["sha256"]?.GetValue<string>()) ?? AddInvalid(reason, reasons);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException)
        {
            reasons.Add(reason);
            return null;
        }
    }

    private static string? AddInvalid(string reason, List<string> reasons)
    {
        reasons.Add(reason);
        return null;
    }

    private static void ComparePinnedFile(string? path, string? expectedSha256, string reason, List<string> reasons)
    {
        if (path is null || expectedSha256 is null)
        {
            return;
        }

        if (!string.Equals(AgentTrialLocalJson.HashFile(path), expectedSha256, StringComparison.Ordinal))
        {
            reasons.Add(reason);
        }
    }

    private static void CompareWorkspaceFile(
        string path,
        string? expectedSha256,
        string missingReason,
        string mismatchReason,
        string artifactName,
        List<string> missing,
        List<string> reasons)
    {
        if (expectedSha256 is null)
        {
            return;
        }

        if (!File.Exists(path))
        {
            missing.Add(artifactName);
            reasons.Add(missingReason);
            return;
        }

        if (!string.Equals(AgentTrialLocalJson.HashFile(path), expectedSha256, StringComparison.Ordinal))
        {
            reasons.Add(mismatchReason);
        }
    }

    private static string? NormalizeSha256(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.StartsWith("sha256:", StringComparison.Ordinal))
        {
            normalized = normalized["sha256:".Length..];
        }

        return normalized.Length == 64 && normalized.All(static value => value is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            ? "sha256:" + normalized
            : null;
    }

}
