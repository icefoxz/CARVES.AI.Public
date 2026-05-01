using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Interaction;

public static class RuntimeTurnAwarenessPolicyContract
{
    public const string CurrentVersion = "runtime-turn-awareness-policy.v3";
    public const int StrategyCodeMaxCount = 8;
}

public static class RuntimeTurnAwarenessPolicyStatuses
{
    public const string NotCompiled = "not_compiled";
    public const string Compiled = "compiled";
    public const string Fallback = "fallback";
}

public static class RuntimeTurnAwarenessStrategyCodes
{
    public const string UserFacing = "user_facing";
    public const string LowFriction = "low_friction";
    public const string DecisionVisible = "decision_visible";
    public const string SequenceVisible = "sequence_visible";
    public const string ReviewGateVisible = "review_gate_visible";
    public const string BoundaryFirst = "boundary_first";
    public const string RiskFirst = "risk_first";
    public const string ContractAware = "contract_aware";
    public const string WeakAssumptionCheck = "weak_assumption_check";
    public const string DirectResponse = "direct_response";
}

public static class RuntimeTurnAwarenessAdmissionHintContract
{
    public const string CurrentVersion = "runtime-turn-awareness-admission-hints.v1";
    public const int MaxWatchedFieldCodes = 8;
    public const int MaxEvidenceCodes = 6;
}

public static class RuntimeTurnAwarenessAdmissionSensitivityCodes
{
    public const string Conservative = "conservative";
    public const string Standard = "standard";
    public const string Responsive = "responsive";
}

public static class RuntimeTurnAwarenessAdmissionUpdateBiasCodes
{
    public const string ExplicitOnly = "explicit_only";
    public const string ExistingCandidate = "existing_candidate";
    public const string RoleField = "role_field";
}

public static class RuntimeTurnAwarenessFollowUpPreferenceCodes
{
    public const string None = "none";
    public const string AskWhenMissing = "ask_when_missing";
    public const string MissingFieldFirst = "missing_field_first";
    public const string BlockerFirst = "blocker_first";
}

public static class RuntimeTurnAwarenessAdmissionAuthorityScopes
{
    public const string GuidanceOnly = "guidance_only";
}

public static class RuntimeTurnAwarenessAdmissionHintEvidenceCodes
{
    public const string RoleAssistant = "role_assistant";
    public const string RoleProjectManager = "role_project_manager";
    public const string RoleArchitecture = "role_architecture";
    public const string RoleGuard = "role_guard";
    public const string QuestionOff = "question_off";
    public const string ReviewGateVisible = "review_gate_visible";
    public const string RiskFirst = "risk_first";
    public const string BoundaryFirst = "boundary_first";
}

public sealed record RuntimeTurnAwarenessAdmissionHints
{
    [JsonPropertyName("contract_version")]
    public string ContractVersion { get; init; } = RuntimeTurnAwarenessAdmissionHintContract.CurrentVersion;

    [JsonPropertyName("guidance_sensitivity")]
    public string GuidanceSensitivity { get; init; } = RuntimeTurnAwarenessAdmissionSensitivityCodes.Standard;

    [JsonPropertyName("existing_candidate_update_bias")]
    public string ExistingCandidateUpdateBias { get; init; } = RuntimeTurnAwarenessAdmissionUpdateBiasCodes.ExistingCandidate;

    [JsonPropertyName("follow_up_preference")]
    public string FollowUpPreference { get; init; } = RuntimeTurnAwarenessFollowUpPreferenceCodes.AskWhenMissing;

    [JsonPropertyName("watched_field_codes")]
    public IReadOnlyList<string> WatchedFieldCodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("evidence_codes")]
    public IReadOnlyList<string> EvidenceCodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("authority_scope")]
    public string AuthorityScope { get; init; } = RuntimeTurnAwarenessAdmissionAuthorityScopes.GuidanceOnly;

    [JsonPropertyName("can_force_guided_collection")]
    public bool CanForceGuidedCollection { get; init; }
}

public sealed record RuntimeTurnAwarenessPolicy
{
    [JsonPropertyName("contract_version")]
    public string ContractVersion { get; init; } = RuntimeTurnAwarenessPolicyContract.CurrentVersion;

    [JsonPropertyName("source_profile_id")]
    public string SourceProfileId { get; init; } = RuntimeTurnAwarenessProfile.RuntimeDefaultProfileId;

    [JsonPropertyName("source_revision_hash")]
    public string SourceRevisionHash { get; init; } = "runtime-awareness-default";

    [JsonPropertyName("role_id")]
    public string RoleId { get; init; } = RuntimeTurnPostureCodes.Posture.None;

    [JsonPropertyName("language_mode")]
    public string LanguageMode { get; init; } = RuntimeTurnAwarenessProfileLanguageModes.Auto;

    [JsonPropertyName("field_priority")]
    public IReadOnlyList<string> FieldPriority { get; init; } = Array.Empty<string>();

    [JsonPropertyName("question_mode")]
    public string QuestionMode { get; init; } = RuntimeTurnAwarenessProfileQuestionModes.OneClearQuestion;

    [JsonPropertyName("max_questions")]
    public int MaxQuestions { get; init; } = RuntimeTurnAwarenessProfileContract.HardMaxQuestionsPerTurn;

    [JsonPropertyName("challenge_level")]
    public string ChallengeLevel { get; init; } = "medium";

    [JsonPropertyName("weak_assumption_check")]
    public bool WeakAssumptionCheck { get; init; }

    [JsonPropertyName("directness")]
    public string Directness { get; init; } = "balanced";

    [JsonPropertyName("warmth")]
    public string Warmth { get; init; } = "balanced";

    [JsonPropertyName("concision")]
    public string Concision { get; init; } = "balanced";

    [JsonPropertyName("handoff_mode")]
    public string HandoffMode { get; init; } = RuntimeTurnAwarenessProfileHandoffModes.ConfirmBeforeHandoff;

    [JsonPropertyName("requires_operator_confirmation")]
    public bool RequiresOperatorConfirmation { get; init; } = true;

    [JsonPropertyName("voice_id")]
    public string VoiceId { get; init; } = RuntimeTurnAwarenessProfileVoiceIds.Neutral;

    [JsonPropertyName("expression_signals")]
    public IReadOnlyList<string> ExpressionSignals { get; init; } = Array.Empty<string>();

    [JsonPropertyName("summary_cues")]
    public IReadOnlyList<string> SummaryCues { get; init; } = Array.Empty<string>();

    [JsonPropertyName("question_cues")]
    public IReadOnlyList<string> QuestionCues { get; init; } = Array.Empty<string>();

    [JsonPropertyName("next_action_cues")]
    public IReadOnlyList<string> NextActionCues { get; init; } = Array.Empty<string>();

    [JsonPropertyName("response_order")]
    public string ResponseOrder { get; init; } = RuntimeTurnAwarenessProfileResponseOrders.SummaryFirst;

    [JsonPropertyName("correction_mode")]
    public string CorrectionMode { get; init; } = RuntimeTurnAwarenessProfileCorrectionModes.Standard;

    [JsonPropertyName("evidence_mode")]
    public string EvidenceMode { get; init; } = RuntimeTurnAwarenessProfileEvidenceModes.Standard;

    [JsonPropertyName("strategy_codes")]
    public IReadOnlyList<string> StrategyCodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("admission_hints")]
    public RuntimeTurnAwarenessAdmissionHints AdmissionHints { get; init; } = new();

    [JsonPropertyName("risk_first")]
    public bool RiskFirst { get; init; }

    [JsonPropertyName("boundary_first")]
    public bool BoundaryFirst { get; init; }
}

public sealed record RuntimeTurnAwarenessPolicyCompileResult(
    bool IsValid,
    string Status,
    RuntimeTurnAwarenessPolicy Policy,
    IReadOnlyList<string> Issues);

public sealed class RuntimeTurnAwarenessPolicyCompiler
{
    private readonly RuntimeTurnAwarenessProfileValidator validator;

    public RuntimeTurnAwarenessPolicyCompiler(RuntimeTurnAwarenessProfileValidator? validator = null)
    {
        this.validator = validator ?? new RuntimeTurnAwarenessProfileValidator();
    }

    public RuntimeTurnAwarenessPolicyCompileResult Compile(
        RuntimeTurnAwarenessProfile? profile,
        RuntimeTurnAwarenessProfile? fallback = null)
    {
        var validation = validator.Validate(profile, fallback);
        var policy = CompilePolicy(validation.EffectiveProfile);
        return new RuntimeTurnAwarenessPolicyCompileResult(
            validation.IsValid,
            validation.IsValid ? RuntimeTurnAwarenessPolicyStatuses.Compiled : RuntimeTurnAwarenessPolicyStatuses.Fallback,
            policy,
            validation.Issues);
    }

    private static RuntimeTurnAwarenessPolicy CompilePolicy(RuntimeTurnAwarenessProfile profile)
    {
        var strategyCodes = ResolveStrategyCodes(profile);
        var fieldPriority = ResolveFieldPriority(profile);
        return new RuntimeTurnAwarenessPolicy
        {
            SourceProfileId = profile.ProfileId,
            SourceRevisionHash = profile.RevisionHash,
            RoleId = profile.RoleId,
            LanguageMode = profile.LanguageMode,
            FieldPriority = fieldPriority,
            QuestionMode = profile.QuestionPolicy.Mode,
            MaxQuestions = Math.Clamp(profile.QuestionPolicy.MaxQuestions, 0, RuntimeTurnAwarenessProfileContract.HardMaxQuestionsPerTurn),
            ChallengeLevel = profile.ChallengePolicy.Level,
            WeakAssumptionCheck = profile.ChallengePolicy.WeakAssumptionCheck
                || string.Equals(profile.ChallengePolicy.Level, "high", StringComparison.Ordinal),
            Directness = profile.TonePolicy.Directness,
            Warmth = profile.TonePolicy.Warmth,
            Concision = profile.TonePolicy.Concision,
            HandoffMode = profile.HandoffPolicy.Mode,
            RequiresOperatorConfirmation = profile.HandoffPolicy.RequiresOperatorConfirmation,
            VoiceId = profile.ExpressionPolicy.VoiceId,
            ExpressionSignals = ResolveExpressionSignals(profile.ExpressionPolicy),
            SummaryCues = ResolveExpressionCues(profile.ExpressionPolicy.SummaryCues),
            QuestionCues = ResolveExpressionCues(profile.ExpressionPolicy.QuestionCues),
            NextActionCues = ResolveExpressionCues(profile.ExpressionPolicy.NextActionCues),
            ResponseOrder = profile.InteractionPolicy.ResponseOrder,
            CorrectionMode = profile.InteractionPolicy.CorrectionMode,
            EvidenceMode = profile.InteractionPolicy.EvidenceMode,
            StrategyCodes = strategyCodes,
            AdmissionHints = ResolveAdmissionHints(profile, fieldPriority, strategyCodes),
            RiskFirst = strategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.RiskFirst, StringComparer.Ordinal),
            BoundaryFirst = strategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.BoundaryFirst, StringComparer.Ordinal),
        };
    }

    private static RuntimeTurnAwarenessAdmissionHints ResolveAdmissionHints(
        RuntimeTurnAwarenessProfile profile,
        IReadOnlyList<string> fieldPriority,
        IReadOnlyList<string> strategyCodes)
    {
        var evidenceCodes = new List<string>(RuntimeTurnAwarenessAdmissionHintContract.MaxEvidenceCodes);
        AddEvidence(evidenceCodes, ResolveRoleEvidenceCode(profile.RoleId));
        AddEvidence(
            evidenceCodes,
            string.Equals(profile.QuestionPolicy.Mode, RuntimeTurnAwarenessProfileQuestionModes.Off, StringComparison.Ordinal)
                || profile.QuestionPolicy.MaxQuestions <= 0,
            RuntimeTurnAwarenessAdmissionHintEvidenceCodes.QuestionOff);
        AddEvidence(
            evidenceCodes,
            fieldPriority.Contains("review_gate", StringComparer.Ordinal),
            RuntimeTurnAwarenessAdmissionHintEvidenceCodes.ReviewGateVisible);
        AddEvidence(
            evidenceCodes,
            strategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.RiskFirst, StringComparer.Ordinal),
            RuntimeTurnAwarenessAdmissionHintEvidenceCodes.RiskFirst);
        AddEvidence(
            evidenceCodes,
            strategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.BoundaryFirst, StringComparer.Ordinal),
            RuntimeTurnAwarenessAdmissionHintEvidenceCodes.BoundaryFirst);

        return new RuntimeTurnAwarenessAdmissionHints
        {
            GuidanceSensitivity = ResolveGuidanceSensitivity(profile.RoleId, strategyCodes),
            ExistingCandidateUpdateBias = ResolveExistingCandidateUpdateBias(profile.RoleId),
            FollowUpPreference = ResolveFollowUpPreference(profile, fieldPriority, strategyCodes),
            WatchedFieldCodes = fieldPriority
                .Take(RuntimeTurnAwarenessAdmissionHintContract.MaxWatchedFieldCodes)
                .ToArray(),
            EvidenceCodes = evidenceCodes
                .Distinct(StringComparer.Ordinal)
                .Take(RuntimeTurnAwarenessAdmissionHintContract.MaxEvidenceCodes)
                .ToArray(),
            AuthorityScope = RuntimeTurnAwarenessAdmissionAuthorityScopes.GuidanceOnly,
            CanForceGuidedCollection = false,
        };
    }

    private static string ResolveGuidanceSensitivity(
        string roleId,
        IReadOnlyList<string> strategyCodes)
    {
        if (string.Equals(roleId, RuntimeTurnPostureCodes.Posture.Guard, StringComparison.Ordinal)
            || strategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.BoundaryFirst, StringComparer.Ordinal))
        {
            return RuntimeTurnAwarenessAdmissionSensitivityCodes.Conservative;
        }

        if (string.Equals(roleId, RuntimeTurnPostureCodes.Posture.ProjectManager, StringComparison.Ordinal))
        {
            return RuntimeTurnAwarenessAdmissionSensitivityCodes.Responsive;
        }

        return RuntimeTurnAwarenessAdmissionSensitivityCodes.Standard;
    }

    private static string ResolveExistingCandidateUpdateBias(string roleId)
    {
        return roleId switch
        {
            RuntimeTurnPostureCodes.Posture.ProjectManager => RuntimeTurnAwarenessAdmissionUpdateBiasCodes.RoleField,
            RuntimeTurnPostureCodes.Posture.Assistant => RuntimeTurnAwarenessAdmissionUpdateBiasCodes.ExistingCandidate,
            _ => RuntimeTurnAwarenessAdmissionUpdateBiasCodes.ExplicitOnly,
        };
    }

    private static string ResolveFollowUpPreference(
        RuntimeTurnAwarenessProfile profile,
        IReadOnlyList<string> fieldPriority,
        IReadOnlyList<string> strategyCodes)
    {
        if (string.Equals(profile.QuestionPolicy.Mode, RuntimeTurnAwarenessProfileQuestionModes.Off, StringComparison.Ordinal)
            || profile.QuestionPolicy.MaxQuestions <= 0)
        {
            return RuntimeTurnAwarenessFollowUpPreferenceCodes.None;
        }

        if (strategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.RiskFirst, StringComparer.Ordinal)
            || strategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.BoundaryFirst, StringComparer.Ordinal))
        {
            return RuntimeTurnAwarenessFollowUpPreferenceCodes.BlockerFirst;
        }

        return fieldPriority.Contains("review_gate", StringComparer.Ordinal)
            ? RuntimeTurnAwarenessFollowUpPreferenceCodes.MissingFieldFirst
            : RuntimeTurnAwarenessFollowUpPreferenceCodes.AskWhenMissing;
    }

    private static string? ResolveRoleEvidenceCode(string roleId)
    {
        return roleId switch
        {
            RuntimeTurnPostureCodes.Posture.Assistant => RuntimeTurnAwarenessAdmissionHintEvidenceCodes.RoleAssistant,
            RuntimeTurnPostureCodes.Posture.ProjectManager => RuntimeTurnAwarenessAdmissionHintEvidenceCodes.RoleProjectManager,
            RuntimeTurnPostureCodes.Posture.Architecture => RuntimeTurnAwarenessAdmissionHintEvidenceCodes.RoleArchitecture,
            RuntimeTurnPostureCodes.Posture.Guard => RuntimeTurnAwarenessAdmissionHintEvidenceCodes.RoleGuard,
            _ => null,
        };
    }

    private static void AddEvidence(ICollection<string> evidenceCodes, string? code)
    {
        if (!string.IsNullOrWhiteSpace(code)
            && evidenceCodes.Count < RuntimeTurnAwarenessAdmissionHintContract.MaxEvidenceCodes)
        {
            evidenceCodes.Add(code);
        }
    }

    private static void AddEvidence(ICollection<string> evidenceCodes, bool condition, string code)
    {
        if (condition)
        {
            AddEvidence(evidenceCodes, code);
        }
    }

    private static IReadOnlyList<string> ResolveExpressionSignals(RuntimeTurnAwarenessExpressionPolicy expressionPolicy)
    {
        return (expressionPolicy.StyleSignals ?? Array.Empty<string>())
            .Where(RuntimeTurnAwarenessProfileValidator.AllowedExpressionSignals.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(RuntimeTurnAwarenessProfileContract.ExpressionSignalMaxCount)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveExpressionCues(IReadOnlyList<string>? cues)
    {
        return (cues ?? Array.Empty<string>())
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Select(cue => cue.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(RuntimeTurnAwarenessProfileContract.ExpressionCueMaxCount)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveFieldPriority(RuntimeTurnAwarenessProfile profile)
    {
        return profile.FieldPriority
            .Concat(DefaultFieldPriorityForRole(profile.RoleId))
            .Where(RuntimeTurnAwarenessProfileValidator.AllowedFieldPriorityCodes.Contains)
            .Distinct(StringComparer.Ordinal)
            .Take(RuntimeTurnAwarenessProfileContract.FieldPriorityMaxCount)
            .ToArray();
    }

    private static IReadOnlyList<string> DefaultFieldPriorityForRole(string roleId)
    {
        return roleId switch
        {
            RuntimeTurnPostureCodes.Posture.Assistant =>
            [
                "desired_outcome",
                "topic",
                "success_signal",
                "scope",
                "risk",
            ],
            RuntimeTurnPostureCodes.Posture.Architecture =>
            [
                "scope",
                "constraint",
                "non_goal",
                "risk",
                "success_signal",
                "desired_outcome",
                "topic",
            ],
            RuntimeTurnPostureCodes.Posture.Guard =>
            [
                "risk",
                "constraint",
                "non_goal",
                "scope",
                "success_signal",
                "desired_outcome",
                "topic",
            ],
            RuntimeTurnPostureCodes.Posture.ProjectManager =>
            [
                "desired_outcome",
                "scope",
                "success_signal",
                "owner",
                "review_gate",
                "topic",
                "risk",
            ],
            _ =>
            [
                "topic",
                "desired_outcome",
                "scope",
                "success_signal",
            ],
        };
    }

    private static IReadOnlyList<string> ResolveStrategyCodes(RuntimeTurnAwarenessProfile profile)
    {
        var codes = new List<string>(RuntimeTurnAwarenessPolicyContract.StrategyCodeMaxCount);
        switch (profile.RoleId)
        {
            case RuntimeTurnPostureCodes.Posture.Assistant:
                codes.Add(RuntimeTurnAwarenessStrategyCodes.UserFacing);
                codes.Add(RuntimeTurnAwarenessStrategyCodes.LowFriction);
                break;
            case RuntimeTurnPostureCodes.Posture.Architecture:
                codes.Add(RuntimeTurnAwarenessStrategyCodes.BoundaryFirst);
                codes.Add(RuntimeTurnAwarenessStrategyCodes.ContractAware);
                codes.Add(RuntimeTurnAwarenessStrategyCodes.RiskFirst);
                break;
            case RuntimeTurnPostureCodes.Posture.Guard:
                codes.Add(RuntimeTurnAwarenessStrategyCodes.BoundaryFirst);
                codes.Add(RuntimeTurnAwarenessStrategyCodes.RiskFirst);
                codes.Add(RuntimeTurnAwarenessStrategyCodes.ReviewGateVisible);
                break;
            case RuntimeTurnPostureCodes.Posture.ProjectManager:
                codes.Add(RuntimeTurnAwarenessStrategyCodes.DecisionVisible);
                codes.Add(RuntimeTurnAwarenessStrategyCodes.SequenceVisible);
                codes.Add(RuntimeTurnAwarenessStrategyCodes.ReviewGateVisible);
                break;
        }

        if (profile.ChallengePolicy.WeakAssumptionCheck
            || string.Equals(profile.ChallengePolicy.Level, "high", StringComparison.Ordinal))
        {
            codes.Add(RuntimeTurnAwarenessStrategyCodes.WeakAssumptionCheck);
        }

        if (string.Equals(profile.TonePolicy.Directness, "high", StringComparison.Ordinal))
        {
            codes.Add(RuntimeTurnAwarenessStrategyCodes.DirectResponse);
        }

        return codes
            .Distinct(StringComparer.Ordinal)
            .Take(RuntimeTurnAwarenessPolicyContract.StrategyCodeMaxCount)
            .ToArray();
    }
}
