using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

internal static class WorkerCompletionClaimExtractor
{
    private static readonly IReadOnlyList<string> DefaultRequiredFields =
    [
        "changed_files",
        "contract_items_satisfied",
        "tests_run",
        "evidence_paths",
        "known_limitations",
        "next_recommendation",
    ];

    private static readonly IReadOnlyDictionary<string, string> FieldAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["changedfiles"] = "changed_files",
        ["changedpaths"] = "changed_files",
        ["fileschanged"] = "changed_files",
        ["contractitemssatisfied"] = "contract_items_satisfied",
        ["contractitems"] = "contract_items_satisfied",
        ["contractchecks"] = "contract_items_satisfied",
        ["testsrun"] = "tests_run",
        ["validationrun"] = "tests_run",
        ["validationtestsrun"] = "tests_run",
        ["evidencepaths"] = "evidence_paths",
        ["evidence"] = "evidence_paths",
        ["knownlimitations"] = "known_limitations",
        ["limitations"] = "known_limitations",
        ["nextrecommendation"] = "next_recommendation",
        ["recommendation"] = "next_recommendation",
    };

    public static WorkerExecutionResult Attach(WorkerRequest request, WorkerExecutionResult result)
    {
        var workerPacket = ResolveWorkerExecutionPacket(request.ExecutionRequest);
        var contract = request.ExecutionRequest?.Packet?.ClosureContract;
        var claim = Extract(result, workerPacket, contract);
        var autoCompletedClaim = TryCompleteFromAdapterObservations(request, result, workerPacket, contract, claim);
        return result with { CompletionClaim = autoCompletedClaim ?? claim };
    }

    public static WorkerCompletionClaim Extract(WorkerExecutionResult result, ExecutionPacketClosureContract? contract)
    {
        return Extract(result, workerPacket: null, contract);
    }

    public static WorkerCompletionClaim Extract(
        WorkerExecutionResult result,
        WorkerExecutionPacket? workerPacket,
        ExecutionPacketClosureContract? contract = null)
    {
        var hasWorkerPacket = !string.IsNullOrWhiteSpace(workerPacket?.PacketId);
        var completionClaimRequired = hasWorkerPacket
            ? workerPacket!.CompletionClaimSchema.Required
            : contract?.CompletionClaimRequired == true;
        if (!completionClaimRequired || !result.Succeeded)
        {
            return WorkerCompletionClaim.None;
        }

        var declaredFields = hasWorkerPacket
            ? workerPacket!.CompletionClaimSchema.Fields
            : contract?.CompletionClaimFields ?? Array.Empty<string>();
        var requiredFields = (declaredFields.Count == 0 ? DefaultRequiredFields : declaredFields)
            .Select(NormalizeCanonicalField)
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var source = CombineSources(result.Rationale, result.ResponsePreview, result.Summary);
        if (string.IsNullOrWhiteSpace(source))
        {
            return MissingClaim(
                requiredFields,
                workerPacket,
                hasWorkerPacket,
                "Worker result did not include a response body to parse for completion claim fields.");
        }

        var parsed = ParseLabeledFields(source);
        var presentFields = requiredFields
            .Where(field => parsed.ContainsKey(field))
            .ToArray();
        var missingFields = requiredFields
            .Except(presentFields, StringComparer.Ordinal)
            .ToArray();
        var changedFiles = ValuesFor(parsed, "changed_files", fallback: result.ChangedFiles);
        var contractItemsSatisfied = ValuesFor(parsed, "contract_items_satisfied");
        var packetValidation = EvaluatePacketValidation(workerPacket, source, missingFields, changedFiles, contractItemsSatisfied);
        var status = presentFields.Length == 0
            ? "missing"
            : missingFields.Length > 0
                ? "partial"
                : packetValidation.Status == "failed"
                    ? "invalid"
                    : "present";

        return new WorkerCompletionClaim
        {
            Required = true,
            Status = status,
            Source = hasWorkerPacket ? "worker_execution_packet" : "worker_response",
            PacketId = workerPacket?.PacketId,
            SourceExecutionPacketId = workerPacket?.SourceExecutionPacketId,
            ClaimIsTruth = workerPacket?.CompletionClaimSchema.ClaimIsTruth ?? false,
            HostValidationRequired = workerPacket?.CompletionClaimSchema.HostValidationRequired ?? true,
            PacketValidationStatus = packetValidation.Status,
            PacketValidationBlockers = packetValidation.Blockers,
            RequiredFields = requiredFields,
            PresentFields = presentFields,
            MissingFields = missingFields,
            ChangedFiles = changedFiles,
            ContractItemsSatisfied = contractItemsSatisfied,
            RequiredContractItems = packetValidation.RequiredContractItems,
            MissingContractItems = packetValidation.MissingContractItems,
            TestsRun = ValuesFor(parsed, "tests_run"),
            EvidencePaths = ValuesFor(parsed, "evidence_paths"),
            DisallowedChangedFiles = packetValidation.DisallowedChangedFiles,
            ForbiddenVocabularyHits = packetValidation.ForbiddenVocabularyHits,
            KnownLimitations = ValuesFor(parsed, "known_limitations"),
            NextRecommendation = FirstValueFor(parsed, "next_recommendation"),
            RawClaimPreview = Preview(source),
            RawClaimHash = Hash(source),
            Notes = BuildNotes(hasWorkerPacket, packetValidation),
        };
    }

    private static WorkerCompletionClaim MissingClaim(
        IReadOnlyList<string> requiredFields,
        WorkerExecutionPacket? workerPacket,
        bool hasWorkerPacket,
        string note)
    {
        return new WorkerCompletionClaim
        {
            Required = true,
            Status = "missing",
            Source = hasWorkerPacket ? "worker_execution_packet" : "worker_response",
            PacketId = workerPacket?.PacketId,
            SourceExecutionPacketId = workerPacket?.SourceExecutionPacketId,
            ClaimIsTruth = workerPacket?.CompletionClaimSchema.ClaimIsTruth ?? false,
            HostValidationRequired = workerPacket?.CompletionClaimSchema.HostValidationRequired ?? true,
            PacketValidationStatus = hasWorkerPacket ? "failed" : "not_evaluated",
            PacketValidationBlockers = hasWorkerPacket ? ["completion_claim_missing"] : Array.Empty<string>(),
            RequiredFields = requiredFields,
            MissingFields = requiredFields,
            Notes = [note],
        };
    }

    private static WorkerExecutionPacket? ResolveWorkerExecutionPacket(WorkerExecutionRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(request?.WorkerExecutionPacket.PacketId))
        {
            return request.WorkerExecutionPacket;
        }

        return !string.IsNullOrWhiteSpace(request?.Packet?.WorkerExecutionPacket.PacketId)
            ? request.Packet.WorkerExecutionPacket
            : null;
    }

    private static WorkerCompletionClaim? TryCompleteFromAdapterObservations(
        WorkerRequest request,
        WorkerExecutionResult result,
        WorkerExecutionPacket? workerPacket,
        ExecutionPacketClosureContract? contract,
        WorkerCompletionClaim claim)
    {
        if (workerPacket is null
            || string.IsNullOrWhiteSpace(workerPacket.PacketId)
            || !workerPacket.CompletionClaimSchema.Required
            || !result.Succeeded)
        {
            return null;
        }

        var autoSource = BuildAdapterCompletionClaimSource(request, result, workerPacket, claim);
        if (string.IsNullOrWhiteSpace(autoSource))
        {
            return null;
        }

        var completed = Extract(
            result with
            {
                Rationale = CombineSources(result.Rationale, result.ResponsePreview, result.Summary, autoSource),
            },
            workerPacket,
            contract);
        var source = string.Equals(claim.Status, "not_required", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(claim.Status, "missing", StringComparison.OrdinalIgnoreCase)
            ? "worker_execution_packet_adapter_generated"
            : "worker_execution_packet_adapter_completed";
        return completed with
        {
            Source = source,
            Notes =
            [
                .. completed.Notes,
                "Adapter completed the worker completion claim from WorkerExecutionPacket, changed-file observation, command trace, and deterministic evidence paths.",
                "Adapter-completed claim remains candidate evidence only; Host validation and Review closure remain authoritative.",
            ],
        };
    }

    private static string BuildAdapterCompletionClaimSource(
        WorkerRequest request,
        WorkerExecutionResult result,
        WorkerExecutionPacket workerPacket,
        WorkerCompletionClaim claim)
    {
        var changedFiles = PreferClaimValues(
            claim.ChangedFiles,
            result.ChangedFiles
                .Concat(result.ObservedChangedFiles)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray());
        var contractItems = claim.ContractItemsSatisfied
            .Concat(InferMechanicalContractItems(workerPacket, changedFiles))
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var testsRun = PreferClaimValues(
            claim.TestsRun,
            InferTestsRun(request, result));
        var evidencePaths = ResolveAdapterEvidencePaths(result, workerPacket, claim, changedFiles);
        var knownLimitations = PreferClaimValues(claim.KnownLimitations, ["not_declared"]);
        var nextRecommendation = string.IsNullOrWhiteSpace(claim.NextRecommendation)
            ? "submit for Host review"
            : claim.NextRecommendation;

        return string.Join(
            Environment.NewLine,
            "- changed_files: " + FormatValues(changedFiles),
            "- contract_items_satisfied: " + FormatValues(contractItems),
            "- tests_run: " + FormatValues(testsRun),
            "- evidence_paths: " + FormatValues(evidencePaths),
            "- known_limitations: " + FormatValues(knownLimitations),
            "- next_recommendation: " + nextRecommendation);
    }

    private static IReadOnlyList<string> InferMechanicalContractItems(
        WorkerExecutionPacket workerPacket,
        IReadOnlyList<string> changedFiles)
    {
        var inferred = new List<string>();
        foreach (var item in workerPacket.RequiredContractMatrix)
        {
            if (!IsWorkerClaimableContractItem(item))
            {
                continue;
            }

            if (string.Equals(item, "patch_scope_recorded", StringComparison.OrdinalIgnoreCase)
                && changedFiles.Count > 0)
            {
                inferred.Add(item);
                continue;
            }

            if (string.Equals(item, "scope_hygiene", StringComparison.OrdinalIgnoreCase)
                && changedFiles.All(path => IsAllowedPath(path, workerPacket.AllowedFiles)))
            {
                inferred.Add(item);
                continue;
            }

            if (string.Equals(item, "result_channel_recorded", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(workerPacket.ResultSubmission.CandidateResultChannel))
            {
                inferred.Add(item);
                continue;
            }

            if (string.Equals(item, "completion_claim_recorded", StringComparison.OrdinalIgnoreCase))
            {
                inferred.Add(item);
            }
        }

        return inferred;
    }

    private static IReadOnlyList<string> InferTestsRun(WorkerRequest request, WorkerExecutionResult result)
    {
        var workerCommands = result.CommandTrace
            .Select(static item => string.Join(' ', item.Command))
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .Where(static command => command.Contains("test", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (workerCommands.Length > 0)
        {
            return workerCommands;
        }

        var hostValidationCommands = request.ValidationCommands
            .Select(static command => string.Join(' ', command))
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .Where(static command => command.Contains("test", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return hostValidationCommands.Length > 0
            ? hostValidationCommands.Select(static command => $"host_validation_pending:{command}").ToArray()
            : ["host_validation_pending"];
    }

    private static IReadOnlyList<string> ResolveAdapterEvidencePaths(
        WorkerExecutionResult result,
        WorkerExecutionPacket workerPacket,
        WorkerCompletionClaim claim,
        IReadOnlyList<string> changedFiles)
    {
        var declared = claim.EvidencePaths
            .Where(IsConcreteEvidencePath)
            .ToArray();
        if (declared.Length > 0)
        {
            return declared;
        }

        var runRoot = $".ai/artifacts/worker-executions/{result.RunId}";
        var evidence = new List<string>
        {
            $"{runRoot}/evidence.json",
        };
        if (result.CommandTrace.Count > 0)
        {
            evidence.Add($"{runRoot}/command.log");
        }

        if (result.CommandTrace.Any(item => string.Join(' ', item.Command).Contains("test", StringComparison.OrdinalIgnoreCase)))
        {
            evidence.Add($"{runRoot}/test.log");
        }

        if (changedFiles.Count > 0)
        {
            evidence.Add($"{runRoot}/patch.diff");
        }

        if (!string.IsNullOrWhiteSpace(workerPacket.ResultSubmission.CandidateResultChannel))
        {
            evidence.Add(workerPacket.ResultSubmission.CandidateResultChannel);
        }

        return evidence.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsConcreteEvidencePath(string value)
    {
        var normalized = NormalizePath(value);
        return !string.IsNullOrWhiteSpace(normalized)
               && (normalized.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
                   || normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
                   || normalized.StartsWith("artifacts/", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> PreferClaimValues(
        IReadOnlyList<string> claimValues,
        IReadOnlyList<string> fallbackValues)
    {
        var concreteClaimValues = claimValues
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Where(static value => !string.Equals(value, "worker execution artifact", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return concreteClaimValues.Length > 0
            ? concreteClaimValues
            : fallbackValues.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
    }

    private static string FormatValues(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? string.Empty : string.Join("; ", values);
    }

    private static WorkerCompletionClaimPacketValidation EvaluatePacketValidation(
        WorkerExecutionPacket? workerPacket,
        string rawClaim,
        IReadOnlyList<string> missingFields,
        IReadOnlyList<string> changedFiles,
        IReadOnlyList<string> contractItemsSatisfied)
    {
        if (string.IsNullOrWhiteSpace(workerPacket?.PacketId))
        {
            return new WorkerCompletionClaimPacketValidation("not_evaluated");
        }

        var normalizedSatisfied = contractItemsSatisfied
            .Select(NormalizeClaimValue)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requiredContractItems = workerPacket.RequiredContractMatrix
            .Select(NormalizeClaimValue)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Where(IsWorkerClaimableContractItem)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var missingContractItems = requiredContractItems
            .Where(required => !normalizedSatisfied.Contains(required, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var disallowedChangedFiles = changedFiles
            .Where(path => !IsAllowedPath(path, workerPacket.AllowedFiles))
            .ToArray();
        var forbiddenVocabularyHits = workerPacket.ForbiddenVocabulary
            .Where(term => ContainsForbiddenVocabulary(rawClaim, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var blockers = new List<string>();
        blockers.AddRange(missingFields.Select(field => $"completion_claim_missing_field:{field}"));
        blockers.AddRange(missingContractItems.Select(item => $"completion_claim_missing_contract_item:{item}"));
        blockers.AddRange(disallowedChangedFiles.Select(path => $"completion_claim_disallowed_changed_file:{path}"));
        blockers.AddRange(forbiddenVocabularyHits.Select(term => $"completion_claim_forbidden_vocabulary:{term}"));

        return new WorkerCompletionClaimPacketValidation(
            blockers.Count == 0 ? "passed" : "failed",
            blockers.ToArray(),
            requiredContractItems,
            missingContractItems,
            disallowedChangedFiles,
            forbiddenVocabularyHits);
    }

    private static IReadOnlyList<string> BuildNotes(
        bool hasWorkerPacket,
        WorkerCompletionClaimPacketValidation packetValidation)
    {
        var notes = new List<string>
        {
            "Worker completion claim is a worker declaration only; Host validation and Review closure remain authoritative.",
        };
        notes.Add(hasWorkerPacket
            ? $"Worker completion claim was validated against WorkerExecutionPacket; packet_validation={packetValidation.Status}."
            : "Worker completion claim was parsed without a WorkerExecutionPacket; packet validation was not evaluated.");
        return notes;
    }

    private static Dictionary<string, IReadOnlyList<string>> ParseLabeledFields(string text)
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var rawLine in text
                     .Replace("\\r\\n", "\n", StringComparison.Ordinal)
                     .Replace("\\n", "\n", StringComparison.Ordinal)
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            line = line.TrimStart('-', '*').Trim();
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var label = ResolveField(line[..separatorIndex]);
            if (label is null)
            {
                continue;
            }

            fields[label] = SplitValues(line[(separatorIndex + 1)..]);
        }

        return fields;
    }

    private static string? ResolveField(string label)
    {
        var normalized = new string(label
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (FieldAliases.TryGetValue(normalized, out var canonical))
        {
            return canonical;
        }

        canonical = NormalizeCanonicalField(label);
        return string.IsNullOrWhiteSpace(canonical) ? null : canonical;
    }

    private static string NormalizeCanonicalField(string field)
    {
        var normalized = field.Trim().Trim('`').Replace("-", "_", StringComparison.Ordinal).Replace(" ", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "changed_files" or "contract_items_satisfied" or "tests_run" or "evidence_paths" or "known_limitations" or "next_recommendation" => normalized,
            _ => string.Empty,
        };
    }

    private static string NormalizeClaimValue(string value)
    {
        return value.Trim().Trim('`').Trim();
    }

    private static bool IsWorkerClaimableContractItem(string item)
    {
        return !string.Equals(item, "review_artifact_present", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(item, "validation_recorded", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(item, "safety_recorded", StringComparison.OrdinalIgnoreCase)
               && !string.Equals(item, "roundtrip_validation_evidence", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowedPath(string path, IReadOnlyList<string> allowedFiles)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath) || allowedFiles.Count == 0)
        {
            return true;
        }

        return allowedFiles
            .Select(NormalizePath)
            .Where(static allowed => !string.IsNullOrWhiteSpace(allowed))
            .Any(allowed =>
                string.Equals(normalizedPath, allowed, StringComparison.OrdinalIgnoreCase)
                || (allowed.EndsWith("/", StringComparison.Ordinal)
                    && normalizedPath.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                || normalizedPath.StartsWith($"{allowed}/", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Trim('`').Replace('\\', '/');
    }

    private static bool ContainsForbiddenVocabulary(string text, string term)
    {
        var normalizedTerm = term.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTerm))
        {
            return false;
        }

        return ExtractTokens(text)
            .Any(token => string.Equals(token, normalizedTerm, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ExtractTokens(string text)
    {
        var current = new StringBuilder();
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                current.Append(character);
                continue;
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static IReadOnlyList<string> ValuesFor(
        IReadOnlyDictionary<string, IReadOnlyList<string>> fields,
        string key,
        IReadOnlyList<string>? fallback = null)
    {
        return fields.TryGetValue(key, out var values)
            ? values
            : fallback ?? Array.Empty<string>();
    }

    private static string FirstValueFor(IReadOnlyDictionary<string, IReadOnlyList<string>> fields, string key)
    {
        return fields.TryGetValue(key, out var values)
            ? values.FirstOrDefault() ?? string.Empty
            : string.Empty;
    }

    private static IReadOnlyList<string> SplitValues(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return Array.Empty<string>();
        }

        return trimmed
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Trim().Trim('`').Trim())
            .Where(value => value.Length > 0)
            .ToArray();
    }

    private static string Preview(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static string Hash(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string CombineSources(params string?[] values)
    {
        return string.Join(
            Environment.NewLine,
            values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!.Trim())
                .Distinct(StringComparer.Ordinal));
    }

    private sealed class WorkerCompletionClaimPacketValidation
    {
        public WorkerCompletionClaimPacketValidation(
            string status,
            IReadOnlyList<string>? blockers = null,
            IReadOnlyList<string>? requiredContractItems = null,
            IReadOnlyList<string>? missingContractItems = null,
            IReadOnlyList<string>? disallowedChangedFiles = null,
            IReadOnlyList<string>? forbiddenVocabularyHits = null)
        {
            Status = status;
            Blockers = blockers ?? Array.Empty<string>();
            RequiredContractItems = requiredContractItems ?? Array.Empty<string>();
            MissingContractItems = missingContractItems ?? Array.Empty<string>();
            DisallowedChangedFiles = disallowedChangedFiles ?? Array.Empty<string>();
            ForbiddenVocabularyHits = forbiddenVocabularyHits ?? Array.Empty<string>();
        }

        public string Status { get; }

        public IReadOnlyList<string> Blockers { get; }

        public IReadOnlyList<string> RequiredContractItems { get; }

        public IReadOnlyList<string> MissingContractItems { get; }

        public IReadOnlyList<string> DisallowedChangedFiles { get; }

        public IReadOnlyList<string> ForbiddenVocabularyHits { get; }
    }
}
