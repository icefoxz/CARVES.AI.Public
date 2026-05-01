using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

internal static class AcceptanceContractSummaryFormatter
{
    public static string BuildPlainTextBlock(string heading, AcceptanceContract? contract)
    {
        if (contract is null)
        {
            return string.Empty;
        }

        var lines = new List<string> { heading };
        lines.AddRange(BuildSummaryLines(contract));
        return string.Join(Environment.NewLine, lines);
    }

    public static IReadOnlyList<string> BuildSummaryLines(AcceptanceContract contract)
    {
        var lines = new List<string>
        {
            $"- contract: {contract.ContractId} [{ToSnakeCase(contract.Status.ToString())}]",
        };

        if (!string.IsNullOrWhiteSpace(contract.Title))
        {
            lines.Add($"- title: {Truncate(contract.Title, 160)}");
        }

        if (!string.IsNullOrWhiteSpace(contract.Intent.Goal))
        {
            lines.Add($"- goal: {Truncate(contract.Intent.Goal, 220)}");
        }

        if (!string.IsNullOrWhiteSpace(contract.Intent.BusinessValue))
        {
            lines.Add($"- business value: {Truncate(contract.Intent.BusinessValue, 180)}");
        }

        var examples = contract.AcceptanceExamples
            .Select(BuildExampleSummary)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Take(3)
            .ToArray();
        for (var index = 0; index < examples.Length; index++)
        {
            lines.Add($"- example {index + 1}: {examples[index]}");
        }

        var mustNot = JoinLimited(contract.Constraints.MustNot, 4, 180);
        if (!string.IsNullOrWhiteSpace(mustNot))
        {
            lines.Add($"- must not: {mustNot}");
        }

        var architecture = JoinLimited(contract.Constraints.Architecture, 3, 180);
        if (!string.IsNullOrWhiteSpace(architecture))
        {
            lines.Add($"- architecture constraints: {architecture}");
        }

        if (contract.Constraints.ScopeLimit is not null)
        {
            var scopeLimit = new List<string>();
            if (contract.Constraints.ScopeLimit.MaxFilesChanged is not null)
            {
                scopeLimit.Add($"max_files_changed={contract.Constraints.ScopeLimit.MaxFilesChanged.Value}");
            }

            if (contract.Constraints.ScopeLimit.MaxLinesChanged is not null)
            {
                scopeLimit.Add($"max_lines_changed={contract.Constraints.ScopeLimit.MaxLinesChanged.Value}");
            }

            if (scopeLimit.Count > 0)
            {
                lines.Add($"- scope limit: {string.Join("; ", scopeLimit)}");
            }
        }

        var nonGoals = JoinLimited(contract.NonGoals, 4, 180);
        if (!string.IsNullOrWhiteSpace(nonGoals))
        {
            lines.Add($"- non-goals: {nonGoals}");
        }

        var policyChecks = JoinLimited(contract.Checks.PolicyChecks, 4, 180);
        if (!string.IsNullOrWhiteSpace(policyChecks))
        {
            lines.Add($"- policy checks: {policyChecks}");
        }

        var evidence = contract.EvidenceRequired
            .Select(FormatEvidenceRequirement)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        lines.Add($"- evidence required: {JoinLimited(evidence, 4, 180, emptyValue: "(none)")}");

        var decisions = contract.HumanReview.Decisions
            .Select(decision => ToSnakeCase(decision.ToString()))
            .ToArray();
        lines.Add(
            $"- human review: required={(contract.HumanReview.Required ? "yes" : "no")}; provisional_allowed={(contract.HumanReview.ProvisionalAllowed ? "yes" : "no")}; decisions={JoinLimited(decisions, 4, 120, emptyValue: "(none)")}");

        var traceability = BuildTraceabilitySummary(contract.Traceability);
        if (!string.IsNullOrWhiteSpace(traceability))
        {
            lines.Add($"- traceability: {traceability}");
        }

        return lines;
    }

    private static string BuildExampleSummary(AcceptanceContractExample example)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(example.Given))
        {
            parts.Add($"given {Truncate(example.Given, 90)}");
        }

        if (!string.IsNullOrWhiteSpace(example.When))
        {
            parts.Add($"when {Truncate(example.When, 90)}");
        }

        if (!string.IsNullOrWhiteSpace(example.Then))
        {
            parts.Add($"then {Truncate(example.Then, 90)}");
        }

        return string.Join("; ", parts);
    }

    private static string FormatEvidenceRequirement(AcceptanceContractEvidenceRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement.Type))
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(requirement.Description)
            ? requirement.Type
            : $"{requirement.Type} ({Truncate(requirement.Description, 80)})";
    }

    private static string BuildTraceabilitySummary(AcceptanceContractTraceability traceability)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(traceability.SourceCardId))
        {
            parts.Add($"card={traceability.SourceCardId}");
        }

        if (!string.IsNullOrWhiteSpace(traceability.SourceTaskId))
        {
            parts.Add($"task={traceability.SourceTaskId}");
        }

        if (traceability.DerivedTaskIds.Count > 0)
        {
            parts.Add($"derived={JoinLimited(traceability.DerivedTaskIds, 3, 90)}");
        }

        return string.Join("; ", parts);
    }

    private static string JoinLimited(
        IEnumerable<string> items,
        int maxItems,
        int maxLength,
        string emptyValue = "")
    {
        var materialized = items
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
        if (materialized.Length == 0)
        {
            return emptyValue;
        }

        var limited = materialized.Take(maxItems).ToList();
        var joined = string.Join(", ", limited);
        if (materialized.Length > limited.Count)
        {
            joined = $"{joined}, +{materialized.Length - limited.Count} more";
        }

        return Truncate(joined, maxLength);
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var buffer = new System.Text.StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                var previous = value[index - 1];
                var nextIsLower = index + 1 < value.Length && char.IsLower(value[index + 1]);
                if (char.IsLower(previous) || char.IsDigit(previous) || nextIsLower)
                {
                    buffer.Append('_');
                }
            }

            buffer.Append(char.ToLowerInvariant(current));
        }

        return buffer.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }
}
