namespace Carves.Matrix.Tests;

public sealed class MatrixPublicClaimWordingTests
{
    private static readonly string[] PublicDocumentPaths =
    [
        Path.Combine("docs", "matrix", "README.md"),
        Path.Combine("docs", "matrix", "quickstart.en.md"),
        Path.Combine("docs", "matrix", "quickstart.zh-CN.md"),
        Path.Combine("docs", "matrix", "known-limitations.md")
    ];

    private static readonly string[] RequiredBoundaryTerms =
    [
        "producer identity",
        "signature",
        "transparency",
        "hosted verification",
        "certification",
        "benchmark",
        "sandbox",
        "semantic correctness"
    ];

    private static readonly string[] QualifiedProofTerms =
    [
        "local consistency proof",
        "summary-only proof bundle",
        "local workflow self-check"
    ];

    private static readonly string[] BannedUnqualifiedClaims =
    [
        "benchmark result",
        "proof system",
        "tamper-proof attestation",
        "public attestation",
        "signed proof",
        "hosted verification is available",
        "producer identity verified"
    ];

    [Fact]
    public void PublicMatrixDocs_KeepProofLanguageInsideLocalSummaryOnlyBounds()
    {
        foreach (var path in PublicDocumentPaths)
        {
            var text = ReadRepoText(path);

            AssertContainsAny(QualifiedProofTerms, text, path);
            foreach (var term in RequiredBoundaryTerms)
            {
                Assert.Contains(term, text, StringComparison.OrdinalIgnoreCase);
            }

            AssertNoBannedUnqualifiedClaims(path, text);
        }
    }

    [Fact]
    public void MatrixCliUsage_DeclaresLocalSummaryOnlyNonClaims()
    {
        var usage = ReadRepoText(Path.Combine("src", "CARVES.Matrix.Core", "MatrixCliUsage.cs"));

        AssertContainsAny(QualifiedProofTerms, usage, "MatrixCliUsage.cs");
        foreach (var term in RequiredBoundaryTerms)
        {
            Assert.Contains(term, usage, StringComparison.OrdinalIgnoreCase);
        }

        AssertNoBannedUnqualifiedClaims("MatrixCliUsage.cs", usage);
    }

    private static string ReadRepoText(string relativePath)
    {
        return File.ReadAllText(Path.Combine(MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(), relativePath));
    }

    private static void AssertContainsAny(IEnumerable<string> terms, string text, string source)
    {
        Assert.True(
            terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)),
            $"{source} must contain at least one qualified Matrix proof term.");
    }

    private static void AssertNoBannedUnqualifiedClaims(string source, string text)
    {
        foreach (var claim in BannedUnqualifiedClaims)
        {
            Assert.DoesNotContain(claim, text, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var phrase in FindCaseInsensitive(text, "certified safe"))
        {
            Assert.StartsWith("not certified safe", phrase, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IEnumerable<string> FindCaseInsensitive(string text, string needle)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(needle, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                yield break;
            }

            var prefixStart = Math.Max(0, index - 4);
            yield return text.Substring(prefixStart, Math.Min(text.Length - prefixStart, needle.Length + 4));
            start = index + needle.Length;
        }
    }
}
