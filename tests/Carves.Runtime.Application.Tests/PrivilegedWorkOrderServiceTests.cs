using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Tests;

public sealed class PrivilegedWorkOrderServiceTests
{
    [Fact]
    public void TryIssueCertificate_IssuesCertificateWhenStructuredConfirmationBindsTargetOperationRoleAndExpiry()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PrivilegedWorkOrderService(workspace.Paths);
        var workOrderId = "WO-P9-VALID";
        var targetHash = "sha256:target";
        var operationHash = PrivilegedWorkOrderService.ComputeOperationHash(
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);
        var expectedCertificateId = PrivilegedWorkOrderService.ComputeExpectedCertificateId(
            workOrderId,
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);

        var result = service.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = workOrderId,
            ActorId = "user:p9",
            ActorRoles = ["operator", "release-manager"],
            OperationId = "release_channel",
            TargetKind = "card",
            TargetId = "CARD-P9",
            TargetHash = targetHash,
            Now = DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
            Confirmation = new PrivilegedSecondConfirmation
            {
                TargetKind = "card",
                TargetId = "CARD-P9",
                TargetHash = targetHash,
                OperationId = "release_channel",
                OperationHash = operationHash,
                ActorRole = "release-manager",
                ExpiresAtUtc = DateTimeOffset.Parse("2026-04-20T09:30:00Z"),
                ExpectedCertificateId = expectedCertificateId,
                IrreversibilityAcknowledged = true,
            },
        });

        Assert.True(result.CertificateIssued);
        Assert.Equal("privileged_certificate_issued", result.AdmissionState);
        Assert.NotNull(result.Certificate);
        Assert.Equal(expectedCertificateId, result.Certificate!.CertificateId);
        Assert.Equal("release_channel", result.Certificate.OperationId);
        Assert.Equal(operationHash, result.Certificate.OperationHash);
        Assert.Equal("card", result.Certificate.TargetKind);
        Assert.Equal("CARD-P9", result.Certificate.TargetId);
        Assert.Equal(targetHash, result.Certificate.TargetHash);
        Assert.Equal("release-manager", result.Certificate.ActorRole);
        Assert.Equal("release-manager", result.Certificate.RequiredRole);
        Assert.Equal("host.privileged_transition.release_channel", result.Certificate.ExpectedHostRoute);
        Assert.Equal("privileged_transition_authorized:release_channel", result.Certificate.ExpectedTerminalState);
        var expectedTransition = Assert.Single(result.Certificate.ExpectedTransitions);
        Assert.Equal(".carves-platform/", expectedTransition.Root);
        Assert.Equal("release_channel", expectedTransition.Operation);
        Assert.Equal("CARD-P9", expectedTransition.ObjectId);
        Assert.Equal("pending_authorization", expectedTransition.From);
        Assert.Equal("authorized", expectedTransition.To);
        Assert.True(result.Certificate.IrreversibilityAcknowledged);
        Assert.False(string.IsNullOrWhiteSpace(result.Certificate.CertificateHash));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.CertificatePath!.Replace('/', Path.DirectorySeparatorChar))));

        var verification = service.VerifyCertificate(
            result.CertificatePath!,
            "release_channel",
            targetHash,
            DateTimeOffset.Parse("2026-04-20T09:05:00Z"));
        Assert.True(verification.Verified);
        Assert.Equal(expectedCertificateId, verification.Certificate!.CertificateId);
    }

    [Fact]
    public void VerifyCertificate_RejectsExpiredPrivilegedCertificateAtConsumptionTime()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PrivilegedWorkOrderService(workspace.Paths);
        var workOrderId = "WO-P9-EXPIRED-CONSUME";
        var targetHash = "sha256:target";
        var operationHash = PrivilegedWorkOrderService.ComputeOperationHash(
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);
        var expectedCertificateId = PrivilegedWorkOrderService.ComputeExpectedCertificateId(
            workOrderId,
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);

        var result = service.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = workOrderId,
            ActorId = "user:p9",
            ActorRoles = ["operator", "release-manager"],
            OperationId = "release_channel",
            TargetKind = "card",
            TargetId = "CARD-P9",
            TargetHash = targetHash,
            Now = DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
            Confirmation = new PrivilegedSecondConfirmation
            {
                TargetKind = "card",
                TargetId = "CARD-P9",
                TargetHash = targetHash,
                OperationId = "release_channel",
                OperationHash = operationHash,
                ActorRole = "release-manager",
                ExpiresAtUtc = DateTimeOffset.Parse("2026-04-20T09:30:00Z"),
                ExpectedCertificateId = expectedCertificateId,
                IrreversibilityAcknowledged = true,
            },
        });
        Assert.True(result.CertificateIssued);

        var verification = service.VerifyCertificate(
            result.CertificatePath!,
            "release_channel",
            targetHash,
            DateTimeOffset.Parse("2026-04-20T09:31:00Z"));

        Assert.False(verification.Verified);
        Assert.Contains(PrivilegedWorkOrderService.ConfirmationExpiredStopReason, verification.StopReasons);
    }

    [Fact]
    public void TryIssueCertificate_BlocksNaturalLanguageOnlyConfirmation()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PrivilegedWorkOrderService(workspace.Paths);

        var result = service.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = "WO-P9-NL",
            ActorId = "user:p9",
            ActorRoles = ["release-manager"],
            OperationId = "release_channel",
            TargetKind = "card",
            TargetId = "CARD-P9",
            TargetHash = "sha256:target",
            Now = DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
        });

        Assert.False(result.CertificateIssued);
        Assert.Equal("blocked", result.AdmissionState);
        Assert.Contains(PrivilegedWorkOrderService.ConfirmationMissingStopReason, result.StopReasons);
        Assert.Contains("structured", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryIssueCertificate_RejectsRoleHashExpiryAndIrreversibilityFailures()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PrivilegedWorkOrderService(workspace.Paths);
        var workOrderId = "WO-P9-BLOCKED";
        var expectedCertificateId = PrivilegedWorkOrderService.ComputeExpectedCertificateId(
            workOrderId,
            "promote_memory_truth",
            "memory_proposal",
            "MP-P9",
            "sha256:target");

        var result = service.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = workOrderId,
            ActorId = "user:p9",
            ActorRoles = ["operator"],
            OperationId = "promote_memory_truth",
            TargetKind = "memory_proposal",
            TargetId = "MP-P9",
            TargetHash = "sha256:target",
            Now = DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
            Confirmation = new PrivilegedSecondConfirmation
            {
                TargetKind = "memory_proposal",
                TargetId = "MP-P9",
                TargetHash = "sha256:other-target",
                OperationId = "promote_memory_truth",
                OperationHash = "sha256:not-operation",
                ActorRole = "operator",
                ExpiresAtUtc = DateTimeOffset.Parse("2026-04-20T08:59:00Z"),
                ExpectedCertificateId = expectedCertificateId,
                IrreversibilityAcknowledged = false,
            },
        });

        Assert.False(result.CertificateIssued);
        Assert.Contains(PrivilegedWorkOrderService.RoleMissingStopReason, result.StopReasons);
        Assert.Contains(PrivilegedWorkOrderService.TargetHashMismatchStopReason, result.StopReasons);
        Assert.Contains(PrivilegedWorkOrderService.OperationHashMismatchStopReason, result.StopReasons);
        Assert.Contains(PrivilegedWorkOrderService.CertificateMismatchStopReason, result.StopReasons);
        Assert.Contains(PrivilegedWorkOrderService.ConfirmationExpiredStopReason, result.StopReasons);
        Assert.Contains(PrivilegedWorkOrderService.IrreversibilityNotAcknowledgedStopReason, result.StopReasons);
    }

    [Fact]
    public void TryIssueCertificate_RejectsConfirmationOperationIdMismatchEvenWhenOperationHashMatchesRequestedOperation()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PrivilegedWorkOrderService(workspace.Paths);
        var workOrderId = "WO-P9-OP-ID-MISMATCH";
        var targetHash = "sha256:target";
        var operationHash = PrivilegedWorkOrderService.ComputeOperationHash(
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);
        var expectedCertificateId = PrivilegedWorkOrderService.ComputeExpectedCertificateId(
            workOrderId,
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);

        var result = service.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = workOrderId,
            ActorId = "user:p9",
            ActorRoles = ["operator", "release-manager"],
            OperationId = "release_channel",
            TargetKind = "card",
            TargetId = "CARD-P9",
            TargetHash = targetHash,
            Now = DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
            Confirmation = new PrivilegedSecondConfirmation
            {
                TargetKind = "card",
                TargetId = "CARD-P9",
                TargetHash = targetHash,
                OperationId = "promote_memory_truth",
                OperationHash = operationHash,
                ActorRole = "release-manager",
                ExpiresAtUtc = DateTimeOffset.Parse("2026-04-20T09:30:00Z"),
                ExpectedCertificateId = expectedCertificateId,
                IrreversibilityAcknowledged = true,
            },
        });

        Assert.False(result.CertificateIssued);
        Assert.Contains(PrivilegedWorkOrderService.OperationIdMismatchStopReason, result.StopReasons);
        Assert.DoesNotContain(PrivilegedWorkOrderService.OperationHashMismatchStopReason, result.StopReasons);
        Assert.Null(result.Certificate);
        Assert.Null(result.CertificatePath);
    }

    [Fact]
    public void TryIssueCertificate_RejectsCrossTargetReplayWhenStructuredConfirmationIsReusedForAnotherTarget()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PrivilegedWorkOrderService(workspace.Paths);
        var workOrderId = "WO-P9-CROSS-TARGET";
        var targetHash = "sha256:target-a";
        var operationHash = PrivilegedWorkOrderService.ComputeOperationHash(
            "release_channel",
            "card",
            "CARD-P9-A",
            targetHash);
        var expectedCertificateId = PrivilegedWorkOrderService.ComputeExpectedCertificateId(
            workOrderId,
            "release_channel",
            "card",
            "CARD-P9-A",
            targetHash);

        var result = service.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = workOrderId,
            ActorId = "user:p9",
            ActorRoles = ["operator", "release-manager"],
            OperationId = "release_channel",
            TargetKind = "card",
            TargetId = "CARD-P9-B",
            TargetHash = "sha256:target-b",
            Now = DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
            Confirmation = new PrivilegedSecondConfirmation
            {
                TargetKind = "card",
                TargetId = "CARD-P9-A",
                TargetHash = targetHash,
                OperationId = "release_channel",
                OperationHash = operationHash,
                ActorRole = "release-manager",
                ExpiresAtUtc = DateTimeOffset.Parse("2026-04-20T09:30:00Z"),
                ExpectedCertificateId = expectedCertificateId,
                IrreversibilityAcknowledged = true,
            },
        });

        Assert.False(result.CertificateIssued);
        Assert.Contains(PrivilegedWorkOrderService.TargetUnboundStopReason, result.StopReasons);
        Assert.Contains(PrivilegedWorkOrderService.TargetHashMismatchStopReason, result.StopReasons);
        Assert.DoesNotContain(PrivilegedWorkOrderService.OperationHashMismatchStopReason, result.StopReasons);
        Assert.Null(result.Certificate);
    }

    [Fact]
    public void VerifyCertificate_RejectsTamperedExpectedTransitions()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PrivilegedWorkOrderService(workspace.Paths);
        var workOrderId = "WO-P9-TAMPER-TRANSITIONS";
        var targetHash = "sha256:target";
        var operationHash = PrivilegedWorkOrderService.ComputeOperationHash(
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);
        var expectedCertificateId = PrivilegedWorkOrderService.ComputeExpectedCertificateId(
            workOrderId,
            "release_channel",
            "card",
            "CARD-P9",
            targetHash);

        var result = service.TryIssueCertificate(new PrivilegedWorkOrderRequest
        {
            WorkOrderId = workOrderId,
            ActorId = "user:p9",
            ActorRoles = ["operator", "release-manager"],
            OperationId = "release_channel",
            TargetKind = "card",
            TargetId = "CARD-P9",
            TargetHash = targetHash,
            Now = DateTimeOffset.Parse("2026-04-20T09:00:00Z"),
            Confirmation = new PrivilegedSecondConfirmation
            {
                TargetKind = "card",
                TargetId = "CARD-P9",
                TargetHash = targetHash,
                OperationId = "release_channel",
                OperationHash = operationHash,
                ActorRole = "release-manager",
                ExpiresAtUtc = DateTimeOffset.Parse("2026-04-20T09:30:00Z"),
                ExpectedCertificateId = expectedCertificateId,
                IrreversibilityAcknowledged = true,
            },
        });
        Assert.True(result.CertificateIssued);

        var certificatePath = Path.Combine(workspace.RootPath, result.CertificatePath!.Replace('/', Path.DirectorySeparatorChar));
        var payload = File.ReadAllText(certificatePath)
            .Replace("\"root\": \".carves-platform/\"", "\"root\": \".ai/runtime/\"", StringComparison.Ordinal);
        File.WriteAllText(certificatePath, payload);

        var verification = service.VerifyCertificate(
            result.CertificatePath!,
            "release_channel",
            targetHash,
            DateTimeOffset.Parse("2026-04-20T09:05:00Z"));

        Assert.False(verification.Verified);
        Assert.Contains(PrivilegedWorkOrderService.CertificateMismatchStopReason, verification.StopReasons);
    }
}
