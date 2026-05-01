using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Audit.Core;

public sealed class AuditEvidenceService
{
    public const string ProducerVersion = "0.1.0-alpha.1";
    private const string DefaultGuardPolicyPath = AuditInputReader.DefaultGuardPolicyPath;
    private readonly AuditInputReader reader;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public AuditEvidenceService()
        : this(new AuditInputReader())
    {
    }

    public AuditEvidenceService(AuditInputReader reader)
    {
        this.reader = reader;
    }

    public ShieldEvidenceDocument BuildEvidence(AuditInputOptions options)
    {
        return BuildEvidence(options, reader.Read(options));
    }

    public ShieldEvidenceDocument BuildEvidence(AuditInputOptions options, AuditInputSnapshot snapshot)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var guardPolicy = ReadGuardPolicy(options.WorkingDirectory);
        var guardCi = ReadGuardCi(options.WorkingDirectory);
        var guard = BuildGuardEvidence(snapshot, guardPolicy, guardCi);
        var handoff = BuildHandoffEvidence(snapshot, generatedAtUtc);
        var audit = BuildAuditEvidence(snapshot);
        var sampleWindowDays = MaxWindowDays(
            guard.Decisions?.WindowDays,
            handoff.Packets?.WindowDays,
            audit.Records?.EarliestRecordedAtUtc is not null && audit.Records.LatestRecordedAtUtc is not null
                ? WindowDays(audit.Records.EarliestRecordedAtUtc.Value, audit.Records.LatestRecordedAtUtc.Value)
                : null);

        return new ShieldEvidenceDocument(
            AuditJsonContracts.ShieldEvidenceSchemaVersion,
            $"shev_audit_{generatedAtUtc:yyyyMMddHHmmssfff}",
            generatedAtUtc,
            "both",
            sampleWindowDays,
            BuildRepositoryEvidence(options.WorkingDirectory),
            new ShieldEvidencePrivacy(
                SourceIncluded: false,
                RawDiffIncluded: false,
                PromptIncluded: false,
                SecretsIncluded: false,
                RedactionApplied: true,
                UploadIntent: "local_only"),
            new ShieldEvidenceDimensions(guard, handoff, audit),
            new ShieldEvidenceProvenance(
                "carves-audit",
                ProducerVersion,
                "local",
                "working_tree_scan",
                BuildInputArtifacts(options.WorkingDirectory, snapshot),
                BuildWarnings(snapshot, guardPolicy, guardCi)));
    }

    private static ShieldEvidenceRepository BuildRepositoryEvidence(string repositoryRoot)
    {
        return new ShieldEvidenceRepository(
            Directory.Exists(Path.Combine(repositoryRoot, ".git")) ? "git" : "unknown",
            "unknown",
            DefaultBranch: null,
            CommitSha: TryReadHeadCommit(repositoryRoot));
    }

    private static string? TryReadHeadCommit(string repositoryRoot)
    {
        var headPath = Path.Combine(repositoryRoot, ".git", "HEAD");
        if (!File.Exists(headPath))
        {
            return null;
        }

        try
        {
            var head = File.ReadAllText(headPath).Trim();
            if (!head.StartsWith("ref:", StringComparison.Ordinal))
            {
                return LooksLikeSha(head) ? head : null;
            }

            var refPath = head["ref:".Length..].Trim().Replace('/', Path.DirectorySeparatorChar);
            var fullRefPath = Path.Combine(repositoryRoot, ".git", refPath);
            if (!File.Exists(fullRefPath))
            {
                return null;
            }

            var value = File.ReadAllText(fullRefPath).Trim();
            return LooksLikeSha(value) ? value : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool LooksLikeSha(string value)
    {
        return value.Length is >= 7 and <= 64
            && value.All(character => char.IsDigit(character)
                || character is (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
    }

    private static ShieldGuardEvidence BuildGuardEvidence(
        AuditInputSnapshot snapshot,
        ShieldGuardPolicyEvidence policy,
        ShieldGuardCiEvidence ci)
    {
        var hasGuardEvidence = policy.Present || snapshot.Guard.Records.Count > 0 || ci.Detected;
        if (!hasGuardEvidence)
        {
            return new ShieldGuardEvidence(false, null, null, null, null);
        }

        var decisions = BuildGuardDecisions(snapshot.Guard);
        var proofs = ci.WorkflowPaths is { Count: > 0 } && ci.GuardCheckCommandDetected == true
            ? ci.WorkflowPaths.Select(path => new ShieldProofRef("ci_workflow", path)).ToArray()
            : null;

        return new ShieldGuardEvidence(
            true,
            policy,
            ci,
            decisions,
            proofs is { Length: > 0 } ? proofs : null);
    }

    private static ShieldGuardDecisionsEvidence BuildGuardDecisions(GuardAuditInput guard)
    {
        int? windowDays = guard.Records.Count > 0
            ? WindowDays(
                guard.Records.Min(record => record.RecordedAtUtc),
                guard.Records.Max(record => record.RecordedAtUtc))
            : null;

        return new ShieldGuardDecisionsEvidence(
            guard.Records.Count > 0,
            windowDays,
            guard.Records.Count(record => string.Equals(record.Outcome, "allow", StringComparison.OrdinalIgnoreCase)),
            guard.Records.Count(record =>
                string.Equals(record.Outcome, "review", StringComparison.OrdinalIgnoreCase)
                || string.Equals(record.Outcome, "needs_review", StringComparison.OrdinalIgnoreCase)),
            guard.Records.Count(record => string.Equals(record.Outcome, "block", StringComparison.OrdinalIgnoreCase)),
            UnresolvedReviewCount: null,
            UnresolvedBlockCount: null);
    }

    private static ShieldHandoffEvidence BuildHandoffEvidence(AuditInputSnapshot snapshot, DateTimeOffset generatedAtUtc)
    {
        var packets = snapshot.HandoffPackets
            .Where(input => input.Packet is not null)
            .Select(input => input.Packet!)
            .OrderBy(packet => packet.CreatedAtUtc)
            .ToArray();
        if (packets.Length == 0)
        {
            return new ShieldHandoffEvidence(false, null, null, null);
        }

        var latest = packets[^1];
        var staleCount = snapshot.HandoffPackets.Count(input => input.InputStatus is not "loaded" and not "missing");
        return new ShieldHandoffEvidence(
            true,
            new ShieldHandoffPacketsEvidence(
                Present: true,
                Count: packets.Length,
                WindowDays: WindowDays(packets[0].CreatedAtUtc, packets[^1].CreatedAtUtc)),
            new ShieldLatestHandoffPacketEvidence(
                SchemaValid: true,
                AgeDays: Math.Max(0, (int)Math.Floor((generatedAtUtc - latest.CreatedAtUtc).TotalDays)),
                RepoOrientationFresh: null,
                TargetRepoMatches: null,
                CurrentObjectivePresent: !string.IsNullOrWhiteSpace(latest.CurrentObjective),
                RemainingWorkPresent: latest.RemainingWorkCount > 0,
                MustNotRepeatPresent: latest.MustNotRepeatCount > 0,
                latest.CompletedFactsWithEvidenceCount,
                latest.DecisionRefCount,
                latest.Confidence),
            new ShieldHandoffContinuityEvidence(
                SessionSwitchCount: packets.Length,
                SessionSwitchesWithPacket: packets.Length,
                StalePacketCount: staleCount));
    }

    private static ShieldAuditEvidence BuildAuditEvidence(AuditInputSnapshot snapshot)
    {
        var events = BuildAuditEventEvidence(snapshot);
        var logPresent = File.Exists(snapshot.Guard.InputPath)
            || snapshot.HandoffPackets.Any(input => input.InputStatus is not "missing");
        if (!logPresent && events.Count == 0)
        {
            return new ShieldAuditEvidence(false, null, null, null, null);
        }

        var malformedCount = snapshot.Guard.Diagnostics.MalformedRecordCount
            + snapshot.HandoffPackets.Count(input => input.Diagnostics.Any(diagnostic =>
                diagnostic.Code is "handoff_malformed_json" or "handoff_missing_id" or "handoff_invalid_created_at"));
        var earliest = events.Count > 0 ? events.Min(item => item.OccurredAtUtc) : (DateTimeOffset?)null;
        var latest = events.Count > 0 ? events.Max(item => item.OccurredAtUtc) : (DateTimeOffset?)null;
        var blockCount = snapshot.Guard.Records.Count(record => string.Equals(record.Outcome, "block", StringComparison.OrdinalIgnoreCase));
        var reviewCount = snapshot.Guard.Records.Count(record =>
            string.Equals(record.Outcome, "review", StringComparison.OrdinalIgnoreCase)
            || string.Equals(record.Outcome, "needs_review", StringComparison.OrdinalIgnoreCase));
        var integrityPassed = !snapshot.HasFatalInputError && !snapshot.IsDegraded;

        return new ShieldAuditEvidence(
            true,
            new ShieldAuditLogEvidence(
                Present: logPresent,
                Readable: !snapshot.HasFatalInputError,
                SchemaSupported: snapshot.Guard.Records.Count > 0
                    || snapshot.HandoffPackets.Any(input => input.Packet is not null)
                    || snapshot.Guard.Diagnostics.FutureVersionRecordCount == 0,
                AppendOnlyClaimed: false,
                IntegrityCheckPassed: integrityPassed),
            new ShieldAuditRecordsEvidence(
                events.Count,
                malformedCount,
                snapshot.Guard.Diagnostics.FutureVersionRecordCount,
                snapshot.Guard.Diagnostics.OversizedRecordCount,
                earliest,
                latest,
                RecordsWithRuleIdCount: snapshot.Guard.Records.Count(HasRuleId),
                RecordsWithEvidenceCount: events.Count(item => item.Refs.Count > 0)),
            new ShieldAuditCoverageEvidence(
                BlockDecisionCount: blockCount,
                BlockExplainCoveredCount: 0,
                ReviewDecisionCount: reviewCount,
                ReviewExplainCoveredCount: 0),
            new ShieldAuditReportsEvidence(
                SummaryGeneratedInWindow: false,
                ChangeReportGeneratedInWindow: false,
                FailurePatternDistributionPresent: false));
    }

    private static IReadOnlyList<AuditEvent> BuildAuditEventEvidence(AuditInputSnapshot snapshot)
    {
        var events = new List<AuditEvent>();
        events.AddRange(snapshot.Guard.Records.Select(record => new AuditEvent(
            "guard_decision",
            "guard",
            AuditInputReader.DefaultGuardDecisionPath,
            record.SchemaVersion.ToString(),
            record.RunId ?? "unknown",
            record.RecordedAtUtc,
            NormalizeStatus(record.Outcome, "unknown"),
            snapshot.Guard.Diagnostics.IsDegraded ? "degraded" : "known",
            null,
            record.EvidenceRefs)));

        events.AddRange(snapshot.HandoffPackets
            .Where(input => input.Packet is not null)
            .Select(input => new AuditEvent(
                "handoff_packet",
                "handoff",
                AuditInputReader.DefaultHandoffPacketPath,
                input.Packet!.SchemaVersion,
                input.Packet.HandoffId,
                input.Packet.CreatedAtUtc,
                NormalizeStatus(input.Packet.ResumeStatus, "unknown"),
                NormalizeStatus(input.Packet.Confidence, "unknown"),
                null,
                input.Packet.EvidenceRefs.Count > 0 ? input.Packet.EvidenceRefs : input.Packet.ContextRefs)));
        return events;
    }

    private static bool HasRuleId(GuardDecisionRecord record)
    {
        return record.Violations.Concat(record.Warnings)
            .Any(issue => !string.IsNullOrWhiteSpace(issue.RuleId));
    }

    private static ShieldGuardPolicyEvidence ReadGuardPolicy(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, DefaultGuardPolicyPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            return new ShieldGuardPolicyEvidence(false, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            var root = document.RootElement;
            var schemaVersion = ReadInt(root, "schema_version");
            var policyId = ReadString(root, "policy_id");
            var pathPolicy = TryGet(root, "path_policy");
            var changeShape = TryGet(root, "change_shape");
            var decision = TryGet(root, "decision");
            var schemaValid = schemaVersion == 1
                && !string.IsNullOrWhiteSpace(policyId)
                && pathPolicy.HasValue
                && decision.HasValue;
            var protectedPathAction = pathPolicy.HasValue ? ReadString(pathPolicy.Value, "protected_path_action") : null;
            var outsideAllowedAction = pathPolicy.HasValue ? ReadString(pathPolicy.Value, "outside_allowed_action") : null;
            var failClosed = decision.HasValue ? ReadBool(decision.Value, "fail_closed") : null;
            var reviewIsPassing = decision.HasValue ? ReadBool(decision.Value, "review_is_passing") : null;
            var emitEvidence = decision.HasValue ? ReadBool(decision.Value, "emit_evidence") : null;
            var protectedPrefixes = pathPolicy.HasValue
                ? ReadStringArray(pathPolicy.Value, "protected_path_prefixes")
                : [];

            return new ShieldGuardPolicyEvidence(
                true,
                DefaultGuardPolicyPath,
                schemaValid,
                schemaVersion,
                policyId,
                protectedPrefixes.Count > 0 ? protectedPrefixes : null,
                protectedPathAction,
                outsideAllowedAction,
                failClosed,
                reviewIsPassing,
                emitEvidence,
                root.TryGetProperty("change_budget", out _),
                root.TryGetProperty("dependency_policy", out _),
                changeShape.HasValue && (ReadBool(changeShape.Value, "require_tests_for_source_changes") == true
                    || !string.IsNullOrWhiteSpace(ReadString(changeShape.Value, "missing_tests_action"))),
                changeShape.HasValue && !string.IsNullOrWhiteSpace(ReadString(changeShape.Value, "mixed_feature_and_refactor_action")));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new ShieldGuardPolicyEvidence(
                true,
                DefaultGuardPolicyPath,
                SchemaValid: false,
                SchemaVersion: null,
                PolicyId: null,
                EffectiveProtectedPathPrefixes: null,
                ProtectedPathAction: null,
                OutsideAllowedAction: null,
                FailClosed: null,
                ReviewIsPassing: null,
                EmitEvidence: null,
                ChangeBudgetPresent: null,
                DependencyPolicyPresent: null,
                SourceTestRulePresent: null,
                MixedFeatureRefactorRulePresent: null);
        }
    }

    private static ShieldGuardCiEvidence ReadGuardCi(string repositoryRoot)
    {
        var workflowRoot = Path.Combine(repositoryRoot, ".github", "workflows");
        if (!Directory.Exists(workflowRoot))
        {
            return new ShieldGuardCiEvidence(false, null, null, null, null);
        }

        var workflowFiles = Directory
            .EnumerateFiles(workflowRoot, "*.*", SearchOption.TopDirectoryOnly)
            .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (workflowFiles.Length == 0)
        {
            return new ShieldGuardCiEvidence(false, null, null, null, null);
        }

        var relativePaths = workflowFiles
            .Select(path => ToRepoRelativePath(repositoryRoot, path))
            .ToArray();
        var commandDetected = false;
        var failDisabled = false;
        foreach (var path in workflowFiles)
        {
            var content = SafeReadText(path);
            commandDetected |= content.Contains("carves guard check", StringComparison.OrdinalIgnoreCase)
                || content.Contains("carves-guard check", StringComparison.OrdinalIgnoreCase)
                || content.Contains("carves-guard.exe check", StringComparison.OrdinalIgnoreCase)
                || content.Contains("carves.exe guard check", StringComparison.OrdinalIgnoreCase)
                || content.Contains("carves.dll guard check", StringComparison.OrdinalIgnoreCase);
            failDisabled |= content.Contains("continue-on-error: true", StringComparison.OrdinalIgnoreCase)
                || content.Contains("|| true", StringComparison.OrdinalIgnoreCase);
        }

        return new ShieldGuardCiEvidence(
            true,
            "github_actions",
            relativePaths,
            commandDetected,
            commandDetected && !failDisabled);
    }

    private static IReadOnlyList<string> BuildWarnings(
        AuditInputSnapshot snapshot,
        ShieldGuardPolicyEvidence policy,
        ShieldGuardCiEvidence ci)
    {
        var warnings = new List<string>();
        if (snapshot.Guard.InputStatus == "missing")
        {
            warnings.Add("guard_decisions_missing");
        }

        if (snapshot.Guard.Diagnostics.MalformedRecordCount > 0)
        {
            warnings.Add($"guard_malformed_records:{snapshot.Guard.Diagnostics.MalformedRecordCount}");
        }

        if (snapshot.Guard.Diagnostics.FutureVersionRecordCount > 0)
        {
            warnings.Add($"guard_future_schema_records:{snapshot.Guard.Diagnostics.FutureVersionRecordCount}");
        }

        if (snapshot.Guard.Diagnostics.OversizedRecordCount > 0)
        {
            warnings.Add($"guard_oversized_records:{snapshot.Guard.Diagnostics.OversizedRecordCount}");
        }

        warnings.AddRange(snapshot.HandoffPackets
            .SelectMany(input => input.Diagnostics.Select(diagnostic => diagnostic.Code))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal));

        if (policy.Present && policy.SchemaValid == false)
        {
            warnings.Add("guard_policy_invalid");
        }

        if (ci.Detected && ci.GuardCheckCommandDetected != true)
        {
            warnings.Add("guard_ci_without_guard_check_command");
        }

        return warnings.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<ShieldEvidenceInputArtifact> BuildInputArtifacts(
        string repositoryRoot,
        AuditInputSnapshot snapshot)
    {
        var artifacts = new List<ShieldEvidenceInputArtifact>();
        AddInputArtifact(artifacts, repositoryRoot, "guard_decisions", snapshot.Guard.InputPath, snapshot.Guard.InputStatus);
        foreach (var handoff in snapshot.HandoffPackets)
        {
            AddInputArtifact(artifacts, repositoryRoot, "handoff_packet", handoff.InputPath, handoff.InputStatus);
        }

        return artifacts
            .GroupBy(artifact => $"{artifact.Kind}:{artifact.Path}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(artifact => artifact.Kind, StringComparer.Ordinal)
            .ThenBy(artifact => artifact.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddInputArtifact(
        List<ShieldEvidenceInputArtifact> artifacts,
        string repositoryRoot,
        string kind,
        string path,
        string inputStatus)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(path);
            artifacts.Add(new ShieldEvidenceInputArtifact(
                kind,
                ToEvidencePath(repositoryRoot, path),
                ComputeFileSha256(path),
                fileInfo.Length,
                inputStatus));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string ToEvidencePath(string repositoryRoot, string path)
    {
        var fullRoot = Path.GetFullPath(repositoryRoot);
        var fullPath = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(fullRoot, fullPath);
        if (!Path.IsPathRooted(relativePath)
            && !string.Equals(relativePath, "..", StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return relativePath.Replace('\\', '/');
        }

        return fullPath.Replace('\\', '/');
    }

    private static string SafeReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static JsonElement? TryGet(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property)
            ? property
            : null;
    }

    private static int? ReadInt(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool? ReadBool(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            .Select(item => item.GetString()!.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ToRepoRelativePath(string repositoryRoot, string path)
    {
        return Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
    }

    private static string NormalizeStatus(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }

    private static int WindowDays(DateTimeOffset earliest, DateTimeOffset latest)
    {
        if (latest < earliest)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling((latest - earliest).TotalDays));
    }

    private static int? MaxWindowDays(params int?[] windows)
    {
        var concrete = windows.Where(window => window.HasValue).Select(window => window!.Value).ToArray();
        return concrete.Length == 0 ? null : concrete.Max();
    }
}
