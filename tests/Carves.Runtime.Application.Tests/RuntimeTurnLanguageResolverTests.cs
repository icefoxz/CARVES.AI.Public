using Carves.Runtime.Application.Interaction;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTurnLanguageResolverTests
{
    [Fact]
    public void Resolve_DefaultsToEnglishWhenNoSignalExists()
    {
        var resolver = new RuntimeTurnLanguageResolver();

        var result = resolver.Resolve(new RuntimeTurnLanguageResolutionInput("Let's discuss the plan."));

        Assert.Equal(RuntimeTurnLanguageResolutionContract.CurrentVersion, result.ContractVersion);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.ResponseLanguage);
        Assert.Equal(RuntimeTurnLanguageResolutionSources.DefaultEnglish, result.Source);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.SessionLanguage);
        Assert.False(result.ExplicitUserOverride);
        Assert.False(result.DetectedChinese);
    }

    [Fact]
    public void Resolve_MarksChineseWhenTurnContainsChinese()
    {
        var resolver = new RuntimeTurnLanguageResolver();

        var result = resolver.Resolve(new RuntimeTurnLanguageResolutionInput("Runtime scope 先整理这个方向。"));

        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.Chinese, result.ResponseLanguage);
        Assert.Equal(RuntimeTurnLanguageResolutionSources.TextContainsChinese, result.Source);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.Chinese, result.SessionLanguage);
        Assert.False(result.ExplicitUserOverride);
        Assert.True(result.DetectedChinese);
    }

    [Fact]
    public void Resolve_KeepsSessionChineseForLaterEnglishTurns()
    {
        var resolver = new RuntimeTurnLanguageResolver();

        var result = resolver.Resolve(new RuntimeTurnLanguageResolutionInput(
            "Continue with the same candidate.",
            SessionLanguage: RuntimeTurnAwarenessProfileLanguageModes.Chinese));

        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.Chinese, result.ResponseLanguage);
        Assert.Equal(RuntimeTurnLanguageResolutionSources.SessionState, result.Source);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.Chinese, result.SessionLanguage);
        Assert.False(result.ExplicitUserOverride);
        Assert.False(result.DetectedChinese);
    }

    [Fact]
    public void Resolve_KeepsSessionEnglishForLaterChineseTurns()
    {
        var resolver = new RuntimeTurnLanguageResolver();

        var result = resolver.Resolve(new RuntimeTurnLanguageResolutionInput(
            "这个方向继续整理。",
            SessionLanguage: RuntimeTurnAwarenessProfileLanguageModes.English));

        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.ResponseLanguage);
        Assert.Equal(RuntimeTurnLanguageResolutionSources.SessionState, result.Source);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.SessionLanguage);
        Assert.False(result.ExplicitUserOverride);
        Assert.True(result.DetectedChinese);
    }

    [Fact]
    public void Resolve_UserExplicitEnglishOverridesChineseStateAndText()
    {
        var resolver = new RuntimeTurnLanguageResolver();

        var result = resolver.Resolve(new RuntimeTurnLanguageResolutionInput(
            "请用英文回答，范围仍是 Runtime。",
            SessionLanguage: RuntimeTurnAwarenessProfileLanguageModes.Chinese));

        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.ResponseLanguage);
        Assert.Equal(RuntimeTurnLanguageResolutionSources.ExplicitUserLanguage, result.Source);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.SessionLanguage);
        Assert.True(result.ExplicitUserOverride);
        Assert.True(result.DetectedChinese);
    }

    [Fact]
    public void Resolve_UserExplicitChineseOverridesProfileEnglish()
    {
        var resolver = new RuntimeTurnLanguageResolver();

        var result = resolver.Resolve(new RuntimeTurnLanguageResolutionInput(
            "Please switch to Chinese for the next answer.",
            ProfileLanguageMode: RuntimeTurnAwarenessProfileLanguageModes.English));

        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.Chinese, result.ResponseLanguage);
        Assert.Equal(RuntimeTurnLanguageResolutionSources.ExplicitUserLanguage, result.Source);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.Chinese, result.SessionLanguage);
        Assert.True(result.ExplicitUserOverride);
    }

    [Fact]
    public void Resolve_ProfileLanguageBeatsAutomaticChineseDetection()
    {
        var resolver = new RuntimeTurnLanguageResolver();

        var result = resolver.Resolve(new RuntimeTurnLanguageResolutionInput(
            "这个方向可以复核。",
            ProfileLanguageMode: RuntimeTurnAwarenessProfileLanguageModes.English));

        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.ResponseLanguage);
        Assert.Equal(RuntimeTurnLanguageResolutionSources.ProfileLanguage, result.Source);
        Assert.Equal(RuntimeTurnAwarenessProfileLanguageModes.English, result.SessionLanguage);
        Assert.False(result.ExplicitUserOverride);
        Assert.True(result.DetectedChinese);
    }
}
