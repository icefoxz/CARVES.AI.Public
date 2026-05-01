using System.Text.RegularExpressions;
using Carves.Runtime.Application.CodeGraph;

namespace Carves.Runtime.Application.Refactoring;

public sealed partial class RefactoringService
{
    private static readonly Regex MethodSignaturePattern = new(@"^\s*(public|private|internal|protected|static|\[).*$", RegexOptions.Compiled);

    private IReadOnlyList<RefactoringFinding> DetectFindings()
    {
        var findings = new List<RefactoringFinding>();
        foreach (var directory in CodeDirectoryDiscoveryPolicy.ResolveEffectiveDirectories(repoRoot, systemConfig))
        {
            var absoluteDirectory = directory == "."
                ? repoRoot
                : Path.Combine(repoRoot, directory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteDirectory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(absoluteDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(path);
                TryAddLargeFileFinding(findings, path, lines);
                TryAddLargeFunctionFinding(findings, path, lines);
            }
        }

        return findings;
    }

    private void TryAddLargeFileFinding(ICollection<RefactoringFinding> findings, string path, IReadOnlyList<string> lines)
    {
        if (lines.Count <= 180)
        {
            return;
        }

        findings.Add(CreateFinding("file_too_large", path, $"File contains {lines.Count} lines", new Dictionary<string, int>
        {
            ["line_count"] = lines.Count,
        }));
    }

    private void TryAddLargeFunctionFinding(ICollection<RefactoringFinding> findings, string path, IReadOnlyList<string> lines)
    {
        var currentBlockLines = 0;
        foreach (var line in lines)
        {
            if (MethodSignaturePattern.IsMatch(line))
            {
                currentBlockLines = 1;
                continue;
            }

            if (currentBlockLines == 0)
            {
                continue;
            }

            currentBlockLines += 1;
            if (currentBlockLines == 90)
            {
                findings.Add(CreateFinding("function_too_large", path, "A method-like block exceeded 90 lines", new Dictionary<string, int>
                {
                    ["block_length"] = currentBlockLines,
                }));
            }
        }
    }

    private RefactoringFinding CreateFinding(string kind, string path, string reason, IReadOnlyDictionary<string, int> metrics)
    {
        return new RefactoringFinding(
            $"RF-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}",
            kind,
            Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
            reason,
            "warning",
            metrics);
    }
}
