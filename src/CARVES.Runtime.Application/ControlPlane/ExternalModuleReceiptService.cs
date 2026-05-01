using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ExternalModuleReceiptService
{
    public const string ReceiptSchema = "carves.external_module_receipt.v0.98-rc.p8";
    public const string ReceiptRegistryVersion = "external-module-adapter-registry@0.98-rc.p8";
    public const string ProviderUntrustedStopReason = "SC-PROVIDER-UNTRUSTED";
    public const string ToolOutputUnverifiedStopReason = "SC-TOOL-OUTPUT-UNVERIFIED";
    public const string UnknownModuleStopReason = "SC-EXTERNAL-MODULE-UNKNOWN";
    public const string ReceiptTamperedStopReason = "SC-EXTERNAL-MODULE-RECEIPT-TAMPERED";
    public const string ReceiptMissingStopReason = "SC-EXTERNAL-MODULE-RECEIPT-MISSING";
    public const string DuplicateModuleStopReason = "SC-EXTERNAL-MODULE-DUPLICATE";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly IReadOnlyList<ExternalModuleAdapterDefinition> DefaultDefinitions =
    [
        Module("guard", "CARVES.Guard", "adapter.guard", "carves.guard.input_contract.v0.98-rc", "guard_report", "carves.guard.verdict.v0.98-rc"),
        Module("handoff", "CARVES.Handoff", "adapter.handoff", "carves.handoff.input_contract.v0.98-rc", "handoff_packet", "carves.handoff.verdict.v0.98-rc"),
        Module("matrix", "CARVES.Matrix", "adapter.matrix", "carves.matrix.input_contract.v0.98-rc", "matrix_decision", "carves.matrix.verdict.v0.98-rc"),
        Module("audit", "CARVES.Audit", "adapter.audit", "carves.audit.input_contract.v0.98-rc", "audit_report", "carves.audit.verdict.v0.98-rc"),
        Module("shield", "CARVES.Shield", "adapter.shield", "carves.shield.input_contract.v0.98-rc", "shield_report", "carves.shield.verdict.v0.98-rc"),
        Module("codegraph_projection", "CARVES.CodeGraphProjection", "adapter.codegraph_projection", "carves.codegraph_projection.input_contract.v0.98-rc", "codegraph_projection", "carves.codegraph_projection.verdict.v0.98-rc"),
        Module("memory_proposalizer", "CARVES.MemoryProposalizer", "adapter.memory_proposalizer", "carves.memory_proposalizer.input_contract.v0.98-rc", "memory_observation_projection", "carves.memory_proposalizer.verdict.v0.98-rc"),
    ];

    private readonly ControlPlanePaths paths;

    public ExternalModuleReceiptService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public ExternalModuleAdapterRegistry BuildRegistry()
    {
        return new ExternalModuleAdapterRegistry
        {
            RegistryVersion = ReceiptRegistryVersion,
            Modules = DefaultDefinitions,
        };
    }

    public ExternalModuleReceiptStoreResult CallAndStoreReceipt(
        ExternalModuleCallRequest request,
        Func<ExternalModuleInvocationContext, ExternalModuleInvocationResult> invoke)
    {
        var registry = BuildRegistry();
        var definition = registry.Modules.FirstOrDefault(module => string.Equals(module.ModuleId, request.ModuleId, StringComparison.Ordinal));
        if (definition is null)
        {
            return ExternalModuleReceiptStoreResult.Block(
                request.ModuleId,
                [UnknownModuleStopReason],
                $"External module '{request.ModuleId}' is not registered.");
        }

        var inputContractHash = ComputeInputContractHash(request.InputContractJson);
        var invocation = invoke(new ExternalModuleInvocationContext(
            definition.ModuleId,
            definition.ModuleOwner,
            definition.CapabilityId,
            definition.InputContractSchema,
            inputContractHash,
            request.InputContractJson));
        return StoreReceipt(new ExternalModuleReceiptStoreRequest
        {
            WorkOrderId = request.WorkOrderId,
            TransactionId = request.TransactionId,
            OperationId = request.OperationId,
            ModuleId = definition.ModuleId,
            InputContractJson = request.InputContractJson,
            OutputArtifactPath = invocation.OutputArtifactPath,
            OutputArtifactHash = invocation.OutputArtifactHash,
            Verdict = invocation.Verdict,
            VerdictSchema = invocation.VerdictSchema,
            TrustLevel = invocation.TrustLevel,
            Summary = invocation.Summary,
            Facts = invocation.Facts,
        });
    }

    public ExternalModuleReceiptStoreResult StoreReceipt(ExternalModuleReceiptStoreRequest request)
    {
        var registry = BuildRegistry();
        var definition = registry.Modules.FirstOrDefault(module => string.Equals(module.ModuleId, request.ModuleId, StringComparison.Ordinal));
        if (definition is null)
        {
            return ExternalModuleReceiptStoreResult.Block(
                request.ModuleId,
                [UnknownModuleStopReason],
                $"External module '{request.ModuleId}' is not registered.");
        }

        var outputPath = NormalizeOptional(request.OutputArtifactPath);
        var actualOutputHash = File.Exists(ResolvePath(outputPath))
            ? HashFile(ResolvePath(outputPath))
            : null;
        var expectedOutputHash = NormalizeOptional(request.OutputArtifactHash);
        var outputVerified = !string.IsNullOrWhiteSpace(actualOutputHash)
            && string.Equals(actualOutputHash, expectedOutputHash, StringComparison.Ordinal);
        var inputContractHash = ComputeInputContractHash(request.InputContractJson);
        var trustLevel = request.TrustLevel ?? definition.DefaultTrustLevel;
        var stopReasons = ResolveStopReasons(outputVerified, trustLevel);
        var receiptState = ResolveReceiptState(outputVerified, trustLevel);
        var disposition = ResolveDisposition(outputVerified, trustLevel);
        var receipt = new ExternalModuleReceiptRecord
        {
            ReceiptId = $"emr-{Guid.NewGuid():N}",
            WorkOrderId = NormalizeOptional(request.WorkOrderId),
            TransactionId = NormalizeOptional(request.TransactionId),
            OperationId = NormalizeOptional(request.OperationId) ?? $"external_module:{definition.ModuleId}",
            ModuleId = definition.ModuleId,
            ModuleOwner = definition.ModuleOwner,
            CapabilityId = definition.CapabilityId,
            InputContractSchema = definition.InputContractSchema,
            InputContractHash = inputContractHash,
            OutputArtifactKind = definition.OutputArtifactKind,
            OutputArtifactPath = ToRepoRelative(outputPath),
            ExpectedOutputArtifactHash = expectedOutputHash,
            OutputArtifactHash = actualOutputHash,
            OutputArtifactVerified = outputVerified,
            Verdict = NormalizeOptional(request.Verdict) ?? "unknown",
            VerdictSchema = NormalizeOptional(request.VerdictSchema) ?? definition.VerdictSchema,
            TrustLevel = trustLevel,
            ReceiptReplayRule = definition.ReceiptReplayRule,
            GovernanceBoundary = definition.GovernanceBoundary,
            ReceiptState = receiptState,
            TransactionDisposition = disposition,
            StopReasons = stopReasons,
            Summary = NormalizeOptional(request.Summary) ?? "External module receipt recorded.",
            Facts = request.Facts,
            IssuedAtUtc = DateTimeOffset.UtcNow,
        };
        receipt.ReceiptHash = ComputeReceiptHash(receipt);
        var receiptPath = GetReceiptPath(receipt.WorkOrderId ?? "unbound", definition.ModuleId, receipt.ReceiptId);
        Directory.CreateDirectory(Path.GetDirectoryName(receiptPath)!);
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt, JsonOptions));

        return new ExternalModuleReceiptStoreResult(
            receiptState == "verified",
            receiptState == "untrusted",
            receiptState == "unverifiable",
            receiptPath,
            ToRepoRelative(receiptPath),
            receipt,
            BuildDecisionCitation(receipt),
            stopReasons,
            disposition,
            receipt.Summary);
    }

    public ExternalModuleReceiptReplayResult ReplayReceipt(string receiptPath)
    {
        if (!File.Exists(ResolvePath(receiptPath)))
        {
            return ExternalModuleReceiptReplayResult.Broken(
                ToRepoRelative(receiptPath),
                [ToolOutputUnverifiedStopReason],
                $"External module receipt '{receiptPath}' does not exist.");
        }

        var fullReceiptPath = ResolvePath(receiptPath);
        var receipt = JsonSerializer.Deserialize<ExternalModuleReceiptRecord>(File.ReadAllText(fullReceiptPath), JsonOptions);
        if (receipt is null)
        {
            return ExternalModuleReceiptReplayResult.Broken(
                ToRepoRelative(fullReceiptPath),
                [ReceiptTamperedStopReason],
                "External module receipt could not be parsed.");
        }

        var expectedReceiptHash = ComputeReceiptHash(receipt);
        if (!string.Equals(expectedReceiptHash, receipt.ReceiptHash, StringComparison.Ordinal))
        {
            return ExternalModuleReceiptReplayResult.Broken(
                ToRepoRelative(fullReceiptPath),
                [ReceiptTamperedStopReason],
                "External module receipt hash does not match its payload.");
        }

        var outputPath = ResolvePath(receipt.OutputArtifactPath);
        var actualOutputHash = File.Exists(outputPath) ? HashFile(outputPath) : null;
        var outputVerified = !string.IsNullOrWhiteSpace(actualOutputHash)
            && string.Equals(actualOutputHash, receipt.OutputArtifactHash, StringComparison.Ordinal)
            && string.Equals(receipt.OutputArtifactHash, receipt.ExpectedOutputArtifactHash, StringComparison.Ordinal);
        var stopReasons = ResolveStopReasons(outputVerified, receipt.TrustLevel);
        var receiptState = ResolveReceiptState(outputVerified, receipt.TrustLevel);
        var disposition = ResolveDisposition(outputVerified, receipt.TrustLevel);
        var citation = BuildDecisionCitation(receipt);

        return new ExternalModuleReceiptReplayResult(
            receiptState,
            outputVerified,
            receiptState == "verified",
            receiptState == "untrusted",
            receiptState == "unverifiable",
            ToRepoRelative(fullReceiptPath),
            receipt,
            citation,
            stopReasons,
            disposition,
            receiptState == "verified"
                ? "External module receipt replay verified the receipt hash and output artifact hash."
                : receiptState == "untrusted"
                    ? "External module receipt replay verified hashes but trust policy requires downgrade."
                    : "External module receipt replay could not verify the output artifact hash.");
    }

    public ExternalModuleReceiptDecision EvaluateForDecision(IReadOnlyList<ExternalModuleReceiptReplayResult> receipts)
    {
        var citedHashes = receipts
            .Where(static receipt => !string.IsNullOrWhiteSpace(receipt.Citation.ReceiptHash))
            .Select(static receipt => receipt.Citation.ReceiptHash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var stopReasons = receipts.SelectMany(static receipt => receipt.StopReasons).Distinct(StringComparer.Ordinal).ToArray();
        var disposition = receipts.Any(static receipt => receipt.ShouldStop)
            ? "stop_transaction"
            : receipts.Any(static receipt => receipt.ShouldDowngrade)
                ? "downgrade_transaction"
                : "cite_receipts";

        return new ExternalModuleReceiptDecision(citedHashes, stopReasons, disposition);
    }

    public ExternalModuleReceiptVerificationResult VerifyReceipts(ExternalModuleReceiptVerificationRequest request)
    {
        var receiptPaths = request.ReceiptPaths
            .Select(NormalizeOptional)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var requiredModuleIds = request.RequiredModuleIds
            .Select(NormalizeOptional)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (receiptPaths.Length == 0 && requiredModuleIds.Length == 0)
        {
            return ExternalModuleReceiptVerificationResult.NotRequired();
        }

        var registry = BuildRegistry();
        var registryModules = registry.Modules
            .Select(static module => module.ModuleId)
            .ToHashSet(StringComparer.Ordinal);
        var replays = receiptPaths
            .Select(static receiptPath => receiptPath!)
            .Select(ReplayReceipt)
            .ToArray();
        var verificationStopReasons = new List<string>();
        var seenModules = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requiredModuleId in requiredModuleIds)
        {
            if (requiredModuleId is null || !registryModules.Contains(requiredModuleId))
            {
                verificationStopReasons.Add(UnknownModuleStopReason);
            }
        }

        foreach (var replay in replays)
        {
            var moduleId = replay.Receipt?.ModuleId;
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                continue;
            }

            if (!registryModules.Contains(moduleId))
            {
                verificationStopReasons.Add(UnknownModuleStopReason);
                continue;
            }

            if (!seenModules.Add(moduleId))
            {
                verificationStopReasons.Add(DuplicateModuleStopReason);
            }
        }

        var foundModules = replays
            .Select(static replay => replay.Receipt?.ModuleId)
            .Where(static moduleId => !string.IsNullOrWhiteSpace(moduleId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var requiredModuleId in requiredModuleIds)
        {
            if (requiredModuleId is null || !foundModules.Contains(requiredModuleId))
            {
                verificationStopReasons.Add(ReceiptMissingStopReason);
            }
        }

        var decision = EvaluateForDecision(replays);
        var stopReasons = decision.StopReasons
            .Concat(verificationStopReasons)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var disposition = verificationStopReasons.Count != 0 || replays.Any(static replay => replay.ShouldStop)
            ? "stop_transaction"
            : decision.TransactionDisposition;
        return new ExternalModuleReceiptVerificationResult(
            receiptPaths.Length != 0,
            disposition,
            replays,
            decision.CitedReceiptHashes,
            stopReasons,
            disposition switch
            {
                "cite_receipts" => "External module receipts replayed and can be cited for Runtime governance decisions.",
                "downgrade_transaction" => "External module receipts replayed, but trust policy requires the Runtime decision to downgrade.",
                _ => "External module receipts did not satisfy the verified adapter receipt contract.",
            });
    }

    public string HashFile(string path)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ResolvePath(path)))).ToLowerInvariant();
    }

    public string GetReceiptPath(string workOrderId, string moduleId, string receiptId)
    {
        return Path.Combine(
            paths.RuntimeRoot,
            "external-module-receipts",
            Sanitize(workOrderId),
            Sanitize(moduleId),
            $"{Sanitize(receiptId)}.json");
    }

    private static ExternalModuleAdapterDefinition Module(
        string moduleId,
        string moduleOwner,
        string capabilityId,
        string inputContractSchema,
        string outputArtifactKind,
        string verdictSchema)
    {
        return new ExternalModuleAdapterDefinition(
            moduleId,
            moduleOwner,
            capabilityId,
            inputContractSchema,
            outputArtifactKind,
            verdictSchema,
            ExternalModuleReceiptTrustLevel.Trusted,
            "receipt_hash_and_output_artifact_hash_must_replay",
            "receipt_only_no_internal_rules");
    }

    private static IReadOnlyList<string> ResolveStopReasons(bool outputVerified, ExternalModuleReceiptTrustLevel trustLevel)
    {
        if (!outputVerified)
        {
            return [ToolOutputUnverifiedStopReason];
        }

        return trustLevel == ExternalModuleReceiptTrustLevel.Untrusted
            ? [ProviderUntrustedStopReason]
            : [];
    }

    private static string ResolveReceiptState(bool outputVerified, ExternalModuleReceiptTrustLevel trustLevel)
    {
        if (!outputVerified)
        {
            return "unverifiable";
        }

        return trustLevel == ExternalModuleReceiptTrustLevel.Untrusted
            ? "untrusted"
            : "verified";
    }

    private static string ResolveDisposition(bool outputVerified, ExternalModuleReceiptTrustLevel trustLevel)
    {
        if (!outputVerified)
        {
            return "stop_transaction";
        }

        return trustLevel == ExternalModuleReceiptTrustLevel.Untrusted
            ? "downgrade_transaction"
            : "cite_receipt";
    }

    private static ExternalModuleDecisionCitation BuildDecisionCitation(ExternalModuleReceiptRecord receipt)
    {
        return new ExternalModuleDecisionCitation(
            receipt.ModuleId,
            receipt.ModuleOwner,
            receipt.ReceiptHash,
            receipt.OutputArtifactHash,
            receipt.Verdict,
            receipt.TransactionDisposition);
    }

    private static string ComputeInputContractHash(string inputContractJson)
    {
        var normalized = NormalizeJson(inputContractJson);
        return ComputeContentHash(normalized);
    }

    private static string ComputeReceiptHash(ExternalModuleReceiptRecord receipt)
    {
        return ComputeContentHash(JsonSerializer.Serialize(
            new ExternalModuleReceiptHashPayload
            {
                Schema = receipt.Schema,
                ReceiptId = receipt.ReceiptId,
                WorkOrderId = receipt.WorkOrderId,
                TransactionId = receipt.TransactionId,
                OperationId = receipt.OperationId,
                ModuleId = receipt.ModuleId,
                ModuleOwner = receipt.ModuleOwner,
                CapabilityId = receipt.CapabilityId,
                InputContractSchema = receipt.InputContractSchema,
                InputContractHash = receipt.InputContractHash,
                OutputArtifactKind = receipt.OutputArtifactKind,
                OutputArtifactPath = receipt.OutputArtifactPath,
                ExpectedOutputArtifactHash = receipt.ExpectedOutputArtifactHash,
                OutputArtifactHash = receipt.OutputArtifactHash,
                OutputArtifactVerified = receipt.OutputArtifactVerified,
                Verdict = receipt.Verdict,
                VerdictSchema = receipt.VerdictSchema,
                TrustLevel = receipt.TrustLevel,
                ReceiptReplayRule = receipt.ReceiptReplayRule,
                GovernanceBoundary = receipt.GovernanceBoundary,
                ReceiptState = receipt.ReceiptState,
                TransactionDisposition = receipt.TransactionDisposition,
                StopReasons = receipt.StopReasons,
                Summary = receipt.Summary,
                Facts = receipt.Facts,
                IssuedAtUtc = receipt.IssuedAtUtc,
            },
            JsonOptions));
    }

    private static string NormalizeJson(string json)
    {
        try
        {
            return JsonNode.Parse(json)?.ToJsonString(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false,
            }) ?? json.Trim();
        }
        catch (JsonException)
        {
            return json.Trim();
        }
    }

    private static string ComputeContentHash(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private string ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(paths.RepoRoot, path.Replace('/', Path.DirectorySeparatorChar));
    }

    private string ToRepoRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var fullPath = ResolvePath(path);
        var relative = Path.GetRelativePath(paths.RepoRoot, fullPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string Sanitize(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "unbound" : value.Trim();
        return new string(normalized
            .Select(static item => char.IsLetterOrDigit(item) || item is '-' or '_' or '.' ? item : '-')
            .ToArray());
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed class ExternalModuleAdapterRegistry
{
    public string Schema { get; init; } = "carves.external_module_adapter_registry.v0.98-rc.p8";

    public string RegistryVersion { get; init; } = string.Empty;

    public IReadOnlyList<ExternalModuleAdapterDefinition> Modules { get; init; } = [];
}

public sealed record ExternalModuleAdapterDefinition(
    string ModuleId,
    string ModuleOwner,
    string CapabilityId,
    string InputContractSchema,
    string OutputArtifactKind,
    string VerdictSchema,
    ExternalModuleReceiptTrustLevel DefaultTrustLevel,
    string ReceiptReplayRule,
    string GovernanceBoundary);

public sealed record ExternalModuleCallRequest
{
    public string? WorkOrderId { get; init; }

    public string? TransactionId { get; init; }

    public string? OperationId { get; init; }

    public string ModuleId { get; init; } = string.Empty;

    public string InputContractJson { get; init; } = "{}";
}

public sealed record ExternalModuleReceiptVerificationRequest
{
    public IReadOnlyList<string> ReceiptPaths { get; init; } = [];

    public IReadOnlyList<string> RequiredModuleIds { get; init; } = [];
}

public sealed record ExternalModuleInvocationContext(
    string ModuleId,
    string ModuleOwner,
    string CapabilityId,
    string InputContractSchema,
    string InputContractHash,
    string InputContractJson);

public sealed record ExternalModuleInvocationResult(
    string OutputArtifactPath,
    string OutputArtifactHash,
    string Verdict,
    string VerdictSchema,
    ExternalModuleReceiptTrustLevel TrustLevel,
    string Summary,
    IReadOnlyDictionary<string, string?> Facts);

public sealed record ExternalModuleReceiptStoreRequest
{
    public string? WorkOrderId { get; init; }

    public string? TransactionId { get; init; }

    public string? OperationId { get; init; }

    public string ModuleId { get; init; } = string.Empty;

    public string InputContractJson { get; init; } = "{}";

    public string? OutputArtifactPath { get; init; }

    public string? OutputArtifactHash { get; init; }

    public string? Verdict { get; init; }

    public string? VerdictSchema { get; init; }

    public ExternalModuleReceiptTrustLevel? TrustLevel { get; init; }

    public string? Summary { get; init; }

    public IReadOnlyDictionary<string, string?> Facts { get; init; } = new Dictionary<string, string?>();
}

public sealed class ExternalModuleReceiptRecord
{
    public string Schema { get; init; } = ExternalModuleReceiptService.ReceiptSchema;

    public string ReceiptId { get; init; } = string.Empty;

    public string? WorkOrderId { get; init; }

    public string? TransactionId { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public string ModuleId { get; init; } = string.Empty;

    public string ModuleOwner { get; init; } = string.Empty;

    public string CapabilityId { get; init; } = string.Empty;

    public string InputContractSchema { get; init; } = string.Empty;

    public string InputContractHash { get; init; } = string.Empty;

    public string OutputArtifactKind { get; init; } = string.Empty;

    public string OutputArtifactPath { get; init; } = string.Empty;

    public string? ExpectedOutputArtifactHash { get; init; }

    public string? OutputArtifactHash { get; init; }

    public bool OutputArtifactVerified { get; init; }

    public string Verdict { get; init; } = string.Empty;

    public string VerdictSchema { get; init; } = string.Empty;

    public ExternalModuleReceiptTrustLevel TrustLevel { get; init; } = ExternalModuleReceiptTrustLevel.Trusted;

    public string ReceiptReplayRule { get; init; } = string.Empty;

    public string GovernanceBoundary { get; init; } = string.Empty;

    public string ReceiptState { get; init; } = string.Empty;

    public string TransactionDisposition { get; init; } = string.Empty;

    public IReadOnlyList<string> StopReasons { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string?> Facts { get; init; } = new Dictionary<string, string?>();

    public DateTimeOffset IssuedAtUtc { get; init; }

    public string ReceiptHash { get; set; } = string.Empty;
}

public sealed class ExternalModuleReceiptHashPayload
{
    public string Schema { get; init; } = string.Empty;

    public string ReceiptId { get; init; } = string.Empty;

    public string? WorkOrderId { get; init; }

    public string? TransactionId { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public string ModuleId { get; init; } = string.Empty;

    public string ModuleOwner { get; init; } = string.Empty;

    public string CapabilityId { get; init; } = string.Empty;

    public string InputContractSchema { get; init; } = string.Empty;

    public string InputContractHash { get; init; } = string.Empty;

    public string OutputArtifactKind { get; init; } = string.Empty;

    public string OutputArtifactPath { get; init; } = string.Empty;

    public string? ExpectedOutputArtifactHash { get; init; }

    public string? OutputArtifactHash { get; init; }

    public bool OutputArtifactVerified { get; init; }

    public string Verdict { get; init; } = string.Empty;

    public string VerdictSchema { get; init; } = string.Empty;

    public ExternalModuleReceiptTrustLevel TrustLevel { get; init; } = ExternalModuleReceiptTrustLevel.Trusted;

    public string ReceiptReplayRule { get; init; } = string.Empty;

    public string GovernanceBoundary { get; init; } = string.Empty;

    public string ReceiptState { get; init; } = string.Empty;

    public string TransactionDisposition { get; init; } = string.Empty;

    public IReadOnlyList<string> StopReasons { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string?> Facts { get; init; } = new Dictionary<string, string?>();

    public DateTimeOffset IssuedAtUtc { get; init; }
}

public enum ExternalModuleReceiptTrustLevel
{
    Trusted,
    Untrusted,
}

public sealed record ExternalModuleDecisionCitation(
    string ModuleId,
    string ModuleOwner,
    string ReceiptHash,
    string? OutputArtifactHash,
    string Verdict,
    string TransactionDisposition);

public sealed record ExternalModuleReceiptStoreResult(
    bool Verified,
    bool ShouldDowngrade,
    bool ShouldStop,
    string? ReceiptPath,
    string? ReceiptRelativePath,
    ExternalModuleReceiptRecord? Receipt,
    ExternalModuleDecisionCitation Citation,
    IReadOnlyList<string> StopReasons,
    string TransactionDisposition,
    string Summary)
{
    public static ExternalModuleReceiptStoreResult Block(
        string moduleId,
        IReadOnlyList<string> stopReasons,
        string summary)
    {
        return new ExternalModuleReceiptStoreResult(
            false,
            false,
            true,
            null,
            null,
            null,
            new ExternalModuleDecisionCitation(moduleId, string.Empty, string.Empty, null, "unknown", "stop_transaction"),
            stopReasons,
            "stop_transaction",
            summary);
    }
}

public sealed record ExternalModuleReceiptReplayResult(
    string ReplayState,
    bool OutputArtifactVerified,
    bool Verified,
    bool ShouldDowngrade,
    bool ShouldStop,
    string ReceiptRelativePath,
    ExternalModuleReceiptRecord? Receipt,
    ExternalModuleDecisionCitation Citation,
    IReadOnlyList<string> StopReasons,
    string TransactionDisposition,
    string Summary)
{
    public static ExternalModuleReceiptReplayResult Broken(
        string receiptRelativePath,
        IReadOnlyList<string> stopReasons,
        string summary)
    {
        return new ExternalModuleReceiptReplayResult(
            "broken",
            false,
            false,
            false,
            true,
            receiptRelativePath,
            null,
            new ExternalModuleDecisionCitation(string.Empty, string.Empty, string.Empty, null, "unknown", "stop_transaction"),
            stopReasons,
            "stop_transaction",
            summary);
    }
}

public sealed record ExternalModuleReceiptDecision(
    IReadOnlyList<string> CitedReceiptHashes,
    IReadOnlyList<string> StopReasons,
    string TransactionDisposition);

public sealed record ExternalModuleReceiptVerificationResult(
    bool VerificationRequired,
    string TransactionDisposition,
    IReadOnlyList<ExternalModuleReceiptReplayResult> Replays,
    IReadOnlyList<string> CitedReceiptHashes,
    IReadOnlyList<string> StopReasons,
    string Summary)
{
    public static ExternalModuleReceiptVerificationResult NotRequired()
    {
        return new ExternalModuleReceiptVerificationResult(
            false,
            "not_required",
            [],
            [],
            [],
            "External module receipt verification is not required for this request.");
    }
}
