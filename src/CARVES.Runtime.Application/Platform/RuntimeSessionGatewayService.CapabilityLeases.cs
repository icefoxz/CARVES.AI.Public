namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeSessionGatewayService
{
    private const string CapabilityLeaseIssuer = "host_admission";
    private const string CapabilityLeaseAuthority = "runtime_control_kernel";
    private const string CapabilityLeaseRequester = "session_gateway_admission";
    private static readonly TimeSpan CapabilityLeaseDuration = TimeSpan.FromMinutes(30);
    private static readonly IReadOnlyList<string> ExternalAdapterCapabilityIds =
    [
        "adapter.guard",
        "adapter.handoff",
        "adapter.matrix",
        "adapter.audit",
        "adapter.shield",
        "adapter.codegraph_projection",
        "adapter.memory_proposalizer",
    ];

    public SessionGatewayCapabilityLeaseSurface VerifyCapabilityLeaseDryRun(SessionGatewayCapabilityLeaseVerificationRequest request)
    {
        return VerifyCapabilityLease(request);
    }

    private SessionGatewayCapabilityLeaseSurface IssueCapabilityLeaseDryRun(string workOrderId)
    {
        var capabilityIds = typedExecutionCoreService.GetOperationRegistry().Operations
            .Select(static definition => definition.CapabilityRequired)
            .Concat(ExternalAdapterCapabilityIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var now = DateTimeOffset.UtcNow;
        return VerifyCapabilityLease(new SessionGatewayCapabilityLeaseVerificationRequest
        {
            WorkOrderId = workOrderId,
            Issuer = CapabilityLeaseIssuer,
            RequestedBy = CapabilityLeaseRequester,
            CapabilityIds = capabilityIds,
            Now = now,
            ValidUntil = now.Add(CapabilityLeaseDuration),
        });
    }

    private static SessionGatewayCapabilityLeaseSurface BuildNotRequiredCapabilityLease()
    {
        return new SessionGatewayCapabilityLeaseSurface
        {
            IssuedBy = CapabilityLeaseIssuer,
            IssuerAuthority = CapabilityLeaseAuthority,
            RequestedBy = CapabilityLeaseRequester,
            LeaseState = "not_required",
            ExecutionEnabled = false,
            CapabilitySubsetOfParent = true,
        };
    }

    private SessionGatewayCapabilityLeaseSurface VerifyCapabilityLease(SessionGatewayCapabilityLeaseVerificationRequest request)
    {
        var now = request.Now ?? DateTimeOffset.UtcNow;
        var validUntil = request.ValidUntil ?? now.Add(CapabilityLeaseDuration);
        var requestedCapabilities = request.CapabilityIds
            .Where(static capability => !string.IsNullOrWhiteSpace(capability))
            .Select(static capability => capability.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var stopReasons = ResolveCapabilityLeaseStopReasons(request, now, validUntil, requestedCapabilities);
        var leaseState = ResolveCapabilityLeaseState(request, now, validUntil, stopReasons);

        return new SessionGatewayCapabilityLeaseSurface
        {
            LeaseId = leaseState == "issued" ? $"cldry-{Guid.NewGuid():N}" : null,
            WorkOrderId = NormalizeOptional(request.WorkOrderId),
            ParentLeaseId = NormalizeOptional(request.ParentLeaseId),
            IssuedBy = NormalizeOptional(request.Issuer) ?? "(none)",
            IssuerAuthority = CapabilityLeaseAuthority,
            RequestedBy = NormalizeOptional(request.RequestedBy) ?? "(none)",
            LeaseState = leaseState,
            IssuedAt = leaseState == "issued" ? now : null,
            ValidUntil = validUntil,
            Revocable = leaseState == "issued",
            ExecutionEnabled = false,
            CapabilitySubsetOfParent = IsCapabilitySubsetOfParent(request, requestedCapabilities),
            CapabilityIds = leaseState == "issued" ? requestedCapabilities : [],
            Read = BuildCapabilityGrant(requestedCapabilities, static capability => capability.StartsWith("read.", StringComparison.Ordinal)),
            Execute = BuildCapabilityGrant(requestedCapabilities, static capability =>
                capability.StartsWith("execute.", StringComparison.Ordinal)
                || capability.StartsWith("evaluate.", StringComparison.Ordinal)
                || capability.StartsWith("artifact.", StringComparison.Ordinal)
                || capability.StartsWith("lease.", StringComparison.Ordinal)),
            GitOperations = BuildCapabilityGrant(requestedCapabilities, static capability => capability.StartsWith("git.", StringComparison.Ordinal)),
            TruthOperations = BuildCapabilityGrant(requestedCapabilities, static capability => capability.StartsWith("truth.", StringComparison.Ordinal)),
            ExternalAdapters = BuildCapabilityGrant(requestedCapabilities, static capability => capability.StartsWith("adapter.", StringComparison.Ordinal)),
            StopReasons = stopReasons,
        };
    }

    private IReadOnlyList<string> ResolveCapabilityLeaseStopReasons(
        SessionGatewayCapabilityLeaseVerificationRequest request,
        DateTimeOffset now,
        DateTimeOffset validUntil,
        IReadOnlyList<string> requestedCapabilities)
    {
        var reasons = new List<string>();
        if (!string.Equals(request.Issuer, CapabilityLeaseIssuer, StringComparison.Ordinal))
        {
            reasons.Add("SC-LEASE-ISSUER-UNAUTHORIZED");
        }

        if (request.Revoked)
        {
            reasons.Add("SC-LEASE-REVOKED");
        }

        if (validUntil <= now)
        {
            reasons.Add("SC-LEASE-EXPIRED");
        }

        var knownCapabilities = ResolveKnownCapabilityIds();
        foreach (var capability in requestedCapabilities)
        {
            if (!knownCapabilities.Contains(capability))
            {
                reasons.Add($"SC-CAPABILITY-UNKNOWN:{capability}");
            }
        }

        if (!IsCapabilitySubsetOfParent(request, requestedCapabilities))
        {
            reasons.Add("SC-CHILD-LEASE-CAPABILITY-ESCALATION");
        }

        return reasons;
    }

    private static string ResolveCapabilityLeaseState(
        SessionGatewayCapabilityLeaseVerificationRequest request,
        DateTimeOffset now,
        DateTimeOffset validUntil,
        IReadOnlyList<string> stopReasons)
    {
        if (request.Revoked)
        {
            return "revoked";
        }

        if (validUntil <= now)
        {
            return "expired";
        }

        return stopReasons.Count == 0 ? "issued" : "rejected";
    }

    private static bool IsCapabilitySubsetOfParent(
        SessionGatewayCapabilityLeaseVerificationRequest request,
        IReadOnlyList<string> requestedCapabilities)
    {
        if (string.IsNullOrWhiteSpace(request.ParentLeaseId))
        {
            return true;
        }

        var parentCapabilities = request.ParentCapabilityIds.ToHashSet(StringComparer.Ordinal);
        return requestedCapabilities.All(parentCapabilities.Contains);
    }

    private HashSet<string> ResolveKnownCapabilityIds()
    {
        return typedExecutionCoreService.GetOperationRegistry().Operations
            .Select(static definition => definition.CapabilityRequired)
            .Concat(ExternalAdapterCapabilityIds)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static SessionGatewayCapabilityGrantSurface BuildCapabilityGrant(
        IReadOnlyList<string> capabilityIds,
        Func<string, bool> predicate)
    {
        return new SessionGatewayCapabilityGrantSurface
        {
            Allow = capabilityIds.Where(predicate).ToArray(),
        };
    }
}
