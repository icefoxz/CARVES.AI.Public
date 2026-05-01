namespace Carves.Runtime.Application.Interaction;

public static class RuntimeTurnLanguageResolutionContract
{
    public const string CurrentVersion = "runtime-turn-language-resolution.v1";
}

public static class RuntimeTurnLanguageResolutionSources
{
    public const string DefaultEnglish = "default_english";
    public const string TextContainsChinese = "text_contains_chinese";
    public const string ExplicitUserLanguage = "explicit_user_language";
    public const string SessionState = "session_state";
    public const string ProfileLanguage = "profile_language";
}

public sealed record RuntimeTurnLanguageResolutionInput(
    string? UserText,
    string? ProfileLanguageMode = null,
    string? SessionLanguage = null);

public sealed record RuntimeTurnLanguageResolution(
    string ContractVersion,
    string ResponseLanguage,
    string Source,
    string? SessionLanguage,
    bool ExplicitUserOverride,
    bool DetectedChinese);

public sealed class RuntimeTurnLanguageResolver
{
    public RuntimeTurnLanguageResolution Resolve(RuntimeTurnLanguageResolutionInput input)
    {
        var detectedChinese = ContainsChinese(input.UserText);
        if (ResolveExplicitUserLanguage(input.UserText) is { } explicitLanguage)
        {
            return Resolution(
                explicitLanguage,
                RuntimeTurnLanguageResolutionSources.ExplicitUserLanguage,
                explicitLanguage,
                explicitUserOverride: true,
                detectedChinese);
        }

        if (NormalizeLanguage(input.ProfileLanguageMode) is { } profileLanguage)
        {
            return Resolution(
                profileLanguage,
                RuntimeTurnLanguageResolutionSources.ProfileLanguage,
                profileLanguage,
                explicitUserOverride: false,
                detectedChinese);
        }

        if (NormalizeLanguage(input.SessionLanguage) is { } sessionLanguage)
        {
            return Resolution(
                sessionLanguage,
                RuntimeTurnLanguageResolutionSources.SessionState,
                sessionLanguage,
                explicitUserOverride: false,
                detectedChinese);
        }

        if (detectedChinese)
        {
            return Resolution(
                RuntimeTurnAwarenessProfileLanguageModes.Chinese,
                RuntimeTurnLanguageResolutionSources.TextContainsChinese,
                RuntimeTurnAwarenessProfileLanguageModes.Chinese,
                explicitUserOverride: false,
                detectedChinese);
        }

        return Resolution(
            RuntimeTurnAwarenessProfileLanguageModes.English,
            RuntimeTurnLanguageResolutionSources.DefaultEnglish,
            RuntimeTurnAwarenessProfileLanguageModes.English,
            explicitUserOverride: false,
            detectedChinese);
    }

    public static bool ContainsChinese(string? text)
    {
        return !string.IsNullOrWhiteSpace(text)
            && text.Any(character => character is >= '\u4e00' and <= '\u9fff');
    }

    private static RuntimeTurnLanguageResolution Resolution(
        string responseLanguage,
        string source,
        string? sessionLanguage,
        bool explicitUserOverride,
        bool detectedChinese)
    {
        return new RuntimeTurnLanguageResolution(
            RuntimeTurnLanguageResolutionContract.CurrentVersion,
            responseLanguage,
            source,
            sessionLanguage,
            explicitUserOverride,
            detectedChinese);
    }

    private static string? ResolveExplicitUserLanguage(string? userText)
    {
        var normalized = NormalizeText(userText);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (ContainsAny(
                normalized,
                "用中文",
                "以中文",
                "中文交流",
                "中文回复",
                "中文回答",
                "说中文",
                "切换中文",
                "切换到中文",
                "use chinese",
                "reply in chinese",
                "respond in chinese",
                "switch to chinese",
                "chinese please"))
        {
            return RuntimeTurnAwarenessProfileLanguageModes.Chinese;
        }

        if (ContainsAny(
                normalized,
                "用英文",
                "用英语",
                "以英文",
                "以英语",
                "英文交流",
                "英语交流",
                "英文回复",
                "英语回复",
                "英文回答",
                "英语回答",
                "说英文",
                "说英语",
                "切换英文",
                "切换英语",
                "切换到英文",
                "切换到英语",
                "use english",
                "reply in english",
                "respond in english",
                "switch to english",
                "english please",
                "in english"))
        {
            return RuntimeTurnAwarenessProfileLanguageModes.English;
        }

        return null;
    }

    private static string? NormalizeLanguage(string? language)
    {
        return language switch
        {
            RuntimeTurnAwarenessProfileLanguageModes.Chinese => RuntimeTurnAwarenessProfileLanguageModes.Chinese,
            RuntimeTurnAwarenessProfileLanguageModes.English => RuntimeTurnAwarenessProfileLanguageModes.English,
            _ => null,
        };
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is >= '\u4e00' and <= '\u9fff'
                ? character
                : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool ContainsAny(string text, params string[] fragments)
    {
        return fragments.Any(fragment => text.Contains(fragment, StringComparison.Ordinal));
    }
}
