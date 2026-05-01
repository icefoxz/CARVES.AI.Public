using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class PrivilegedWorkOrderService
{
    public const string CertificateSchema = "carves.privileged_work_order_certificate.v0.98-rc.p9";
    public const string UnknownOperationStopReason = "SC-PRIVILEGED-UNKNOWN-OPERATION";
    public const string ConfirmationMissingStopReason = "SC-PRIVILEGED-CONFIRMATION-MISSING";
    public const string RoleMissingStopReason = "SC-PRIVILEGED-ROLE-MISSING";
    public const string TargetUnboundStopReason = "SC-PRIVILEGED-TARGET-UNBOUND";
    public const string TargetHashMismatchStopReason = "SC-PRIVILEGED-TARGET-HASH-MISMATCH";
    public const string OperationIdMismatchStopReason = "SC-PRIVILEGED-OPERATION-ID-MISMATCH";
    public const string OperationHashMismatchStopReason = "SC-PRIVILEGED-OPERATION-HASH-MISMATCH";
    public const string CertificateMismatchStopReason = "SC-PRIVILEGED-CERTIFICATE-MISMATCH";
    public const string ConfirmationExpiredStopReason = "SC-PRIVILEGED-CONFIRMATION-EXPIRED";
    public const string IrreversibilityNotAcknowledgedStopReason = "SC-PRIVILEGED-IRREVERSIBILITY-NOT-ACKNOWLEDGED";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private static readonly IReadOnlyList<PrivilegedOperationDefinition> OperationDefinitions =
    [
        Operation("release_channel", "release-manager", "Release can publish an externally consumed channel and cannot run inside delegated L3."),
        Operation("delete_resource", "admin", "Delete can remove governed resources and cannot rely on natural-language confirmation."),
        Operation("cleanup_resource", "admin", "Cleanup can remove local residue and must not be used as hidden truth writeback."),
        Operation("promote_memory_truth", "memory-owner", "Memory Truth promotion changes long-term knowledge and requires privileged review."),
        Operation("refresh_authoritative_codegraph", "codegraph-owner", "Authoritative CodeGraph refresh changes planner-facing structure truth."),
    ];

    private readonly ControlPlanePaths paths;
    private readonly GovernedTruthTransitionProfileService governedTruthTransitionProfileService;

    public PrivilegedWorkOrderService(ControlPlanePaths paths)
    {
        this.paths = paths;
        governedTruthTransitionProfileService = new GovernedTruthTransitionProfileService();
    }

    public IReadOnlyList<PrivilegedOperationDefinition> ListOperations()
    {
        return OperationDefinitions;
    }

    public PrivilegedWorkOrderResult TryIssueCertificate(PrivilegedWorkOrderRequest request)
    {
        var now = request.Now ?? DateTimeOffset.UtcNow;
        var operation = OperationDefinitions.FirstOrDefault(item => string.Equals(item.OperationId, request.OperationId, StringComparison.Ordinal));
        if (operation is null)
        {
            return Block(request, null, [UnknownOperationStopReason], "Unknown privileged operation.");
        }

        var confirmation = request.Confirmation;
        if (confirmation is null)
        {
            return Block(request, operation, [ConfirmationMissingStopReason], "Structured second confirmation is required for privileged work orders.");
        }

        var stopReasons = ResolveStopReasons(request, operation, confirmation, now);
        if (stopReasons.Count != 0)
        {
            return Block(request, operation, stopReasons, "Privileged work order confirmation did not satisfy policy.");
        }

        var certificateId = ComputeExpectedCertificateId(
            request.WorkOrderId,
            operation.OperationId,
            confirmation.TargetKind,
            confirmation.TargetId,
            confirmation.TargetHash);
        var certificate = new PrivilegedWorkOrderCertificate
        {
            CertificateId = certificateId,
            WorkOrderId = request.WorkOrderId,
            OperationId = operation.OperationId,
            OperationHash = confirmation.OperationHash,
            TargetKind = confirmation.TargetKind,
            TargetId = confirmation.TargetId,
            TargetHash = confirmation.TargetHash,
            ActorId = request.ActorId,
            ActorRole = confirmation.ActorRole,
            RequiredRole = operation.RequiredRole,
            ExpiresAtUtc = confirmation.ExpiresAtUtc,
            ExpectedHostRoute = governedTruthTransitionProfileService.ResolvePrivilegedExpectedHostRoute(operation.OperationId),
            ExpectedTerminalState = governedTruthTransitionProfileService.ResolvePrivilegedExpectedTerminalState(operation.OperationId),
            ExpectedTransitions = governedTruthTransitionProfileService.BuildPrivilegedExpectedTransitions(
                operation.OperationId,
                confirmation.TargetKind,
                confirmation.TargetId),
            IrreversibilityNotice = operation.IrreversibilityNotice,
            IrreversibilityAcknowledged = confirmation.IrreversibilityAcknowledged,
            IssuedAtUtc = now,
        };
        certificate.CertificateHash = ComputeCertificateHash(certificate);

        var certificatePath = GetCertificatePath(request.WorkOrderId, certificate.CertificateId);
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
        File.WriteAllText(certificatePath, JsonSerializer.Serialize(certificate, JsonOptions));

        return new PrivilegedWorkOrderResult(
            true,
            "privileged_certificate_issued",
            operation.OperationId,
            operation.RequiredRole,
            operation.IrreversibilityNotice,
            confirmation,
            certificate,
            ToRepoRelative(certificatePath),
            [],
            "Privileged work order certificate issued. Execution remains disabled until the privileged route consumes this certificate.");
    }

    public PrivilegedCertificateVerificationResult VerifyCertificate(
        string certificatePath,
        string expectedOperationId,
        string expectedTargetHash,
        DateTimeOffset? now = null)
    {
        var fullPath = ResolvePath(certificatePath);
        if (!File.Exists(fullPath))
        {
            return new PrivilegedCertificateVerificationResult(false, null, [CertificateMismatchStopReason], "Privileged certificate is missing.");
        }

        var certificate = JsonSerializer.Deserialize<PrivilegedWorkOrderCertificate>(File.ReadAllText(fullPath), JsonOptions);
        if (certificate is null)
        {
            return new PrivilegedCertificateVerificationResult(false, null, [CertificateMismatchStopReason], "Privileged certificate could not be parsed.");
        }

        var expectedHash = ComputeCertificateHash(certificate);
        var stopReasons = new List<string>();
        if (!string.Equals(expectedHash, certificate.CertificateHash, StringComparison.Ordinal))
        {
            stopReasons.Add(CertificateMismatchStopReason);
        }

        if (!string.Equals(expectedOperationId, certificate.OperationId, StringComparison.Ordinal)
            || !string.Equals(expectedTargetHash, certificate.TargetHash, StringComparison.Ordinal))
        {
            stopReasons.Add(CertificateMismatchStopReason);
        }

        var expectedTransitions = governedTruthTransitionProfileService.BuildPrivilegedExpectedTransitions(
            expectedOperationId,
            certificate.TargetKind,
            certificate.TargetId);
        if (!certificate.ExpectedTransitions.SequenceEqual(expectedTransitions))
        {
            stopReasons.Add(CertificateMismatchStopReason);
        }

        if (!string.Equals(
                certificate.ExpectedHostRoute,
                governedTruthTransitionProfileService.ResolvePrivilegedExpectedHostRoute(expectedOperationId),
                StringComparison.Ordinal)
            || !string.Equals(
                certificate.ExpectedTerminalState,
                governedTruthTransitionProfileService.ResolvePrivilegedExpectedTerminalState(expectedOperationId),
                StringComparison.Ordinal))
        {
            stopReasons.Add(CertificateMismatchStopReason);
        }

        if (certificate.ExpiresAtUtc <= (now ?? DateTimeOffset.UtcNow))
        {
            stopReasons.Add(ConfirmationExpiredStopReason);
        }

        return new PrivilegedCertificateVerificationResult(
            stopReasons.Count == 0,
            certificate,
            stopReasons,
            stopReasons.Count == 0
                ? "Privileged certificate verified."
                : "Privileged certificate did not match the expected privileged transition.");
    }

    public string GetCertificatePath(string workOrderId, string certificateId)
    {
        return Path.Combine(
            paths.RuntimeRoot,
            "privileged-work-orders",
            Sanitize(workOrderId),
            $"{Sanitize(certificateId)}.json");
    }

    public static string ComputeOperationHash(string operationId, string targetKind, string targetId, string targetHash)
    {
        return ComputeContentHash(string.Join(
            "|",
            Normalize(operationId),
            Normalize(targetKind),
            Normalize(targetId),
            Normalize(targetHash)));
    }

    public static string ComputeExpectedCertificateId(
        string workOrderId,
        string operationId,
        string targetKind,
        string targetId,
        string targetHash)
    {
        var hash = ComputeContentHash(string.Join(
            "|",
            Normalize(workOrderId),
            Normalize(operationId),
            Normalize(targetKind),
            Normalize(targetId),
            Normalize(targetHash)));
        return $"pwc-{hash[..16]}";
    }

    private PrivilegedWorkOrderResult Block(
        PrivilegedWorkOrderRequest request,
        PrivilegedOperationDefinition? operation,
        IReadOnlyList<string> stopReasons,
        string summary)
    {
        return new PrivilegedWorkOrderResult(
            false,
            "blocked",
            request.OperationId,
            operation?.RequiredRole,
            operation?.IrreversibilityNotice ?? "Privileged work orders are irreversible or externally visible and require structured confirmation.",
            request.Confirmation,
            null,
            null,
            stopReasons,
            summary);
    }

    private static IReadOnlyList<string> ResolveStopReasons(
        PrivilegedWorkOrderRequest request,
        PrivilegedOperationDefinition operation,
        PrivilegedSecondConfirmation confirmation,
        DateTimeOffset now)
    {
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(confirmation.TargetKind)
            || string.IsNullOrWhiteSpace(confirmation.TargetId)
            || string.IsNullOrWhiteSpace(confirmation.TargetHash))
        {
            reasons.Add(TargetUnboundStopReason);
        }

        if (!string.Equals(request.TargetKind, confirmation.TargetKind, StringComparison.Ordinal)
            || !string.Equals(request.TargetId, confirmation.TargetId, StringComparison.Ordinal))
        {
            reasons.Add(TargetUnboundStopReason);
        }

        if (!string.Equals(request.TargetHash, confirmation.TargetHash, StringComparison.Ordinal))
        {
            reasons.Add(TargetHashMismatchStopReason);
        }

        if (!request.ActorRoles.Contains(operation.RequiredRole, StringComparer.Ordinal)
            || !string.Equals(confirmation.ActorRole, operation.RequiredRole, StringComparison.Ordinal))
        {
            reasons.Add(RoleMissingStopReason);
        }

        if (!string.Equals(operation.OperationId, confirmation.OperationId, StringComparison.Ordinal))
        {
            reasons.Add(OperationIdMismatchStopReason);
        }

        var expectedOperationHash = ComputeOperationHash(
            operation.OperationId,
            confirmation.TargetKind,
            confirmation.TargetId,
            confirmation.TargetHash);
        if (!string.Equals(expectedOperationHash, confirmation.OperationHash, StringComparison.Ordinal))
        {
            reasons.Add(OperationHashMismatchStopReason);
        }

        var expectedCertificateId = ComputeExpectedCertificateId(
            request.WorkOrderId,
            operation.OperationId,
            confirmation.TargetKind,
            confirmation.TargetId,
            confirmation.TargetHash);
        if (!string.Equals(expectedCertificateId, confirmation.ExpectedCertificateId, StringComparison.Ordinal))
        {
            reasons.Add(CertificateMismatchStopReason);
        }

        if (confirmation.ExpiresAtUtc <= now)
        {
            reasons.Add(ConfirmationExpiredStopReason);
        }

        if (!confirmation.IrreversibilityAcknowledged)
        {
            reasons.Add(IrreversibilityNotAcknowledgedStopReason);
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static PrivilegedOperationDefinition Operation(
        string operationId,
        string requiredRole,
        string irreversibilityNotice)
    {
        return new PrivilegedOperationDefinition(operationId, requiredRole, irreversibilityNotice);
    }

    private static string ComputeCertificateHash(PrivilegedWorkOrderCertificate certificate)
    {
        return ComputeContentHash(JsonSerializer.Serialize(
            new PrivilegedCertificateHashPayload
            {
                Schema = certificate.Schema,
                CertificateId = certificate.CertificateId,
                WorkOrderId = certificate.WorkOrderId,
                OperationId = certificate.OperationId,
                OperationHash = certificate.OperationHash,
                TargetKind = certificate.TargetKind,
                TargetId = certificate.TargetId,
                TargetHash = certificate.TargetHash,
                ActorId = certificate.ActorId,
                ActorRole = certificate.ActorRole,
                RequiredRole = certificate.RequiredRole,
                ExpiresAtUtc = certificate.ExpiresAtUtc,
                ExpectedHostRoute = certificate.ExpectedHostRoute,
                ExpectedTerminalState = certificate.ExpectedTerminalState,
                ExpectedTransitions = certificate.ExpectedTransitions,
                IrreversibilityNotice = certificate.IrreversibilityNotice,
                IrreversibilityAcknowledged = certificate.IrreversibilityAcknowledged,
                IssuedAtUtc = certificate.IssuedAtUtc,
            },
            JsonOptions));
    }

    private static string ComputeContentHash(string content)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(paths.RepoRoot, path.Replace('/', Path.DirectorySeparatorChar));
    }

    private string ToRepoRelative(string path)
    {
        var relative = Path.GetRelativePath(paths.RepoRoot, ResolvePath(path));
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string Sanitize(string value)
    {
        return new string(Normalize(value)
            .Select(static item => char.IsLetterOrDigit(item) || item is '-' or '_' or '.' ? item : '-')
            .ToArray());
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "(missing)" : value.Trim();
    }
}

public sealed record PrivilegedOperationDefinition(
    string OperationId,
    string RequiredRole,
    string IrreversibilityNotice);

public sealed record PrivilegedWorkOrderRequest
{
    public string WorkOrderId { get; init; } = string.Empty;

    public string ActorId { get; init; } = string.Empty;

    public IReadOnlyList<string> ActorRoles { get; init; } = [];

    public string OperationId { get; init; } = string.Empty;

    public string TargetKind { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string TargetHash { get; init; } = string.Empty;

    public PrivilegedSecondConfirmation? Confirmation { get; init; }

    public DateTimeOffset? Now { get; init; }
}

public sealed record PrivilegedSecondConfirmation
{
    public string TargetKind { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string TargetHash { get; init; } = string.Empty;

    public string OperationId { get; init; } = string.Empty;

    public string OperationHash { get; init; } = string.Empty;

    public string ActorRole { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public string ExpectedCertificateId { get; init; } = string.Empty;

    public bool IrreversibilityAcknowledged { get; init; }
}

public sealed class PrivilegedWorkOrderCertificate
{
    public string Schema { get; init; } = PrivilegedWorkOrderService.CertificateSchema;

    public string CertificateId { get; init; } = string.Empty;

    public string WorkOrderId { get; init; } = string.Empty;

    public string OperationId { get; init; } = string.Empty;

    public string OperationHash { get; init; } = string.Empty;

    public string TargetKind { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string TargetHash { get; init; } = string.Empty;

    public string ActorId { get; init; } = string.Empty;

    public string ActorRole { get; init; } = string.Empty;

    public string RequiredRole { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public string ExpectedHostRoute { get; init; } = string.Empty;

    public string ExpectedTerminalState { get; init; } = string.Empty;

    public IReadOnlyList<StateTransitionOperation> ExpectedTransitions { get; init; } = [];

    public string IrreversibilityNotice { get; init; } = string.Empty;

    public bool IrreversibilityAcknowledged { get; init; }

    public DateTimeOffset IssuedAtUtc { get; init; }

    public string CertificateHash { get; set; } = string.Empty;
}

public sealed class PrivilegedCertificateHashPayload
{
    public string Schema { get; init; } = string.Empty;

    public string CertificateId { get; init; } = string.Empty;

    public string WorkOrderId { get; init; } = string.Empty;

    public string OperationId { get; init; } = string.Empty;

    public string OperationHash { get; init; } = string.Empty;

    public string TargetKind { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string TargetHash { get; init; } = string.Empty;

    public string ActorId { get; init; } = string.Empty;

    public string ActorRole { get; init; } = string.Empty;

    public string RequiredRole { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    public string ExpectedHostRoute { get; init; } = string.Empty;

    public string ExpectedTerminalState { get; init; } = string.Empty;

    public IReadOnlyList<StateTransitionOperation> ExpectedTransitions { get; init; } = [];

    public string IrreversibilityNotice { get; init; } = string.Empty;

    public bool IrreversibilityAcknowledged { get; init; }

    public DateTimeOffset IssuedAtUtc { get; init; }
}

public sealed record PrivilegedWorkOrderResult(
    bool CertificateIssued,
    string AdmissionState,
    string OperationId,
    string? RequiredRole,
    string IrreversibilityNotice,
    PrivilegedSecondConfirmation? Confirmation,
    PrivilegedWorkOrderCertificate? Certificate,
    string? CertificatePath,
    IReadOnlyList<string> StopReasons,
    string Summary);

public sealed record PrivilegedCertificateVerificationResult(
    bool Verified,
    PrivilegedWorkOrderCertificate? Certificate,
    IReadOnlyList<string> StopReasons,
    string Summary);
