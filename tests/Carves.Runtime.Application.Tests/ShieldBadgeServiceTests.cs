using Carves.Runtime.Application.Shield;

namespace Carves.Runtime.Application.Tests;

public sealed class ShieldBadgeServiceTests
{
    [Fact]
    public void Create_ProducesStaticSvgAndSelfCheckMetadata()
    {
        var evidence = File.ReadAllText(Path.Combine(
            ResolveRepoRoot(),
            "docs",
            "shield",
            "examples",
            "shield-evidence-standard.example.json"));
        var evaluation = new ShieldEvaluationService().Evaluate(evidence, ShieldEvaluationOutput.Combined);

        var result = new ShieldBadgeService().Create(evaluation, "docs/shield-badge.svg");

        Assert.True(result.IsOk);
        Assert.True(result.SelfCheck);
        Assert.False(result.Certification);
        Assert.NotNull(result.Badge);
        Assert.Equal("90/100 Strong", result.Badge!.Message);
        Assert.Equal("green", result.Badge.ColorName);
        Assert.Equal("#2ea44f", result.Badge.Color);
        Assert.Equal("G8.H8.A8", result.Badge.StandardCompact);
        Assert.Contains("self-check", result.Badge.AltText, StringComparison.Ordinal);
        Assert.Contains("certification false", result.Badge.AltText, StringComparison.Ordinal);
        Assert.Contains("<svg", result.Badge.Svg, StringComparison.Ordinal);
        Assert.Contains("CARVES Shield", result.Badge.Svg, StringComparison.Ordinal);
        Assert.Contains("90/100 Strong", result.Badge.Svg, StringComparison.Ordinal);
        Assert.Equal("![CARVES Shield](docs/shield-badge.svg)", result.Badge.Markdown);
    }

    [Theory]
    [InlineData("critical", "red", "#d73a49", "#ffffff")]
    [InlineData("disciplined", "yellow", "#d4a72c", "#24292f")]
    [InlineData("basic", "white", "#f6f8fa", "#24292f")]
    [InlineData("no_evidence", "gray", "#6a737d", "#ffffff")]
    public void Create_MapsLiteBandsToBadgeColors(string band, string colorName, string color, string textColor)
    {
        var result = new ShieldBadgeService().Create(BuildEvaluation(50, band), "shield.svg");

        Assert.True(result.IsOk);
        Assert.Equal(colorName, result.Badge!.ColorName);
        Assert.Equal(color, result.Badge.Color);
        Assert.Equal(textColor, result.Badge.MessageTextColor);
    }

    [Fact]
    public void Create_InvalidPrivacyEvaluationDoesNotProducePositiveBadge()
    {
        var evaluation = new ShieldEvaluationResult(
            ShieldEvaluationService.ResultSchemaVersion,
            ShieldEvaluationStatuses.InvalidPrivacyPosture,
            "self_check",
            "invalid",
            Certification: false,
            ShieldEvaluationService.EvidenceSchemaVersion,
            "invalid",
            SampleWindowDays: null,
            ConsumedEvidenceSha256: null,
            Standard: null,
            Lite: null,
            Errors:
            [
                new ShieldEvaluationError(
                    "invalid_privacy_posture",
                    "privacy.raw_diff_included must be false.",
                    ["privacy.raw_diff_included"]),
            ]);

        var result = new ShieldBadgeService().Create(evaluation, "shield.svg");

        Assert.False(result.IsOk);
        Assert.Null(result.Badge);
        Assert.False(result.Certification);
        Assert.Contains(result.Errors, error => error.Code == "invalid_privacy_posture");
    }

    private static ShieldEvaluationResult BuildEvaluation(int score, string band)
    {
        var dimensions = new Dictionary<string, ShieldStandardDimensionResult>(StringComparer.Ordinal)
        {
            ["guard"] = new("guard", "5", 5, "yellow", Array.Empty<string>(), "Guard projected to level 5."),
            ["handoff"] = new("handoff", "4", 4, "white", Array.Empty<string>(), "Handoff projected to level 4."),
            ["audit"] = new("audit", "3", 3, "white", Array.Empty<string>(), "Audit projected to level 3."),
        };
        var contributions = new Dictionary<string, ShieldDimensionContribution>(StringComparer.Ordinal)
        {
            ["guard"] = new("5", 40, 22),
            ["handoff"] = new("4", 30, 13),
            ["audit"] = new("3", 30, 10),
        };

        return new ShieldEvaluationResult(
            ShieldEvaluationService.ResultSchemaVersion,
            ShieldEvaluationStatuses.Ok,
            "self_check",
            "local_only",
            Certification: false,
            ShieldEvaluationService.EvidenceSchemaVersion,
            "fixture",
            SampleWindowDays: 30,
            ConsumedEvidenceSha256: null,
            new ShieldStandardResult(
                ShieldEvaluationService.StandardRubricId,
                "CARVES G5.H4.A3 /30d PASS",
                dimensions,
                OverallScore: null,
                Array.Empty<string>()),
            new ShieldLiteResult(
                ShieldEvaluationService.LiteModelId,
                score,
                band,
                SelfCheck: true,
                SampleWindowDays: 30,
                contributions,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            Array.Empty<ShieldEvaluationError>());
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root.");
    }
}
