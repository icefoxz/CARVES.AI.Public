using System.Net;

namespace Carves.Runtime.Application.Shield;

public sealed class ShieldBadgeService
{
    public const string ResultSchemaVersion = "shield-badge.v0";

    private readonly ShieldEvaluationService evaluator;

    public ShieldBadgeService()
        : this(new ShieldEvaluationService())
    {
    }

    public ShieldBadgeService(ShieldEvaluationService evaluator)
    {
        this.evaluator = evaluator;
    }

    public ShieldBadgeResult CreateFromEvidenceFile(string repositoryRoot, string evidencePath, string? outputPath = null)
    {
        var evaluation = evaluator.EvaluateFile(repositoryRoot, evidencePath, ShieldEvaluationOutput.Combined);
        return Create(evaluation, outputPath);
    }

    public ShieldBadgeResult Create(ShieldEvaluationResult evaluation, string? outputPath = null)
    {
        if (!evaluation.IsOk || evaluation.Standard is null || evaluation.Lite is null)
        {
            return new ShieldBadgeResult(
                ResultSchemaVersion,
                evaluation.Status,
                SelfCheck: true,
                Certification: false,
                outputPath,
                Badge: null,
                evaluation,
                evaluation.Errors);
        }

        var standardCompact = BuildStandardCompact(evaluation.Standard);
        var color = ResolveColor(evaluation.Lite.Band);
        var message = $"{evaluation.Lite.Score}/100 {ToTitle(evaluation.Lite.Band)}";
        var label = "CARVES Shield";
        var altText = $"CARVES Shield self-check: {message}; {standardCompact}; certification false";
        var markdownTarget = string.IsNullOrWhiteSpace(outputPath)
            ? "shield-badge.svg"
            : outputPath.Replace('\\', '/');
        var payload = new ShieldBadgePayload(
            label,
            message,
            color.Hex,
            color.Name,
            color.Text,
            standardCompact,
            evaluation.Lite.Score,
            evaluation.Lite.Band,
            evaluation.Standard.CriticalGates,
            altText,
            $"![{label}]({markdownTarget})",
            RenderSvg(label, message, color.Hex, color.Text, altText));

        return new ShieldBadgeResult(
            ResultSchemaVersion,
            ShieldEvaluationStatuses.Ok,
            SelfCheck: true,
            Certification: false,
            outputPath,
            payload,
            evaluation,
            Array.Empty<ShieldEvaluationError>());
    }

    private static string BuildStandardCompact(ShieldStandardResult standard)
    {
        var guard = standard.Dimensions.TryGetValue("guard", out var guardDimension) ? guardDimension.Level : "0";
        var handoff = standard.Dimensions.TryGetValue("handoff", out var handoffDimension) ? handoffDimension.Level : "0";
        var audit = standard.Dimensions.TryGetValue("audit", out var auditDimension) ? auditDimension.Level : "0";
        return $"G{guard}.H{handoff}.A{audit}";
    }

    private static BadgeColor ResolveColor(string band)
    {
        return band switch
        {
            "critical" => new BadgeColor("red", "#d73a49", "#ffffff"),
            "strong" => new BadgeColor("green", "#2ea44f", "#ffffff"),
            "disciplined" => new BadgeColor("yellow", "#d4a72c", "#24292f"),
            "basic" => new BadgeColor("white", "#f6f8fa", "#24292f"),
            "no_evidence" => new BadgeColor("gray", "#6a737d", "#ffffff"),
            _ => new BadgeColor("gray", "#6a737d", "#ffffff"),
        };
    }

    private static string ToTitle(string value)
    {
        return value switch
        {
            "no_evidence" => "No Evidence",
            "" => value,
            _ => char.ToUpperInvariant(value[0]) + value[1..],
        };
    }

    private static string RenderSvg(string label, string message, string messageColor, string messageTextColor, string altText)
    {
        var leftWidth = EstimateWidth(label);
        var rightWidth = EstimateWidth(message);
        var totalWidth = leftWidth + rightWidth;
        var escapedLabel = WebUtility.HtmlEncode(label);
        var escapedMessage = WebUtility.HtmlEncode(message);
        var escapedAlt = WebUtility.HtmlEncode(altText);

        return $"""
        <svg xmlns="http://www.w3.org/2000/svg" width="{totalWidth}" height="20" role="img" aria-label="{escapedAlt}">
          <title>{escapedAlt}</title>
          <linearGradient id="s" x2="0" y2="100%">
            <stop offset="0" stop-color="#bbb" stop-opacity=".1"/>
            <stop offset="1" stop-opacity=".1"/>
          </linearGradient>
          <clipPath id="r">
            <rect width="{totalWidth}" height="20" rx="3" fill="#fff"/>
          </clipPath>
          <g clip-path="url(#r)">
            <rect width="{leftWidth}" height="20" fill="#555"/>
            <rect x="{leftWidth}" width="{rightWidth}" height="20" fill="{messageColor}"/>
            <rect width="{totalWidth}" height="20" fill="url(#s)"/>
          </g>
          <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
            <text x="{leftWidth / 2}" y="15" fill="#010101" fill-opacity=".3">{escapedLabel}</text>
            <text x="{leftWidth / 2}" y="14">{escapedLabel}</text>
          </g>
          <g fill="{messageTextColor}" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
            <text x="{leftWidth + rightWidth / 2}" y="15" fill="#010101" fill-opacity=".3">{escapedMessage}</text>
            <text x="{leftWidth + rightWidth / 2}" y="14">{escapedMessage}</text>
          </g>
        </svg>
        """;
    }

    private static int EstimateWidth(string text)
    {
        return Math.Max(40, (text.Length * 7) + 10);
    }

    private sealed record BadgeColor(string Name, string Hex, string Text);
}
