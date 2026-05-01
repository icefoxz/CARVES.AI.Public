using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Interaction;

public static class RuntimeTurnAwarenessProfileContract
{
    public const string CurrentVersion = "runtime-turn-awareness-profile.v2";
    public const int FieldPriorityMaxCount = 8;
    public const int ExpressionSignalMaxCount = 6;
    public const int ExpressionCueMaxCount = 2;
    public const int ExpressionCueMaxUtf8Bytes = 96;
    public const int HardMaxQuestionsPerTurn = 1;
}

public static class RuntimeTurnAwarenessProfileLanguageModes
{
    public const string Auto = "auto";
    public const string Chinese = "zh";
    public const string English = "en";
}

public static class RuntimeTurnAwarenessProfileQuestionModes
{
    public const string Off = "off";
    public const string OneClearQuestion = "one_clear_question";
    public const string ConfirmOrCorrect = "confirm_or_correct";
}

public static class RuntimeTurnAwarenessProfileHandoffModes
{
    public const string CandidateOnly = "candidate_only";
    public const string ConfirmBeforeHandoff = "confirm_before_handoff";
    public const string Disabled = "disabled";
}

public static class RuntimeTurnAwarenessProfileVoiceIds
{
    public const string Neutral = "neutral";
    public const string CalmAssistant = "calm_assistant";
    public const string DirectProjectManager = "direct_project_manager";
    public const string StrictReviewer = "strict_reviewer";
}

public static class RuntimeTurnAwarenessProfileExpressionSignals
{
    public const string Compact = "compact";
    public const string Direct = "direct";
    public const string Supportive = "supportive";
    public const string AssumptionCheck = "assumption_check";
    public const string BoundaryVisible = "boundary_visible";
    public const string SequenceVisible = "sequence_visible";
}

public static class RuntimeTurnAwarenessProfileResponseOrders
{
    public const string SummaryFirst = "summary_first";
    public const string QuestionFirst = "question_first";
    public const string BlockerFirst = "blocker_first";
}

public static class RuntimeTurnAwarenessProfileCorrectionModes
{
    public const string Standard = "standard";
    public const string AlwaysVisible = "correction_visible";
    public const string Quiet = "quiet";
}

public static class RuntimeTurnAwarenessProfileEvidenceModes
{
    public const string Standard = "standard";
    public const string EvidenceVisible = "evidence_visible";
    public const string Compact = "compact";
}

public sealed record RuntimeTurnAwarenessQuestionPolicy
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = RuntimeTurnAwarenessProfileQuestionModes.OneClearQuestion;

    [JsonPropertyName("max_questions")]
    public int MaxQuestions { get; init; } = RuntimeTurnAwarenessProfileContract.HardMaxQuestionsPerTurn;
}

public sealed record RuntimeTurnAwarenessChallengePolicy
{
    [JsonPropertyName("level")]
    public string Level { get; init; } = "medium";

    [JsonPropertyName("weak_assumption_check")]
    public bool WeakAssumptionCheck { get; init; }
}

public sealed record RuntimeTurnAwarenessTonePolicy
{
    [JsonPropertyName("directness")]
    public string Directness { get; init; } = "balanced";

    [JsonPropertyName("warmth")]
    public string Warmth { get; init; } = "balanced";

    [JsonPropertyName("concision")]
    public string Concision { get; init; } = "balanced";
}

public sealed record RuntimeTurnAwarenessHandoffPolicy
{
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = RuntimeTurnAwarenessProfileHandoffModes.ConfirmBeforeHandoff;

    [JsonPropertyName("requires_operator_confirmation")]
    public bool RequiresOperatorConfirmation { get; init; } = true;

    [JsonPropertyName("auto_handoff")]
    public bool AutoHandoff { get; init; }
}

public sealed record RuntimeTurnAwarenessExpressionPolicy
{
    [JsonPropertyName("voice_id")]
    public string VoiceId { get; init; } = RuntimeTurnAwarenessProfileVoiceIds.Neutral;

    [JsonPropertyName("style_signals")]
    public IReadOnlyList<string> StyleSignals { get; init; } = Array.Empty<string>();

    [JsonPropertyName("summary_cues")]
    public IReadOnlyList<string> SummaryCues { get; init; } = Array.Empty<string>();

    [JsonPropertyName("question_cues")]
    public IReadOnlyList<string> QuestionCues { get; init; } = Array.Empty<string>();

    [JsonPropertyName("next_action_cues")]
    public IReadOnlyList<string> NextActionCues { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeTurnAwarenessInteractionPolicy
{
    [JsonPropertyName("response_order")]
    public string ResponseOrder { get; init; } = RuntimeTurnAwarenessProfileResponseOrders.SummaryFirst;

    [JsonPropertyName("correction_mode")]
    public string CorrectionMode { get; init; } = RuntimeTurnAwarenessProfileCorrectionModes.Standard;

    [JsonPropertyName("evidence_mode")]
    public string EvidenceMode { get; init; } = RuntimeTurnAwarenessProfileEvidenceModes.Standard;
}

public sealed record RuntimeTurnAwarenessProfile
{
    public const string RuntimeDefaultProfileId = "runtime-awareness-default";

    [JsonPropertyName("profile_id")]
    public string ProfileId { get; init; } = RuntimeDefaultProfileId;

    [JsonPropertyName("version")]
    public string Version { get; init; } = RuntimeTurnAwarenessProfileContract.CurrentVersion;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = "runtime_default";

    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; } = "Runtime awareness default";

    [JsonPropertyName("role_id")]
    public string RoleId { get; init; } = RuntimeTurnPostureCodes.Posture.None;

    [JsonPropertyName("language_mode")]
    public string LanguageMode { get; init; } = RuntimeTurnAwarenessProfileLanguageModes.Auto;

    [JsonPropertyName("field_priority")]
    public IReadOnlyList<string> FieldPriority { get; init; } = Array.Empty<string>();

    [JsonPropertyName("question_policy")]
    public RuntimeTurnAwarenessQuestionPolicy QuestionPolicy { get; init; } = new();

    [JsonPropertyName("challenge_policy")]
    public RuntimeTurnAwarenessChallengePolicy ChallengePolicy { get; init; } = new();

    [JsonPropertyName("tone_policy")]
    public RuntimeTurnAwarenessTonePolicy TonePolicy { get; init; } = new();

    [JsonPropertyName("handoff_policy")]
    public RuntimeTurnAwarenessHandoffPolicy HandoffPolicy { get; init; } = new();

    [JsonPropertyName("expression_policy")]
    public RuntimeTurnAwarenessExpressionPolicy ExpressionPolicy { get; init; } = new();

    [JsonPropertyName("interaction_policy")]
    public RuntimeTurnAwarenessInteractionPolicy InteractionPolicy { get; init; } = new();

    [JsonPropertyName("forbidden_authority_tokens")]
    public IReadOnlyList<string> ForbiddenAuthorityTokens { get; init; } = RuntimeAuthorityActionTokens.All;

    [JsonPropertyName("revision_hash")]
    public string RevisionHash { get; init; } = "runtime-awareness-default";

    public static RuntimeTurnAwarenessProfile RuntimeDefault { get; } = new();

    public static RuntimeTurnAwarenessProfile FromStyleProfile(RuntimeTurnStyleProfile? styleProfile)
    {
        if (styleProfile is null)
        {
            return RuntimeDefault;
        }

        return new RuntimeTurnAwarenessProfile
        {
            ProfileId = styleProfile.StyleProfileId,
            Scope = styleProfile.Scope,
            DisplayName = styleProfile.DisplayName,
            RoleId = styleProfile.DefaultPostureId,
            LanguageMode = RuntimeTurnAwarenessProfileLanguageModes.Auto,
            FieldPriority = NormalizeFieldPriority(styleProfile.PreferredFocusCodes),
            QuestionPolicy = new RuntimeTurnAwarenessQuestionPolicy
            {
                Mode = MapQuestionMode(styleProfile.QuestionStyle),
                MaxQuestions = Math.Clamp(styleProfile.MaxQuestions, 0, RuntimeTurnAwarenessProfileContract.HardMaxQuestionsPerTurn),
            },
            ChallengePolicy = new RuntimeTurnAwarenessChallengePolicy
            {
                Level = NormalizeLevel(styleProfile.ChallengeLevel),
                WeakAssumptionCheck = string.Equals(NormalizeToken(styleProfile.ChallengeLevel), "high", StringComparison.Ordinal),
            },
            TonePolicy = new RuntimeTurnAwarenessTonePolicy
            {
                Directness = NormalizeLevel(styleProfile.Directness),
            },
            HandoffPolicy = new RuntimeTurnAwarenessHandoffPolicy(),
            ExpressionPolicy = new RuntimeTurnAwarenessExpressionPolicy
            {
                StyleSignals = NormalizeExpressionSignalsFromStyleProfile(styleProfile),
            },
            InteractionPolicy = new RuntimeTurnAwarenessInteractionPolicy(),
            ForbiddenAuthorityTokens = RuntimeAuthorityActionTokens.All,
            RevisionHash = styleProfile.RevisionHash,
        };
    }

    public RuntimeTurnStyleProfile ToStyleProfile()
    {
        return new RuntimeTurnStyleProfile
        {
            StyleProfileId = ProfileId,
            Version = RuntimeTurnStyleProfile.CurrentVersion,
            Scope = Scope,
            DisplayName = DisplayName,
            DefaultPostureId = RoleId,
            Directness = TonePolicy.Directness,
            ChallengeLevel = ChallengePolicy.Level,
            QuestionStyle = QuestionPolicy.Mode switch
            {
                RuntimeTurnAwarenessProfileQuestionModes.ConfirmOrCorrect => "confirm_or_correct",
                RuntimeTurnAwarenessProfileQuestionModes.Off => "off",
                _ => "one_clear_question",
            },
            SummaryStyle = "decision_led",
            RiskSurfaceStyle = "surface_blockers_first",
            PreferredFocusCodes = FieldPriority,
            MaxQuestions = Math.Clamp(QuestionPolicy.MaxQuestions, 0, RuntimeTurnStyleProfile.HardMaxQuestionsPerTurn),
            RevisionHash = RevisionHash,
        };
    }

    private static IReadOnlyList<string> NormalizeFieldPriority(IReadOnlyList<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Select(NormalizeToken)
            .Where(value => RuntimeTurnAwarenessProfileValidator.AllowedFieldPriorityCodes.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .Take(RuntimeTurnAwarenessProfileContract.FieldPriorityMaxCount)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeExpressionSignalsFromStyleProfile(RuntimeTurnStyleProfile styleProfile)
    {
        var signals = new List<string>(RuntimeTurnAwarenessProfileContract.ExpressionSignalMaxCount);
        switch (NormalizeToken(styleProfile.Directness))
        {
            case "high":
                signals.Add(RuntimeTurnAwarenessProfileExpressionSignals.Direct);
                break;
        }

        switch (NormalizeToken(styleProfile.ChallengeLevel))
        {
            case "high":
                signals.Add(RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck);
                break;
        }

        var note = NormalizeToken(styleProfile.CustomPersonalityNote);
        if (ContainsAny(note, "concise", "compact", "short", "succinct", "简洁", "紧凑"))
        {
            signals.Add(RuntimeTurnAwarenessProfileExpressionSignals.Compact);
        }

        if (ContainsAny(note, "patient", "calm", "support", "gentle", "温和", "耐心", "支持"))
        {
            signals.Add(RuntimeTurnAwarenessProfileExpressionSignals.Supportive);
        }

        if (ContainsAny(note, "challenge", "assumption", "weak", "push back", "质疑", "假设"))
        {
            signals.Add(RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck);
        }

        if (ContainsAny(note, "project", "plan", "sequence", "owner", "review", "项目", "计划", "顺序", "负责人", "复审"))
        {
            signals.Add(RuntimeTurnAwarenessProfileExpressionSignals.SequenceVisible);
        }

        if (ContainsAny(note, "risk", "blocker", "boundary", "permission", "authority", "风险", "阻塞", "边界", "权限"))
        {
            signals.Add(RuntimeTurnAwarenessProfileExpressionSignals.BoundaryVisible);
        }

        return signals
            .Distinct(StringComparer.Ordinal)
            .Take(RuntimeTurnAwarenessProfileContract.ExpressionSignalMaxCount)
            .ToArray();
    }

    private static string MapQuestionMode(string? questionStyle)
    {
        return NormalizeToken(questionStyle) switch
        {
            "confirm_or_correct" => RuntimeTurnAwarenessProfileQuestionModes.ConfirmOrCorrect,
            "off" => RuntimeTurnAwarenessProfileQuestionModes.Off,
            _ => RuntimeTurnAwarenessProfileQuestionModes.OneClearQuestion,
        };
    }

    private static string NormalizeLevel(string? value)
    {
        return NormalizeToken(value) switch
        {
            "low" => "low",
            "medium" => "medium",
            "balanced" => "balanced",
            "high" => "high",
            _ => "balanced",
        };
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return !string.IsNullOrWhiteSpace(text)
            && needles.Any(needle => text.Contains(needle, StringComparison.Ordinal));
    }
}

public sealed record RuntimeTurnAwarenessProfileValidation(
    bool IsValid,
    RuntimeTurnAwarenessProfile EffectiveProfile,
    IReadOnlyList<string> Issues);

public sealed class RuntimeTurnAwarenessProfileValidator
{
    private const int MaxCodeLength = 32;

    internal static readonly IReadOnlySet<string> AllowedFieldPriorityCodes = new HashSet<string>(
        [
            "topic",
            "desired_outcome",
            "scope",
            "success_signal",
            "constraint",
            "non_goal",
            "risk",
            "owner",
            "review_gate",
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedScopes = new HashSet<string>(
        [
            "runtime_default",
            "user_default",
            "workspace_default",
            "project_override",
            "session_override",
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedRoles = new HashSet<string>(
        [
            RuntimeTurnPostureCodes.Posture.None,
            RuntimeTurnPostureCodes.Posture.ProjectManager,
            RuntimeTurnPostureCodes.Posture.Assistant,
            RuntimeTurnPostureCodes.Posture.Architecture,
            RuntimeTurnPostureCodes.Posture.Guard,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedLanguageModes = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileLanguageModes.Auto,
            RuntimeTurnAwarenessProfileLanguageModes.Chinese,
            RuntimeTurnAwarenessProfileLanguageModes.English,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedQuestionModes = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileQuestionModes.Off,
            RuntimeTurnAwarenessProfileQuestionModes.OneClearQuestion,
            RuntimeTurnAwarenessProfileQuestionModes.ConfirmOrCorrect,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedHandoffModes = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileHandoffModes.CandidateOnly,
            RuntimeTurnAwarenessProfileHandoffModes.ConfirmBeforeHandoff,
            RuntimeTurnAwarenessProfileHandoffModes.Disabled,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedVoiceIds = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileVoiceIds.Neutral,
            RuntimeTurnAwarenessProfileVoiceIds.CalmAssistant,
            RuntimeTurnAwarenessProfileVoiceIds.DirectProjectManager,
            RuntimeTurnAwarenessProfileVoiceIds.StrictReviewer,
        ],
        StringComparer.Ordinal);

    internal static readonly IReadOnlySet<string> AllowedExpressionSignals = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileExpressionSignals.Compact,
            RuntimeTurnAwarenessProfileExpressionSignals.Direct,
            RuntimeTurnAwarenessProfileExpressionSignals.Supportive,
            RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck,
            RuntimeTurnAwarenessProfileExpressionSignals.BoundaryVisible,
            RuntimeTurnAwarenessProfileExpressionSignals.SequenceVisible,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedResponseOrders = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileResponseOrders.SummaryFirst,
            RuntimeTurnAwarenessProfileResponseOrders.QuestionFirst,
            RuntimeTurnAwarenessProfileResponseOrders.BlockerFirst,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedCorrectionModes = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileCorrectionModes.Standard,
            RuntimeTurnAwarenessProfileCorrectionModes.AlwaysVisible,
            RuntimeTurnAwarenessProfileCorrectionModes.Quiet,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedEvidenceModes = new HashSet<string>(
        [
            RuntimeTurnAwarenessProfileEvidenceModes.Standard,
            RuntimeTurnAwarenessProfileEvidenceModes.EvidenceVisible,
            RuntimeTurnAwarenessProfileEvidenceModes.Compact,
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> AllowedIntensityCodes = new HashSet<string>(
        [
            "low",
            "medium",
            "balanced",
            "high",
        ],
        StringComparer.Ordinal);

    public RuntimeTurnAwarenessProfileValidation Validate(
        RuntimeTurnAwarenessProfile? profile,
        RuntimeTurnAwarenessProfile? fallback = null)
    {
        var fallbackProfile = IsProfileStructurallyValid(fallback)
            ? fallback!
            : RuntimeTurnAwarenessProfile.RuntimeDefault;

        if (profile is null)
        {
            return new RuntimeTurnAwarenessProfileValidation(
                false,
                fallbackProfile,
                ["awareness_profile_missing"]);
        }

        var issues = CollectIssues(profile);
        return issues.Count == 0
            ? new RuntimeTurnAwarenessProfileValidation(true, profile, Array.Empty<string>())
            : new RuntimeTurnAwarenessProfileValidation(false, fallbackProfile, issues);
    }

    private static bool IsProfileStructurallyValid(RuntimeTurnAwarenessProfile? profile)
    {
        return profile is not null && CollectIssues(profile).Count == 0;
    }

    private static IReadOnlyList<string> CollectIssues(RuntimeTurnAwarenessProfile profile)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.ProfileId))
        {
            issues.Add("awareness_profile_id_required");
        }

        if (!string.Equals(profile.Version, RuntimeTurnAwarenessProfileContract.CurrentVersion, StringComparison.Ordinal))
        {
            issues.Add("unknown_awareness_profile_version");
        }

        if (!AllowedScopes.Contains(profile.Scope))
        {
            issues.Add("invalid_awareness_profile_scope");
        }

        if (!AllowedRoles.Contains(profile.RoleId))
        {
            issues.Add("invalid_awareness_role_id");
        }

        if (!AllowedLanguageModes.Contains(profile.LanguageMode))
        {
            issues.Add("invalid_awareness_language_mode");
        }

        AddFieldPriorityIssues(profile.FieldPriority, issues);
        AddQuestionPolicyIssues(profile.QuestionPolicy, issues);
        AddChallengePolicyIssues(profile.ChallengePolicy, issues);
        AddTonePolicyIssues(profile.TonePolicy, issues);
        AddHandoffPolicyIssues(profile.HandoffPolicy, issues);
        AddExpressionPolicyIssues(profile.ExpressionPolicy, issues);
        AddInteractionPolicyIssues(profile.InteractionPolicy, issues);
        AddForbiddenAuthorityTokenIssues(profile.ForbiddenAuthorityTokens, issues);

        return issues;
    }

    private static void AddFieldPriorityIssues(IReadOnlyList<string>? values, ICollection<string> issues)
    {
        if (values is null)
        {
            return;
        }

        if (values.Count > RuntimeTurnAwarenessProfileContract.FieldPriorityMaxCount)
        {
            issues.Add("field_priority_too_many");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!IsBoundedCode(value) || !AllowedFieldPriorityCodes.Contains(value))
            {
                issues.Add("field_priority_invalid");
                return;
            }

            if (!seen.Add(value))
            {
                issues.Add("field_priority_duplicate");
                return;
            }
        }
    }

    private static void AddExpressionCueIssues(
        IReadOnlyList<string>? cues,
        string cueKind,
        ICollection<string> issues)
    {
        if (cues is null)
        {
            issues.Add($"expression_policy_{cueKind}_cues_required");
            return;
        }

        if (cues.Count > RuntimeTurnAwarenessProfileContract.ExpressionCueMaxCount)
        {
            issues.Add($"expression_policy_{cueKind}_cues_too_many");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cue in cues)
        {
            if (!IsBoundedExpressionCue(cue))
            {
                issues.Add($"expression_policy_{cueKind}_cue_invalid");
                return;
            }

            if (RuntimeAuthorityIntentClassifier.ContainsAuthorityIntent(cue))
            {
                issues.Add($"expression_policy_{cueKind}_cue_attempts_authority_change");
                return;
            }

            if (!seen.Add(cue.Trim()))
            {
                issues.Add($"expression_policy_{cueKind}_cue_duplicate");
                return;
            }
        }
    }

    private static void AddQuestionPolicyIssues(RuntimeTurnAwarenessQuestionPolicy? policy, ICollection<string> issues)
    {
        if (policy is null)
        {
            issues.Add("question_policy_required");
            return;
        }

        if (!AllowedQuestionModes.Contains(policy.Mode))
        {
            issues.Add("question_policy_invalid_mode");
        }

        if (policy.MaxQuestions is < 0 or > RuntimeTurnAwarenessProfileContract.HardMaxQuestionsPerTurn)
        {
            issues.Add("question_policy_max_questions_exceeds_hard_limit");
        }
    }

    private static void AddChallengePolicyIssues(RuntimeTurnAwarenessChallengePolicy? policy, ICollection<string> issues)
    {
        if (policy is null)
        {
            issues.Add("challenge_policy_required");
            return;
        }

        if (!AllowedIntensityCodes.Contains(policy.Level))
        {
            issues.Add("challenge_policy_invalid_level");
        }
    }

    private static void AddTonePolicyIssues(RuntimeTurnAwarenessTonePolicy? policy, ICollection<string> issues)
    {
        if (policy is null)
        {
            issues.Add("tone_policy_required");
            return;
        }

        if (!AllowedIntensityCodes.Contains(policy.Directness))
        {
            issues.Add("tone_policy_invalid_directness");
        }

        if (!AllowedIntensityCodes.Contains(policy.Warmth))
        {
            issues.Add("tone_policy_invalid_warmth");
        }

        if (!AllowedIntensityCodes.Contains(policy.Concision))
        {
            issues.Add("tone_policy_invalid_concision");
        }
    }

    private static void AddHandoffPolicyIssues(RuntimeTurnAwarenessHandoffPolicy? policy, ICollection<string> issues)
    {
        if (policy is null)
        {
            issues.Add("handoff_policy_required");
            return;
        }

        if (!AllowedHandoffModes.Contains(policy.Mode))
        {
            issues.Add("handoff_policy_invalid_mode");
        }

        if (!policy.RequiresOperatorConfirmation)
        {
            issues.Add("handoff_policy_must_require_operator_confirmation");
        }

        if (policy.AutoHandoff)
        {
            issues.Add("handoff_policy_attempts_auto_handoff");
        }
    }

    private static void AddExpressionPolicyIssues(RuntimeTurnAwarenessExpressionPolicy? policy, ICollection<string> issues)
    {
        if (policy is null)
        {
            issues.Add("expression_policy_required");
            return;
        }

        if (!AllowedVoiceIds.Contains(policy.VoiceId))
        {
            issues.Add("expression_policy_invalid_voice_id");
        }

        if (policy.StyleSignals is null)
        {
            issues.Add("expression_policy_style_signals_required");
            return;
        }

        if (policy.StyleSignals.Count > RuntimeTurnAwarenessProfileContract.ExpressionSignalMaxCount)
        {
            issues.Add("expression_policy_style_signals_too_many");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var signal in policy.StyleSignals)
        {
            if (!IsBoundedCode(signal) || !AllowedExpressionSignals.Contains(signal))
            {
                issues.Add("expression_policy_style_signal_invalid");
                return;
            }

            if (!seen.Add(signal))
            {
                issues.Add("expression_policy_style_signal_duplicate");
                return;
            }
        }

        AddExpressionCueIssues(policy.SummaryCues, "summary", issues);
        AddExpressionCueIssues(policy.QuestionCues, "question", issues);
        AddExpressionCueIssues(policy.NextActionCues, "next_action", issues);
    }

    private static void AddInteractionPolicyIssues(RuntimeTurnAwarenessInteractionPolicy? policy, ICollection<string> issues)
    {
        if (policy is null)
        {
            issues.Add("interaction_policy_required");
            return;
        }

        if (!AllowedResponseOrders.Contains(policy.ResponseOrder))
        {
            issues.Add("interaction_policy_invalid_response_order");
        }

        if (!AllowedCorrectionModes.Contains(policy.CorrectionMode))
        {
            issues.Add("interaction_policy_invalid_correction_mode");
        }

        if (!AllowedEvidenceModes.Contains(policy.EvidenceMode))
        {
            issues.Add("interaction_policy_invalid_evidence_mode");
        }
    }

    private static void AddForbiddenAuthorityTokenIssues(IReadOnlyList<string>? tokens, ICollection<string> issues)
    {
        if (tokens is null || tokens.Count == 0)
        {
            issues.Add("forbidden_authority_tokens_required");
            return;
        }

        var normalized = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToHashSet(StringComparer.Ordinal);
        if (normalized.Count != tokens.Count || normalized.Any(token => !RuntimeAuthorityActionTokens.All.Contains(token, StringComparer.Ordinal)))
        {
            issues.Add("forbidden_authority_tokens_invalid");
            return;
        }

        if (RuntimeAuthorityActionTokens.All.Any(token => !normalized.Contains(token)))
        {
            issues.Add("forbidden_authority_tokens_missing_required");
        }
    }

    private static bool IsBoundedCode(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxCodeLength
            && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');
    }

    private static bool IsBoundedExpressionCue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Encoding.UTF8.GetByteCount(value.Trim()) <= RuntimeTurnAwarenessProfileContract.ExpressionCueMaxUtf8Bytes
            && value.All(character => !char.IsControl(character));
    }
}

public sealed record RuntimeTurnAwarenessProfileLoadResult(
    bool ProfileProvided,
    string Status,
    string Source,
    RuntimeTurnAwarenessProfile EffectiveProfile,
    IReadOnlyList<string> Issues);

public sealed class RuntimeTurnAwarenessProfileLoader
{
    private const string RuntimeDefaultStatus = "runtime_default";
    private const string LoadedStatus = "loaded";
    private const string FallbackStatus = "fallback";
    private static readonly RuntimeTurnAwarenessProfile RuntimeDefault = RuntimeTurnAwarenessProfile.RuntimeDefault;
    private readonly RuntimeTurnAwarenessProfileValidator validator;

    public RuntimeTurnAwarenessProfileLoader(RuntimeTurnAwarenessProfileValidator? validator = null)
    {
        this.validator = validator ?? new RuntimeTurnAwarenessProfileValidator();
    }

    public RuntimeTurnAwarenessProfileLoadResult Load(JsonObject? capabilities, RuntimeTurnAwarenessProfile? fallback = null)
    {
        var (profileNode, source, sourceIssue) = ResolveProfileNode(capabilities);
        if (profileNode is null)
        {
            return sourceIssue is null
                ? new RuntimeTurnAwarenessProfileLoadResult(false, RuntimeDefaultStatus, "runtime_default", RuntimeDefault, Array.Empty<string>())
                : new RuntimeTurnAwarenessProfileLoadResult(true, FallbackStatus, source, ResolveFallback(fallback), [sourceIssue]);
        }

        var profile = ReadProfile(profileNode);
        var validation = validator.Validate(profile, fallback);
        return validation.IsValid
            ? new RuntimeTurnAwarenessProfileLoadResult(true, LoadedStatus, source, validation.EffectiveProfile, Array.Empty<string>())
            : new RuntimeTurnAwarenessProfileLoadResult(true, FallbackStatus, source, validation.EffectiveProfile, validation.Issues);
    }

    private static RuntimeTurnAwarenessProfile ResolveFallback(RuntimeTurnAwarenessProfile? fallback)
    {
        return fallback ?? RuntimeDefault;
    }

    private static (JsonObject? ProfileNode, string Source, string? SourceIssue) ResolveProfileNode(JsonObject? capabilities)
    {
        if (capabilities is null)
        {
            return (null, "runtime_default", null);
        }

        foreach (var key in new[] { "runtime_turn_awareness_profile", "awareness_profile" })
        {
            if (!capabilities.TryGetPropertyValue(key, out var node))
            {
                continue;
            }

            return node is JsonObject profile
                ? (profile, $"client_capabilities.{key}", null)
                : (null, $"client_capabilities.{key}", "awareness_profile_not_object");
        }

        return (null, "runtime_default", null);
    }

    private static RuntimeTurnAwarenessProfile ReadProfile(JsonObject profile)
    {
        return new RuntimeTurnAwarenessProfile
        {
            ProfileId = ReadString(profile, "profile_id", "awareness_profile_id", "id") ?? RuntimeDefault.ProfileId,
            Version = ReadString(profile, "version") ?? RuntimeTurnAwarenessProfileContract.CurrentVersion,
            Scope = ReadString(profile, "scope") ?? RuntimeDefault.Scope,
            DisplayName = ReadString(profile, "display_name", "name") ?? RuntimeDefault.DisplayName,
            RoleId = ReadString(profile, "role_id", "default_posture_id", "posture_id") ?? RuntimeDefault.RoleId,
            LanguageMode = ReadString(profile, "language_mode", "language") ?? RuntimeDefault.LanguageMode,
            FieldPriority = ReadStringArray(profile, "field_priority", "field_priorities"),
            QuestionPolicy = ReadQuestionPolicy(profile["question_policy"] as JsonObject),
            ChallengePolicy = ReadChallengePolicy(profile["challenge_policy"] as JsonObject),
            TonePolicy = ReadTonePolicy(profile["tone_policy"] as JsonObject),
            HandoffPolicy = ReadHandoffPolicy(profile["handoff_policy"] as JsonObject),
            ExpressionPolicy = ReadExpressionPolicy(profile["expression_policy"] as JsonObject, profile),
            InteractionPolicy = ReadInteractionPolicy(profile["interaction_policy"] as JsonObject, profile),
            ForbiddenAuthorityTokens = ReadStringArray(profile, "forbidden_authority_tokens", "forbidden_actions"),
            RevisionHash = ReadString(profile, "revision_hash", "revision") ?? RuntimeDefault.RevisionHash,
        };
    }

    private static RuntimeTurnAwarenessQuestionPolicy ReadQuestionPolicy(JsonObject? policy)
    {
        return policy is null
            ? new RuntimeTurnAwarenessQuestionPolicy()
            : new RuntimeTurnAwarenessQuestionPolicy
            {
                Mode = ReadString(policy, "mode", "question_style") ?? RuntimeTurnAwarenessProfileQuestionModes.OneClearQuestion,
                MaxQuestions = ReadInt(policy, "max_questions") ?? RuntimeTurnAwarenessProfileContract.HardMaxQuestionsPerTurn,
            };
    }

    private static RuntimeTurnAwarenessChallengePolicy ReadChallengePolicy(JsonObject? policy)
    {
        return policy is null
            ? new RuntimeTurnAwarenessChallengePolicy()
            : new RuntimeTurnAwarenessChallengePolicy
            {
                Level = ReadString(policy, "level", "challenge_level") ?? "medium",
                WeakAssumptionCheck = ReadBool(policy, "weak_assumption_check") ?? false,
            };
    }

    private static RuntimeTurnAwarenessTonePolicy ReadTonePolicy(JsonObject? policy)
    {
        return policy is null
            ? new RuntimeTurnAwarenessTonePolicy()
            : new RuntimeTurnAwarenessTonePolicy
            {
                Directness = ReadString(policy, "directness") ?? "balanced",
                Warmth = ReadString(policy, "warmth") ?? "balanced",
                Concision = ReadString(policy, "concision") ?? "balanced",
            };
    }

    private static RuntimeTurnAwarenessExpressionPolicy ReadExpressionPolicy(JsonObject? policy, JsonObject profile)
    {
        return policy is null
            ? new RuntimeTurnAwarenessExpressionPolicy
            {
                VoiceId = ReadString(profile, "voice_id", "personality_id") ?? RuntimeTurnAwarenessProfileVoiceIds.Neutral,
                StyleSignals = ReadStringArray(profile, "style_signals", "personality_signals"),
                SummaryCues = ReadStringArray(profile, "summary_cues", "personality_summary_cues"),
                QuestionCues = ReadStringArray(profile, "question_cues", "personality_question_cues"),
                NextActionCues = ReadStringArray(profile, "next_action_cues", "personality_next_action_cues"),
            }
            : new RuntimeTurnAwarenessExpressionPolicy
            {
                VoiceId = ReadString(policy, "voice_id", "personality_id") ?? RuntimeTurnAwarenessProfileVoiceIds.Neutral,
                StyleSignals = ReadStringArray(policy, "style_signals", "personality_signals"),
                SummaryCues = ReadStringArray(policy, "summary_cues", "personality_summary_cues"),
                QuestionCues = ReadStringArray(policy, "question_cues", "personality_question_cues"),
                NextActionCues = ReadStringArray(policy, "next_action_cues", "personality_next_action_cues"),
            };
    }

    private static RuntimeTurnAwarenessInteractionPolicy ReadInteractionPolicy(JsonObject? policy, JsonObject profile)
    {
        return policy is null
            ? new RuntimeTurnAwarenessInteractionPolicy
            {
                ResponseOrder = ReadString(profile, "response_order", "answer_order") ?? RuntimeTurnAwarenessProfileResponseOrders.SummaryFirst,
                CorrectionMode = ReadString(profile, "correction_mode") ?? RuntimeTurnAwarenessProfileCorrectionModes.Standard,
                EvidenceMode = ReadString(profile, "evidence_mode") ?? RuntimeTurnAwarenessProfileEvidenceModes.Standard,
            }
            : new RuntimeTurnAwarenessInteractionPolicy
            {
                ResponseOrder = ReadString(policy, "response_order", "answer_order") ?? RuntimeTurnAwarenessProfileResponseOrders.SummaryFirst,
                CorrectionMode = ReadString(policy, "correction_mode") ?? RuntimeTurnAwarenessProfileCorrectionModes.Standard,
                EvidenceMode = ReadString(policy, "evidence_mode") ?? RuntimeTurnAwarenessProfileEvidenceModes.Standard,
            };
    }

    private static RuntimeTurnAwarenessHandoffPolicy ReadHandoffPolicy(JsonObject? policy)
    {
        return policy is null
            ? new RuntimeTurnAwarenessHandoffPolicy()
            : new RuntimeTurnAwarenessHandoffPolicy
            {
                Mode = ReadString(policy, "mode") ?? RuntimeTurnAwarenessProfileHandoffModes.ConfirmBeforeHandoff,
                RequiresOperatorConfirmation = ReadBool(policy, "requires_operator_confirmation") ?? true,
                AutoHandoff = ReadBool(policy, "auto_handoff") ?? false,
            };
    }

    private static string? ReadString(JsonObject source, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (source.TryGetPropertyValue(key, out var node)
                && node is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static int? ReadInt(JsonObject source, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (source.TryGetPropertyValue(key, out var node)
                && node is JsonValue value
                && value.TryGetValue<int>(out var number))
            {
                return number;
            }
        }

        return null;
    }

    private static bool? ReadBool(JsonObject source, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (source.TryGetPropertyValue(key, out var node)
                && node is JsonValue value
                && value.TryGetValue<bool>(out var flag))
            {
                return flag;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject source, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!source.TryGetPropertyValue(key, out var node) || node is not JsonArray array)
            {
                continue;
            }

            return array
                .OfType<JsonValue>()
                .Select(value => value.TryGetValue<string>(out var text) ? text.Trim() : null)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!)
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
