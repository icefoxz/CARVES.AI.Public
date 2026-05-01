namespace Carves.Runtime.Application.Platform;

public sealed record PlatformVocabularyLintReport(
    IReadOnlyList<PlatformVocabularyLintFinding> Findings)
{
    public int WarningCount => Findings.Count(finding => string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase));

    public int SuggestionCount => Findings.Count(finding => string.Equals(finding.Severity, "suggestion", StringComparison.OrdinalIgnoreCase));

    public bool HasBlockingViolations => WarningCount > 0;
}
