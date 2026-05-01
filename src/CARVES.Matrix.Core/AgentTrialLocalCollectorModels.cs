using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

public sealed record AgentTrialLocalCollectorOptions(string WorkspaceRoot)
{
    public string PackMetadataRelativePath { get; init; } = ".carves/trial/pack.json";

    public string ChallengeMetadataRelativePath { get; init; } = ".carves/trial/challenge.json";

    public string InstructionPackRelativePath { get; init; } = ".carves/trial/instruction-pack.json";

    public string TaskContractRelativePath { get; init; } = ".carves/trial/task-contract.json";

    public string AgentReportRelativePath { get; init; } = "artifacts/agent-report.json";

    public string ArtifactRootRelativePath { get; init; } = "artifacts";

    public string BaseRef { get; init; } = "HEAD";

    public string WorktreeRef { get; init; } = "working-tree";

    public DateTimeOffset? CreatedAt { get; init; }

    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromMinutes(5);
}

public sealed record AgentTrialLocalCollectorResult(
    string LocalCollectionStatus,
    string DiffScopeSummaryPath,
    string TestEvidencePath,
    string TrialResultPath,
    IReadOnlyList<string> MissingRequiredArtifacts,
    IReadOnlyList<string> FailureReasons);

internal sealed record AgentTrialTaskContract(
    string SuiteId,
    string PackId,
    string PackVersion,
    string TaskId,
    string TaskVersion,
    string PromptId,
    string PromptVersion,
    string ChallengeId,
    string ChallengeSource,
    IReadOnlyList<string> AllowedPaths,
    IReadOnlyList<string> ForbiddenPaths,
    IReadOnlyList<string> RequiredCommands)
{
    public static AgentTrialTaskContract From(JsonObject root)
    {
        return new AgentTrialTaskContract(
            AgentTrialLocalJson.GetRequiredString(root, "suite_id"),
            AgentTrialLocalJson.GetRequiredString(root, "pack_id"),
            AgentTrialLocalJson.GetRequiredString(root, "pack_version"),
            AgentTrialLocalJson.GetRequiredString(root, "task_id"),
            AgentTrialLocalJson.GetRequiredString(root, "task_version"),
            AgentTrialLocalJson.GetRequiredString(root, "prompt_id"),
            AgentTrialLocalJson.GetRequiredString(root, "prompt_version"),
            AgentTrialLocalJson.GetRequiredString(root, "challenge_id"),
            AgentTrialLocalJson.GetRequiredString(root, "challenge_source"),
            AgentTrialLocalJson.GetStringArray(root, "allowed_paths"),
            AgentTrialLocalJson.GetStringArray(root, "forbidden_paths"),
            AgentTrialLocalJson.GetStringArray(root, "required_commands"));
    }

    public static AgentTrialTaskContract FromChallengeAuthority(JsonObject root)
    {
        return new AgentTrialTaskContract(
            AgentTrialLocalJson.GetRequiredString(root, "suite_id"),
            AgentTrialLocalJson.GetRequiredString(root, "pack_id"),
            AgentTrialLocalJson.GetRequiredString(root, "pack_version"),
            AgentTrialLocalJson.GetRequiredString(root, "task_id"),
            AgentTrialLocalJson.GetRequiredString(root, "task_version"),
            AgentTrialLocalJson.GetRequiredString(root, "prompt_id"),
            AgentTrialLocalJson.GetRequiredString(root, "prompt_version"),
            AgentTrialLocalJson.GetRequiredString(root, "challenge_id"),
            AgentTrialLocalJson.GetRequiredString(root, "challenge_source"),
            [],
            [],
            []);
    }
}

internal sealed record AgentTrialAgentReport(
    bool Exists,
    bool ClaimedTestsPassed,
    IReadOnlySet<string> ClaimedFilesChanged);

internal sealed record AgentTrialInstructionPack(
    bool Exists,
    bool Verified,
    string InstructionPackId,
    string InstructionPackVersion,
    string ExpectedInstructionPackSha256,
    string ActualInstructionPackSha256,
    string PromptId,
    string PromptVersion,
    string PromptPath,
    string PromptSha256,
    IReadOnlyList<AgentTrialInstructionFile> CanonicalInstructionFiles,
    string? FailureReason)
{
    public static AgentTrialInstructionPack Unavailable(string expectedInstructionPackSha256, string actualInstructionPackSha256, string? failureReason)
    {
        return new AgentTrialInstructionPack(
            Exists: !string.Equals(actualInstructionPackSha256, AgentTrialLocalJson.MissingArtifactHash, StringComparison.Ordinal),
            Verified: false,
            "unavailable",
            "unavailable",
            expectedInstructionPackSha256,
            actualInstructionPackSha256,
            "unavailable",
            "unavailable",
            "unavailable",
            AgentTrialLocalJson.MissingArtifactHash,
            [],
            failureReason);
    }
}

internal sealed record AgentTrialInstructionFile(string Path, string Role, string Sha256);
