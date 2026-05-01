using System.Security.Cryptography;
using System.Text;

namespace Carves.Runtime.Application.Interaction;

public sealed record RuntimeGuidanceCandidateReducerInput(
    RuntimeGuidanceCandidate? ExistingCandidate,
    RuntimeGuidanceFieldOperationCompilerResult FieldOperations,
    string? CandidateId = null,
    string? SourceTurnRef = null,
    bool ResetCandidate = false,
    IReadOnlyList<string>? FieldPriority = null,
    IReadOnlyList<string>? ReviewGateFields = null);

public sealed record RuntimeGuidanceCandidateReducerResult(
    bool ShouldProjectCandidate,
    RuntimeGuidanceCandidate? Candidate);

public sealed class RuntimeGuidanceCandidateReducer
{
    private const int MaxListItems = 3;
    private static readonly string[] RequiredFields = ["topic", "desired_outcome", "scope", "success_signal"];

    public RuntimeGuidanceCandidateReducerResult Reduce(RuntimeGuidanceCandidateReducerInput input)
    {
        if (!input.FieldOperations.ShouldCompile)
        {
            return new RuntimeGuidanceCandidateReducerResult(false, null);
        }

        var existingCandidate = input.ResetCandidate ? null : input.ExistingCandidate;
        var operations = input.FieldOperations.OperationSet;
        var topic = ApplyScalarOperation(existingCandidate?.Topic, operations.Topic);
        var desiredOutcome = ApplyScalarOperation(existingCandidate?.DesiredOutcome, operations.DesiredOutcome);
        var scope = ApplyScalarOperation(existingCandidate?.Scope, operations.Scope);
        var constraints = ApplyListOperation(existingCandidate?.Constraints, operations.Constraints);
        var nonGoals = ApplyListOperation(existingCandidate?.NonGoals, operations.NonGoals);
        var successSignal = ApplyScalarOperation(existingCandidate?.SuccessSignal, operations.SuccessSignal);
        var owner = ApplyScalarOperation(existingCandidate?.Owner, operations.Owner);
        var reviewGate = ApplyScalarOperation(existingCandidate?.ReviewGate, operations.ReviewGate);
        var risks = ApplyListOperation(existingCandidate?.Risks, operations.Risks);
        var requiredFieldOrder = ResolveRequiredFieldOrder(input.FieldPriority);
        var reviewGateFieldOrder = ResolveReviewGateFieldOrder(input.ReviewGateFields, input.FieldPriority);
        var readinessBlocks = BuildReadinessBlocks(
            existingCandidate?.ReadinessBlocks,
            input.FieldOperations.FieldEvidence,
            requiredFieldOrder,
            reviewGateFieldOrder,
            topic,
            desiredOutcome,
            scope,
            constraints,
            nonGoals,
            successSignal,
            owner,
            reviewGate,
            risks);
        var missingFields = readinessBlocks
            .Select(block => block.Field)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var readinessFieldCount = RequiredFields.Length + (readinessBlocks.Any(block => reviewGateFieldOrder.Contains(block.Field, StringComparer.Ordinal)) ? reviewGateFieldOrder.Count : 0);
        var readinessScore = (int)Math.Round(((readinessFieldCount - missingFields.Length) * 100d) / readinessFieldCount);
        var sourceTurnRefs = MergeList(existingCandidate?.SourceTurnRefs, NormalizeOptional(input.SourceTurnRef) is { } source ? [source] : []);
        var revisionHash = BuildRevisionHash(topic, desiredOutcome, scope, constraints, nonGoals, successSignal, owner, reviewGate, risks);
        var candidateId = (input.ResetCandidate ? null : NormalizeOptional(input.CandidateId))
            ?? existingCandidate?.CandidateId
            ?? $"candidate-{revisionHash[..12]}";

        var candidate = new RuntimeGuidanceCandidate
        {
            CandidateId = candidateId,
            State = missingFields.Length == 0 ? RuntimeGuidanceCandidateStates.ReviewReady : RuntimeGuidanceCandidateStates.Collecting,
            Topic = topic,
            DesiredOutcome = desiredOutcome,
            Scope = scope,
            Constraints = constraints,
            NonGoals = nonGoals,
            SuccessSignal = successSignal,
            Owner = owner,
            ReviewGate = reviewGate,
            Risks = risks,
            OpenQuestions = missingFields,
            SourceTurnRefs = sourceTurnRefs,
            Confidence = Math.Round(readinessScore / 100d, 2),
            ReadinessScore = readinessScore,
            MissingFields = missingFields,
            ReadinessBlocks = readinessBlocks,
            RevisionHash = revisionHash,
            ParkedUntilChangeHash = existingCandidate?.ParkedUntilChangeHash,
        };

        return new RuntimeGuidanceCandidateReducerResult(true, candidate);
    }

    private static string ApplyScalarOperation(string? existing, RuntimeGuidanceScalarFieldOperation operation)
    {
        return operation.Kind switch
        {
            RuntimeGuidanceFieldOperationKind.Set => operation.Value ?? string.Empty,
            RuntimeGuidanceFieldOperationKind.Clear => string.Empty,
            _ => NormalizeOptional(existing) ?? string.Empty,
        };
    }

    private static IReadOnlyList<string> ApplyListOperation(
        IReadOnlyList<string>? existing,
        RuntimeGuidanceListFieldOperation operation)
    {
        return operation.Kind switch
        {
            RuntimeGuidanceFieldOperationKind.Set => operation.Values.Take(MaxListItems).ToArray(),
            RuntimeGuidanceFieldOperationKind.Append => MergeList(existing, operation.Values),
            RuntimeGuidanceFieldOperationKind.Clear => operation.Values.Take(MaxListItems).ToArray(),
            _ => (existing ?? Array.Empty<string>()).Take(MaxListItems).ToArray(),
        };
    }

    private static bool IsMissingField(
        string field,
        string topic,
        string desiredOutcome,
        string scope,
        IReadOnlyList<string> constraints,
        IReadOnlyList<string> nonGoals,
        string successSignal,
        string owner,
        string reviewGate,
        IReadOnlyList<string> risks)
    {
        return field switch
        {
            "topic" => string.IsNullOrWhiteSpace(topic),
            "desired_outcome" => string.IsNullOrWhiteSpace(desiredOutcome),
            "scope" => string.IsNullOrWhiteSpace(scope),
            "constraint" => constraints.Count == 0,
            "non_goal" => nonGoals.Count == 0,
            "success_signal" => string.IsNullOrWhiteSpace(successSignal),
            "owner" => string.IsNullOrWhiteSpace(owner),
            "review_gate" => string.IsNullOrWhiteSpace(reviewGate),
            "risk" => risks.Count == 0,
            _ => false,
        };
    }

    private static IReadOnlyList<RuntimeGuidanceCandidateReadinessBlock> BuildReadinessBlocks(
        IReadOnlyList<RuntimeGuidanceCandidateReadinessBlock>? existingBlocks,
        IReadOnlyList<RuntimeGuidanceExtractedFieldEvidence> fieldEvidence,
        IReadOnlyList<string> requiredFieldOrder,
        IReadOnlyList<string> reviewGateFieldOrder,
        string topic,
        string desiredOutcome,
        string scope,
        IReadOnlyList<string> constraints,
        IReadOnlyList<string> nonGoals,
        string successSignal,
        string owner,
        string reviewGate,
        IReadOnlyList<string> risks)
    {
        var evidenceByField = fieldEvidence
            .GroupBy(field => NormalizeReadinessFieldName(field.Field), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var requiredBlocks = requiredFieldOrder
            .Select(field => BuildReadinessBlock(
                field,
                evidenceByField.GetValueOrDefault(field),
                FindExistingReadinessBlock(existingBlocks, field),
                IsMissingField(field, topic, desiredOutcome, scope, constraints, nonGoals, successSignal, owner, reviewGate, risks)))
            .Where(block => block is not null)
            .Select(block => block!)
            .ToArray();
        if (requiredBlocks.Length > 0)
        {
            return requiredBlocks;
        }

        return reviewGateFieldOrder
            .Select(field => BuildReadinessBlock(
                field,
                evidenceByField.GetValueOrDefault(field),
                FindExistingReadinessBlock(existingBlocks, field),
                IsMissingField(field, topic, desiredOutcome, scope, constraints, nonGoals, successSignal, owner, reviewGate, risks)))
            .Where(block => block is not null)
            .Select(block => block!)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRequiredFieldOrder(IReadOnlyList<string>? fieldPriority)
    {
        if (fieldPriority is null || fieldPriority.Count == 0)
        {
            return RequiredFields;
        }

        return fieldPriority
            .Where(field => RequiredFields.Contains(field, StringComparer.Ordinal))
            .Concat(RequiredFields)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveReviewGateFieldOrder(
        IReadOnlyList<string>? reviewGateFields,
        IReadOnlyList<string>? fieldPriority)
    {
        if (reviewGateFields is null || reviewGateFields.Count == 0)
        {
            return Array.Empty<string>();
        }

        var reviewGates = reviewGateFields
            .Where(field => field is "risk" or "constraint" or "non_goal" or "owner" or "review_gate")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return (fieldPriority ?? Array.Empty<string>())
            .Where(reviewGates.Contains)
            .Concat(reviewGates)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeReadinessFieldName(string field)
    {
        return field switch
        {
            "constraints" => "constraint",
            "non_goals" => "non_goal",
            "risks" => "risk",
            _ => field,
        };
    }

    private static RuntimeGuidanceCandidateReadinessBlock? BuildReadinessBlock(
        string field,
        RuntimeGuidanceExtractedFieldEvidence? evidence,
        RuntimeGuidanceCandidateReadinessBlock? existingBlock,
        bool missing)
    {
        if (evidence is { IssueCodes.Count: > 0 })
        {
            return BuildEvidenceReadinessBlock(field, evidence);
        }

        if (missing)
        {
            if (CanCarryExistingReadinessBlock(evidence) && IsReusableReadinessBlock(existingBlock, field))
            {
                return existingBlock;
            }

            return new RuntimeGuidanceCandidateReadinessBlock(
                field,
                RuntimeGuidanceCandidateReadinessBlockCodes.MissingRequiredField,
                Array.Empty<string>(),
                Array.Empty<string>(),
                false,
                0d);
        }

        return null;
    }

    private static bool CanCarryExistingReadinessBlock(RuntimeGuidanceExtractedFieldEvidence? evidence)
    {
        return !string.Equals(
            evidence?.Operation,
            RuntimeGuidanceFieldOperationNames.Clear,
            StringComparison.Ordinal);
    }

    private static RuntimeGuidanceCandidateReadinessBlock? FindExistingReadinessBlock(
        IReadOnlyList<RuntimeGuidanceCandidateReadinessBlock>? existingBlocks,
        string field)
    {
        return existingBlocks?
            .FirstOrDefault(block => string.Equals(block.Field, field, StringComparison.Ordinal));
    }

    private static bool IsReusableReadinessBlock(RuntimeGuidanceCandidateReadinessBlock? block, string field)
    {
        return block is not null
            && string.Equals(block.Field, field, StringComparison.Ordinal)
            && IsKnownReadinessBlockReason(block.Reason);
    }

    private static bool IsKnownReadinessBlockReason(string reason)
    {
        return reason is RuntimeGuidanceCandidateReadinessBlockCodes.MissingRequiredField
            or RuntimeGuidanceCandidateReadinessBlockCodes.AmbiguousRequiredField
            or RuntimeGuidanceCandidateReadinessBlockCodes.ConflictingRequiredEvidence
            or RuntimeGuidanceCandidateReadinessBlockCodes.CandidateGroupConflict;
    }

    private static RuntimeGuidanceCandidateReadinessBlock BuildEvidenceReadinessBlock(
        string field,
        RuntimeGuidanceExtractedFieldEvidence evidence)
    {
        return new RuntimeGuidanceCandidateReadinessBlock(
            field,
            ResolveReadinessBlockReason(evidence),
            NormalizeEvidenceCodes(evidence.Sources.Select(source => source.Kind)),
            NormalizeEvidenceCodes(evidence.Sources.Select(source => source.CandidateGroup)),
            evidence.Sources.Any(source => source.Negated),
            evidence.Sources.Count == 0
                ? 0d
                : Math.Clamp(Math.Round(evidence.Sources.Max(source => source.Confidence), 2), 0d, 1d));
    }

    private static string ResolveReadinessBlockReason(RuntimeGuidanceExtractedFieldEvidence evidence)
    {
        if (evidence.IssueCodes.Contains(RuntimeGuidanceFieldEvidenceIssueCodes.CandidateAlternative, StringComparer.Ordinal))
        {
            return RuntimeGuidanceCandidateReadinessBlockCodes.CandidateGroupConflict;
        }

        if (evidence.IssueCodes.Contains(RuntimeGuidanceFieldEvidenceIssueCodes.ConflictingEvidence, StringComparer.Ordinal))
        {
            return RuntimeGuidanceCandidateReadinessBlockCodes.ConflictingRequiredEvidence;
        }

        return RuntimeGuidanceCandidateReadinessBlockCodes.AmbiguousRequiredField;
    }

    private static IReadOnlyList<string> NormalizeEvidenceCodes(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    private static IReadOnlyList<string> MergeList(
        IReadOnlyList<string>? existing,
        IReadOnlyList<string> next)
    {
        return (existing ?? Array.Empty<string>())
            .Concat(next)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaxListItems)
            .ToArray();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildRevisionHash(
        string topic,
        string desiredOutcome,
        string scope,
        IReadOnlyList<string> constraints,
        IReadOnlyList<string> nonGoals,
        string successSignal,
        string owner,
        string reviewGate,
        IReadOnlyList<string> risks)
    {
        var material = string.Join(
            "\n",
            topic,
            desiredOutcome,
            scope,
            string.Join('|', constraints),
            string.Join('|', nonGoals),
            successSignal,
            owner,
            reviewGate,
            string.Join('|', risks));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material.ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
