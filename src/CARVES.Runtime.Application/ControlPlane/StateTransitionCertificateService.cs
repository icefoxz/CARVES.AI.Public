using System.Text.Json;

namespace Carves.Runtime.Application.ControlPlane;

public interface IStateTransitionCertificateService
{
    string GetRunCertificatePath(string runId);

    StateTransitionCertificateIssueResult TryIssue(StateTransitionCertificateIssueRequest request);

    StateTransitionCertificateIssueResult RebindCommittedEffect(StateTransitionCertificateRebindRequest request);

    StateTransitionCertificateVerificationResult VerifyRequired(
        string? certificatePath,
        IReadOnlyList<string> requiredOperations);

    StateTransitionCertificateVerificationResult VerifyRequired(StateTransitionCertificateVerificationRequest request);

    StateTransitionCertificateEvidence BuildEvidence(string kind, string path, bool required = true);
}

public sealed class StateTransitionCertificateService : IStateTransitionCertificateService
{
    public const string CertificateSchema = "carves.state_transition_certificate.v0.98-rc.p6";
    public const string HostIssuer = "runtime-control-plane";
    public const string MissingCertificateStopReason = "SC-STC-MISSING";
    public const string StaleCertificateStopReason = "SC-STC-STALE";
    public const string RejectedCertificateStopReason = "SC-STC-REJECTED";
    public const string ContextMismatchStopReason = "SC-STC-CONTEXT-MISMATCH";
    public const string LedgerContextMismatchStopReason = "SC-STC-LEDGER-CONTEXT-MISMATCH";
    public const string TransitionMismatchStopReason = "SC-STC-TRANSITION-MISMATCH";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private readonly ControlPlanePaths paths;
    private readonly EffectLedgerService effectLedgerService;

    public StateTransitionCertificateService(
        ControlPlanePaths paths,
        EffectLedgerService? effectLedgerService = null)
    {
        this.paths = paths;
        this.effectLedgerService = effectLedgerService ?? new EffectLedgerService(paths);
    }

    public string GetRunCertificatePath(string runId)
    {
        return Path.Combine(paths.WorkerExecutionArtifactsRoot, runId, "state-transition-certificate.json");
    }

    public StateTransitionCertificateIssueResult TryIssue(StateTransitionCertificateIssueRequest request)
    {
        var stopReasons = ResolveStopReasons(request);
        if (stopReasons.Count != 0)
        {
            return StateTransitionCertificateIssueResult.Block(
                stopReasons,
                $"State transition certificate rejected: {string.Join(", ", stopReasons)}.");
        }

        var evidence = request.RequiredEvidence
            .Select(evidenceInput => new StateTransitionCertificateEvidence
            {
                Kind = evidenceInput.Kind,
                Path = NormalizePath(evidenceInput.Path),
                Hash = evidenceInput.Hash,
                Required = evidenceInput.Required,
            })
            .ToArray();
        var certificatePath = string.IsNullOrWhiteSpace(request.CertificatePath)
            ? GetRunCertificatePath(request.RunId)
            : request.CertificatePath;
        var relativeCertificatePath = effectLedgerService.ToRepoRelative(certificatePath);
        var recordWithoutHash = new StateTransitionCertificateRecord
        {
            CertificateId = string.IsNullOrWhiteSpace(request.CertificateId)
                ? $"STC-{request.RunId}"
                : request.CertificateId,
            Issuer = request.Issuer,
            HostRoute = request.HostRoute,
            TaskId = request.TaskId,
            RunId = request.RunId,
            WorkOrderId = NormalizeOptional(request.WorkOrderId),
            LeaseId = NormalizeOptional(request.LeaseId),
            TransactionHash = NormalizeOptional(request.TransactionHash),
            TerminalState = request.TerminalState,
            Transitions = request.Transitions,
            RequiredEvidence = evidence,
            PolicyVerdict = request.PolicyVerdict,
            EffectLedgerPath = NormalizePath(request.EffectLedgerPath),
            EffectLedgerEventHash = request.EffectLedgerEventHash,
            CertificatePath = relativeCertificatePath,
            CertificateHash = string.Empty,
            IssuedAtUtc = DateTimeOffset.UtcNow,
        };
        var certificateHash = ComputeCertificateHash(recordWithoutHash);
        var record = recordWithoutHash with
        {
            CertificateHash = certificateHash,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
        File.WriteAllText(certificatePath, JsonSerializer.Serialize(record, JsonOptions));
        var fileHash = effectLedgerService.HashFile(certificatePath);
        return StateTransitionCertificateIssueResult.Allow(
            record,
            relativeCertificatePath,
            fileHash);
    }

    public StateTransitionCertificateIssueResult RebindCommittedEffect(StateTransitionCertificateRebindRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CertificatePath))
        {
            return StateTransitionCertificateIssueResult.Block(
                [MissingCertificateStopReason],
                "State transition certificate rebind requires certificate_path.");
        }

        if (string.IsNullOrWhiteSpace(request.EffectLedgerPath)
            || string.IsNullOrWhiteSpace(request.EffectLedgerEventHash))
        {
            return StateTransitionCertificateIssueResult.Block(
                [EffectLedgerService.AuditIncompleteStopReason],
                "State transition certificate rebind requires a committed effect ledger binding.");
        }

        var fullCertificatePath = Path.IsPathRooted(request.CertificatePath)
            ? request.CertificatePath
            : Path.Combine(paths.RepoRoot, request.CertificatePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullCertificatePath))
        {
            return StateTransitionCertificateIssueResult.Block(
                [MissingCertificateStopReason],
                $"State transition certificate was not found at {request.CertificatePath}.");
        }

        StateTransitionCertificateRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<StateTransitionCertificateRecord>(File.ReadAllText(fullCertificatePath), JsonOptions);
        }
        catch (JsonException)
        {
            return StateTransitionCertificateIssueResult.Block(
                [RejectedCertificateStopReason],
                $"State transition certificate at {request.CertificatePath} is not valid JSON.");
        }

        if (record is null)
        {
            return StateTransitionCertificateIssueResult.Block(
                [RejectedCertificateStopReason],
                $"State transition certificate at {request.CertificatePath} is empty.");
        }

        if (string.IsNullOrWhiteSpace(record.CertificateHash))
        {
            return StateTransitionCertificateIssueResult.Block(
                [RejectedCertificateStopReason],
                "State transition certificate is missing certificate_hash.");
        }

        var expectedHash = ComputeCertificateHash(record with { CertificateHash = string.Empty });
        if (!string.Equals(expectedHash, record.CertificateHash, StringComparison.Ordinal))
        {
            return StateTransitionCertificateIssueResult.Block(
                [RejectedCertificateStopReason],
                "State transition certificate_hash does not match its payload.");
        }

        var verificationRequest = new StateTransitionCertificateVerificationRequest
        {
            CertificatePath = record.CertificatePath,
            RequiredOperations = record.Transitions.Select(static transition => transition.Operation).ToArray(),
            RequiredTransitions = record.Transitions,
            ExpectedWorkOrderId = string.IsNullOrWhiteSpace(request.ExpectedWorkOrderId) ? null : request.ExpectedWorkOrderId,
            ExpectedTaskId = string.IsNullOrWhiteSpace(request.ExpectedTaskId) ? null : request.ExpectedTaskId,
            ExpectedRunId = string.IsNullOrWhiteSpace(request.ExpectedRunId) ? null : request.ExpectedRunId,
            ExpectedHostRoute = string.IsNullOrWhiteSpace(request.ExpectedHostRoute) ? null : request.ExpectedHostRoute,
            ExpectedTerminalState = string.IsNullOrWhiteSpace(request.ExpectedTerminalState) ? null : request.ExpectedTerminalState,
            ExpectedLeaseId = string.IsNullOrWhiteSpace(request.ExpectedLeaseId) ? null : request.ExpectedLeaseId,
            RequireSealedLedger = false,
        };
        var verification = VerifyRequired(verificationRequest);
        if (!verification.CanWriteBack)
        {
            return StateTransitionCertificateIssueResult.Block(
                verification.StopReasons,
                verification.FailureMessage ?? "State transition certificate rebind requires a valid pre-writeback certificate.");
        }

        var normalizedLedgerPath = NormalizePath(request.EffectLedgerPath);
        var fullLedgerPath = Path.IsPathRooted(request.EffectLedgerPath)
            ? request.EffectLedgerPath
            : Path.Combine(paths.RepoRoot, normalizedLedgerPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullLedgerPath))
        {
            return StateTransitionCertificateIssueResult.Block(
                [EffectLedgerService.AuditIncompleteStopReason],
                $"State transition certificate effect ledger is missing at {request.EffectLedgerPath}.");
        }

        if (TryReadLedgerEventByHash(fullLedgerPath, request.EffectLedgerEventHash) is null)
        {
            return StateTransitionCertificateIssueResult.Block(
                [EffectLedgerService.AuditIncompleteStopReason],
                "Committed effect ledger event is not replayable from the ledger.");
        }

        var additionalEvidence = request.AdditionalEvidence
            .Select(evidence => new StateTransitionCertificateEvidence
            {
                Kind = evidence.Kind,
                Path = NormalizePath(evidence.Path),
                Hash = evidence.Hash,
                Required = evidence.Required,
            })
            .ToArray();
        var additionalEvidenceKinds = additionalEvidence
            .Select(static evidence => evidence.Kind)
            .ToHashSet(StringComparer.Ordinal);
        var retainedEvidence = record.RequiredEvidence
            .Where(evidence => !string.Equals(evidence.Kind, "effect_ledger_event", StringComparison.Ordinal))
            .Where(evidence => !additionalEvidenceKinds.Contains(evidence.Kind))
            .ToList();
        retainedEvidence.AddRange(additionalEvidence);
        retainedEvidence.Add(new StateTransitionCertificateEvidence
        {
            Kind = "effect_ledger_event",
            Path = normalizedLedgerPath,
            Hash = request.EffectLedgerEventHash,
            Required = true,
        });

        var reboundWithoutHash = record with
        {
            RequiredEvidence = retainedEvidence,
            EffectLedgerPath = normalizedLedgerPath,
            EffectLedgerEventHash = request.EffectLedgerEventHash,
            CertificateHash = string.Empty,
        };
        var rebound = reboundWithoutHash with
        {
            CertificateHash = ComputeCertificateHash(reboundWithoutHash),
        };

        File.WriteAllText(fullCertificatePath, JsonSerializer.Serialize(rebound, JsonOptions));
        return StateTransitionCertificateIssueResult.Allow(
            rebound,
            rebound.CertificatePath,
            effectLedgerService.HashFile(fullCertificatePath));
    }

    public StateTransitionCertificateVerificationResult VerifyRequired(
        string? certificatePath,
        IReadOnlyList<string> requiredOperations)
    {
        return StateTransitionCertificateVerificationResult.Block(
            [TransitionMismatchStopReason],
            "State transition certificate verification requires expected transition descriptors; operation-only verification is not sufficient for governed truth writeback.");
    }

    public StateTransitionCertificateVerificationResult VerifyRequired(StateTransitionCertificateVerificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CertificatePath))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [MissingCertificateStopReason],
                "State transition certificate is required before governed truth writeback.");
        }

        var fullPath = Path.IsPathRooted(request.CertificatePath)
            ? request.CertificatePath
            : Path.Combine(paths.RepoRoot, request.CertificatePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [MissingCertificateStopReason],
                $"State transition certificate was not found at {request.CertificatePath}.");
        }

        StateTransitionCertificateRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<StateTransitionCertificateRecord>(File.ReadAllText(fullPath), JsonOptions);
        }
        catch (JsonException)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [RejectedCertificateStopReason],
                $"State transition certificate at {request.CertificatePath} is not valid JSON.");
        }

        if (record is null)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [RejectedCertificateStopReason],
                $"State transition certificate at {request.CertificatePath} is empty.");
        }

        if (!string.Equals(record.Issuer, HostIssuer, StringComparison.Ordinal))
        {
            return StateTransitionCertificateVerificationResult.Block(
                ["SC-STC-ISSUER-UNAUTHORIZED"],
                $"State transition certificate issuer is {record.Issuer}.");
        }

        if (request.RequiredTransitions.Count == 0)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [TransitionMismatchStopReason],
                "State transition certificate verification requires expected transition descriptors; required_transitions may not be empty for governed verification.");
        }

        var contextMismatch = ResolveContextMismatch(record, request);
        if (contextMismatch.Count != 0)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [ContextMismatchStopReason],
                $"State transition certificate context mismatch: {string.Join(", ", contextMismatch)}.");
        }

        if (string.IsNullOrWhiteSpace(record.CertificateHash))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [RejectedCertificateStopReason],
                "State transition certificate is missing certificate_hash.");
        }

        var expectedCertificateHash = ComputeCertificateHash(record with { CertificateHash = string.Empty });
        if (!string.Equals(expectedCertificateHash, record.CertificateHash, StringComparison.Ordinal))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [RejectedCertificateStopReason],
                "State transition certificate_hash does not match its payload.");
        }

        var operations = record.Transitions
            .Select(static transition => transition.Operation)
            .ToHashSet(StringComparer.Ordinal);
        var missing = request.RequiredOperations
            .Where(operation => !operations.Contains(operation))
            .ToArray();
        if (missing.Length != 0)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [MissingCertificateStopReason],
                $"State transition certificate is missing required operation(s): {string.Join(", ", missing)}.");
        }

        var missingTransitions = request.RequiredTransitions
            .Where(required => !record.Transitions.Any(actual => MatchesTransition(actual, required)))
            .Select(FormatTransition)
            .ToArray();
        if (missingTransitions.Length != 0)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [TransitionMismatchStopReason],
                $"State transition certificate is missing required transition(s): {string.Join(", ", missingTransitions)}.");
        }

        if (!string.Equals(record.PolicyVerdict, "allow", StringComparison.Ordinal))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [RejectedCertificateStopReason],
                $"State transition certificate policy verdict is {record.PolicyVerdict}.");
        }

        if (record.RequiredEvidence.Any(static evidence => evidence.Required && string.IsNullOrWhiteSpace(evidence.Hash)))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [RejectedCertificateStopReason],
                "State transition certificate contains required evidence without a hash.");
        }

        if (string.IsNullOrWhiteSpace(record.EffectLedgerPath)
            || string.IsNullOrWhiteSpace(record.EffectLedgerEventHash))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [EffectLedgerService.AuditIncompleteStopReason],
                "State transition certificate is missing effect ledger binding.");
        }

        var effectLedgerPath = Path.IsPathRooted(record.EffectLedgerPath)
            ? record.EffectLedgerPath
            : Path.Combine(paths.RepoRoot, record.EffectLedgerPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(effectLedgerPath))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [EffectLedgerService.AuditIncompleteStopReason],
                $"State transition certificate effect ledger is missing at {record.EffectLedgerPath}.");
        }

        var ledgerReplay = request.RequireSealedLedger
            ? effectLedgerService.Replay(effectLedgerPath)
            : effectLedgerService.ReplayOpen(effectLedgerPath);
        if (!ledgerReplay.CanWriteBack || (request.RequireSealedLedger && !ledgerReplay.Sealed))
        {
            return StateTransitionCertificateVerificationResult.Block(
                [EffectLedgerService.AuditIncompleteStopReason],
                $"State transition certificate effect ledger is not replayable and sealed: {ledgerReplay.Summary}");
        }

        var ledgerEvent = TryReadLedgerEventByHash(effectLedgerPath, record.EffectLedgerEventHash);
        if (ledgerEvent is null)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [EffectLedgerService.AuditIncompleteStopReason],
                "State transition certificate effect ledger event hash is not replayable from the ledger.");
        }

        var ledgerContextMismatch = ResolveLedgerContextMismatch(record, request, ledgerReplay, ledgerEvent);
        if (ledgerContextMismatch.Count != 0)
        {
            return StateTransitionCertificateVerificationResult.Block(
                [LedgerContextMismatchStopReason],
                $"State transition certificate ledger context mismatch: {string.Join(", ", ledgerContextMismatch)}.");
        }

        foreach (var evidence in record.RequiredEvidence.Where(static evidence => evidence.Required))
        {
            if (string.IsNullOrWhiteSpace(evidence.Path))
            {
                return StateTransitionCertificateVerificationResult.Block(
                    [MissingCertificateStopReason],
                    $"State transition certificate required evidence '{evidence.Kind}' has no path.");
            }

            if (string.Equals(evidence.Kind, "effect_ledger_event", StringComparison.Ordinal))
            {
                if (!string.Equals(evidence.Hash, record.EffectLedgerEventHash, StringComparison.Ordinal))
                {
                    return StateTransitionCertificateVerificationResult.Block(
                        [StaleCertificateStopReason],
                        "State transition certificate effect ledger event hash is stale.");
                }

                if (TryReadLedgerEventByHash(effectLedgerPath, evidence.Hash) is null)
                {
                    return StateTransitionCertificateVerificationResult.Block(
                        [EffectLedgerService.AuditIncompleteStopReason],
                        "State transition certificate effect ledger event hash is not replayable from the ledger.");
                }

                continue;
            }

            var evidencePath = Path.IsPathRooted(evidence.Path)
                ? evidence.Path
                : Path.Combine(paths.RepoRoot, evidence.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(evidencePath))
            {
                return StateTransitionCertificateVerificationResult.Block(
                    [MissingCertificateStopReason],
                    $"State transition certificate required evidence '{evidence.Kind}' is missing at {evidence.Path}.");
            }

            var actualHash = effectLedgerService.HashFile(evidencePath);
            if (!string.Equals(actualHash, evidence.Hash, StringComparison.Ordinal))
            {
                return StateTransitionCertificateVerificationResult.Block(
                    [StaleCertificateStopReason],
                    $"State transition certificate required evidence '{evidence.Kind}' hash is stale.");
            }
        }

        return StateTransitionCertificateVerificationResult.Allow(record);
    }

    public StateTransitionCertificateEvidence BuildEvidence(string kind, string path, bool required = true)
    {
        var normalizedPath = NormalizePath(path);
        var fullPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(paths.RepoRoot, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
        var hash = File.Exists(fullPath) ? effectLedgerService.HashFile(fullPath) : string.Empty;
        return new StateTransitionCertificateEvidence
        {
            Kind = kind,
            Path = normalizedPath,
            Hash = hash,
            Required = required,
        };
    }

    private IReadOnlyList<string> ResolveStopReasons(StateTransitionCertificateIssueRequest request)
    {
        var reasons = new List<string>();
        if (!string.Equals(request.Issuer, HostIssuer, StringComparison.Ordinal))
        {
            reasons.Add("SC-STC-ISSUER-UNAUTHORIZED");
        }

        if (string.IsNullOrWhiteSpace(request.HostRoute))
        {
            reasons.Add("SC-STC-HOST-ROUTE-MISSING");
        }

        if (request.Transitions.Count == 0)
        {
            reasons.Add(MissingCertificateStopReason);
        }

        if (!string.Equals(request.PolicyVerdict, "allow", StringComparison.Ordinal))
        {
            reasons.Add(RejectedCertificateStopReason);
        }

        if (string.IsNullOrWhiteSpace(request.EffectLedgerPath)
            || string.IsNullOrWhiteSpace(request.EffectLedgerEventHash))
        {
            reasons.Add(EffectLedgerService.AuditIncompleteStopReason);
        }
        else
        {
            var effectLedgerPath = Path.IsPathRooted(request.EffectLedgerPath)
                ? request.EffectLedgerPath
                : Path.Combine(paths.RepoRoot, request.EffectLedgerPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(effectLedgerPath))
            {
                reasons.Add(EffectLedgerService.AuditIncompleteStopReason);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedLeaseId)
            && !string.Equals(request.ExpectedLeaseId, request.LeaseId, StringComparison.Ordinal))
        {
            reasons.Add(StaleCertificateStopReason);
        }

        if (!string.IsNullOrWhiteSpace(request.ExpectedTransactionHash)
            && !string.Equals(request.ExpectedTransactionHash, request.TransactionHash, StringComparison.Ordinal))
        {
            reasons.Add(StaleCertificateStopReason);
        }

        foreach (var evidence in request.RequiredEvidence)
        {
            if (!evidence.Required)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(evidence.Path) || string.IsNullOrWhiteSpace(evidence.Hash))
            {
                reasons.Add(MissingCertificateStopReason);
                continue;
            }

            if (string.Equals(evidence.Kind, "effect_ledger_event", StringComparison.Ordinal))
            {
                if (!string.Equals(evidence.Hash, request.EffectLedgerEventHash, StringComparison.Ordinal))
                {
                    reasons.Add(StaleCertificateStopReason);
                }

                var ledgerPath = Path.IsPathRooted(evidence.Path)
                    ? evidence.Path
                    : Path.Combine(paths.RepoRoot, evidence.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(ledgerPath))
                {
                    reasons.Add(MissingCertificateStopReason);
                }
                else if (TryReadLedgerEventByHash(ledgerPath, evidence.Hash) is null)
                {
                    reasons.Add(EffectLedgerService.AuditIncompleteStopReason);
                }

                continue;
            }

            var fullPath = Path.IsPathRooted(evidence.Path)
                ? evidence.Path
                : Path.Combine(paths.RepoRoot, evidence.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                reasons.Add(MissingCertificateStopReason);
                continue;
            }

            var actualHash = effectLedgerService.HashFile(fullPath);
            if (!string.Equals(actualHash, evidence.Hash, StringComparison.Ordinal))
            {
                reasons.Add(StaleCertificateStopReason);
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private string NormalizePath(string path)
    {
        return Path.IsPathRooted(path)
            ? effectLedgerService.ToRepoRelative(path)
            : path.Replace('\\', '/');
    }

    private static EffectLedgerEventRecord? TryReadLedgerEventByHash(string ledgerPath, string eventHash)
    {
        foreach (var line in File.ReadLines(ledgerPath).Where(static line => !string.IsNullOrWhiteSpace(line)))
        {
            if (!string.Equals(EffectLedgerService.ComputeContentHash(line), eventHash, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                return JsonSerializer.Deserialize<EffectLedgerEventRecord>(line, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveContextMismatch(
        StateTransitionCertificateRecord record,
        StateTransitionCertificateVerificationRequest request)
    {
        var mismatches = new List<string>();
        AddMismatch(mismatches, "work_order_id", request.ExpectedWorkOrderId, record.WorkOrderId);
        AddMismatch(mismatches, "task_id", request.ExpectedTaskId, record.TaskId);
        AddMismatch(mismatches, "run_id", request.ExpectedRunId, record.RunId);
        AddMismatch(mismatches, "host_route", request.ExpectedHostRoute, record.HostRoute);
        AddMismatch(mismatches, "terminal_state", request.ExpectedTerminalState, record.TerminalState);
        AddMismatch(mismatches, "lease_id", request.ExpectedLeaseId, record.LeaseId);
        AddMismatch(mismatches, "transaction_hash", request.ExpectedTransactionHash, record.TransactionHash);
        return mismatches;
    }

    private static IReadOnlyList<string> ResolveLedgerContextMismatch(
        StateTransitionCertificateRecord record,
        StateTransitionCertificateVerificationRequest request,
        EffectLedgerReplayResult replay,
        EffectLedgerEventRecord ledgerEvent)
    {
        var mismatches = new List<string>();
        AddMismatch(mismatches, "ledger.task_id", record.TaskId, replay.TaskId);
        AddMismatch(mismatches, "ledger.run_id", record.RunId, replay.RunId);
        AddMismatch(mismatches, "ledger.terminal_state", record.TerminalState, replay.TerminalState);
        AddMismatch(mismatches, "event.task_id", record.TaskId, ledgerEvent.TaskId);
        AddMismatch(mismatches, "event.run_id", record.RunId, ledgerEvent.RunId);
        AddMismatch(mismatches, "event.terminal_state", record.TerminalState, ledgerEvent.TerminalState);
        AddMismatch(mismatches, "ledger.work_order_id.expected", request.ExpectedWorkOrderId, replay.WorkOrderId);
        AddMismatch(mismatches, "event.task_id.expected", request.ExpectedTaskId, ledgerEvent.TaskId);
        AddMismatch(mismatches, "event.run_id.expected", request.ExpectedRunId, ledgerEvent.RunId);
        AddMismatch(mismatches, "event.terminal_state.expected", request.ExpectedTerminalState, ledgerEvent.TerminalState);
        AddMismatch(mismatches, "event.work_order_id.expected", request.ExpectedWorkOrderId, ledgerEvent.WorkOrderId);

        if (!string.IsNullOrWhiteSpace(record.WorkOrderId))
        {
            AddMismatch(mismatches, "ledger.work_order_id", record.WorkOrderId, replay.WorkOrderId);
            AddMismatch(mismatches, "event.work_order_id", record.WorkOrderId, ledgerEvent.WorkOrderId);
        }

        if (!string.IsNullOrWhiteSpace(record.LeaseId))
        {
            AddMismatch(mismatches, "ledger.lease_id", record.LeaseId, replay.LeaseId);
            AddMismatch(mismatches, "event.lease_id", record.LeaseId, ledgerEvent.LeaseId);
        }

        if (!string.IsNullOrWhiteSpace(record.TransactionHash))
        {
            AddMismatch(mismatches, "ledger.transaction_hash", record.TransactionHash, replay.TransactionHash);
            AddMismatch(mismatches, "event.transaction_hash", record.TransactionHash, ledgerEvent.TransactionHash);
        }

        foreach (var transition in request.RequiredTransitions)
        {
            if (string.Equals(transition.Operation, "review_submission_recorded", StringComparison.Ordinal))
            {
                AddFactMismatch(mismatches, ledgerEvent, "review_submission_id", transition.ObjectId);
                AddFactMismatch(mismatches, ledgerEvent, "review_submission_from", transition.From);
                AddFactMismatch(mismatches, ledgerEvent, "review_submission_to", transition.To);
            }

            if (transition.Operation.StartsWith("task_status_to_", StringComparison.Ordinal))
            {
                AddFactMismatch(mismatches, ledgerEvent, "task_status_from", transition.From);
                AddFactMismatch(mismatches, ledgerEvent, "task_status_to", transition.To);
            }
        }

        return mismatches;
    }

    private static void AddFactMismatch(
        ICollection<string> mismatches,
        EffectLedgerEventRecord ledgerEvent,
        string factName,
        string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return;
        }

        ledgerEvent.Facts.TryGetValue(factName, out var actual);
        AddMismatch(mismatches, $"event.fact.{factName}", expected, actual);
    }

    private static void AddMismatch(ICollection<string> mismatches, string field, string? expected, string? actual)
    {
        if (!string.IsNullOrWhiteSpace(expected)
            && !string.Equals(expected, actual, StringComparison.Ordinal))
        {
            mismatches.Add($"{field} expected '{expected}' but certificate has '{actual}'");
        }
    }

    private static bool MatchesTransition(StateTransitionOperation actual, StateTransitionOperation required)
    {
        return FieldMatches(actual.Root, required.Root)
            && FieldMatches(actual.Operation, required.Operation)
            && FieldMatches(actual.ObjectId, required.ObjectId)
            && FieldMatches(actual.From, required.From)
            && FieldMatches(actual.To, required.To);
    }

    private static bool FieldMatches(string actual, string required)
    {
        return string.IsNullOrWhiteSpace(required)
            || string.Equals(actual, required, StringComparison.Ordinal);
    }

    private static string FormatTransition(StateTransitionOperation transition)
    {
        return $"{transition.Root}:{transition.Operation}:{transition.ObjectId}:{transition.From}->{transition.To}";
    }

    private static string ComputeCertificateHash(StateTransitionCertificateRecord record)
    {
        return EffectLedgerService.ComputeContentHash(JsonSerializer.Serialize(record, JsonOptions));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

public sealed record StateTransitionCertificateIssueRequest
{
    public string CertificateId { get; init; } = string.Empty;

    public string CertificatePath { get; init; } = string.Empty;

    public string Issuer { get; init; } = StateTransitionCertificateService.HostIssuer;

    public string HostRoute { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string? WorkOrderId { get; init; }

    public string? LeaseId { get; init; }

    public string? ExpectedLeaseId { get; init; }

    public string? TransactionHash { get; init; }

    public string? ExpectedTransactionHash { get; init; }

    public string TerminalState { get; init; } = string.Empty;

    public IReadOnlyList<StateTransitionOperation> Transitions { get; init; } = [];

    public IReadOnlyList<StateTransitionCertificateEvidence> RequiredEvidence { get; init; } = [];

    public string PolicyVerdict { get; init; } = "allow";

    public string EffectLedgerPath { get; init; } = string.Empty;

    public string EffectLedgerEventHash { get; init; } = string.Empty;
}

public sealed record StateTransitionCertificateVerificationRequest
{
    public string? CertificatePath { get; init; }

    public IReadOnlyList<string> RequiredOperations { get; init; } = [];

    public IReadOnlyList<StateTransitionOperation> RequiredTransitions { get; init; } = [];

    public string? ExpectedWorkOrderId { get; init; }

    public string? ExpectedTaskId { get; init; }

    public string? ExpectedRunId { get; init; }

    public string? ExpectedHostRoute { get; init; }

    public string? ExpectedTerminalState { get; init; }

    public string? ExpectedLeaseId { get; init; }

    public string? ExpectedTransactionHash { get; init; }

    public bool RequireSealedLedger { get; init; } = true;
}

public sealed record StateTransitionCertificateRebindRequest
{
    public string CertificatePath { get; init; } = string.Empty;

    public string EffectLedgerPath { get; init; } = string.Empty;

    public string EffectLedgerEventHash { get; init; } = string.Empty;

    public string? ExpectedWorkOrderId { get; init; }

    public string? ExpectedTaskId { get; init; }

    public string? ExpectedRunId { get; init; }

    public string? ExpectedHostRoute { get; init; }

    public string? ExpectedTerminalState { get; init; }

    public string? ExpectedLeaseId { get; init; }

    public IReadOnlyList<StateTransitionCertificateEvidence> AdditionalEvidence { get; init; } = [];
}

public sealed record StateTransitionOperation
{
    public string Root { get; init; } = string.Empty;

    public string Operation { get; init; } = string.Empty;

    public string ObjectId { get; init; } = string.Empty;

    public string From { get; init; } = string.Empty;

    public string To { get; init; } = string.Empty;
}

public sealed record StateTransitionCertificateEvidence
{
    public string Kind { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;

    public bool Required { get; init; } = true;
}

public sealed record StateTransitionCertificateRecord
{
    public string Schema { get; init; } = StateTransitionCertificateService.CertificateSchema;

    public string CertificateId { get; init; } = string.Empty;

    public string CertificateHash { get; init; } = string.Empty;

    public string Issuer { get; init; } = string.Empty;

    public string HostRoute { get; init; } = string.Empty;

    public string TaskId { get; init; } = string.Empty;

    public string RunId { get; init; } = string.Empty;

    public string? WorkOrderId { get; init; }

    public string? LeaseId { get; init; }

    public string? TransactionHash { get; init; }

    public string TerminalState { get; init; } = string.Empty;

    public IReadOnlyList<StateTransitionOperation> Transitions { get; init; } = [];

    public IReadOnlyList<StateTransitionCertificateEvidence> RequiredEvidence { get; init; } = [];

    public string PolicyVerdict { get; init; } = string.Empty;

    public string EffectLedgerPath { get; init; } = string.Empty;

    public string EffectLedgerEventHash { get; init; } = string.Empty;

    public string CertificatePath { get; init; } = string.Empty;

    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record StateTransitionCertificateIssueResult(
    bool CanIssue,
    StateTransitionCertificateRecord? Certificate,
    string? CertificatePath,
    string? CertificateHash,
    IReadOnlyList<string> StopReasons,
    string? FailureMessage)
{
    public static StateTransitionCertificateIssueResult Allow(
        StateTransitionCertificateRecord certificate,
        string certificatePath,
        string certificateHash)
    {
        return new StateTransitionCertificateIssueResult(
            true,
            certificate,
            certificatePath,
            certificateHash,
            [],
            null);
    }

    public static StateTransitionCertificateIssueResult Block(
        IReadOnlyList<string> stopReasons,
        string failureMessage)
    {
        return new StateTransitionCertificateIssueResult(
            false,
            null,
            null,
            null,
            stopReasons,
            failureMessage);
    }
}

public sealed record StateTransitionCertificateVerificationResult(
    bool CanWriteBack,
    StateTransitionCertificateRecord? Certificate,
    IReadOnlyList<string> StopReasons,
    string? FailureMessage)
{
    public static StateTransitionCertificateVerificationResult Allow(StateTransitionCertificateRecord certificate)
    {
        return new StateTransitionCertificateVerificationResult(true, certificate, [], null);
    }

    public static StateTransitionCertificateVerificationResult Block(
        IReadOnlyList<string> stopReasons,
        string failureMessage)
    {
        return new StateTransitionCertificateVerificationResult(false, null, stopReasons, failureMessage);
    }
}
