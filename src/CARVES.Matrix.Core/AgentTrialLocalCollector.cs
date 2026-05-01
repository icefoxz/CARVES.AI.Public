using System.Text.Json.Nodes;

namespace Carves.Matrix.Core;

public static class AgentTrialLocalCollector
{
    private const string TaskContractPinMismatch = "task_contract_pin_mismatch";
    private const string TaskContractPinMissing = "task_contract_pin_missing";
    private const string InstructionPackPinMissing = "instruction_pack_pin_missing";
    private const string InstructionPackPinMismatch = "instruction_pack_pin_mismatch";
    private const string DiffScopeSummaryFileName = "diff-scope-summary.json";
    private const string TestEvidenceFileName = "test-evidence.json";
    private const string TrialResultFileName = "carves-agent-trial-result.json";
    private const string AgentReportSchemaVersion = "agent-report.v0";
    private static readonly string[] AgentReportRequiredTopLevelProperties =
    [
        "schema_version",
        "task_id",
        "task_version",
        "challenge_id",
        "agent_profile_snapshot",
        "completion_status",
        "claimed_files_changed",
        "claimed_tests_run",
        "claimed_tests_passed",
        "risks",
        "deviations",
        "blocked_or_uncertain_decisions",
        "follow_up_work",
        "evidence_refs",
        "privacy",
    ];

    private static readonly HashSet<string> AgentReportAllowedTopLevelProperties = new(AgentReportRequiredTopLevelProperties, StringComparer.Ordinal);

    public static AgentTrialLocalCollectorResult Collect(AgentTrialLocalCollectorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var workspaceRoot = Path.GetFullPath(options.WorkspaceRoot);
        var artifactRoot = ResolveWorkspacePath(workspaceRoot, options.ArtifactRootRelativePath);
        var packMetadataPath = ResolveWorkspacePath(workspaceRoot, options.PackMetadataRelativePath);
        var challengeMetadataPath = ResolveWorkspacePath(workspaceRoot, options.ChallengeMetadataRelativePath);
        var instructionPackPath = ResolveWorkspacePath(workspaceRoot, options.InstructionPackRelativePath);
        var taskContractPath = ResolveWorkspacePath(workspaceRoot, options.TaskContractRelativePath);
        var agentReportPath = ResolveWorkspacePath(workspaceRoot, options.AgentReportRelativePath);
        var diffScopeSummaryPath = Path.Combine(artifactRoot, DiffScopeSummaryFileName);
        var testEvidencePath = Path.Combine(artifactRoot, TestEvidenceFileName);
        var trialResultPath = Path.Combine(artifactRoot, TrialResultFileName);

        var portableAuthority = AgentTrialPortableAuthorityValidator.Validate(workspaceRoot, options.CommandTimeout);
        var authorityPackMetadataPath = portableAuthority.PackMetadataPath ?? packMetadataPath;
        var authorityChallengeMetadataPath = portableAuthority.ChallengeMetadataPath ?? challengeMetadataPath;
        var taskContractPin = ResolveTaskContractPin(
            taskContractPath,
            authorityChallengeMetadataPath,
            authorityPackMetadataPath,
            portableAuthority.ExpectedTaskContractSha256);
        var contract = taskContractPin.Contract;
        var instructionPack = ResolveInstructionPack(
            instructionPackPath,
            authorityChallengeMetadataPath,
            authorityPackMetadataPath,
            portableAuthority.ExpectedInstructionPackSha256);
        var agentReport = ReadAgentReport(agentReportPath);
        var missingRequiredArtifacts = new List<string>(portableAuthority.MissingRequiredArtifacts);
        var failureReasons = new List<string>(portableAuthority.FailureReasons);
        if (!taskContractPin.Verified)
        {
            failureReasons.Add(taskContractPin.FailureReason ?? TaskContractPinMissing);
        }

        if (!instructionPack.Verified)
        {
            missingRequiredArtifacts.Add("instruction_pack");
            failureReasons.Add(instructionPack.FailureReason ?? InstructionPackPinMissing);
        }

        if (!agentReport.Exists)
        {
            missingRequiredArtifacts.Add("agent_report");
            failureReasons.Add("agent_report_missing");
        }

        var evidenceAuthorityVerified = taskContractPin.Verified && portableAuthority.Verified;
        var unavailableReason = portableAuthority.FirstFailureReason ?? taskContractPin.FailureReason;

        var preCommandSnapshot = evidenceAuthorityVerified
            ? AgentTrialLocalDiffReader.ReadWorkspaceSnapshot(workspaceRoot, contract, agentReport, "pre_command", options.CommandTimeout)
            : null;

        var commandResults = evidenceAuthorityVerified
            ? RunRequiredCommands(contract, workspaceRoot, options.CommandTimeout, failureReasons)
            : [];

        var diff = evidenceAuthorityVerified
            ? AgentTrialLocalDiffReader.FromSnapshots(
                preCommandSnapshot!,
                AgentTrialLocalDiffReader.ReadWorkspaceSnapshot(workspaceRoot, contract, agentReport, "post_command", options.CommandTimeout))
            : AgentTrialLocalDiffReader.Unavailable(unavailableReason);
        if (!diff.Available)
        {
            failureReasons.Add("diff_scope_unavailable");
        }

        AgentTrialLocalJson.WriteObject(
            diffScopeSummaryPath,
            AgentTrialLocalDiffReader.ToJson(diff, contract, options.BaseRef, options.WorktreeRef));

        AgentTrialLocalJson.WriteObject(
            testEvidencePath,
            AgentTrialLocalEvidenceBuilder.BuildTestEvidence(contract, agentReport, commandResults));

        var collectionStatus = ResolveCollectionStatus(
            taskContractPin,
            portableAuthority,
            instructionPack,
            agentReport,
            diff,
            commandResults);
        AgentTrialLocalJson.WriteObject(
            trialResultPath,
            AgentTrialLocalEvidenceBuilder.BuildTrialResult(
                workspaceRoot,
                contract,
                collectionStatus,
                missingRequiredArtifacts,
                failureReasons,
                taskContractPin.ExpectedTaskContractSha256,
                taskContractPin.ActualTaskContractSha256,
                instructionPack,
                agentReportPath,
                diffScopeSummaryPath,
                testEvidencePath,
                options.CreatedAt ?? DateTimeOffset.UtcNow));

        return new AgentTrialLocalCollectorResult(
            collectionStatus,
            diffScopeSummaryPath,
            testEvidencePath,
            trialResultPath,
            missingRequiredArtifacts,
            failureReasons);
    }

    private static AgentTrialInstructionPack ResolveInstructionPack(
        string instructionPackPath,
        string challengeMetadataPath,
        string packMetadataPath,
        string? expectedInstructionPackSha256Override = null)
    {
        var actualInstructionPackSha256 = AgentTrialLocalJson.HashFileOrMissing(instructionPackPath);
        var challengeRoot = File.Exists(challengeMetadataPath)
            ? AgentTrialLocalJson.ReadObject(challengeMetadataPath)
            : null;
        var packRoot = File.Exists(packMetadataPath)
            ? AgentTrialLocalJson.ReadObject(packMetadataPath)
            : null;
        var expectedInstructionPackSha256 = expectedInstructionPackSha256Override ??
            ReadExpectedInstructionPackSha256(challengeRoot) ??
            ReadExpectedInstructionPackSha256(packRoot);

        if (expectedInstructionPackSha256 is null)
        {
            return AgentTrialInstructionPack.Unavailable(
                AgentTrialLocalJson.MissingArtifactHash,
                actualInstructionPackSha256,
                InstructionPackPinMissing);
        }

        if (!File.Exists(instructionPackPath))
        {
            return AgentTrialInstructionPack.Unavailable(
                expectedInstructionPackSha256,
                actualInstructionPackSha256,
                "instruction_pack_missing");
        }

        if (!string.Equals(expectedInstructionPackSha256, actualInstructionPackSha256, StringComparison.Ordinal))
        {
            return AgentTrialInstructionPack.Unavailable(
                expectedInstructionPackSha256,
                actualInstructionPackSha256,
                InstructionPackPinMismatch);
        }

        var root = AgentTrialLocalJson.ReadObject(instructionPackPath);
        var prompt = ReadPromptSample(root);
        return new AgentTrialInstructionPack(
            Exists: true,
            Verified: true,
            AgentTrialLocalJson.GetRequiredString(root, "instruction_pack_id"),
            AgentTrialLocalJson.GetRequiredString(root, "instruction_pack_version"),
            expectedInstructionPackSha256,
            actualInstructionPackSha256,
            prompt.PromptId,
            prompt.PromptVersion,
            prompt.Path,
            prompt.Sha256,
            ReadInstructionFiles(root),
            null);
    }

    private static AgentTrialAgentReport ReadAgentReport(string agentReportPath)
    {
        if (!File.Exists(agentReportPath))
        {
            return new AgentTrialAgentReport(false, false, new HashSet<string>(StringComparer.Ordinal));
        }

        var root = AgentTrialLocalJson.ReadObject(agentReportPath);
        ValidateAgentReportTopLevel(root);
        var claimedFiles = AgentTrialLocalJson.GetStringArray(root, "claimed_files_changed")
            .Select(path => path.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        return new AgentTrialAgentReport(
            true,
            AgentTrialLocalJson.GetBooleanOrDefault(root, "claimed_tests_passed", false),
            claimedFiles);
    }

    private static void ValidateAgentReportTopLevel(JsonObject root)
    {
        var issues = new List<string>();
        var actualSchemaVersion = TryGetString(root, "schema_version");
        if (!string.Equals(actualSchemaVersion, AgentReportSchemaVersion, StringComparison.Ordinal))
        {
            issues.Add($"schema_version expected {AgentReportSchemaVersion}");
        }

        var missing = AgentReportRequiredTopLevelProperties
            .Where(property => !root.ContainsKey(property))
            .ToArray();
        if (missing.Length > 0)
        {
            issues.Add("missing fields: " + string.Join(", ", missing));
        }

        var unexpected = root
            .Select(property => property.Key)
            .Where(property => !AgentReportAllowedTopLevelProperties.Contains(property))
            .OrderBy(property => property, StringComparer.Ordinal)
            .ToArray();
        if (unexpected.Length > 0)
        {
            issues.Add("unexpected fields: " + string.Join(", ", unexpected));
        }

        if (issues.Count > 0)
        {
            throw new InvalidDataException(
                "Agent report schema invalid: "
                + string.Join("; ", issues)
                + ". Use artifacts/agent-report.template.json, keep schema_version exactly agent-report.v0, and do not add extra top-level fields.");
        }
    }

    private static string? TryGetString(JsonObject root, string propertyName)
    {
        try
        {
            return root[propertyName]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static AgentTrialTaskContractPin ResolveTaskContractPin(
        string taskContractPath,
        string challengeMetadataPath,
        string packMetadataPath,
        string? expectedTaskContractSha256Override = null)
    {
        var actualTaskContractSha256 = AgentTrialLocalJson.HashFileOrMissing(taskContractPath);
        var challengeRoot = File.Exists(challengeMetadataPath)
            ? AgentTrialLocalJson.ReadObject(challengeMetadataPath)
            : null;
        var packRoot = File.Exists(packMetadataPath)
            ? AgentTrialLocalJson.ReadObject(packMetadataPath)
            : null;
        var authorityContract = challengeRoot is not null
            ? AgentTrialTaskContract.FromChallengeAuthority(challengeRoot)
            : AgentTrialTaskContract.From(AgentTrialLocalJson.ReadObject(taskContractPath));
        var expectedTaskContractSha256 = expectedTaskContractSha256Override ??
            ReadExpectedTaskContractSha256(challengeRoot) ??
            ReadExpectedTaskContractSha256(packRoot);

        if (expectedTaskContractSha256 is null)
        {
            return new AgentTrialTaskContractPin(
                false,
                authorityContract,
                AgentTrialLocalJson.MissingArtifactHash,
                actualTaskContractSha256,
                TaskContractPinMissing);
        }

        if (!string.Equals(expectedTaskContractSha256, actualTaskContractSha256, StringComparison.Ordinal))
        {
            return new AgentTrialTaskContractPin(
                false,
                authorityContract,
                expectedTaskContractSha256,
                actualTaskContractSha256,
                TaskContractPinMismatch);
        }

        return new AgentTrialTaskContractPin(
            true,
            AgentTrialTaskContract.From(AgentTrialLocalJson.ReadObject(taskContractPath)),
            expectedTaskContractSha256,
            actualTaskContractSha256,
            null);
    }

    private static string? ReadExpectedTaskContractSha256(JsonObject? root)
    {
        if (root is null)
        {
            return null;
        }

        return NormalizeSha256(ReadOptionalString(root, "expected_task_contract_sha256")) ??
            NormalizeSha256(ReadOptionalString(root, "task_contract_sha256"));
    }

    private static string? ReadExpectedInstructionPackSha256(JsonObject? root)
    {
        if (root is null)
        {
            return null;
        }

        return NormalizeSha256(ReadOptionalString(root, "expected_instruction_pack_sha256")) ??
            NormalizeSha256(ReadOptionalString(root, "instruction_pack_sha256"));
    }

    private static string? ReadOptionalString(JsonObject root, string propertyName)
    {
        return root.TryGetPropertyValue(propertyName, out var value) && value is not null
            ? value.GetValue<string>()
            : null;
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

        return normalized.Length == 64 && normalized.All(IsLowerHex)
            ? "sha256:" + normalized
            : null;
    }

    private static (string PromptId, string PromptVersion, string Path, string Sha256) ReadPromptSample(JsonObject root)
    {
        var prompts = root["prompt_samples"]?.AsArray()
            ?? throw new InvalidDataException("Required array property is missing: prompt_samples");
        var firstPrompt = prompts.OfType<JsonObject>().FirstOrDefault()
            ?? throw new InvalidDataException("At least one prompt sample is required.");

        return (
            AgentTrialLocalJson.GetRequiredString(firstPrompt, "prompt_id"),
            AgentTrialLocalJson.GetRequiredString(firstPrompt, "prompt_version"),
            AgentTrialLocalJson.GetRequiredString(firstPrompt, "path"),
            AgentTrialLocalJson.GetRequiredString(firstPrompt, "sha256"));
    }

    private static IReadOnlyList<AgentTrialInstructionFile> ReadInstructionFiles(JsonObject root)
    {
        var files = root["canonical_instruction_files"]?.AsArray()
            ?? throw new InvalidDataException("Required array property is missing: canonical_instruction_files");

        return files
            .OfType<JsonObject>()
            .Select(file => new AgentTrialInstructionFile(
                AgentTrialLocalJson.GetRequiredString(file, "path"),
                AgentTrialLocalJson.GetRequiredString(file, "role"),
                AgentTrialLocalJson.GetRequiredString(file, "sha256")))
            .ToArray();
    }

    private static bool IsLowerHex(char value)
    {
        return value is (>= '0' and <= '9') or (>= 'a' and <= 'f');
    }

    private static IReadOnlyList<(string Command, AgentTrialProcessResult Result)> RunRequiredCommands(
        AgentTrialTaskContract contract,
        string workspaceRoot,
        TimeSpan timeout,
        List<string> failureReasons)
    {
        var results = new List<(string Command, AgentTrialProcessResult Result)>();
        foreach (var command in contract.RequiredCommands)
        {
            try
            {
                var result = AgentTrialLocalProcessRunner.RunShell(command, workspaceRoot, timeout);
                if (result.ExitCode != 0)
                {
                    failureReasons.Add(result.TimedOut ? "required_command_timed_out" : "required_command_failed");
                }

                results.Add((command, result));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
            {
                var now = DateTimeOffset.UtcNow;
                failureReasons.Add("required_command_unavailable");
                results.Add((
                    command,
                    new AgentTrialProcessResult(
                        127,
                        string.Empty,
                        ex.Message,
                        TimedOut: false,
                        now,
                        now,
                        DurationMs: 0)));
            }
        }

        return results;
    }

    private static string ResolveCollectionStatus(
        AgentTrialTaskContractPin taskContractPin,
        AgentTrialPortableAuthorityValidation portableAuthority,
        AgentTrialInstructionPack instructionPack,
        AgentTrialAgentReport agentReport,
        AgentTrialDiffSnapshot diff,
        IReadOnlyList<(string Command, AgentTrialProcessResult Result)> commandResults)
    {
        if (!taskContractPin.Verified || !portableAuthority.Verified || !instructionPack.Verified || !agentReport.Exists)
        {
            return "failed_closed";
        }

        if (!diff.Available || commandResults.Any(result => result.Result.ExitCode != 0))
        {
            return "partial_local_only";
        }

        return "collectable";
    }

    private sealed record AgentTrialTaskContractPin(
        bool Verified,
        AgentTrialTaskContract Contract,
        string ExpectedTaskContractSha256,
        string ActualTaskContractSha256,
        string? FailureReason);

    private static string ResolveWorkspacePath(string workspaceRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"Expected repo-relative path: {relativePath}", nameof(relativePath));
        }

        var fullRoot = Path.GetFullPath(workspaceRoot);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Path escapes workspace root: {relativePath}", nameof(relativePath));
        }

        return fullPath;
    }
}
