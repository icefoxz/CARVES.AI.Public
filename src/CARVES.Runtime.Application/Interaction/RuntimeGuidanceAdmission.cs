using System.Text;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Interaction;

public static class RuntimeGuidanceAdmissionKinds
{
    public const string ChatOnly = "chat_only";
    public const string CandidateStart = "candidate_start";
    public const string CandidateUpdate = "candidate_update";
    public const string CandidatePark = "candidate_park";
    public const string CandidateClear = "candidate_clear";
    public const string CandidateForget = "candidate_forget";
    public const string GovernedHandoff = "governed_handoff";
}

public static class RuntimeGuidanceAdmissionContract
{
    public const string CurrentVersion = "runtime-guidance-admission.v1";
}

public static class RuntimeGuidanceAdmissionClassifierContract
{
    public const string CurrentVersion = "runtime-guidance-admission-classifier.v4";
    public const int MaxEvidenceCodes = 8;
    public const int MaxBlockedReasonCodes = 4;
}

public static class RuntimeGuidanceAdmissionReasonCodes
{
    public const string EmptyInput = "empty_input";
    public const string NoAdmissionSignal = "no_admission_signal";
    public const string DiscussionOnly = "discussion_only";
    public const string NegatedHandoff = "negated_handoff";
    public const string ParkRequest = "park_request";
    public const string ClearRequest = "clear_request";
    public const string ForgetRequest = "forget_request";
    public const string GovernedHandoffRequest = "governed_handoff_request";
    public const string ExistingCandidatePresent = "existing_candidate_present";
    public const string FieldAssignmentSignal = "field_assignment_signal";
    public const string CandidateStartMarker = "candidate_start_marker";
    public const string CandidateUpdateMarker = "candidate_update_marker";
    public const string AwarenessAdmissionHint = "awareness_admission_hint";
}

public static class RuntimeGuidanceAdmissionClassifierEvidenceCodes
{
    public const string FieldAssignment = "field_assignment";
    public const string CandidateStart = "candidate_start";
    public const string CandidateUpdate = "candidate_update";
    public const string SemanticCandidateFrame = "semantic_candidate_frame";
    public const string SemanticUpdateFrame = "semantic_update_frame";
    public const string ExistingCandidate = "existing_candidate";
    public const string ExplicitPark = "explicit_park";
    public const string ExplicitClear = "explicit_clear";
    public const string ExplicitForget = "explicit_forget";
    public const string GovernedHandoff = "governed_handoff";
    public const string DiscussionOnly = "discussion_only";
    public const string ExplicitNoGuidance = "explicit_no_guidance";
    public const string NegatedHandoff = "negated_handoff";
    public const string AwarenessGuidanceHint = "awareness_guidance_hint";
    public const string AwarenessResponsive = "awareness_responsive";
    public const string AwarenessConservative = "awareness_conservative";
    public const string OrdinaryText = "ordinary_text";
}

public static class RuntimeGuidanceLifecycleControlKinds
{
    public const string None = "none";
    public const string Park = "park";
    public const string Clear = "clear";
    public const string Forget = "forget";
}

public static class RuntimeGuidanceLifecycleControlContract
{
    public const string CurrentVersion = "runtime-guidance-lifecycle-control.v2";
}

public static class RuntimeGuidanceLifecycleControlReasonCodes
{
    public const string NoLifecycleControl = "no_lifecycle_control";
    public const string ParkRequest = "park_request";
    public const string ClearRequest = "clear_request";
    public const string ForgetRequest = "forget_request";
    public const string NegatedParkRequest = "negated_park_request";
    public const string NegatedClearRequest = "negated_clear_request";
    public const string NegatedForgetRequest = "negated_forget_request";
}

public sealed record RuntimeGuidanceLifecycleControlDecision(
    [property: JsonPropertyName("kind")]
    string Kind,
    [property: JsonPropertyName("requests_park")]
    bool RequestsPark,
    [property: JsonPropertyName("requests_clear")]
    bool RequestsClear,
    [property: JsonPropertyName("requests_forget")]
    bool RequestsForget,
    [property: JsonPropertyName("is_negated")]
    bool IsNegated,
    [property: JsonPropertyName("contract_version")]
    string ContractVersion,
    [property: JsonPropertyName("reason_codes")]
    IReadOnlyList<string> ReasonCodes,
    [property: JsonPropertyName("confidence")]
    double Confidence);

public static class RuntimeGuidanceLifecycleControl
{
    public static RuntimeGuidanceLifecycleControlDecision Decide(string? userText)
    {
        var text = Normalize(userText);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Decision(
                RuntimeGuidanceLifecycleControlKinds.None,
                requestsPark: false,
                requestsClear: false,
                requestsForget: false,
                isNegated: false,
                [RuntimeGuidanceLifecycleControlReasonCodes.NoLifecycleControl],
                0.66d);
        }

        var parkRequested = ContainsAny(text, ParkRequestPhrases);
        var clearRequested = ContainsAny(text, ClearRequestPhrases);
        var forgetRequested = ContainsAny(text, ForgetRequestPhrases);
        var parkNegated = parkRequested && ContainsAny(text, NegatedParkRequestPhrases);
        var clearNegated = clearRequested && ContainsAny(text, NegatedClearRequestPhrases);
        var forgetNegated = forgetRequested && ContainsAny(text, NegatedForgetRequestPhrases);

        if (forgetRequested && !forgetNegated)
        {
            return Decision(
                RuntimeGuidanceLifecycleControlKinds.Forget,
                requestsPark: false,
                requestsClear: false,
                requestsForget: true,
                isNegated: false,
                [RuntimeGuidanceLifecycleControlReasonCodes.ForgetRequest],
                0.97d);
        }

        if (clearRequested && !clearNegated)
        {
            return Decision(
                RuntimeGuidanceLifecycleControlKinds.Clear,
                requestsPark: false,
                requestsClear: true,
                requestsForget: false,
                isNegated: false,
                [RuntimeGuidanceLifecycleControlReasonCodes.ClearRequest],
                0.97d);
        }

        if (parkRequested && !parkNegated)
        {
            return Decision(
                RuntimeGuidanceLifecycleControlKinds.Park,
                requestsPark: true,
                requestsClear: false,
                requestsForget: false,
                isNegated: false,
                [RuntimeGuidanceLifecycleControlReasonCodes.ParkRequest],
                0.97d);
        }

        if (forgetNegated)
        {
            return Decision(
                RuntimeGuidanceLifecycleControlKinds.None,
                requestsPark: false,
                requestsClear: false,
                requestsForget: false,
                isNegated: true,
                [RuntimeGuidanceLifecycleControlReasonCodes.NegatedForgetRequest],
                0.92d);
        }

        if (clearNegated)
        {
            return Decision(
                RuntimeGuidanceLifecycleControlKinds.None,
                requestsPark: false,
                requestsClear: false,
                requestsForget: false,
                isNegated: true,
                [RuntimeGuidanceLifecycleControlReasonCodes.NegatedClearRequest],
                0.92d);
        }

        if (parkNegated)
        {
            return Decision(
                RuntimeGuidanceLifecycleControlKinds.None,
                requestsPark: false,
                requestsClear: false,
                requestsForget: false,
                isNegated: true,
                [RuntimeGuidanceLifecycleControlReasonCodes.NegatedParkRequest],
                0.92d);
        }

        return Decision(
            RuntimeGuidanceLifecycleControlKinds.None,
            requestsPark: false,
            requestsClear: false,
            requestsForget: false,
            isNegated: false,
            [RuntimeGuidanceLifecycleControlReasonCodes.NoLifecycleControl],
            0.66d);
    }

    private static RuntimeGuidanceLifecycleControlDecision Decision(
        string kind,
        bool requestsPark,
        bool requestsClear,
        bool requestsForget,
        bool isNegated,
        IReadOnlyList<string> reasonCodes,
        double confidence)
    {
        return new RuntimeGuidanceLifecycleControlDecision(
            kind,
            requestsPark,
            requestsClear,
            requestsForget,
            isNegated,
            RuntimeGuidanceLifecycleControlContract.CurrentVersion,
            reasonCodes
                .Where(reasonCode => !string.IsNullOrWhiteSpace(reasonCode))
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToArray(),
            Math.Clamp(Math.Round(confidence, 2), 0d, 1d));
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var source = value
            .Trim()
            .ToLowerInvariant()
            .Replace("’", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool ContainsAny(string text, params string[] fragments)
    {
        return fragments.Any(fragment => text.Contains(fragment, StringComparison.Ordinal));
    }

    private static readonly string[] ClearRequestPhrases =
    [
        "clear guidance",
        "clear candidate",
        "discard candidate",
        "reset candidate",
        "不要继续这个方向",
        "清掉候选",
        "清除候选",
        "清空候选",
        "重置候选",
        "清掉引导",
        "清除引导",
        "重置引导",
    ];

    private static readonly string[] ParkRequestPhrases =
    [
        "park candidate",
        "park guidance",
        "park this",
        "pause this",
        "pause candidate",
        "pause guidance",
        "not land this yet",
        "先不落案",
        "暂时放置",
        "先放着",
        "先暂停",
    ];

    private static readonly string[] ForgetRequestPhrases =
    [
        "forget guidance",
        "forget candidate",
        "forget this candidate",
        "discard candidate memory",
        "忘记候选",
        "遗忘候选",
        "不要记住这个候选",
        "不要记住这个方向",
    ];

    private static readonly string[] NegatedParkRequestPhrases =
    [
        "不要暂时放置",
        "别暂时放置",
        "不要先放着",
        "别先放着",
        "不要暂停",
        "别暂停",
        "不要先不落案",
        "别先不落案",
        "do not park",
        "dont park",
        "not asking to park",
        "do not park candidate",
        "dont park candidate",
        "not asking to park candidate",
        "do not pause this",
        "dont pause this",
        "not asking to pause",
    ];

    private static readonly string[] NegatedClearRequestPhrases =
    [
        "不要清掉候选",
        "别清掉候选",
        "不要清除候选",
        "别清除候选",
        "不要清空候选",
        "别清空候选",
        "不要重置候选",
        "别重置候选",
        "不要清掉引导",
        "别清掉引导",
        "不要清除引导",
        "别清除引导",
        "不要重置引导",
        "别重置引导",
        "do not clear candidate",
        "dont clear candidate",
        "not asking to clear candidate",
        "do not discard candidate",
        "dont discard candidate",
        "not asking to discard candidate",
        "do not reset candidate",
        "dont reset candidate",
        "not asking to reset candidate",
        "do not clear guidance",
        "dont clear guidance",
        "not asking to clear guidance",
    ];

    private static readonly string[] NegatedForgetRequestPhrases =
    [
        "不要忘记候选",
        "别忘记候选",
        "不要遗忘候选",
        "别遗忘候选",
        "不要忘掉候选",
        "别忘掉候选",
        "do not forget candidate",
        "dont forget candidate",
        "not asking to forget candidate",
        "do not forget this candidate",
        "dont forget this candidate",
        "not asking to forget this candidate",
        "do not discard candidate memory",
        "dont discard candidate memory",
        "not asking to discard candidate memory",
    ];
}

public sealed record RuntimeGuidanceAdmissionDecision(
    [property: JsonPropertyName("kind")]
    string Kind,
    [property: JsonPropertyName("enters_guided_collection")]
    bool EntersGuidedCollection,
    [property: JsonPropertyName("can_build_candidate")]
    bool CanBuildCandidate,
    [property: JsonPropertyName("requests_park")]
    bool RequestsPark,
    [property: JsonPropertyName("requests_clear")]
    bool RequestsClear,
    [property: JsonPropertyName("requests_forget")]
    bool RequestsForget,
    [property: JsonPropertyName("requests_governed_handoff")]
    bool RequestsGovernedHandoff,
    [property: JsonPropertyName("contract_version")]
    string ContractVersion,
    [property: JsonPropertyName("reason_codes")]
    IReadOnlyList<string> ReasonCodes,
    [property: JsonPropertyName("confidence")]
    double Confidence);

public sealed record RuntimeGuidanceAdmissionClassifierInput(
    string? UserText,
    RuntimeGuidanceCandidate? ExistingCandidate = null,
    RuntimeTurnAwarenessAdmissionHints? AdmissionHints = null);

public sealed record RuntimeGuidanceAdmissionClassifierResult
{
    [JsonPropertyName("contract_version")]
    public string ContractVersion { get; init; } = RuntimeGuidanceAdmissionClassifierContract.CurrentVersion;

    [JsonPropertyName("recommended_kind")]
    public string RecommendedKind { get; init; } = RuntimeGuidanceAdmissionKinds.ChatOnly;

    [JsonPropertyName("has_prefilter_signal")]
    public bool HasPrefilterSignal { get; init; }

    [JsonPropertyName("has_field_assignment_signal")]
    public bool HasFieldAssignmentSignal { get; init; }

    [JsonPropertyName("has_candidate_start_signal")]
    public bool HasCandidateStartSignal { get; init; }

    [JsonPropertyName("has_candidate_update_signal")]
    public bool HasCandidateUpdateSignal { get; init; }

    [JsonPropertyName("has_awareness_guidance_signal")]
    public bool HasAwarenessGuidanceSignal { get; init; }

    [JsonPropertyName("is_discussion_only")]
    public bool IsDiscussionOnly { get; init; }

    [JsonPropertyName("is_negated_handoff")]
    public bool IsNegatedHandoff { get; init; }

    [JsonPropertyName("requests_park")]
    public bool RequestsPark { get; init; }

    [JsonPropertyName("requests_clear")]
    public bool RequestsClear { get; init; }

    [JsonPropertyName("requests_forget")]
    public bool RequestsForget { get; init; }

    [JsonPropertyName("requests_governed_handoff")]
    public bool RequestsGovernedHandoff { get; init; }

    [JsonPropertyName("enters_guided_collection")]
    public bool EntersGuidedCollection { get; init; }

    [JsonPropertyName("can_build_candidate")]
    public bool CanBuildCandidate { get; init; }

    [JsonPropertyName("candidate_signal_score")]
    public int CandidateSignalScore { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; } = 0.66d;

    [JsonPropertyName("evidence_codes")]
    public IReadOnlyList<string> EvidenceCodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("decision_reason_codes")]
    public IReadOnlyList<string> DecisionReasonCodes { get; init; } = Array.Empty<string>();

    [JsonPropertyName("blocked_reason_codes")]
    public IReadOnlyList<string> BlockedReasonCodes { get; init; } = Array.Empty<string>();
}

public static class RuntimeGuidanceAdmission
{
    private static readonly RuntimeGuidanceAdmissionClassifier AdmissionClassifier = new();

    public static RuntimeGuidanceAdmissionDecision Decide(
        string? userText,
        RuntimeGuidanceCandidate? existingCandidate = null,
        RuntimeTurnAwarenessAdmissionHints? admissionHints = null)
    {
        var classification = AdmissionClassifier.Classify(new RuntimeGuidanceAdmissionClassifierInput(userText, existingCandidate, admissionHints));
        return Decision(
            classification.RecommendedKind,
            classification.EntersGuidedCollection,
            classification.CanBuildCandidate,
            classification.RequestsPark,
            classification.RequestsClear,
            classification.RequestsForget,
            classification.RequestsGovernedHandoff,
            classification.DecisionReasonCodes.Count > 0 ? classification.DecisionReasonCodes : ToDecisionReasonCodes(classification),
            classification.Confidence);
    }

    public static RuntimeGuidanceAdmissionClassifierResult ClassifySignals(
        string? userText,
        RuntimeGuidanceCandidate? existingCandidate = null,
        RuntimeTurnAwarenessAdmissionHints? admissionHints = null)
    {
        return AdmissionClassifier.Classify(new RuntimeGuidanceAdmissionClassifierInput(userText, existingCandidate, admissionHints));
    }

    public static bool HasCandidateSignal(string? userText)
    {
        var decision = Decide(userText);
        return decision.EntersGuidedCollection && decision.CanBuildCandidate;
    }

    private static IReadOnlyList<string> ToDecisionReasonCodes(RuntimeGuidanceAdmissionClassifierResult classification)
    {
        if (classification.RequestsPark)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.ParkRequest];
        }

        if (classification.RequestsClear)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.ClearRequest];
        }

        if (classification.RequestsForget)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.ForgetRequest];
        }

        if (classification.RequestsGovernedHandoff)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.GovernedHandoffRequest];
        }

        if (classification.HasCandidateUpdateSignal)
        {
            var reasonCodes = new List<string> { RuntimeGuidanceAdmissionReasonCodes.ExistingCandidatePresent };
            if (classification.HasFieldAssignmentSignal)
            {
                reasonCodes.Add(RuntimeGuidanceAdmissionReasonCodes.FieldAssignmentSignal);
            }
            else if (classification.HasAwarenessGuidanceSignal)
            {
                reasonCodes.Add(RuntimeGuidanceAdmissionReasonCodes.AwarenessAdmissionHint);
            }
            else
            {
                reasonCodes.Add(RuntimeGuidanceAdmissionReasonCodes.CandidateUpdateMarker);
            }

            return reasonCodes;
        }

        if (classification.HasCandidateStartSignal)
        {
            if (classification.HasFieldAssignmentSignal)
            {
                return [RuntimeGuidanceAdmissionReasonCodes.FieldAssignmentSignal];
            }

            return classification.HasAwarenessGuidanceSignal
                ? [RuntimeGuidanceAdmissionReasonCodes.AwarenessAdmissionHint]
                : [RuntimeGuidanceAdmissionReasonCodes.CandidateStartMarker];
        }

        if (classification.IsDiscussionOnly)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.DiscussionOnly];
        }

        if (classification.IsNegatedHandoff)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.NegatedHandoff];
        }

        return [RuntimeGuidanceAdmissionReasonCodes.NoAdmissionSignal];
    }

    private static RuntimeGuidanceAdmissionDecision Decision(
        string kind,
        bool entersGuidedCollection,
        bool canBuildCandidate,
        bool requestsPark,
        bool requestsClear,
        bool requestsForget,
        bool requestsGovernedHandoff,
        IReadOnlyList<string> reasonCodes,
        double confidence)
    {
        return new RuntimeGuidanceAdmissionDecision(
            kind,
            entersGuidedCollection,
            canBuildCandidate,
            requestsPark,
            requestsClear,
            requestsForget,
            requestsGovernedHandoff,
            RuntimeGuidanceAdmissionContract.CurrentVersion,
            reasonCodes
                .Where(reasonCode => !string.IsNullOrWhiteSpace(reasonCode))
                .Take(RuntimeGuidanceAdmissionClassifierContract.MaxBlockedReasonCodes)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Math.Clamp(Math.Round(confidence, 2), 0d, 1d));
    }
}

public sealed class RuntimeGuidanceAdmissionClassifier
{
    public RuntimeGuidanceAdmissionClassifierResult Classify(RuntimeGuidanceAdmissionClassifierInput input)
    {
        var text = Normalize(input.UserText);
        if (string.IsNullOrWhiteSpace(text))
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.ChatOnly,
                evidenceCodes: [],
                blockedReasonCodes: [RuntimeGuidanceAdmissionReasonCodes.EmptyInput],
                hasPrefilterSignal: false,
                candidateSignalScore: 0,
                confidence: 0.66d);
        }

        var lifecycleControl = RuntimeGuidanceLifecycleControl.Decide(input.UserText);
        var admissionHints = input.AdmissionHints ?? new RuntimeTurnAwarenessAdmissionHints();
        var semanticFrame = RuntimeGuidanceAdmissionSemanticFrame.Analyze(text);
        var evidenceCodes = new List<string>(RuntimeGuidanceAdmissionClassifierContract.MaxEvidenceCodes);
        var hasFieldAssignmentSignal = HasFieldBearingGuidance(text);
        var hasCandidateUpdateMarker = HasCandidateUpdateMarker(text)
            || semanticFrame.HasCandidateUpdateFrame;
        var hasCandidateStartMarker = HasCandidateStartMarker(text)
            || semanticFrame.HasCandidateStartFrame;
        var explicitNoGuidance = IsExplicitNoGuidanceRequest(text);
        var discussionOnly = explicitNoGuidance
            || IsStrongDiscussionOnlyIntent(text)
            || (IsDiscussionOnlyIntent(text) && !hasFieldAssignmentSignal);
        var negatedHandoff = IsNegatedHandoffRequest(text);
        var requestsPark = lifecycleControl.RequestsPark
            && !discussionOnly
            && !negatedHandoff;
        var requestsClear = lifecycleControl.RequestsClear && !discussionOnly;
        var requestsForget = lifecycleControl.RequestsForget && !discussionOnly;
        var hasExistingCandidate = input.ExistingCandidate is not null;
        var hasAwarenessGuidanceSignal = HasAwarenessGuidanceSignal(text, admissionHints);
        var hasAwarenessStartSignal = AllowsAwarenessCandidateStart(admissionHints)
            && hasAwarenessGuidanceSignal;
        var hasAwarenessUpdateSignal = hasExistingCandidate
            && AllowsAwarenessCandidateUpdate(admissionHints)
            && hasAwarenessGuidanceSignal;
        var requestsGovernedHandoff = IsGovernedHandoffRequest(
            text,
            hasFieldAssignmentSignal,
            hasCandidateUpdateMarker,
            semanticFrame.HasCandidateStartFrame,
            discussionOnly,
            negatedHandoff);
        var candidateSignalScore = ScoreCandidateSignal(
            hasFieldAssignmentSignal,
            hasCandidateStartMarker,
            hasCandidateUpdateMarker,
            hasExistingCandidate,
            discussionOnly,
            negatedHandoff,
            hasAwarenessStartSignal || hasAwarenessUpdateSignal);
        var candidateStartThreshold = ResolveCandidateStartThreshold(admissionHints);
        var candidateUpdateThreshold = ResolveCandidateUpdateThreshold(admissionHints);
        AddEvidence(evidenceCodes, hasFieldAssignmentSignal, RuntimeGuidanceAdmissionClassifierEvidenceCodes.FieldAssignment);
        AddEvidence(evidenceCodes, hasCandidateStartMarker, RuntimeGuidanceAdmissionClassifierEvidenceCodes.CandidateStart);
        AddEvidence(evidenceCodes, hasCandidateUpdateMarker, RuntimeGuidanceAdmissionClassifierEvidenceCodes.CandidateUpdate);
        AddEvidence(evidenceCodes, semanticFrame.HasCandidateStartFrame, RuntimeGuidanceAdmissionClassifierEvidenceCodes.SemanticCandidateFrame);
        AddEvidence(evidenceCodes, semanticFrame.HasCandidateUpdateFrame, RuntimeGuidanceAdmissionClassifierEvidenceCodes.SemanticUpdateFrame);
        AddEvidence(evidenceCodes, hasExistingCandidate, RuntimeGuidanceAdmissionClassifierEvidenceCodes.ExistingCandidate);
        AddEvidence(evidenceCodes, requestsPark, RuntimeGuidanceAdmissionClassifierEvidenceCodes.ExplicitPark);
        AddEvidence(evidenceCodes, requestsClear, RuntimeGuidanceAdmissionClassifierEvidenceCodes.ExplicitClear);
        AddEvidence(evidenceCodes, requestsForget, RuntimeGuidanceAdmissionClassifierEvidenceCodes.ExplicitForget);
        AddEvidence(evidenceCodes, requestsGovernedHandoff, RuntimeGuidanceAdmissionClassifierEvidenceCodes.GovernedHandoff);
        AddEvidence(evidenceCodes, explicitNoGuidance, RuntimeGuidanceAdmissionClassifierEvidenceCodes.ExplicitNoGuidance);
        AddEvidence(evidenceCodes, discussionOnly, RuntimeGuidanceAdmissionClassifierEvidenceCodes.DiscussionOnly);
        AddEvidence(evidenceCodes, negatedHandoff, RuntimeGuidanceAdmissionClassifierEvidenceCodes.NegatedHandoff);
        AddEvidence(evidenceCodes, hasAwarenessGuidanceSignal, RuntimeGuidanceAdmissionClassifierEvidenceCodes.AwarenessGuidanceHint);
        AddEvidence(
            evidenceCodes,
            hasAwarenessGuidanceSignal
                && string.Equals(admissionHints.GuidanceSensitivity, RuntimeTurnAwarenessAdmissionSensitivityCodes.Responsive, StringComparison.Ordinal),
            RuntimeGuidanceAdmissionClassifierEvidenceCodes.AwarenessResponsive);
        AddEvidence(
            evidenceCodes,
            hasAwarenessGuidanceSignal
                && string.Equals(admissionHints.GuidanceSensitivity, RuntimeTurnAwarenessAdmissionSensitivityCodes.Conservative, StringComparison.Ordinal),
            RuntimeGuidanceAdmissionClassifierEvidenceCodes.AwarenessConservative);

        var hasPrefilterSignal = evidenceCodes.Count > 0;
        if (requestsPark)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.CandidatePark,
                evidenceCodes,
                blockedReasonCodes: [RuntimeGuidanceAdmissionReasonCodes.ParkRequest],
                requestsPark: true,
                candidateSignalScore: candidateSignalScore,
                confidence: 0.97d);
        }

        if (requestsClear)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.CandidateClear,
                evidenceCodes,
                blockedReasonCodes: [RuntimeGuidanceAdmissionReasonCodes.ClearRequest],
                requestsClear: true,
                candidateSignalScore: candidateSignalScore,
                confidence: 0.97d);
        }

        if (requestsForget)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.CandidateForget,
                evidenceCodes,
                blockedReasonCodes: [RuntimeGuidanceAdmissionReasonCodes.ForgetRequest],
                requestsForget: true,
                candidateSignalScore: candidateSignalScore,
                confidence: 0.97d);
        }

        if (requestsGovernedHandoff)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.GovernedHandoff,
                evidenceCodes,
                blockedReasonCodes: [],
                requestsGovernedHandoff: true,
                hasPrefilterSignal: true,
                entersGuidedCollection: true,
                canBuildCandidate: true,
                candidateSignalScore: candidateSignalScore,
                confidence: 0.92d);
        }

        var hasCandidateUpdateSignal = hasExistingCandidate
            && !discussionOnly
            && candidateSignalScore >= candidateUpdateThreshold
            && (hasFieldAssignmentSignal || hasCandidateUpdateMarker || hasAwarenessUpdateSignal);
        if (hasCandidateUpdateSignal)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.CandidateUpdate,
                evidenceCodes,
                blockedReasonCodes: [],
                hasPrefilterSignal: true,
                hasFieldAssignmentSignal: hasFieldAssignmentSignal,
                hasCandidateStartSignal: false,
                hasCandidateUpdateSignal: true,
                hasAwarenessGuidanceSignal: hasAwarenessUpdateSignal,
                entersGuidedCollection: true,
                canBuildCandidate: true,
                candidateSignalScore: candidateSignalScore,
                confidence: hasFieldAssignmentSignal ? 0.9d : hasAwarenessUpdateSignal ? 0.72d : 0.74d);
        }

        var hasCandidateStartSignal = !discussionOnly
            && candidateSignalScore >= candidateStartThreshold
            && (hasFieldAssignmentSignal || hasCandidateStartMarker || hasAwarenessStartSignal);
        if (hasCandidateStartSignal)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.CandidateStart,
                evidenceCodes,
                blockedReasonCodes: [],
                hasPrefilterSignal: true,
                hasFieldAssignmentSignal: hasFieldAssignmentSignal,
                hasCandidateStartSignal: true,
                hasCandidateUpdateSignal: false,
                hasAwarenessGuidanceSignal: hasAwarenessStartSignal,
                entersGuidedCollection: true,
                canBuildCandidate: true,
                candidateSignalScore: candidateSignalScore,
                confidence: hasFieldAssignmentSignal ? 0.88d : hasAwarenessStartSignal ? 0.72d : 0.74d);
        }

        if (discussionOnly)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.ChatOnly,
                evidenceCodes,
                blockedReasonCodes: [RuntimeGuidanceAdmissionReasonCodes.DiscussionOnly],
                hasPrefilterSignal: true,
                hasFieldAssignmentSignal: hasFieldAssignmentSignal,
                hasAwarenessGuidanceSignal: hasAwarenessGuidanceSignal,
                isDiscussionOnly: true,
                isNegatedHandoff: negatedHandoff,
                candidateSignalScore: candidateSignalScore,
                confidence: explicitNoGuidance ? 0.97d : 0.93d);
        }

        if (negatedHandoff)
        {
            return BuildResult(
                RuntimeGuidanceAdmissionKinds.ChatOnly,
                evidenceCodes,
                blockedReasonCodes: [RuntimeGuidanceAdmissionReasonCodes.NegatedHandoff],
                hasPrefilterSignal: true,
                isNegatedHandoff: true,
                candidateSignalScore: candidateSignalScore,
                confidence: 0.92d);
        }

        return BuildResult(
            RuntimeGuidanceAdmissionKinds.ChatOnly,
            hasPrefilterSignal ? evidenceCodes : [RuntimeGuidanceAdmissionClassifierEvidenceCodes.OrdinaryText],
            blockedReasonCodes: [RuntimeGuidanceAdmissionReasonCodes.NoAdmissionSignal],
            hasPrefilterSignal: hasPrefilterSignal,
            candidateSignalScore: candidateSignalScore,
            confidence: 0.66d);
    }

    private static RuntimeGuidanceAdmissionClassifierResult BuildResult(
        string recommendedKind,
        IReadOnlyList<string> evidenceCodes,
        IReadOnlyList<string> blockedReasonCodes,
        bool requestsPark = false,
        bool requestsClear = false,
        bool requestsForget = false,
        bool requestsGovernedHandoff = false,
        bool hasPrefilterSignal = true,
        bool hasFieldAssignmentSignal = false,
        bool hasCandidateStartSignal = false,
        bool hasCandidateUpdateSignal = false,
        bool hasAwarenessGuidanceSignal = false,
        bool isDiscussionOnly = false,
        bool isNegatedHandoff = false,
        bool entersGuidedCollection = false,
        bool canBuildCandidate = false,
        int candidateSignalScore = 0,
        double confidence = 0.66d)
    {
        return new RuntimeGuidanceAdmissionClassifierResult
        {
            RecommendedKind = recommendedKind,
            HasPrefilterSignal = hasPrefilterSignal,
            HasFieldAssignmentSignal = hasFieldAssignmentSignal,
            HasCandidateStartSignal = hasCandidateStartSignal,
            HasCandidateUpdateSignal = hasCandidateUpdateSignal,
            HasAwarenessGuidanceSignal = hasAwarenessGuidanceSignal,
            IsDiscussionOnly = isDiscussionOnly,
            IsNegatedHandoff = isNegatedHandoff,
            RequestsPark = requestsPark,
            RequestsClear = requestsClear,
            RequestsForget = requestsForget,
            RequestsGovernedHandoff = requestsGovernedHandoff,
            EntersGuidedCollection = entersGuidedCollection,
            CanBuildCandidate = canBuildCandidate,
            CandidateSignalScore = Math.Clamp(candidateSignalScore, 0, 8),
            Confidence = Math.Clamp(Math.Round(confidence, 2), 0d, 1d),
            EvidenceCodes = evidenceCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .Take(RuntimeGuidanceAdmissionClassifierContract.MaxEvidenceCodes)
                .ToArray(),
            DecisionReasonCodes = ResolveDecisionReasonCodes(
                    recommendedKind,
                    blockedReasonCodes,
                    requestsPark,
                    requestsClear,
                    requestsForget,
                    requestsGovernedHandoff,
                    hasFieldAssignmentSignal,
                    hasCandidateStartSignal,
                    hasCandidateUpdateSignal,
                    hasAwarenessGuidanceSignal,
                    isDiscussionOnly,
                    isNegatedHandoff)
                .Take(RuntimeGuidanceAdmissionClassifierContract.MaxBlockedReasonCodes)
                .ToArray(),
            BlockedReasonCodes = blockedReasonCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.Ordinal)
                .Take(RuntimeGuidanceAdmissionClassifierContract.MaxBlockedReasonCodes)
                .ToArray(),
        };
    }

    private static IReadOnlyList<string> ResolveDecisionReasonCodes(
        string recommendedKind,
        IReadOnlyList<string> blockedReasonCodes,
        bool requestsPark,
        bool requestsClear,
        bool requestsForget,
        bool requestsGovernedHandoff,
        bool hasFieldAssignmentSignal,
        bool hasCandidateStartSignal,
        bool hasCandidateUpdateSignal,
        bool hasAwarenessGuidanceSignal,
        bool isDiscussionOnly,
        bool isNegatedHandoff)
    {
        if (blockedReasonCodes.Count > 0)
        {
            return blockedReasonCodes
                .Where(reasonCode => !string.IsNullOrWhiteSpace(reasonCode))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (requestsPark)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.ParkRequest];
        }

        if (requestsClear)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.ClearRequest];
        }

        if (requestsForget)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.ForgetRequest];
        }

        if (requestsGovernedHandoff)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.GovernedHandoffRequest];
        }

        if (hasCandidateUpdateSignal)
        {
            var reasonCodes = new List<string> { RuntimeGuidanceAdmissionReasonCodes.ExistingCandidatePresent };
            reasonCodes.Add(hasFieldAssignmentSignal
                ? RuntimeGuidanceAdmissionReasonCodes.FieldAssignmentSignal
                : hasAwarenessGuidanceSignal
                    ? RuntimeGuidanceAdmissionReasonCodes.AwarenessAdmissionHint
                    : RuntimeGuidanceAdmissionReasonCodes.CandidateUpdateMarker);
            return reasonCodes;
        }

        if (hasCandidateStartSignal)
        {
            if (hasFieldAssignmentSignal)
            {
                return [RuntimeGuidanceAdmissionReasonCodes.FieldAssignmentSignal];
            }

            return hasAwarenessGuidanceSignal
                ? [RuntimeGuidanceAdmissionReasonCodes.AwarenessAdmissionHint]
                : [RuntimeGuidanceAdmissionReasonCodes.CandidateStartMarker];
        }

        if (isDiscussionOnly)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.DiscussionOnly];
        }

        if (isNegatedHandoff)
        {
            return [RuntimeGuidanceAdmissionReasonCodes.NegatedHandoff];
        }

        return string.Equals(recommendedKind, RuntimeGuidanceAdmissionKinds.ChatOnly, StringComparison.Ordinal)
            ? [RuntimeGuidanceAdmissionReasonCodes.NoAdmissionSignal]
            : Array.Empty<string>();
    }

    private static int ScoreCandidateSignal(
        bool hasFieldAssignmentSignal,
        bool hasCandidateStartMarker,
        bool hasCandidateUpdateMarker,
        bool hasExistingCandidate,
        bool discussionOnly,
        bool negatedHandoff,
        bool hasAwarenessGuidanceSignal)
    {
        var candidateSignalScore = 0;
        candidateSignalScore += hasFieldAssignmentSignal ? 3 : 0;
        candidateSignalScore += hasCandidateStartMarker ? 2 : 0;
        candidateSignalScore += hasCandidateUpdateMarker ? 2 : 0;
        candidateSignalScore += hasExistingCandidate ? 1 : 0;
        candidateSignalScore += hasAwarenessGuidanceSignal ? 2 : 0;
        candidateSignalScore -= discussionOnly ? 4 : 0;
        candidateSignalScore -= negatedHandoff ? 3 : 0;
        return Math.Clamp(candidateSignalScore, 0, 8);
    }

    private static int ResolveCandidateStartThreshold(RuntimeTurnAwarenessAdmissionHints admissionHints)
    {
        return admissionHints.GuidanceSensitivity switch
        {
            RuntimeTurnAwarenessAdmissionSensitivityCodes.Responsive => 1,
            RuntimeTurnAwarenessAdmissionSensitivityCodes.Conservative => 3,
            _ => 2,
        };
    }

    private static int ResolveCandidateUpdateThreshold(RuntimeTurnAwarenessAdmissionHints admissionHints)
    {
        return admissionHints.GuidanceSensitivity switch
        {
            RuntimeTurnAwarenessAdmissionSensitivityCodes.Responsive => 2,
            RuntimeTurnAwarenessAdmissionSensitivityCodes.Conservative => 4,
            _ => 3,
        };
    }

    private static bool AllowsAwarenessCandidateStart(RuntimeTurnAwarenessAdmissionHints admissionHints)
    {
        return string.Equals(
            admissionHints.GuidanceSensitivity,
            RuntimeTurnAwarenessAdmissionSensitivityCodes.Responsive,
            StringComparison.Ordinal);
    }

    private static bool AllowsAwarenessCandidateUpdate(RuntimeTurnAwarenessAdmissionHints admissionHints)
    {
        return admissionHints.ExistingCandidateUpdateBias is RuntimeTurnAwarenessAdmissionUpdateBiasCodes.RoleField
            or RuntimeTurnAwarenessAdmissionUpdateBiasCodes.ExistingCandidate;
    }

    private static bool HasAwarenessGuidanceSignal(string text, RuntimeTurnAwarenessAdmissionHints admissionHints)
    {
        if (!string.Equals(
                admissionHints.AuthorityScope,
                RuntimeTurnAwarenessAdmissionAuthorityScopes.GuidanceOnly,
                StringComparison.Ordinal))
        {
            return false;
        }

        return admissionHints.WatchedFieldCodes.Any(fieldCode => FieldCodeMentions(fieldCode).Any(fragment =>
            text.Contains(fragment, StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<string> FieldCodeMentions(string fieldCode)
    {
        return fieldCode switch
        {
            "topic" => ["项目方向", "功能方向", "主题", "方向", "topic", "direction"],
            "desired_outcome" => ["目标", "产出", "想达到", "outcome", "goal", "desired outcome"],
            "scope" => ["范围", "边界", "scope", "boundary"],
            "success_signal" => ["成功标准", "验收", "确认标准", "success signal", "acceptance", "ready when"],
            "constraint" => ["约束", "限制", "不能", "constraint", "restriction"],
            "non_goal" => ["非目标", "不做", "out of scope", "non-goal", "non goal"],
            "risk" => ["风险", "risk", "blocker"],
            "owner" => ["负责人", "跟进人", "owner", "assignee"],
            "review_gate" => ["复核点", "复核门槛", "复核条件", "review gate", "review checkpoint"],
            _ => Array.Empty<string>(),
        };
    }

    private static void AddEvidence(ICollection<string> evidenceCodes, bool condition, string code)
    {
        if (condition && evidenceCodes.Count < RuntimeGuidanceAdmissionClassifierContract.MaxEvidenceCodes)
        {
            evidenceCodes.Add(code);
        }
    }

    private static bool IsGovernedHandoffRequest(
        string text,
        bool hasFieldBearingGuidance,
        bool hasCandidateUpdateMarker,
        bool hasSemanticCandidateFrame,
        bool discussionOnly,
        bool negatedHandoff)
    {
        if (hasFieldBearingGuidance
            || hasCandidateUpdateMarker
            || hasSemanticCandidateFrame
            || negatedHandoff
            || discussionOnly)
        {
            return false;
        }

        return ContainsAny(
            text,
            "可以落案",
            "落案",
            "governed intent draft",
            "intent draft",
            "land it",
            "land this",
            "handoff",
            "planning intake",
            "use governed route");
    }

    private static bool IsNegatedHandoffRequest(string text)
    {
        if (ContainsAny(
            text,
            "要不要落案",
            "需不需要落案",
            "是否落案",
            "whether to land",
            "whether this should land",
            "whether it should land"))
        {
            return false;
        }

        return ContainsAny(
            text,
            "不要落案",
            "别落案",
            "不需要落案",
            "不是要落案",
            "do not land",
            "don't land",
            "not asking to land",
            "not ready to land");
    }

    private static bool IsDiscussionOnlyIntent(string text)
    {
        return ContainsAny(
            text,
            "只是问",
            "只是确认",
            "只是讨论",
            "先聊聊",
            "聊一下",
            "聊聊",
            "是什么意思",
            "是什么",
            "是不是",
            "可以落案吗",
            "不要进入流程",
            "别进入流程",
            "不要进入引导",
            "别进入引导",
            "do not start guidance",
            "don't start guidance",
            "do not guide",
            "don't guide",
            "not build a candidate",
            "not start a candidate",
            "just asking",
            "just checking",
            "only discuss",
            "only want to ask",
            "want to ask a question",
            "can this be landed",
            "can we land this",
            "what does project direction mean",
            "what does feature direction mean");
    }

    private static bool IsStrongDiscussionOnlyIntent(string text)
    {
        return ContainsAny(
            text,
            "不要记录候选",
            "别记录候选",
            "先别记录候选",
            "不要收集候选",
            "别收集候选",
            "不要进入流程",
            "别进入流程",
            "不要进入引导",
            "别进入引导",
            "do not record candidate",
            "don't record candidate",
            "do not capture candidate",
            "don't capture candidate",
            "do not collect candidate",
            "don't collect candidate",
            "do not start guidance",
            "don't start guidance",
            "do not guide",
            "don't guide",
            "not build a candidate",
            "not start a candidate");
    }

    private static bool IsExplicitNoGuidanceRequest(string text)
    {
        return ContainsAny(
            text,
            "不要进入流程",
            "别进入流程",
            "不要进入引导",
            "别进入引导",
            "不要记录候选",
            "别记录候选",
            "先别记录候选",
            "不要收集候选",
            "别收集候选",
            "do not start guidance",
            "don't start guidance",
            "do not guide",
            "don't guide",
            "do not record candidate",
            "don't record candidate",
            "do not capture candidate",
            "don't capture candidate",
            "do not collect candidate",
            "don't collect candidate",
            "not build a candidate",
            "not start a candidate");
    }

    private static bool HasFieldBearingGuidance(string text)
    {
        var assignmentText = RemoveFieldQuestionPhrases(text);
        return ContainsAny(
            assignmentText,
            "目标是",
            "目标不是",
            "目标改成",
            "目标改为",
            "目标应该是",
            "目标可能",
            "目标:",
            "目标：",
            "范围是",
            "范围改成",
            "范围改为",
            "范围可能",
            "范围:",
            "范围：",
            "成功标准是",
            "成功标准:",
            "成功标准：",
            "验收是",
            "验收标准",
            "验收:",
            "验收：",
            "约束是",
            "约束:",
            "约束：",
            "非目标是",
            "非目标:",
            "非目标：",
            "风险是",
            "风险可能",
            "风险:",
            "风险：",
            "负责人是",
            "负责人:",
            "负责人：",
            "跟进人是",
            "跟进人:",
            "跟进人：",
            "复核点是",
            "复核点:",
            "复核点：",
            "复核门槛是",
            "复核门槛:",
            "复核门槛：",
            "复核条件是",
            "复核条件:",
            "复核条件：",
            "goal should be",
            "goal is",
            "outcome should be",
            "outcome is",
            "scope should be",
            "scope is",
            "success means",
            "ready when",
            "acceptance should be",
            "acceptance is",
            "acceptance:",
            "constraint is",
            "non-goal is",
            "non goal is",
            "out of scope is",
            "risk is",
            "owner is",
            "owner:",
            "assignee is",
            "assignee:",
            "review gate is",
            "review gate:",
            "review checkpoint is",
            "review checkpoint:");
    }

    private static string RemoveFieldQuestionPhrases(string text)
    {
        foreach (var phrase in new[]
        {
            "目标是什么",
            "范围是什么",
            "成功标准是什么",
            "验收是什么",
            "验收是什么意思",
            "约束是什么",
            "风险是什么",
            "负责人是谁",
            "跟进人是谁",
            "复核点是什么",
            "复核门槛是什么",
            "复核条件是什么",
            "what is the goal",
            "what is the scope",
            "what does acceptance mean",
            "what is acceptance",
            "what is the constraint",
            "what is the risk",
            "who is the owner",
            "who owns this",
            "what is the review gate",
            "what is the review checkpoint",
        })
        {
            text = text.Replace(phrase, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    private static bool HasCandidateStartMarker(string text)
    {
        return ContainsAny(
            text,
            "平铺计划",
            "功能方向",
            "项目方向",
            "我想让",
            "我希望 carves",
            "希望 carves",
            "需要一个",
            "feature direction",
            "project direction",
            "i want to build",
            "we need a")
            || (HasCreationIntent(text) && HasGuidanceObject(text));
    }

    private static bool HasCandidateUpdateMarker(string text)
    {
        return ContainsAny(
            text,
            "补充",
            "补一下",
            "再补",
            "另外",
            "修正",
            "改成",
            "改为",
            "更新为",
            "不是",
            "应该是",
            "去掉",
            "移除",
            "删除",
            "不再包含",
            "换一个方向",
            "换个方向",
            "另一个方向",
            "新方向",
            "correction",
            "replace",
            "change to",
            "remove",
            "drop",
            "delete",
            "supplement",
            "add that",
            "new topic",
            "new direction",
            "different direction");
    }

    private static bool HasCreationIntent(string text)
    {
        return ContainsAny(
            text,
            "我想",
            "我希望",
            "希望",
            "需要",
            "做一个",
            "加一个",
            "增加",
            "新增",
            "设计",
            "建立",
            "build",
            "create",
            "add",
            "need",
            "want");
    }

    private static bool HasGuidanceObject(string text)
    {
        return ContainsAny(
                text,
                "carves",
                "runtime",
                "引导",
                "候选",
                "方向整理",
                "落案",
                "guidance",
                "candidate",
                "intent candidate",
                "landing")
            || (ContainsAny(text, "字段", "field", "fields") && ContainsAny(text, "收集", "collect", "candidate", "候选"))
            || (ContainsAny(text, "确认", "confirmation") && ContainsAny(text, "候选", "candidate", "落案", "landing", "intent"))
            || (ContainsAny(text, "机制", "workflow", "mechanism") && ContainsAny(text, "引导", "guidance", "候选", "candidate", "确认", "confirmation", "落案", "landing"));
    }

    private static string Normalize(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().ToLowerInvariant();
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed record RuntimeGuidanceAdmissionSemanticFrameResult(
    bool HasCandidateStartFrame,
    bool HasCandidateUpdateFrame);

internal static class RuntimeGuidanceAdmissionSemanticFrame
{
    public static RuntimeGuidanceAdmissionSemanticFrameResult Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new RuntimeGuidanceAdmissionSemanticFrameResult(false, false);
        }

        var hasCompletenessSignal = HasCompletenessSignal(text);
        var hasConfirmationSignal = HasConfirmationSignal(text);
        var hasWorkSurfaceSignal = HasWorkSurfaceSignal(text);
        var hasSafetyBoundarySignal = HasSafetyBoundarySignal(text);
        var hasCreationSignal = HasCreationSignal(text);
        var hasRevisionSignal = HasRevisionSignal(text);
        var hasCandidateVocabulary = HasCandidateVocabulary(text);
        var hasFieldCluster = HasFieldCluster(text);

        var startScore = 0;
        startScore += hasCompletenessSignal ? 2 : 0;
        startScore += hasConfirmationSignal ? 2 : 0;
        startScore += hasFieldCluster ? 2 : 0;
        startScore += hasWorkSurfaceSignal ? 1 : 0;
        startScore += hasSafetyBoundarySignal ? 1 : 0;
        startScore += hasCreationSignal ? 1 : 0;
        startScore += hasCandidateVocabulary ? 1 : 0;

        var updateScore = 0;
        updateScore += hasRevisionSignal ? 2 : 0;
        updateScore += hasConfirmationSignal ? 1 : 0;
        updateScore += hasSafetyBoundarySignal ? 1 : 0;
        updateScore += hasCandidateVocabulary ? 1 : 0;
        updateScore += hasFieldCluster ? 1 : 0;

        var hasCandidateStartFrame = startScore >= 5
            && hasConfirmationSignal
            && (hasCompletenessSignal || hasFieldCluster)
            && (hasWorkSurfaceSignal || hasCandidateVocabulary || hasCreationSignal);
        var hasCandidateUpdateFrame = updateScore >= 4
            && hasRevisionSignal
            && (hasConfirmationSignal || hasCandidateVocabulary || hasFieldCluster);

        return new RuntimeGuidanceAdmissionSemanticFrameResult(hasCandidateStartFrame, hasCandidateUpdateFrame);
    }

    private static bool HasCompletenessSignal(string text)
    {
        return ContainsAny(
            text,
            "说清楚",
            "讲清楚",
            "信息够",
            "信息足够",
            "方向明确",
            "字段满足",
            "字段清楚",
            "大概方向明确",
            "清晰 enough",
            "enough signal",
            "enough information",
            "enough context",
            "clear enough",
            "direction is clear",
            "fields are clear",
            "fields are complete",
            "has enough shape");
    }

    private static bool HasConfirmationSignal(string text)
    {
        return ContainsAny(
            text,
            "提醒我确认",
            "提示我确认",
            "问我是否",
            "问我需不需要",
            "确认要不要",
            "确认是否",
            "用户确认",
            "人工确认",
            "先确认",
            "ask me to confirm",
            "asks me to confirm",
            "ask for confirmation",
            "asks for confirmation",
            "user confirmation",
            "human confirmation",
            "confirm before",
            "review before");
    }

    private static bool HasWorkSurfaceSignal(string text)
    {
        return ContainsAny(
            text,
            "聊天",
            "对话",
            "会话",
            "runtime",
            "carves",
            "agent",
            "assistant",
            "系统",
            "它",
            "conversation",
            "chat",
            "session");
    }

    private static bool HasSafetyBoundarySignal(string text)
    {
        return ContainsAny(
            text,
            "不要自动执行",
            "不能自动执行",
            "不是直接执行",
            "不会直接执行",
            "先不要执行",
            "不能自己批准",
            "before executing",
            "before execution",
            "instead of executing",
            "not execute automatically",
            "not auto execute",
            "without executing",
            "before approval");
    }

    private static bool HasCreationSignal(string text)
    {
        return ContainsAny(
            text,
            "应该",
            "需要",
            "希望",
            "可以让",
            "能够",
            "should",
            "need",
            "needs to",
            "can",
            "could");
    }

    private static bool HasRevisionSignal(string text)
    {
        return ContainsAny(
            text,
            "其实",
            "更像",
            "应该只",
            "只需要",
            "改成",
            "改为",
            "不是",
            "不需要",
            "先不要",
            "actually",
            "rather than",
            "instead",
            "only needs to",
            "should only",
            "not need to",
            "does not need to");
    }

    private static bool HasCandidateVocabulary(string text)
    {
        return ContainsAny(
            text,
            "候选",
            "落案",
            "引导",
            "意图",
            "方向整理",
            "candidate",
            "landing",
            "guidance",
            "intent");
    }

    private static bool HasFieldCluster(string text)
    {
        var fieldSignalCount = CountSignals(
            text,
            ("目标", "goal"),
            ("范围", "scope"),
            ("边界", "boundary"),
            ("验收", "acceptance"),
            ("成功标准", "success signal"),
            ("约束", "constraint"),
            ("风险", "risk"),
            ("负责人", "owner"),
            ("复核点", "review gate"));
        return fieldSignalCount >= 2;
    }

    private static int CountSignals(string text, params (string Chinese, string English)[] signals)
    {
        return signals.Count(signal =>
            text.Contains(signal.Chinese, StringComparison.OrdinalIgnoreCase)
            || text.Contains(signal.English, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));
    }
}
