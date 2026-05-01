using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

internal static class AcceptanceContractFactory
{
    public static AcceptanceContract NormalizeCardContract(
        string cardId,
        string title,
        string goal,
        IReadOnlyList<string> acceptance,
        IReadOnlyList<string> constraints,
        CardRealityModel? realityModel,
        AcceptanceContract? contract = null)
    {
        return new AcceptanceContract
        {
            ContractId = ResolveContractId(contract?.ContractId, $"AC-{cardId}"),
            Title = title,
            Status = contract?.Status ?? AcceptanceContractLifecycleStatus.Draft,
            Owner = string.IsNullOrWhiteSpace(contract?.Owner) ? "planner" : contract.Owner.Trim(),
            CreatedAtUtc = ResolveCreatedAt(contract),
            Intent = new AcceptanceContractIntent
            {
                Goal = goal,
                BusinessValue = ResolveText(contract?.Intent.BusinessValue, goal),
            },
            AcceptanceExamples = NormalizeExamples(contract?.AcceptanceExamples),
            Checks = NormalizeChecks(contract?.Checks),
            Constraints = NormalizeConstraints(contract?.Constraints, constraints, sourceConstraints: null),
            NonGoals = ResolveList(contract?.NonGoals, realityModel?.NonGoals),
            AutoCompleteAllowed = contract?.AutoCompleteAllowed ?? false,
            EvidenceRequired = NormalizeEvidenceRequirements(contract?.EvidenceRequired),
            HumanReview = NormalizeHumanReview(contract?.HumanReview, sourceHumanReview: null),
            Traceability = new AcceptanceContractTraceability
            {
                SourceCardId = cardId,
                SourceTaskId = null,
                DerivedTaskIds = contract?.Traceability.DerivedTaskIds?.Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
                RelatedArtifacts = contract?.Traceability.RelatedArtifacts?.Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
            },
        };
    }

    public static AcceptanceContract NormalizeTaskContract(
        string taskId,
        string title,
        string goal,
        string? cardId,
        IReadOnlyList<string> acceptance,
        IReadOnlyList<string> constraints,
        ValidationPlan? validation,
        AcceptanceContract? contract = null,
        AcceptanceContract? sourceContract = null)
    {
        return new AcceptanceContract
        {
            ContractId = ResolveContractId(contract?.ContractId, $"AC-{taskId}"),
            Title = title,
            Status = contract?.Status ?? AcceptanceContractLifecycleStatus.Compiled,
            Owner = string.IsNullOrWhiteSpace(contract?.Owner) ? "planner" : contract.Owner.Trim(),
            CreatedAtUtc = ResolveCreatedAt(contract),
            Intent = new AcceptanceContractIntent
            {
                Goal = goal,
                BusinessValue = ResolveText(contract?.Intent.BusinessValue, sourceContract?.Intent.BusinessValue, goal),
            },
            AcceptanceExamples = NormalizeExamples(contract?.AcceptanceExamples),
            Checks = NormalizeChecks(contract?.Checks, validation, sourceContract?.Checks),
            Constraints = NormalizeConstraints(contract?.Constraints, constraints, sourceContract?.Constraints),
            NonGoals = ResolveList(contract?.NonGoals, sourceContract?.NonGoals),
            AutoCompleteAllowed = contract?.AutoCompleteAllowed ?? sourceContract?.AutoCompleteAllowed ?? false,
            EvidenceRequired = NormalizeEvidenceRequirements(contract?.EvidenceRequired, validation, sourceContract?.EvidenceRequired),
            HumanReview = NormalizeHumanReview(contract?.HumanReview, sourceContract?.HumanReview),
            Traceability = new AcceptanceContractTraceability
            {
                SourceCardId = cardId,
                SourceTaskId = taskId,
                DerivedTaskIds = ResolveList(contract?.Traceability.DerivedTaskIds),
                RelatedArtifacts = ResolveList(contract?.Traceability.RelatedArtifacts),
            },
        };
    }

    private static AcceptanceContractChecks NormalizeChecks(
        AcceptanceContractChecks? checks,
        ValidationPlan? validation = null,
        AcceptanceContractChecks? sourceChecks = null)
    {
        return new AcceptanceContractChecks
        {
            UnitTests = ResolveList(checks?.UnitTests, sourceChecks?.UnitTests),
            IntegrationTests = ResolveList(checks?.IntegrationTests, sourceChecks?.IntegrationTests),
            RegressionTests = ResolveList(checks?.RegressionTests, sourceChecks?.RegressionTests),
            PolicyChecks = ResolveList(checks?.PolicyChecks, sourceChecks?.PolicyChecks),
            AdditionalChecks = ResolveList(checks?.AdditionalChecks, validation?.Checks, sourceChecks?.AdditionalChecks),
        };
    }

    private static AcceptanceContractConstraintSet NormalizeConstraints(
        AcceptanceContractConstraintSet? constraints,
        IReadOnlyList<string> mustNot,
        AcceptanceContractConstraintSet? sourceConstraints)
    {
        return new AcceptanceContractConstraintSet
        {
            MustNot = ResolveList(constraints?.MustNot, mustNot, sourceConstraints?.MustNot),
            Architecture = ResolveList(constraints?.Architecture, sourceConstraints?.Architecture),
            ScopeLimit = constraints?.ScopeLimit ?? sourceConstraints?.ScopeLimit,
        };
    }

    private static IReadOnlyList<AcceptanceContractEvidenceRequirement> NormalizeEvidenceRequirements(
        IReadOnlyList<AcceptanceContractEvidenceRequirement>? explicitRequirements,
        ValidationPlan? validation = null,
        IReadOnlyList<AcceptanceContractEvidenceRequirement>? sourceRequirements = null)
    {
        var normalized = explicitRequirements?
            .Where(item => !string.IsNullOrWhiteSpace(item.Type) || !string.IsNullOrWhiteSpace(item.Description))
            .Select(item => new AcceptanceContractEvidenceRequirement
            {
                Type = ResolveText(item.Type, "named_evidence"),
                Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
            })
            .ToArray();
        if (normalized is { Length: > 0 })
        {
            return normalized;
        }

        if (validation is { ExpectedEvidence.Count: > 0 })
        {
            return validation.ExpectedEvidence
                .Select(item => new AcceptanceContractEvidenceRequirement
                {
                    Type = "named_evidence",
                    Description = item,
                })
                .ToArray();
        }

        return sourceRequirements?
            .Where(item => !string.IsNullOrWhiteSpace(item.Type) || !string.IsNullOrWhiteSpace(item.Description))
            .Select(item => new AcceptanceContractEvidenceRequirement
            {
                Type = ResolveText(item.Type, "named_evidence"),
                Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
            })
            .ToArray()
            ?? Array.Empty<AcceptanceContractEvidenceRequirement>();
    }

    private static AcceptanceContractHumanReviewPolicy NormalizeHumanReview(
        AcceptanceContractHumanReviewPolicy? humanReview,
        AcceptanceContractHumanReviewPolicy? sourceHumanReview)
    {
        var policy = humanReview ?? sourceHumanReview ?? new AcceptanceContractHumanReviewPolicy();
        var decisions = policy.Decisions.Count == 0
            ? new AcceptanceContractHumanReviewPolicy().Decisions
            : policy.Decisions.Distinct().ToArray();
        return new AcceptanceContractHumanReviewPolicy
        {
            Required = policy.Required,
            ProvisionalAllowed = policy.ProvisionalAllowed,
            Decisions = decisions,
        };
    }

    private static IReadOnlyList<AcceptanceContractExample> NormalizeExamples(IReadOnlyList<AcceptanceContractExample>? examples)
    {
        return examples?
            .Where(item => !string.IsNullOrWhiteSpace(item.Given)
                || !string.IsNullOrWhiteSpace(item.When)
                || !string.IsNullOrWhiteSpace(item.Then))
            .Select(item => new AcceptanceContractExample
            {
                Given = item.Given?.Trim() ?? string.Empty,
                When = item.When?.Trim() ?? string.Empty,
                Then = item.Then?.Trim() ?? string.Empty,
            })
            .ToArray()
            ?? Array.Empty<AcceptanceContractExample>();
    }

    private static string ResolveContractId(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static DateTimeOffset ResolveCreatedAt(AcceptanceContract? contract)
        => contract is { CreatedAtUtc: var createdAt } && createdAt != default
            ? createdAt
            : DateTimeOffset.UtcNow;

    private static string ResolveText(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveList(params IReadOnlyList<string>?[] sources)
    {
        return sources
            .Where(source => source is not null)
            .SelectMany(source => source!)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
