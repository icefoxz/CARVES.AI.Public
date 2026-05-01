namespace Carves.Runtime.Application.Interaction;

internal sealed record RuntimeTurnExpressionStyle(
    string? SummaryPrefix,
    string? QuestionPrefix,
    string AwarenessActor,
    IReadOnlyList<string> SummaryCues,
    IReadOnlyList<string> QuestionCues,
    IReadOnlyList<string> NextActionSuffixes)
{
    public static RuntimeTurnExpressionStyle From(RuntimeTurnStyleProfile? profile)
    {
        if (profile is null
            || string.Equals(profile.StyleProfileId, RuntimeTurnStyleProfile.RuntimeDefault.StyleProfileId, StringComparison.Ordinal))
        {
            return Default;
        }

        return new RuntimeTurnExpressionStyle(
            ResolveSummaryPrefix(profile),
            ResolveQuestionPrefix(profile),
            ResolveAwarenessActor(profile),
            ResolveSummaryCues(profile),
            ResolveQuestionCues(profile),
            ResolveNextActionSuffixes(profile));
    }

    public string ApplySummary(string summary)
    {
        var shapedSummary = string.IsNullOrWhiteSpace(SummaryPrefix)
            ? summary
            : $"{SummaryPrefix} {summary}";
        return SummaryCues.Count == 0
            ? shapedSummary
            : RuntimeTurnPostureGuidance.JoinResponse(shapedSummary, string.Join(' ', SummaryCues));
    }

    public string ApplySummary(string summary, string languageMode)
    {
        return IsChinese(languageMode) ? ApplyChineseSummary(summary) : ApplySummary(summary);
    }

    public string ApplyQuestion(string question)
    {
        var shapedQuestion = string.IsNullOrWhiteSpace(QuestionPrefix)
            ? question
            : $"{QuestionPrefix} {question}";
        return QuestionCues.Count == 0
            ? shapedQuestion
            : RuntimeTurnPostureGuidance.JoinResponse(shapedQuestion, string.Join(' ', QuestionCues));
    }

    public string ApplyQuestion(string question, string languageMode)
    {
        return IsChinese(languageMode) ? ApplyChineseQuestion(question) : ApplyQuestion(question);
    }

    public string ApplyAwareness(string awareness)
    {
        var actor = string.IsNullOrWhiteSpace(AwarenessActor)
            ? "runtime guidance"
            : AwarenessActor;
        return $"Awareness in use: {actor} is active; {awareness}";
    }

    public string ApplyAwareness(string awareness, string languageMode)
    {
        if (!IsChinese(languageMode))
        {
            return ApplyAwareness(awareness);
        }

        return $"意识正在工作：{ResolveChineseAwarenessActor()}；{awareness}";
    }

    public string ApplyNextAction(string nextAction)
    {
        return NextActionSuffixes.Count == 0
            ? nextAction
            : RuntimeTurnPostureGuidance.JoinResponse(nextAction, string.Join(' ', NextActionSuffixes));
    }

    public string ApplyNextAction(string nextAction, string languageMode)
    {
        return IsChinese(languageMode) ? ApplyChineseNextAction(nextAction) : ApplyNextAction(nextAction);
    }

    private static RuntimeTurnExpressionStyle Default { get; } = new(null, null, "runtime guidance", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    private string ApplyChineseSummary(string summary)
    {
        var shapedSummary = ResolveChineseSummaryPrefix(SummaryPrefix) is { } prefix
            ? $"{prefix}{summary}"
            : summary;
        var cues = SummaryCues
            .Select(ResolveChineseSummaryCue)
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return cues.Length == 0
            ? shapedSummary
            : RuntimeTurnPostureGuidance.JoinResponse(shapedSummary, string.Join(' ', cues));
    }

    private string ApplyChineseQuestion(string question)
    {
        var shapedQuestion = ResolveChineseQuestionPrefix(QuestionPrefix) is { } prefix
            ? $"{prefix}{question}"
            : question;
        var cues = QuestionCues
            .Select(ResolveChineseQuestionCue)
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return cues.Length == 0
            ? shapedQuestion
            : RuntimeTurnPostureGuidance.JoinResponse(shapedQuestion, string.Join(' ', cues));
    }

    private string ApplyChineseNextAction(string nextAction)
    {
        var suffixes = NextActionSuffixes
            .Select(ResolveChineseNextActionSuffix)
            .Where(suffix => !string.IsNullOrWhiteSpace(suffix))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return suffixes.Length == 0
            ? nextAction
            : RuntimeTurnPostureGuidance.JoinResponse(nextAction, string.Join(' ', suffixes));
    }

    private static string? ResolveChineseSummaryPrefix(string? prefix)
    {
        return prefix switch
        {
            "Assistant posture:" => "助理姿态：",
            "Architecture posture:" => "架构复审姿态：",
            "Guard posture:" => "守卫姿态：",
            "Project-manager posture:" => "项目经理姿态：",
            "Decision state:" => "决策状态：",
            _ => null,
        };
    }

    private static string? ResolveChineseQuestionPrefix(string? prefix)
    {
        return prefix switch
        {
            "Helpful check:" => "助理检查：",
            "Planning check:" => "计划检查：",
            "Confirm or correct:" => "确认或修正：",
            "Confirm first:" => "先确认：",
            _ => null,
        };
    }

    private static string? ResolveChineseSummaryCue(string cue)
    {
        return cue switch
        {
            "Awareness: assistant keeps the next answer easy to use without taking control." => "意识：助理保持回答易用，但不接管控制。",
            "Awareness: architecture reviewer keeps contracts, boundaries, and downstream impact visible." => "意识：架构复审保持契约、边界和下游影响可见。",
            "Awareness: guard keeps authority, safety, and truth-write boundaries visible." => "意识：守卫保持权限、安全和 truth 写入边界可见。",
            "Awareness: project manager keeps decision, sequence, owner, and review gate visible." => "意识：项目经理保持决策、顺序、负责人和复核点可见。",
            "Style: compact." => "风格：简洁。",
            "Style: test assumptions." => "风格：检查假设。",
            "Style: calm and supportive." => "风格：平稳支持。",
            "Style: direct about tradeoffs." => "风格：直接说明取舍。",
            _ => null,
        };
    }

    private static string? ResolveChineseQuestionCue(string cue)
    {
        return cue switch
        {
            "Check the weakest assumption before confirming." => "确认前先检查最弱假设。",
            "Keep the check lightweight." => "保持检查轻量。",
            "Keep correction easy to say." => "让修正容易说出口。",
            _ => null,
        };
    }

    private static string? ResolveChineseNextActionSuffix(string suffix)
    {
        return suffix switch
        {
            "Keep the wording user-facing and bounded." => "保持面向用户且有边界。",
            "Keep sequence, owner, and review point explicit." => "保持顺序、负责人和复核点明确。",
            "Keep the next move explicit." => "明确下一步。",
            "Keep room for discussion before landing." => "落案前保留讨论空间。",
            "Check the weakest assumption before landing." => "落案前检查最弱假设。",
            "Avoid forcing a challenge unless the user asks." => "除非用户要求，否则不要强行质疑。",
            "Name blockers before optional detail." => "先说阻塞，再说可选细节。",
            "Keep wording compact." => "保持措辞简洁。",
            "Check the central assumption before landing." => "落案前检查核心假设。",
            "Use plain wording." => "使用清晰直白的措辞。",
            "Keep the handoff sequence visible." => "保持交接顺序可见。",
            "Keep control boundaries visible." => "保持控制边界可见。",
            _ => null,
        };
    }

    private static string? ResolveSummaryPrefix(RuntimeTurnStyleProfile profile)
    {
        return NormalizeToken(profile.DefaultPostureId) switch
        {
            RuntimeTurnPostureCodes.Posture.Assistant => "Assistant posture:",
            RuntimeTurnPostureCodes.Posture.Architecture => "Architecture posture:",
            RuntimeTurnPostureCodes.Posture.Guard => "Guard posture:",
            RuntimeTurnPostureCodes.Posture.ProjectManager => "Project-manager posture:",
            _ when string.Equals(NormalizeToken(profile.SummaryStyle), "decision_led", StringComparison.Ordinal) => "Decision state:",
            _ => null,
        };
    }

    private static string? ResolveQuestionPrefix(RuntimeTurnStyleProfile profile)
    {
        var explicitPrefix = ResolveQuestionPrefix(profile.QuestionStyle);
        if (!string.IsNullOrWhiteSpace(explicitPrefix))
        {
            return explicitPrefix;
        }

        return NormalizeToken(profile.DefaultPostureId) switch
        {
            RuntimeTurnPostureCodes.Posture.Assistant => "Helpful check:",
            RuntimeTurnPostureCodes.Posture.ProjectManager => "Planning check:",
            _ => null,
        };
    }

    private static string? ResolveQuestionPrefix(string? questionStyle)
    {
        return NormalizeToken(questionStyle) switch
        {
            "confirm_or_correct" => "Confirm or correct:",
            "confirm_first" => "Confirm first:",
            _ => null,
        };
    }

    private static string ResolveAwarenessActor(RuntimeTurnStyleProfile profile)
    {
        return NormalizeToken(profile.DefaultPostureId) switch
        {
            RuntimeTurnPostureCodes.Posture.Assistant => "assistant awareness",
            RuntimeTurnPostureCodes.Posture.Architecture => "architecture-reviewer awareness",
            RuntimeTurnPostureCodes.Posture.Guard => "guard awareness",
            RuntimeTurnPostureCodes.Posture.ProjectManager => "project-manager awareness",
            _ => "runtime guidance",
        };
    }

    private static IReadOnlyList<string> ResolveSummaryCues(RuntimeTurnStyleProfile profile)
    {
        var cues = new List<string>(5);
        switch (NormalizeToken(profile.DefaultPostureId))
        {
            case RuntimeTurnPostureCodes.Posture.Assistant:
                cues.Add("Awareness: assistant keeps the next answer easy to use without taking control.");
                break;
            case RuntimeTurnPostureCodes.Posture.Architecture:
                cues.Add("Awareness: architecture reviewer keeps contracts, boundaries, and downstream impact visible.");
                break;
            case RuntimeTurnPostureCodes.Posture.Guard:
                cues.Add("Awareness: guard keeps authority, safety, and truth-write boundaries visible.");
                break;
            case RuntimeTurnPostureCodes.Posture.ProjectManager:
                cues.Add("Awareness: project manager keeps decision, sequence, owner, and review gate visible.");
                break;
        }

        cues.AddRange(ResolveCustomPersonalityNoteSummaryCues(profile.CustomPersonalityNote));
        return cues
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveQuestionCues(RuntimeTurnStyleProfile profile)
    {
        var cues = new List<string>(3);
        switch (NormalizeToken(profile.ChallengeLevel))
        {
            case "high":
                cues.Add("Check the weakest assumption before confirming.");
                break;
            case "low":
                cues.Add("Keep the check lightweight.");
                break;
        }

        if (ContainsAny(NormalizeToken(profile.CustomPersonalityNote), "patient", "calm", "support", "gentle", "温和", "耐心", "支持"))
        {
            cues.Add("Keep correction easy to say.");
        }

        return cues
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveNextActionSuffixes(RuntimeTurnStyleProfile profile)
    {
        var suffixes = new List<string>(6);
        switch (NormalizeToken(profile.DefaultPostureId))
        {
            case RuntimeTurnPostureCodes.Posture.Assistant:
                suffixes.Add("Keep the wording user-facing and bounded.");
                break;
            case RuntimeTurnPostureCodes.Posture.ProjectManager:
                suffixes.Add("Keep sequence, owner, and review point explicit.");
                break;
        }

        switch (NormalizeToken(profile.Directness))
        {
            case "high":
                suffixes.Add("Keep the next move explicit.");
                break;
            case "low":
                suffixes.Add("Keep room for discussion before landing.");
                break;
        }

        switch (NormalizeToken(profile.ChallengeLevel))
        {
            case "high":
                suffixes.Add("Check the weakest assumption before landing.");
                break;
            case "low":
                suffixes.Add("Avoid forcing a challenge unless the user asks.");
                break;
        }

        if (string.Equals(NormalizeToken(profile.RiskSurfaceStyle), "surface_blockers_first", StringComparison.Ordinal))
        {
            suffixes.Add("Name blockers before optional detail.");
        }

        suffixes.AddRange(ResolveCustomPersonalityNoteSuffixes(profile.CustomPersonalityNote));

        return suffixes
            .Distinct(StringComparer.Ordinal)
            .Take(5)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveCustomPersonalityNoteSummaryCues(string? customPersonalityNote)
    {
        var note = NormalizeToken(customPersonalityNote);
        if (string.IsNullOrWhiteSpace(note))
        {
            return Array.Empty<string>();
        }

        var cues = new List<string>(4);
        if (ContainsAny(note, "concise", "compact", "short", "succinct", "简洁", "紧凑"))
        {
            cues.Add("Style: compact.");
        }

        if (ContainsAny(note, "challenge", "assumption", "weak", "push back", "质疑", "假设"))
        {
            cues.Add("Style: test assumptions.");
        }

        if (ContainsAny(note, "patient", "calm", "support", "gentle", "温和", "耐心", "支持"))
        {
            cues.Add("Style: calm and supportive.");
        }

        if (ContainsAny(note, "strict", "blunt", "direct", "straight", "严格", "直白", "直接"))
        {
            cues.Add("Style: direct about tradeoffs.");
        }

        return cues;
    }

    private static IReadOnlyList<string> ResolveCustomPersonalityNoteSuffixes(string? customPersonalityNote)
    {
        var note = NormalizeToken(customPersonalityNote);
        if (string.IsNullOrWhiteSpace(note))
        {
            return Array.Empty<string>();
        }

        var suffixes = new List<string>(4);
        if (ContainsAny(note, "concise", "compact", "short", "succinct", "简洁", "紧凑"))
        {
            suffixes.Add("Keep wording compact.");
        }

        if (ContainsAny(note, "challenge", "assumption", "weak", "push back", "质疑", "假设"))
        {
            suffixes.Add("Check the central assumption before landing.");
        }

        if (ContainsAny(note, "direct", "plain", "clear", "straight", "直接", "清晰"))
        {
            suffixes.Add("Use plain wording.");
        }

        if (ContainsAny(note, "project", "plan", "sequence", "owner", "review", "项目", "计划", "顺序", "负责人", "复审"))
        {
            suffixes.Add("Keep the handoff sequence visible.");
        }

        if (ContainsAny(note, "risk", "blocker", "boundary", "permission", "authority", "风险", "阻塞", "边界", "权限"))
        {
            suffixes.Add("Keep control boundaries visible.");
        }

        return suffixes;
    }

    private string ResolveChineseAwarenessActor()
    {
        return NormalizeToken(AwarenessActor) switch
        {
            "assistant awareness" => "助理意识",
            "architecture-reviewer awareness" => "架构复审意识",
            "guard awareness" => "守卫意识",
            "project-manager awareness" => "项目经理意识",
            _ => "运行时引导",
        };
    }

    private static bool IsChinese(string languageMode)
    {
        return string.Equals(languageMode, RuntimeTurnAwarenessProfileLanguageModes.Chinese, StringComparison.Ordinal);
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
