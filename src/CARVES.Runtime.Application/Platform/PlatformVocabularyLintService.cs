using System.Text.RegularExpressions;
using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Application.Platform;

public sealed class PlatformVocabularyLintService
{
    private static readonly Regex PrimaryTypeRegex = new(@"\b(public|internal)\s+(?:sealed\s+|partial\s+|abstract\s+)?(?:class|record|struct|interface|enum)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

    private readonly string repoRoot;
    private readonly CarvesCodeStandard standard;

    public PlatformVocabularyLintService(string repoRoot, CarvesCodeStandard standard)
    {
        this.repoRoot = repoRoot;
        this.standard = standard;
    }

    public PlatformVocabularyLintReport Run()
    {
        var findings = new List<PlatformVocabularyLintFinding>();
        foreach (var relativeRoot in standard.ExtremeNaming.PlatformLintPaths)
        {
            var absoluteRoot = Path.Combine(repoRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories))
            {
                AnalyzeFile(filePath, findings);
            }
        }

        return new PlatformVocabularyLintReport(findings.OrderBy(finding => finding.RelativePath, StringComparer.Ordinal).ThenBy(finding => finding.RuleId, StringComparer.Ordinal).ToArray());
    }

    private void AnalyzeFile(string filePath, ICollection<PlatformVocabularyLintFinding> findings)
    {
        var content = File.ReadAllText(filePath);
        var match = PrimaryTypeRegex.Match(content);
        if (!match.Success)
        {
            return;
        }

        var symbolName = match.Groups["name"].Value;
        if (standard.ExtremeNaming.PlatformLintAllowlistTypeNames.Contains(symbolName, StringComparer.Ordinal))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(repoRoot, filePath).Replace(Path.DirectorySeparatorChar, '/');
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        if (!string.Equals(fileName, symbolName, StringComparison.Ordinal))
        {
            findings.Add(new PlatformVocabularyLintFinding(
                "CARVES007",
                "warning",
                relativePath,
                symbolName,
                $"File name should match primary type '{symbolName}'."));
        }

        foreach (var token in standard.ExtremeNaming.ForbiddenGenericWords.Where(token => symbolName.Contains(token, StringComparison.Ordinal)))
        {
            findings.Add(new PlatformVocabularyLintFinding(
                "CARVES003",
                "warning",
                relativePath,
                symbolName,
                $"Type name contains low-signal token '{token}'."));
        }

        if (symbolName.StartsWith("I", StringComparison.Ordinal) &&
            (symbolName.EndsWith("Manager", StringComparison.Ordinal)
             || symbolName.EndsWith("Processor", StringComparison.Ordinal)
             || string.Equals(symbolName, "IService", StringComparison.Ordinal)))
        {
            findings.Add(new PlatformVocabularyLintFinding(
                "CARVES006",
                "warning",
                relativePath,
                symbolName,
                "Interface name must expose a specific semantic role, not a generic abstraction label."));
        }

        foreach (var alias in standard.ExtremeNaming.PlatformForbiddenAliases)
        {
            if (!symbolName.Contains(alias.Key, StringComparison.Ordinal))
            {
                continue;
            }

            findings.Add(new PlatformVocabularyLintFinding(
                "CARVES012",
                "suggestion",
                relativePath,
                symbolName,
                $"Normalize runtime/platform token '{alias.Key}' to canonical term '{alias.Value}' when semantics match."));
        }
    }
}
