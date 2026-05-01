using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace Carves.Runtime.Application.Interaction;

public static class RuntimeTurnPostureFields
{
    public const string TurnClass = "turn_cls";
    public const string IntentLane = "intent_lane";
    public const string PostureId = "posture_id";
    public const string StyleProfileId = "style_profile_id";
    public const string Readiness = "readiness";
    public const string NextAct = "next_act";
    public const string HostRouteHint = "host_route_hint";
    public const string MaxQuestions = "max_questions";
    public const string CandidateId = "candidate_id";
    public const string RevisionHash = "revision_hash";
    public const string SuppressionKey = "suppression_key";
    public const string Blockers = "blockers";
    public const string FocusCodes = "focus_codes";
    public const string AwarenessResponseOrder = "awareness_response_order";
    public const string AwarenessCorrectionMode = "awareness_correction_mode";
    public const string AwarenessEvidenceMode = "awareness_evidence_mode";
    public const string ResponseLanguage = "response_language";
    public const string LanguageResolutionSource = "language_resolution_source";
    public const string SessionLanguage = "session_language";
    public const string LanguageExplicitOverride = "language_explicit_override";
    public const string LanguageDetectedChinese = "language_detected_chinese";
}

public static class RuntimeTurnPostureCodes
{
    public static class TurnClass
    {
        public const string OrdinaryNoOp = "ordinary_no_op";
        public const string GuidedCollection = "guided_collection";
        public const string ReviewReady = "review_ready";
        public const string Parked = "parked";
    }

    public static class IntentLane
    {
        public const string None = "none";
        public const string Discovery = "discovery";
        public const string CandidateReview = "candidate_review";
        public const string IntentDraft = "intent_draft";
        public const string Parked = "parked";
    }

    public static class Posture
    {
        public const string None = "none";
        public const string ProjectManager = "pm";
        public const string Assistant = "assist";
        public const string Architecture = "arch";
        public const string Guard = "guard";
    }

    public static class Readiness
    {
        public const string NoOp = "no_op";
        public const string Collecting = "collecting";
        public const string ReviewCandidate = "review_candidate";
        public const string LandingCandidate = "landing_candidate";
        public const string Parked = "parked";
    }

    public static class NextAct
    {
        public const string Answer = "answer";
        public const string AskOneQuestion = "ask_one_question";
        public const string SummarizeDirection = "summarize_direction";
        public const string PrepareIntentDraft = "prepare_intent_draft";
        public const string Park = "park";
        public const string Wait = "wait";
    }

    public static class HostRouteHint
    {
        public const string None = "none";
        public const string IntentDraft = "intent_draft";
        public const string PlanInit = "plan_init";
        public const string TaskInspect = "task_inspect";
    }
}

public sealed record RuntimeTurnPostureInput(
    string? UserText,
    string? StyleProfileId = null,
    bool CandidateAlreadyParked = false,
    string? CandidateId = null,
    RuntimeTurnStyleProfile? StyleProfile = null,
    string? SourceTurnRef = null,
    RuntimeGuidanceCandidate? ExistingGuidanceCandidate = null,
    string? CandidateRevisionHash = null,
    RuntimeTurnAwarenessProfile? AwarenessProfile = null,
    string? SessionLanguage = null);

public sealed record RuntimeTurnProjectionSeed(
    string TurnClass,
    string IntentLane,
    string Readiness,
    string NextAct,
    string PostureId,
    string StyleProfileId,
    string HostRouteHint,
    int MaxQuestions,
    bool ShouldLoadPostureRegistryProse,
    bool ShouldLoadCustomPersonalityNotes,
    bool PromptSuppressed = false,
    string? CandidateId = null,
    string? RevisionHash = null,
    string? SuppressionKey = null,
    IReadOnlyList<string>? Blockers = null,
    IReadOnlyList<string>? FocusCodes = null)
{
    public IReadOnlyDictionary<string, string> ToCompactProjectionFields()
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.TurnClass] = TurnClass,
            [RuntimeTurnPostureFields.IntentLane] = IntentLane,
            [RuntimeTurnPostureFields.Readiness] = Readiness,
            [RuntimeTurnPostureFields.NextAct] = NextAct,
            [RuntimeTurnPostureFields.PostureId] = PostureId,
            [RuntimeTurnPostureFields.StyleProfileId] = StyleProfileId,
            [RuntimeTurnPostureFields.HostRouteHint] = HostRouteHint,
            [RuntimeTurnPostureFields.MaxQuestions] = MaxQuestions.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        AddOptional(fields, RuntimeTurnPostureFields.CandidateId, CandidateId);
        AddOptional(fields, RuntimeTurnPostureFields.RevisionHash, RevisionHash);
        AddOptional(fields, RuntimeTurnPostureFields.SuppressionKey, SuppressionKey);
        AddOptionalList(fields, RuntimeTurnPostureFields.Blockers, Blockers);
        AddOptionalList(fields, RuntimeTurnPostureFields.FocusCodes, FocusCodes);
        return fields;
    }

    private static void AddOptional(IDictionary<string, string> fields, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[key] = value.Trim();
        }
    }

    private static void AddOptionalList(IDictionary<string, string> fields, string key, IReadOnlyList<string>? values)
    {
        var compact = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Take(3)
            .ToArray();
        if (compact is { Length: > 0 })
        {
            fields[key] = string.Join(',', compact);
        }
    }
}

public static class RuntimeGuidanceCandidateStates
{
    public const string Collecting = "collecting";
    public const string ReviewReady = "review_ready";
    public const string Parked = "parked";
}

public static class RuntimeGuidanceCandidateReadinessBlockCodes
{
    public const string MissingRequiredField = "missing_required_field";
    public const string AmbiguousRequiredField = "ambiguous_required_field";
    public const string ConflictingRequiredEvidence = "conflicting_required_evidence";
    public const string CandidateGroupConflict = "candidate_group_conflict";
}

public sealed record RuntimeGuidanceCandidateReadinessBlock(
    [property: JsonPropertyName("field")]
    string Field,
    [property: JsonPropertyName("reason")]
    string Reason,
    [property: JsonPropertyName("source_kinds")]
    IReadOnlyList<string> SourceKinds,
    [property: JsonPropertyName("candidate_groups")]
    IReadOnlyList<string> CandidateGroups,
    [property: JsonPropertyName("has_negated_evidence")]
    bool HasNegatedEvidence,
    [property: JsonPropertyName("max_confidence")]
    double MaxConfidence);

public sealed record RuntimeGuidanceCandidate
{
    public const string CurrentVersion = "runtime-guidance-candidate.v2";

    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = CurrentVersion;

    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; init; } = RuntimeGuidanceCandidateStates.Collecting;

    [JsonPropertyName("topic")]
    public string Topic { get; init; } = string.Empty;

    [JsonPropertyName("desired_outcome")]
    public string DesiredOutcome { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    [JsonPropertyName("constraints")]
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    [JsonPropertyName("non_goals")]
    public IReadOnlyList<string> NonGoals { get; init; } = Array.Empty<string>();

    [JsonPropertyName("success_signal")]
    public string SuccessSignal { get; init; } = string.Empty;

    [JsonPropertyName("owner")]
    public string Owner { get; init; } = string.Empty;

    [JsonPropertyName("review_gate")]
    public string ReviewGate { get; init; } = string.Empty;

    [JsonPropertyName("risks")]
    public IReadOnlyList<string> Risks { get; init; } = Array.Empty<string>();

    [JsonPropertyName("open_questions")]
    public IReadOnlyList<string> OpenQuestions { get; init; } = Array.Empty<string>();

    [JsonPropertyName("source_turn_refs")]
    public IReadOnlyList<string> SourceTurnRefs { get; init; } = Array.Empty<string>();

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("readiness_score")]
    public int ReadinessScore { get; init; }

    [JsonPropertyName("missing_fields")]
    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    [JsonPropertyName("readiness_blocks")]
    public IReadOnlyList<RuntimeGuidanceCandidateReadinessBlock> ReadinessBlocks { get; init; } = Array.Empty<RuntimeGuidanceCandidateReadinessBlock>();

    [JsonPropertyName("revision_hash")]
    public string RevisionHash { get; init; } = string.Empty;

    [JsonPropertyName("parked_until_change_hash")]
    public string? ParkedUntilChangeHash { get; init; }
}

public sealed record RuntimeGuidanceIntentDraftCandidate
{
    public const string CurrentVersion = "runtime-guidance-intent-draft-candidate.v1";

    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = CurrentVersion;

    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; init; } = string.Empty;

    [JsonPropertyName("source_candidate_revision_hash")]
    public string SourceCandidateRevisionHash { get; init; } = string.Empty;

    [JsonPropertyName("route_hint")]
    public string RouteHint { get; init; } = RuntimeTurnPostureCodes.HostRouteHint.IntentDraft;

    [JsonPropertyName("route_state")]
    public string RouteState { get; init; } = "candidate_only";

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("goal")]
    public string Goal { get; init; } = string.Empty;

    [JsonPropertyName("scope")]
    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    [JsonPropertyName("acceptance")]
    public IReadOnlyList<string> Acceptance { get; init; } = Array.Empty<string>();

    [JsonPropertyName("constraints")]
    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    [JsonPropertyName("non_goals")]
    public IReadOnlyList<string> NonGoals { get; init; } = Array.Empty<string>();

    [JsonPropertyName("risks")]
    public IReadOnlyList<string> Risks { get; init; } = Array.Empty<string>();

    [JsonPropertyName("source_turn_refs")]
    public IReadOnlyList<string> SourceTurnRefs { get; init; } = Array.Empty<string>();
}

public sealed record RuntimeGuidanceCandidateInput(
    string? UserText,
    RuntimeGuidanceCandidate? ExistingCandidate = null,
    string? CandidateId = null,
    string? SourceTurnRef = null,
    IReadOnlyList<string>? FieldPriority = null,
    IReadOnlyList<string>? ReviewGateFields = null,
    RuntimeTurnAwarenessAdmissionHints? AdmissionHints = null);

public sealed record RuntimeGuidanceCandidateBuildResult(
    bool ShouldProjectCandidate,
    RuntimeGuidanceCandidate? Candidate)
{
    public RuntimeGuidanceFieldCollectionDiagnostics? FieldDiagnostics { get; init; }
}

public sealed record RuntimeGuidanceFieldOperationDiagnostic
{
    public RuntimeGuidanceFieldOperationDiagnostic(
        string field,
        string operation,
        bool Ambiguous,
        int ValueCount,
        int EvidenceCount = 0,
        IReadOnlyList<string>? SourceKinds = null,
        IReadOnlyList<string>? CandidateGroups = null,
        bool HasNegatedEvidence = false,
        double MaxConfidence = 0d,
        IReadOnlyList<string>? IssueCodes = null)
    {
        Field = field;
        Operation = operation;
        this.Ambiguous = Ambiguous;
        this.ValueCount = ValueCount;
        this.EvidenceCount = EvidenceCount;
        this.SourceKinds = SourceKinds ?? Array.Empty<string>();
        this.CandidateGroups = CandidateGroups ?? Array.Empty<string>();
        this.HasNegatedEvidence = HasNegatedEvidence;
        this.MaxConfidence = MaxConfidence;
        this.IssueCodes = IssueCodes ?? Array.Empty<string>();
    }

    [JsonPropertyName("field")]
    public string Field { get; init; }

    [JsonPropertyName("op")]
    public string Operation { get; init; }

    [JsonPropertyName("ambiguous")]
    public bool Ambiguous { get; init; }

    [JsonPropertyName("value_count")]
    public int ValueCount { get; init; }

    [JsonPropertyName("evidence_count")]
    public int EvidenceCount { get; init; }

    [JsonPropertyName("source_kinds")]
    public IReadOnlyList<string> SourceKinds { get; init; }

    [JsonPropertyName("candidate_groups")]
    public IReadOnlyList<string> CandidateGroups { get; init; }

    [JsonPropertyName("has_negated_evidence")]
    public bool HasNegatedEvidence { get; init; }

    [JsonPropertyName("max_confidence")]
    public double MaxConfidence { get; init; }

    [JsonPropertyName("issue_codes")]
    public IReadOnlyList<string> IssueCodes { get; init; }
}

public sealed record RuntimeGuidanceFieldCollectionDiagnostics
{
    public const string CurrentVersion = "runtime-guidance-field-collection-diagnostics.v1";
    private const int MaxDiagnosticFields = 9;
    private const int MaxDiagnosticCodes = 8;
    private const int MaxDiagnosticValueCount = 3;
    private const int MaxDiagnosticEvidenceCount = 3;
    private static readonly HashSet<string> AllowedFieldNames = new(
        [
            "topic",
            "desired_outcome",
            "scope",
            "constraints",
            "non_goals",
            "success_signal",
            "owner",
            "review_gate",
            "risks",
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedAmbiguityNames = new(
        [
            "topic",
            "desired_outcome",
            "scope",
            "constraint",
            "constraints",
            "non_goal",
            "non_goals",
            "success_signal",
            "owner",
            "review_gate",
            "risk",
            "risks",
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedOperationNames = new(
        [
            RuntimeGuidanceFieldOperationNames.Preserve,
            RuntimeGuidanceFieldOperationNames.Set,
            RuntimeGuidanceFieldOperationNames.Append,
            RuntimeGuidanceFieldOperationNames.Clear,
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedBlockedReasonCodes = new(
        [
            RuntimeGuidanceAdmissionReasonCodes.EmptyInput,
            RuntimeGuidanceAdmissionReasonCodes.NoAdmissionSignal,
            RuntimeGuidanceAdmissionReasonCodes.DiscussionOnly,
            RuntimeGuidanceAdmissionReasonCodes.NegatedHandoff,
            RuntimeGuidanceAdmissionReasonCodes.ParkRequest,
            RuntimeGuidanceAdmissionReasonCodes.ClearRequest,
            RuntimeGuidanceAdmissionReasonCodes.ForgetRequest,
            RuntimeGuidanceAdmissionReasonCodes.GovernedHandoffRequest,
            RuntimeGuidanceAdmissionReasonCodes.ExistingCandidatePresent,
            RuntimeGuidanceAdmissionReasonCodes.FieldAssignmentSignal,
            RuntimeGuidanceAdmissionReasonCodes.CandidateStartMarker,
            RuntimeGuidanceAdmissionReasonCodes.CandidateUpdateMarker,
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedAdmissionKinds = new(
        [
            RuntimeGuidanceAdmissionKinds.ChatOnly,
            RuntimeGuidanceAdmissionKinds.CandidateStart,
            RuntimeGuidanceAdmissionKinds.CandidateUpdate,
            RuntimeGuidanceAdmissionKinds.CandidatePark,
            RuntimeGuidanceAdmissionKinds.CandidateClear,
            RuntimeGuidanceAdmissionKinds.CandidateForget,
            RuntimeGuidanceAdmissionKinds.GovernedHandoff,
        ],
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedEvidenceKinds = new(
        RuntimeGuidanceFieldEvidenceKinds.All,
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedCandidateGroups = new(
        RuntimeGuidanceFieldEvidenceCandidateGroups.All,
        StringComparer.Ordinal);
    private static readonly HashSet<string> AllowedEvidenceIssueCodes = new(
        RuntimeGuidanceFieldEvidenceIssueCodes.All,
        StringComparer.Ordinal);

    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = CurrentVersion;

    [JsonPropertyName("compiler_contract_version")]
    public string CompilerContractVersion { get; init; } = RuntimeGuidanceFieldOperationCompilerContract.CurrentVersion;

    [JsonPropertyName("extraction_contract_version")]
    public string ExtractionContractVersion { get; init; } = RuntimeGuidanceFieldExtractionContract.CurrentVersion;

    [JsonPropertyName("admission_kind")]
    public string AdmissionKind { get; init; } = RuntimeGuidanceAdmissionKinds.ChatOnly;

    [JsonPropertyName("resets_candidate")]
    public bool ResetsCandidate { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyList<RuntimeGuidanceFieldOperationDiagnostic> Fields { get; init; } = Array.Empty<RuntimeGuidanceFieldOperationDiagnostic>();

    [JsonPropertyName("ambiguities")]
    public IReadOnlyList<string> Ambiguities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("blocked_reasons")]
    public IReadOnlyList<string> BlockedReasons { get; init; } = Array.Empty<string>();

    public static RuntimeGuidanceFieldCollectionDiagnostics FromCompilerResult(RuntimeGuidanceFieldOperationCompilerResult result)
    {
        return new RuntimeGuidanceFieldCollectionDiagnostics
        {
            CompilerContractVersion = result.ContractVersion,
            ExtractionContractVersion = result.ExtractionContractVersion,
            AdmissionKind = result.AdmissionKind,
            ResetsCandidate = result.ResetsCandidate,
            Fields = result.FieldEvidence
                .Select(field => new RuntimeGuidanceFieldOperationDiagnostic(
                    field.Field,
                    field.Operation,
                    field.Ambiguous,
                    field.Values.Count,
                    field.Sources.Count,
                    field.Sources.Select(source => source.Kind).ToArray(),
                    field.Sources.Select(source => source.CandidateGroup).ToArray(),
                    field.Sources.Any(source => source.Negated),
                    field.Sources.Count == 0 ? 0d : field.Sources.Max(source => source.Confidence),
                    field.IssueCodes))
                .ToArray(),
            Ambiguities = result.Ambiguities,
            BlockedReasons = result.BlockedReasons,
        }.ToSurfaceSafe();
    }

    public RuntimeGuidanceFieldCollectionDiagnostics ToSurfaceSafe()
    {
        return this with
        {
            SchemaVersion = CurrentVersion,
            CompilerContractVersion = RuntimeGuidanceFieldOperationCompilerContract.CurrentVersion,
            ExtractionContractVersion = RuntimeGuidanceFieldExtractionContract.CurrentVersion,
            AdmissionKind = AllowedAdmissionKinds.Contains(AdmissionKind)
                ? AdmissionKind
                : RuntimeGuidanceAdmissionKinds.ChatOnly,
            Fields = Fields
                .Select(NormalizeFieldDiagnostic)
                .Where(field => field is not null)
                .Select(field => field!)
                .GroupBy(field => field.Field, StringComparer.Ordinal)
                .Select(group => group.First())
                .Take(MaxDiagnosticFields)
                .ToArray(),
            Ambiguities = NormalizeAllowedCodes(Ambiguities, AllowedAmbiguityNames, MaxDiagnosticCodes),
            BlockedReasons = NormalizeAllowedCodes(BlockedReasons, AllowedBlockedReasonCodes, MaxDiagnosticCodes),
        };
    }

    private static RuntimeGuidanceFieldOperationDiagnostic? NormalizeFieldDiagnostic(RuntimeGuidanceFieldOperationDiagnostic field)
    {
        var fieldName = NormalizeAllowedCode(field.Field, AllowedFieldNames);
        var operation = NormalizeAllowedCode(field.Operation, AllowedOperationNames);
        if (fieldName is null || operation is null)
        {
            return null;
        }

        return new RuntimeGuidanceFieldOperationDiagnostic(
            fieldName,
            operation,
            field.Ambiguous,
            Math.Clamp(field.ValueCount, 0, MaxDiagnosticValueCount),
            Math.Clamp(field.EvidenceCount, 0, MaxDiagnosticEvidenceCount),
            NormalizeAllowedCodes(field.SourceKinds, AllowedEvidenceKinds, MaxDiagnosticCodes),
            NormalizeAllowedCodes(field.CandidateGroups, AllowedCandidateGroups, MaxDiagnosticCodes),
            field.HasNegatedEvidence,
            Math.Clamp(Math.Round(field.MaxConfidence, 2), 0d, 1d),
            NormalizeAllowedCodes(field.IssueCodes, AllowedEvidenceIssueCodes, MaxDiagnosticCodes));
    }

    private static IReadOnlyList<string> NormalizeAllowedCodes(
        IReadOnlyList<string> values,
        IReadOnlySet<string> allowedValues,
        int maxCount)
    {
        return values
            .Select(value => NormalizeAllowedCode(value, allowedValues))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Take(maxCount)
            .ToArray();
    }

    private static string? NormalizeAllowedCode(string? value, IReadOnlySet<string> allowedValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return allowedValues.Contains(normalized) ? normalized : null;
    }
}






public sealed class RuntimeTurnPostureClassifier
{
    private const int HardMaxQuestionsPerTurn = 1;
    private const string DefaultStyleProfileId = "runtime-default";

    public RuntimeTurnProjectionSeed Classify(
        RuntimeTurnPostureInput input,
        RuntimeTurnAwarenessAdmissionHints? admissionHints = null)
    {
        var text = Normalize(input.UserText);
        var admission = RuntimeGuidanceAdmission.Decide(text, input.ExistingGuidanceCandidate, admissionHints);
        var styleProfile = input.StyleProfile;
        var styleProfileId = NormalizeOptionalToken(styleProfile?.StyleProfileId)
            ?? NormalizeOptionalToken(input.StyleProfileId)
            ?? DefaultStyleProfileId;
        var styleRevisionHash = styleProfile is null ? null : NormalizeOptionalToken(styleProfile.RevisionHash);
        var candidateRevisionHash = NormalizeOptionalToken(input.CandidateRevisionHash);
        var focusCodes = ResolveFocusCodes(styleProfile);
        var guidedPostureId = ResolveGuidedPostureId(styleProfile?.DefaultPostureId);
        var shouldLoadCustomPersonalityNotes = !string.IsNullOrWhiteSpace(styleProfile?.CustomPersonalityNote);
        var candidateId = NormalizeOptionalToken(input.CandidateId);

        if (input.CandidateAlreadyParked || admission.RequestsPark)
        {
            return new RuntimeTurnProjectionSeed(
                RuntimeTurnPostureCodes.TurnClass.Parked,
                RuntimeTurnPostureCodes.IntentLane.Parked,
                RuntimeTurnPostureCodes.Readiness.Parked,
                RuntimeTurnPostureCodes.NextAct.Wait,
                guidedPostureId,
                styleProfileId,
                RuntimeTurnPostureCodes.HostRouteHint.None,
                0,
                ShouldLoadPostureRegistryProse: false,
                ShouldLoadCustomPersonalityNotes: shouldLoadCustomPersonalityNotes && !input.CandidateAlreadyParked,
                PromptSuppressed: input.CandidateAlreadyParked,
                CandidateId: candidateId,
                RevisionHash: styleRevisionHash,
                SuppressionKey: BuildParkedSuppressionKey(candidateId, candidateRevisionHash),
                FocusCodes: focusCodes);
        }

        if (admission.EntersGuidedCollection)
        {
            return new RuntimeTurnProjectionSeed(
                RuntimeTurnPostureCodes.TurnClass.GuidedCollection,
                RuntimeTurnPostureCodes.IntentLane.Discovery,
                RuntimeTurnPostureCodes.Readiness.Collecting,
                RuntimeTurnPostureCodes.NextAct.SummarizeDirection,
                guidedPostureId,
                styleProfileId,
                RuntimeTurnPostureCodes.HostRouteHint.IntentDraft,
                ResolveGuidedMaxQuestions(styleProfile),
                ShouldLoadPostureRegistryProse: false,
                ShouldLoadCustomPersonalityNotes: shouldLoadCustomPersonalityNotes,
                RevisionHash: styleRevisionHash,
                FocusCodes: focusCodes);
        }

        return new RuntimeTurnProjectionSeed(
            RuntimeTurnPostureCodes.TurnClass.OrdinaryNoOp,
            RuntimeTurnPostureCodes.IntentLane.None,
            RuntimeTurnPostureCodes.Readiness.NoOp,
            RuntimeTurnPostureCodes.NextAct.Answer,
            RuntimeTurnPostureCodes.Posture.None,
            styleProfileId,
            RuntimeTurnPostureCodes.HostRouteHint.None,
            0,
            ShouldLoadPostureRegistryProse: false,
            ShouldLoadCustomPersonalityNotes: false,
            RevisionHash: styleRevisionHash);
    }

    private static string Normalize(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().ToLowerInvariant();
    }

    private static int ResolveGuidedMaxQuestions(RuntimeTurnStyleProfile? styleProfile)
    {
        if (styleProfile is null)
        {
            return HardMaxQuestionsPerTurn;
        }

        if (string.Equals(NormalizeOptionalToken(styleProfile.QuestionStyle), "off", StringComparison.Ordinal))
        {
            return 0;
        }

        return Math.Clamp(styleProfile.MaxQuestions, 0, HardMaxQuestionsPerTurn);
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static string? NormalizeOptionalToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? BuildParkedSuppressionKey(string? candidateId, string? candidateRevisionHash)
    {
        return string.IsNullOrWhiteSpace(candidateId) || string.IsNullOrWhiteSpace(candidateRevisionHash)
            ? null
            : $"runtime_turn_posture:parked:{candidateId}:{candidateRevisionHash}";
    }

    private static IReadOnlyList<string>? ResolveFocusCodes(RuntimeTurnStyleProfile? styleProfile)
    {
        var codes = styleProfile?.PreferredFocusCodes?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Take(3)
            .ToArray();
        return codes is { Length: > 0 } ? codes : null;
    }

    private static string ResolveGuidedPostureId(string? defaultPostureId)
    {
        return NormalizeOptionalToken(defaultPostureId) switch
        {
            RuntimeTurnPostureCodes.Posture.Assistant => RuntimeTurnPostureCodes.Posture.Assistant,
            RuntimeTurnPostureCodes.Posture.Architecture => RuntimeTurnPostureCodes.Posture.Architecture,
            RuntimeTurnPostureCodes.Posture.Guard => RuntimeTurnPostureCodes.Posture.Guard,
            RuntimeTurnPostureCodes.Posture.ProjectManager => RuntimeTurnPostureCodes.Posture.ProjectManager,
            _ => RuntimeTurnPostureCodes.Posture.ProjectManager,
        };
    }
}

public sealed record RuntimeTurnPostureGuidanceResult(
    RuntimeTurnProjectionSeed Projection,
    IReadOnlyDictionary<string, string> TurnProjectionFields,
    bool ShouldGuide,
    string? DirectionSummary,
    string? Question,
    string? RecommendedNextAction,
    string? SynthesizedResponse,
    bool PromptSuppressed,
    string? SuppressionKey,
    bool ShouldLoadPostureRegistryProse,
    bool ShouldLoadCustomPersonalityNotes)
{
    public RuntimeGuidanceCandidate? GuidanceCandidate { get; init; }

    public RuntimeGuidanceIntentDraftCandidate? IntentDraftCandidate { get; init; }

    public RuntimeGuidanceFieldCollectionDiagnostics? FieldDiagnostics { get; init; }

    public RuntimeGuidanceAdmissionClassifierResult? AdmissionSignals { get; init; }

    public RuntimeTurnAwarenessPolicy? AwarenessPolicy { get; init; }

    public string AwarenessPolicyStatus { get; init; } = RuntimeTurnAwarenessPolicyStatuses.NotCompiled;

    public IReadOnlyList<string> AwarenessPolicyIssues { get; init; } = [];

    public RuntimeTurnLanguageResolution? LanguageResolution { get; init; }
}

public sealed class RuntimeTurnPostureGuidance
{
    private readonly RuntimeTurnPostureClassifier classifier;
    private readonly RuntimeGuidanceCandidateBuilder candidateBuilder;
    private readonly RuntimeTurnAwarenessPolicyCompiler awarenessPolicyCompiler;

    public RuntimeTurnPostureGuidance(
        RuntimeTurnPostureClassifier? classifier = null,
        RuntimeGuidanceCandidateBuilder? candidateBuilder = null,
        RuntimeTurnAwarenessPolicyCompiler? awarenessPolicyCompiler = null)
    {
        this.classifier = classifier ?? new RuntimeTurnPostureClassifier();
        this.candidateBuilder = candidateBuilder ?? new RuntimeGuidanceCandidateBuilder();
        this.awarenessPolicyCompiler = awarenessPolicyCompiler ?? new RuntimeTurnAwarenessPolicyCompiler();
    }

    public RuntimeTurnPostureGuidanceResult Build(RuntimeTurnPostureInput input)
    {
        var effectiveStyleProfile = input.AwarenessProfile?.ToStyleProfile() ?? input.StyleProfile;
        var effectiveInput = input with
        {
            StyleProfile = effectiveStyleProfile,
            StyleProfileId = effectiveStyleProfile?.StyleProfileId ?? input.StyleProfileId,
        };
        var awarenessProfile = input.AwarenessProfile ?? RuntimeTurnAwarenessProfile.FromStyleProfile(effectiveStyleProfile);
        if (input.AwarenessProfile is null)
        {
            awarenessProfile = awarenessProfile with
            {
                LanguageMode = RuntimeTurnAwarenessProfileLanguageModes.Auto,
                ExpressionPolicy = new RuntimeTurnAwarenessExpressionPolicy(),
            };
        }

        var awarenessPolicyCompile = awarenessPolicyCompiler.Compile(awarenessProfile);
        var awarenessPolicy = awarenessPolicyCompile.Policy;
        var languageResolution = ResolveResponseLanguage(effectiveInput.UserText, awarenessPolicy, effectiveInput.SessionLanguage);
        var responseLanguage = languageResolution.ResponseLanguage;
        var admissionSignals = RuntimeGuidanceAdmission.ClassifySignals(
            effectiveInput.UserText,
            effectiveInput.ExistingGuidanceCandidate,
            awarenessPolicy.AdmissionHints);
        var projection = classifier.Classify(effectiveInput, awarenessPolicy.AdmissionHints);
        var fields = AddLanguageProjectionFields(
            AddAwarenessPolicyProjectionFields(projection.ToCompactProjectionFields(), awarenessPolicy),
            languageResolution);
        var admission = RuntimeGuidanceAdmission.Decide(
            effectiveInput.UserText,
            effectiveInput.ExistingGuidanceCandidate,
            awarenessPolicy.AdmissionHints);
        var intentDraftHandoffRequested = admission.RequestsGovernedHandoff;
        var candidateBuild = string.Equals(projection.TurnClass, RuntimeTurnPostureCodes.TurnClass.GuidedCollection, StringComparison.Ordinal)
            ? candidateBuilder.Build(new RuntimeGuidanceCandidateInput(
                effectiveInput.UserText,
                ExistingCandidate: effectiveInput.ExistingGuidanceCandidate,
                CandidateId: effectiveInput.CandidateId,
                SourceTurnRef: effectiveInput.SourceTurnRef,
                FieldPriority: awarenessPolicy.FieldPriority,
                ReviewGateFields: ResolveAwarenessReviewGateFields(awarenessPolicy),
                AdmissionHints: awarenessPolicy.AdmissionHints))
            : null;
        var candidate = candidateBuild?.Candidate;
        var fieldDiagnostics = candidateBuild?.FieldDiagnostics;
        var expressionStyle = RuntimeTurnExpressionStyle.From(effectiveStyleProfile);

        var result = projection.TurnClass switch
        {
            RuntimeTurnPostureCodes.TurnClass.GuidedCollection => BuildGuided(
                projection,
                fields,
                candidate,
                fieldDiagnostics,
                expressionStyle,
                awarenessPolicy,
                responseLanguage,
                intentDraftHandoffRequested),
            RuntimeTurnPostureCodes.TurnClass.Parked => BuildParked(
                projection,
                fields,
                expressionStyle,
                awarenessPolicy,
                responseLanguage),
            _ => BuildOrdinary(projection, fields, awarenessPolicy),
        };
        return result with
        {
            AwarenessPolicyStatus = awarenessPolicyCompile.Status,
            AwarenessPolicyIssues = awarenessPolicyCompile.Issues,
            AdmissionSignals = admissionSignals,
            LanguageResolution = languageResolution,
        };
    }

    internal static IReadOnlyDictionary<string, string> AddAwarenessPolicyProjectionFields(
        IReadOnlyDictionary<string, string> fields,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var enriched = new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.AwarenessResponseOrder] = awarenessPolicy.ResponseOrder,
            [RuntimeTurnPostureFields.AwarenessCorrectionMode] = awarenessPolicy.CorrectionMode,
            [RuntimeTurnPostureFields.AwarenessEvidenceMode] = awarenessPolicy.EvidenceMode,
        };
        return enriched;
    }

    internal static IReadOnlyDictionary<string, string> AddLanguageProjectionFields(
        IReadOnlyDictionary<string, string> fields,
        RuntimeTurnLanguageResolution languageResolution)
    {
        var enriched = new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.ResponseLanguage] = languageResolution.ResponseLanguage,
            [RuntimeTurnPostureFields.LanguageResolutionSource] = languageResolution.Source,
            [RuntimeTurnPostureFields.LanguageExplicitOverride] = ToProjectionBoolean(languageResolution.ExplicitUserOverride),
            [RuntimeTurnPostureFields.LanguageDetectedChinese] = ToProjectionBoolean(languageResolution.DetectedChinese),
        };
        if (!string.IsNullOrWhiteSpace(languageResolution.SessionLanguage))
        {
            enriched[RuntimeTurnPostureFields.SessionLanguage] = languageResolution.SessionLanguage;
        }

        return enriched;
    }

    private static string ToProjectionBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    private static RuntimeTurnLanguageResolution ResolveResponseLanguage(
        string? userText,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string? sessionLanguage)
    {
        var languageResolver = new RuntimeTurnLanguageResolver();
        return languageResolver.Resolve(new RuntimeTurnLanguageResolutionInput(
            userText,
            awarenessPolicy.LanguageMode,
            sessionLanguage));
    }

    private static bool IsChineseResponse(string responseLanguage)
    {
        return string.Equals(responseLanguage, RuntimeTurnAwarenessProfileLanguageModes.Chinese, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ResolveAwarenessReviewGateFields(RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var gateFields = new List<string>(3);
        if (awarenessPolicy.RiskFirst
            || awarenessPolicy.WeakAssumptionCheck
            || awarenessPolicy.ExpressionSignals.Contains(RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck, StringComparer.Ordinal))
        {
            gateFields.Add("risk");
        }

        if (awarenessPolicy.BoundaryFirst
            || awarenessPolicy.ExpressionSignals.Contains(RuntimeTurnAwarenessProfileExpressionSignals.BoundaryVisible, StringComparer.Ordinal))
        {
            gateFields.Add("constraint");
            gateFields.Add("non_goal");
        }

        if (awarenessPolicy.StrategyCodes.Contains(RuntimeTurnAwarenessStrategyCodes.ReviewGateVisible, StringComparer.Ordinal)
            || string.Equals(awarenessPolicy.RoleId, RuntimeTurnPostureCodes.Posture.ProjectManager, StringComparison.Ordinal))
        {
            gateFields.Add("owner");
            gateFields.Add("review_gate");
        }

        return awarenessPolicy.FieldPriority
            .Where(gateFields.Contains)
            .Concat(gateFields)
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    private static RuntimeTurnPostureGuidanceResult BuildGuided(
        RuntimeTurnProjectionSeed projection,
        IReadOnlyDictionary<string, string> fields,
        RuntimeGuidanceCandidate? guidanceCandidate,
        RuntimeGuidanceFieldCollectionDiagnostics? fieldDiagnostics,
        RuntimeTurnExpressionStyle expressionStyle,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage,
        bool intentDraftHandoffRequested)
    {
        if (guidanceCandidate is not null
            && string.Equals(guidanceCandidate.State, RuntimeGuidanceCandidateStates.ReviewReady, StringComparison.Ordinal))
        {
            var intentDraftHandoffAllowed = ShouldProjectIntentDraftHandoff(intentDraftHandoffRequested, awarenessPolicy);
            if (intentDraftHandoffAllowed)
            {
                var landingFields = BuildIntentDraftProjectionFields(fields, guidanceCandidate);
                var handoff = BuildIntentDraftCandidate(guidanceCandidate);
                var landingSummary = expressionStyle.ApplySummary(BuildLandingSummary(responseLanguage, awarenessPolicy), responseLanguage);
                var landingAwareness = expressionStyle.ApplyAwareness(BuildLandingAwareness(responseLanguage), responseLanguage);
                var landingNextAction = expressionStyle.ApplyNextAction(BuildLandingNextAction(responseLanguage, awarenessPolicy), responseLanguage);
                return new RuntimeTurnPostureGuidanceResult(
                    projection,
                    landingFields,
                    ShouldGuide: true,
                    DirectionSummary: landingSummary,
                    Question: null,
                    RecommendedNextAction: landingNextAction,
                    SynthesizedResponse: ComposeGuidedResponse(landingSummary, landingAwareness, null, landingNextAction, awarenessPolicy),
                    PromptSuppressed: projection.PromptSuppressed,
                    SuppressionKey: null,
                    projection.ShouldLoadPostureRegistryProse,
                    projection.ShouldLoadCustomPersonalityNotes)
                {
                    GuidanceCandidate = guidanceCandidate,
                    IntentDraftCandidate = handoff,
                    FieldDiagnostics = fieldDiagnostics,
                    AwarenessPolicy = awarenessPolicy,
                };
            }

            var reviewFields = BuildReviewReadyProjectionFields(
                fields,
                guidanceCandidate,
                intentDraftHandoffRequested && !intentDraftHandoffAllowed);
            var summary = expressionStyle.ApplySummary(BuildReviewReadySummary(guidanceCandidate, awarenessPolicy, responseLanguage), responseLanguage);
            var awareness = BuildCandidateAwarenessLine(expressionStyle, guidanceCandidate, fieldDiagnostics, awarenessPolicy, responseLanguage);
            var question = ShouldEmitQuestion(awarenessPolicy)
                ? expressionStyle.ApplyQuestion(BuildReviewReadyQuestion(responseLanguage, awarenessPolicy), responseLanguage)
                : null;
            var reviewNextAction = expressionStyle.ApplyNextAction(BuildReviewReadyNextAction(responseLanguage, awarenessPolicy), responseLanguage);
            return new RuntimeTurnPostureGuidanceResult(
                projection,
                reviewFields,
                ShouldGuide: true,
                DirectionSummary: summary,
                Question: question,
                RecommendedNextAction: reviewNextAction,
                SynthesizedResponse: ComposeGuidedResponse(summary, awareness, question, reviewNextAction, awarenessPolicy),
                PromptSuppressed: projection.PromptSuppressed,
                SuppressionKey: null,
                projection.ShouldLoadPostureRegistryProse,
                projection.ShouldLoadCustomPersonalityNotes)
            {
                GuidanceCandidate = guidanceCandidate,
                FieldDiagnostics = fieldDiagnostics,
                AwarenessPolicy = awarenessPolicy,
            };
        }

        var collectingFields = BuildCollectingProjectionFields(fields, guidanceCandidate);
        var collectingSummary = expressionStyle.ApplySummary(BuildCollectingSummary(guidanceCandidate, awarenessPolicy, responseLanguage), responseLanguage);
        var collectingAwareness = BuildCandidateAwarenessLine(expressionStyle, guidanceCandidate, fieldDiagnostics, awarenessPolicy, responseLanguage);
        var collectingQuestion = ShouldEmitQuestion(awarenessPolicy)
            ? expressionStyle.ApplyQuestion(BuildCollectingQuestion(guidanceCandidate, awarenessPolicy, responseLanguage), responseLanguage)
            : null;
        var collectingNextAction = expressionStyle.ApplyNextAction(BuildCollectingNextAction(responseLanguage, awarenessPolicy), responseLanguage);
        return new RuntimeTurnPostureGuidanceResult(
            projection,
            collectingFields,
            ShouldGuide: true,
            DirectionSummary: collectingSummary,
            Question: collectingQuestion,
            RecommendedNextAction: collectingNextAction,
            SynthesizedResponse: ComposeGuidedResponse(collectingSummary, collectingAwareness, collectingQuestion, collectingNextAction, awarenessPolicy),
            PromptSuppressed: projection.PromptSuppressed,
            SuppressionKey: null,
            projection.ShouldLoadPostureRegistryProse,
            projection.ShouldLoadCustomPersonalityNotes)
        {
            GuidanceCandidate = guidanceCandidate,
            FieldDiagnostics = fieldDiagnostics,
            AwarenessPolicy = awarenessPolicy,
        };
    }

    private static string ComposeGuidedResponse(
        string? summary,
        string? awareness,
        string? question,
        string? nextAction,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        return awarenessPolicy.ResponseOrder switch
        {
            RuntimeTurnAwarenessProfileResponseOrders.QuestionFirst when !string.IsNullOrWhiteSpace(question) =>
                JoinResponse(question, summary, awareness, nextAction),
            RuntimeTurnAwarenessProfileResponseOrders.BlockerFirst when !string.IsNullOrWhiteSpace(awareness) =>
                JoinResponse(awareness, summary, question, nextAction),
            _ =>
                JoinResponse(summary, awareness, question, nextAction),
        };
    }

    private static bool ShouldProjectIntentDraftHandoff(
        bool intentDraftHandoffRequested,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        return intentDraftHandoffRequested
            && awarenessPolicy.RequiresOperatorConfirmation
            && string.Equals(
                awarenessPolicy.HandoffMode,
                RuntimeTurnAwarenessProfileHandoffModes.ConfirmBeforeHandoff,
                StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> BuildCollectingProjectionFields(
        IReadOnlyDictionary<string, string> fields,
        RuntimeGuidanceCandidate? guidanceCandidate)
    {
        if (guidanceCandidate is null)
        {
            return fields;
        }

        var collectingFields = new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.CandidateId] = guidanceCandidate.CandidateId,
        };
        var blockers = BuildReadinessBlockProjection(guidanceCandidate.ReadinessBlocks);
        if (!string.IsNullOrWhiteSpace(blockers))
        {
            collectingFields[RuntimeTurnPostureFields.Blockers] = blockers;
        }

        return collectingFields;
    }

    private static IReadOnlyDictionary<string, string> BuildIntentDraftProjectionFields(
        IReadOnlyDictionary<string, string> fields,
        RuntimeGuidanceCandidate guidanceCandidate)
    {
        var landingFields = new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.IntentLane] = RuntimeTurnPostureCodes.IntentLane.IntentDraft,
            [RuntimeTurnPostureFields.Readiness] = RuntimeTurnPostureCodes.Readiness.LandingCandidate,
            [RuntimeTurnPostureFields.NextAct] = RuntimeTurnPostureCodes.NextAct.PrepareIntentDraft,
            [RuntimeTurnPostureFields.HostRouteHint] = RuntimeTurnPostureCodes.HostRouteHint.IntentDraft,
            [RuntimeTurnPostureFields.MaxQuestions] = "0",
            [RuntimeTurnPostureFields.CandidateId] = guidanceCandidate.CandidateId,
        };
        return landingFields;
    }

    private static RuntimeGuidanceIntentDraftCandidate BuildIntentDraftCandidate(RuntimeGuidanceCandidate candidate)
    {
        return new RuntimeGuidanceIntentDraftCandidate
        {
            CandidateId = candidate.CandidateId,
            SourceCandidateRevisionHash = candidate.RevisionHash,
            Title = candidate.Topic,
            Goal = candidate.DesiredOutcome,
            Scope = ToOptionalList(candidate.Scope),
            Acceptance = ToOptionalList(candidate.SuccessSignal),
            Constraints = candidate.Constraints,
            NonGoals = candidate.NonGoals,
            Risks = candidate.Risks,
            SourceTurnRefs = candidate.SourceTurnRefs,
        };
    }

    private static IReadOnlyList<string> ToOptionalList(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : [value.Trim()];
    }

    private static IReadOnlyDictionary<string, string> BuildReviewReadyProjectionFields(
        IReadOnlyDictionary<string, string> fields,
        RuntimeGuidanceCandidate guidanceCandidate,
        bool suppressIntentDraftRoute = false)
    {
        var reviewFields = new Dictionary<string, string>(fields, StringComparer.Ordinal)
        {
            [RuntimeTurnPostureFields.IntentLane] = RuntimeTurnPostureCodes.IntentLane.CandidateReview,
            [RuntimeTurnPostureFields.Readiness] = RuntimeTurnPostureCodes.Readiness.ReviewCandidate,
            [RuntimeTurnPostureFields.CandidateId] = guidanceCandidate.CandidateId,
        };
        if (suppressIntentDraftRoute)
        {
            reviewFields[RuntimeTurnPostureFields.NextAct] = RuntimeTurnPostureCodes.NextAct.SummarizeDirection;
            reviewFields[RuntimeTurnPostureFields.HostRouteHint] = RuntimeTurnPostureCodes.HostRouteHint.None;
        }

        return reviewFields;
    }

    private static string BuildLandingSummary(
        string responseLanguage,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var summary = IsChineseResponse(responseLanguage)
            ? "治理意图草稿交接候选已经可以复核；本轮没有创建卡片、任务图、批准、执行、worker 调度、Host 生命周期动作、memory 写入或 truth 写入。"
            : "Governed intent draft handoff candidate is ready; no card, taskgraph, approval, execution, worker dispatch, Host lifecycle action, memory write, or truth write was created.";
        return JoinResponse(summary, BuildCustomSummaryCueSentence(awarenessPolicy));
    }

    private static string BuildLandingAwareness(string responseLanguage)
    {
        return IsChineseResponse(responseLanguage)
            ? "我只把它投影为候选交接；卡片创建、批准、执行和 truth 写入都留在本轮之外。"
            : "I am projecting this as a candidate-only handoff; card creation, approval, execution, and truth write stay outside this turn.";
    }

    private static string BuildLandingNextAction(
        string responseLanguage,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var nextAction = IsChineseResponse(responseLanguage)
            ? "只有在操作者确认这个候选交接后，才进入受治理的规划接入口。"
            : "Use governed planning intake only if the operator confirms this candidate-only handoff.";
        return JoinResponse(nextAction, BuildPersonalityNextActionCue(awarenessPolicy, responseLanguage));
    }

    private static string BuildReviewReadySummary(
        RuntimeGuidanceCandidate candidate,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            var summary = JoinSummaryParts(
                "；",
                [
                    "候选方向已可复核",
                    $"主题={candidate.Topic}",
                    $"目标={candidate.DesiredOutcome}",
                    $"范围={candidate.Scope}",
                    $"成功信号={candidate.SuccessSignal}",
                    OptionalSummaryPart("负责人", candidate.Owner),
                    OptionalSummaryPart("复核点", candidate.ReviewGate),
                ]) + "。这不是执行批准。";
            return JoinResponse(summary, BuildCustomSummaryCueSentence(awarenessPolicy));
        }

        var englishSummary = JoinSummaryParts(
            "; ",
            [
                "Candidate ready for review",
                $"topic={candidate.Topic}",
                $"desired_outcome={candidate.DesiredOutcome}",
                $"scope={candidate.Scope}",
                $"success_signal={candidate.SuccessSignal}",
                OptionalSummaryPart("owner", candidate.Owner),
                OptionalSummaryPart("review_gate", candidate.ReviewGate),
            ]) + ". This is not execution approval.";
        return JoinResponse(englishSummary, BuildCustomSummaryCueSentence(awarenessPolicy));
    }

    private static string? OptionalSummaryPart(string label, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : $"{label}={value.Trim()}";
    }

    private static string JoinSummaryParts(string separator, IReadOnlyList<string?> parts)
    {
        return string.Join(separator, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildReviewReadyQuestion(
        string responseLanguage,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var question = !AllowsReviewReadyHandoffRoute(awarenessPolicy)
            ? IsChineseResponse(responseLanguage)
                ? "请复核这个方向：要修正字段，还是先放置？当前意识配置不投影意图草稿交接。"
                : "Review this direction: revise a field or park it. This awareness profile does not project an intent draft handoff."
            : IsChineseResponse(responseLanguage)
                ? "请复核这个方向：要修正字段、先放置，还是走受治理的意图草稿路线？"
                : "Review this direction: revise a field, park it, or use the governed intent draft route?";

        return JoinResponse(question, BuildPersonalityQuestionCue(awarenessPolicy, responseLanguage));
    }

    private static string? BuildPersonalityQuestionCue(
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        var cues = awarenessPolicy.QuestionCues
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Take(RuntimeTurnAwarenessProfileContract.ExpressionCueMaxCount)
            .Concat(
                [
                    DescribeWarmthQuestionCue(awarenessPolicy.Warmth, responseLanguage),
                    DescribeConcisionQuestionCue(awarenessPolicy.Concision, responseLanguage),
                ])
            .Concat([DescribeCorrectionMode(awarenessPolicy.CorrectionMode, responseLanguage)])
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Select(cue => cue!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return cues.Length == 0 ? null : string.Join(' ', cues);
    }

    private static string? BuildCustomSummaryCueSentence(RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var cues = awarenessPolicy.SummaryCues
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Take(RuntimeTurnAwarenessProfileContract.ExpressionCueMaxCount)
            .ToArray();
        return cues.Length == 0 ? null : string.Join(' ', cues);
    }

    private static string BuildReviewReadyNextAction(
        string responseLanguage,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var nextAction = AllowsReviewReadyHandoffRoute(awarenessPolicy)
            ? IsChineseResponse(responseLanguage)
                ? "只有当操作者要落案时才使用受治理的意图草稿路线；这不是执行批准。"
                : "Use the governed intent draft route only if the operator wants to land it; this is not execution approval."
            : IsChineseResponse(responseLanguage)
                ? "当前意识配置只保留复核候选，不投影意图草稿交接；这不是执行批准。"
                : "This awareness profile keeps the review candidate only and does not project an intent draft handoff; this is not execution approval.";
        return JoinResponse(nextAction, BuildPersonalityNextActionCue(awarenessPolicy, responseLanguage));
    }

    private static bool AllowsReviewReadyHandoffRoute(RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        return awarenessPolicy.RequiresOperatorConfirmation
            && string.Equals(
                awarenessPolicy.HandoffMode,
                RuntimeTurnAwarenessProfileHandoffModes.ConfirmBeforeHandoff,
                StringComparison.Ordinal);
    }

    private static string BuildCollectingSummary(
        RuntimeGuidanceCandidate? candidate,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        var block = SelectReadinessBlock(candidate, awarenessPolicy);
        var roleFocus = BuildAwarenessFocusSentence(awarenessPolicy, responseLanguage);
        if (IsChineseResponse(responseLanguage))
        {
            if (block is null)
            {
                return JoinResponse(
                    "方向仍在收集必要字段；它还不能复核，也不是执行批准。",
                    roleFocus);
            }

            return JoinResponse(
                $"方向被{DescribeReadinessBlock(block, responseLanguage)}阻塞；它还不能复核，也不是执行批准。",
                roleFocus);
        }

        if (block is null)
        {
            return JoinResponse(
                "Direction is still collecting required fields; it is not review-ready and is not execution approval.",
                roleFocus);
        }

        return JoinResponse(
            $"Direction is blocked by {DescribeReadinessBlock(block, responseLanguage)}; it is not review-ready and is not execution approval.",
            roleFocus);
    }

    private static string? BuildAwarenessFocusSentence(RuntimeTurnAwarenessPolicy awarenessPolicy, string responseLanguage)
    {
        return JoinResponse(
            BuildRoleFocusSentence(awarenessPolicy, responseLanguage),
            BuildPersonalityCueSentence(awarenessPolicy, responseLanguage),
            BuildTonePolicySentence(awarenessPolicy, responseLanguage),
            BuildInteractionEvidenceSentence(awarenessPolicy, responseLanguage),
            BuildCustomSummaryCueSentence(awarenessPolicy));
    }

    private static string? BuildRoleFocusSentence(RuntimeTurnAwarenessPolicy awarenessPolicy, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return awarenessPolicy.RoleId switch
            {
                RuntimeTurnPostureCodes.Posture.Assistant =>
                    null,
                RuntimeTurnPostureCodes.Posture.Architecture =>
                    "关注点：架构复审意识会检查边界、约束、非目标和风险。",
                RuntimeTurnPostureCodes.Posture.Guard =>
                    "关注点：守卫意识会检查权限边界、风险、约束和非目标。",
                RuntimeTurnPostureCodes.Posture.ProjectManager =>
                    "关注点：项目经理意识会检查目标、范围、成功信号、负责人和复核点。",
                _ => null,
            };
        }

        return awarenessPolicy.RoleId switch
        {
            RuntimeTurnPostureCodes.Posture.Assistant =>
                null,
            RuntimeTurnPostureCodes.Posture.Architecture =>
                "Focus: architecture checks boundary, constraints, non-goals, and risk.",
            RuntimeTurnPostureCodes.Posture.Guard =>
                "Focus: guard checks authority boundary, risk, constraints, and non-goals.",
            RuntimeTurnPostureCodes.Posture.ProjectManager =>
                "Focus: project manager checks outcome, scope, success signal, owner, and review gate.",
            _ => null,
        };
    }

    private static string? BuildPersonalityCueSentence(RuntimeTurnAwarenessPolicy awarenessPolicy, string responseLanguage)
    {
        if (string.Equals(awarenessPolicy.VoiceId, RuntimeTurnAwarenessProfileVoiceIds.Neutral, StringComparison.Ordinal)
            && awarenessPolicy.ExpressionSignals.Count == 0)
        {
            return null;
        }

        var voice = DescribePersonalityVoice(awarenessPolicy.VoiceId, responseLanguage);
        var signals = DescribePersonalitySignals(awarenessPolicy, responseLanguage);
        if (IsChineseResponse(responseLanguage))
        {
            return string.IsNullOrWhiteSpace(signals)
                ? $"性格提示：{voice}。"
                : $"性格提示：{voice}会保留{signals}。";
        }

        return string.IsNullOrWhiteSpace(signals)
            ? $"Personality cue: {voice}."
            : $"Personality cue: {voice} keeps {signals} visible.";
    }

    private static string? BuildTonePolicySentence(RuntimeTurnAwarenessPolicy awarenessPolicy, string responseLanguage)
    {
        var cues = new[]
            {
                DescribeWarmthSummaryCue(awarenessPolicy.Warmth, responseLanguage),
                DescribeConcisionSummaryCue(awarenessPolicy.Concision, responseLanguage),
            }
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Select(cue => cue!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return cues.Length == 0 ? null : string.Join(' ', cues);
    }

    private static string? BuildPersonalityNextActionCue(
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        var customCues = awarenessPolicy.NextActionCues
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Take(RuntimeTurnAwarenessProfileContract.ExpressionCueMaxCount);
        var signalCues = awarenessPolicy.ExpressionSignals
            .Select(signal => DescribeNextActionSignal(signal, responseLanguage))
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Distinct(StringComparer.Ordinal)
            .Take(2);
        var interactionCues = new[]
            {
                DescribeEvidenceMode(awarenessPolicy.EvidenceMode, responseLanguage),
                DescribeCorrectionNextAction(awarenessPolicy.CorrectionMode, responseLanguage),
            }
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Select(cue => cue!);
        var toneCues = new[]
            {
                DescribeWarmthNextActionCue(awarenessPolicy.Warmth, responseLanguage),
                DescribeConcisionNextActionCue(awarenessPolicy.Concision, responseLanguage),
            }
            .Where(cue => !string.IsNullOrWhiteSpace(cue))
            .Select(cue => cue!);
        var cues = customCues
            .Concat(toneCues)
            .Concat(signalCues)
            .Concat(interactionCues)
            .Distinct(StringComparer.Ordinal)
            .Take(4)
            .ToArray();
        return cues.Length == 0 ? null : string.Join(' ', cues);
    }

    private static string? BuildInteractionEvidenceSentence(
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        return awarenessPolicy.EvidenceMode switch
        {
            RuntimeTurnAwarenessProfileEvidenceModes.EvidenceVisible when IsChineseResponse(responseLanguage) =>
                "证据层：只整理候选字段，不写入长期 truth。",
            RuntimeTurnAwarenessProfileEvidenceModes.EvidenceVisible =>
                "Evidence layer: collect candidate fields only; do not write long-term truth.",
            RuntimeTurnAwarenessProfileEvidenceModes.Compact when IsChineseResponse(responseLanguage) =>
                "证据层：只保留最小必要字段。",
            RuntimeTurnAwarenessProfileEvidenceModes.Compact =>
                "Evidence layer: keep only the minimum necessary fields.",
            _ => null,
        };
    }

    private static string? DescribeCorrectionMode(string correctionMode, string responseLanguage)
    {
        return correctionMode switch
        {
            RuntimeTurnAwarenessProfileCorrectionModes.AlwaysVisible when IsChineseResponse(responseLanguage) =>
                "可以直接修正任何字段。",
            RuntimeTurnAwarenessProfileCorrectionModes.AlwaysVisible =>
                "You can correct any field directly.",
            RuntimeTurnAwarenessProfileCorrectionModes.Quiet when IsChineseResponse(responseLanguage) =>
                "只保留最小追问。",
            RuntimeTurnAwarenessProfileCorrectionModes.Quiet =>
                "Keep the prompt minimal.",
            _ => null,
        };
    }

    private static string? DescribeCorrectionNextAction(string correctionMode, string responseLanguage)
    {
        return correctionMode switch
        {
            RuntimeTurnAwarenessProfileCorrectionModes.AlwaysVisible when IsChineseResponse(responseLanguage) =>
                "修正入口保持打开。",
            RuntimeTurnAwarenessProfileCorrectionModes.AlwaysVisible =>
                "Keep correction open.",
            RuntimeTurnAwarenessProfileCorrectionModes.Quiet when IsChineseResponse(responseLanguage) =>
                "不追加额外追问。",
            RuntimeTurnAwarenessProfileCorrectionModes.Quiet =>
                "Do not add extra prompting.",
            _ => null,
        };
    }

    private static string? DescribeEvidenceMode(string evidenceMode, string responseLanguage)
    {
        return evidenceMode switch
        {
            RuntimeTurnAwarenessProfileEvidenceModes.EvidenceVisible when IsChineseResponse(responseLanguage) =>
                "候选证据不等于 truth。",
            RuntimeTurnAwarenessProfileEvidenceModes.EvidenceVisible =>
                "Candidate evidence is not truth.",
            RuntimeTurnAwarenessProfileEvidenceModes.Compact when IsChineseResponse(responseLanguage) =>
                "只保留必要证据。",
            RuntimeTurnAwarenessProfileEvidenceModes.Compact =>
                "Keep only necessary evidence.",
            _ => null,
        };
    }

    private static string? DescribeWarmthSummaryCue(string warmth, string responseLanguage)
    {
        if (string.Equals(warmth, "high", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "语气：降低修正压力。"
                : "Tone: supportive and low-friction.";
        }

        if (string.Equals(warmth, "low", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "语气：保持中性，不额外安抚。"
                : "Tone: neutral, without extra reassurance.";
        }

        return null;
    }

    private static string? DescribeConcisionSummaryCue(string concision, string responseLanguage)
    {
        if (string.Equals(concision, "high", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "长度：保持紧凑，不追加可选细节。"
                : "Length: compact; avoid optional detail.";
        }

        if (string.Equals(concision, "low", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "长度：保留足够上下文，便于修正。"
                : "Length: keep enough context for correction.";
        }

        return null;
    }

    private static string? DescribeWarmthQuestionCue(string warmth, string responseLanguage)
    {
        if (string.Equals(warmth, "high", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "让修正容易说出口。"
                : "Keep correction easy to say.";
        }

        if (string.Equals(warmth, "low", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "问题保持中性。"
                : "Keep the question neutral.";
        }

        return null;
    }

    private static string? DescribeConcisionQuestionCue(string concision, string responseLanguage)
    {
        if (string.Equals(concision, "high", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "只问必要缺口。"
                : "Ask only the necessary gap.";
        }

        if (string.Equals(concision, "low", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "保留足够上下文便于修正。"
                : "Keep enough context for correction.";
        }

        return null;
    }

    private static string? DescribeWarmthNextActionCue(string warmth, string responseLanguage)
    {
        if (string.Equals(warmth, "high", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "让修正低压力。"
                : "Keep correction low-friction.";
        }

        if (string.Equals(warmth, "low", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "保持措辞中性。"
                : "Keep wording neutral.";
        }

        return null;
    }

    private static string? DescribeConcisionNextActionCue(string concision, string responseLanguage)
    {
        if (string.Equals(concision, "high", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "不追加可选细节。"
                : "Avoid optional detail.";
        }

        if (string.Equals(concision, "low", StringComparison.OrdinalIgnoreCase))
        {
            return IsChineseResponse(responseLanguage)
                ? "保留必要上下文。"
                : "Keep required context visible.";
        }

        return null;
    }

    private static string DescribePersonalityVoice(string voiceId, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return voiceId switch
            {
                RuntimeTurnAwarenessProfileVoiceIds.CalmAssistant => "温和助理语气",
                RuntimeTurnAwarenessProfileVoiceIds.DirectProjectManager => "直接项目经理语气",
                RuntimeTurnAwarenessProfileVoiceIds.StrictReviewer => "严格复审语气",
                _ => "中性语气",
            };
        }

        return voiceId switch
        {
            RuntimeTurnAwarenessProfileVoiceIds.CalmAssistant => "calm assistant voice",
            RuntimeTurnAwarenessProfileVoiceIds.DirectProjectManager => "direct project-manager voice",
            RuntimeTurnAwarenessProfileVoiceIds.StrictReviewer => "strict reviewer voice",
            _ => "neutral voice",
        };
    }

    private static string DescribePersonalitySignals(
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        var descriptions = awarenessPolicy.ExpressionSignals
            .Select(signal => DescribePersonalitySignal(signal, responseLanguage))
            .Where(description => !string.IsNullOrWhiteSpace(description))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        return string.Join(IsChineseResponse(responseLanguage) ? "、" : ", ", descriptions);
    }

    private static string DescribePersonalitySignal(string signal, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return signal switch
            {
                RuntimeTurnAwarenessProfileExpressionSignals.Compact => "简洁表达",
                RuntimeTurnAwarenessProfileExpressionSignals.Direct => "直接判断",
                RuntimeTurnAwarenessProfileExpressionSignals.Supportive => "低压力修正",
                RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck => "假设检查",
                RuntimeTurnAwarenessProfileExpressionSignals.BoundaryVisible => "控制边界",
                RuntimeTurnAwarenessProfileExpressionSignals.SequenceVisible => "顺序和复核点",
                _ => string.Empty,
            };
        }

        return signal switch
        {
            RuntimeTurnAwarenessProfileExpressionSignals.Compact => "compact wording",
            RuntimeTurnAwarenessProfileExpressionSignals.Direct => "direct tradeoff calls",
            RuntimeTurnAwarenessProfileExpressionSignals.Supportive => "low-friction correction",
            RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck => "assumption checks",
            RuntimeTurnAwarenessProfileExpressionSignals.BoundaryVisible => "control boundaries",
            RuntimeTurnAwarenessProfileExpressionSignals.SequenceVisible => "sequence and review point",
            _ => string.Empty,
        };
    }

    private static string DescribeNextActionSignal(string signal, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return signal switch
            {
                RuntimeTurnAwarenessProfileExpressionSignals.Compact => "保持简洁。",
                RuntimeTurnAwarenessProfileExpressionSignals.Direct => "直接指出阻塞。",
                RuntimeTurnAwarenessProfileExpressionSignals.Supportive => "让修正容易说出口。",
                RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck => "落案前先核对核心假设。",
                RuntimeTurnAwarenessProfileExpressionSignals.BoundaryVisible => "保留控制边界。",
                RuntimeTurnAwarenessProfileExpressionSignals.SequenceVisible => "保留顺序和复核点。",
                _ => string.Empty,
            };
        }

        return signal switch
        {
            RuntimeTurnAwarenessProfileExpressionSignals.Compact => "Keep it compact.",
            RuntimeTurnAwarenessProfileExpressionSignals.Direct => "Name the blocker directly.",
            RuntimeTurnAwarenessProfileExpressionSignals.Supportive => "Keep correction easy to say.",
            RuntimeTurnAwarenessProfileExpressionSignals.AssumptionCheck => "Check the central assumption before landing.",
            RuntimeTurnAwarenessProfileExpressionSignals.BoundaryVisible => "Keep control boundaries visible.",
            RuntimeTurnAwarenessProfileExpressionSignals.SequenceVisible => "Keep sequence and review point visible.",
            _ => string.Empty,
        };
    }

    private static string BuildCollectingQuestion(
        RuntimeGuidanceCandidate? candidate,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        var block = SelectReadinessBlock(candidate, awarenessPolicy);
        string question;
        if (block is not null)
        {
            question = block.Reason switch
            {
                RuntimeGuidanceCandidateReadinessBlockCodes.CandidateGroupConflict =>
                    IsChineseResponse(responseLanguage)
                        ? $"复核前应该保留哪一个{DisplayFieldName(block.Field, responseLanguage)}候选？"
                        : $"Which {DisplayFieldName(block.Field, responseLanguage)} candidate should I keep before review?",
                RuntimeGuidanceCandidateReadinessBlockCodes.ConflictingRequiredEvidence =>
                    IsChineseResponse(responseLanguage)
                        ? $"这个候选应该保留哪一个{DisplayFieldName(block.Field, responseLanguage)}？"
                        : $"Which {DisplayFieldName(block.Field, responseLanguage)} should this candidate keep?",
                RuntimeGuidanceCandidateReadinessBlockCodes.AmbiguousRequiredField =>
                    IsChineseResponse(responseLanguage)
                        ? $"请澄清{DisplayFieldName(block.Field, responseLanguage)}，让我只保留一个稳定值？"
                        : $"Clarify {DisplayFieldName(block.Field, responseLanguage)} so I can keep one stable value?",
                _ => BuildMissingFieldQuestion(block.Field, responseLanguage),
            };

            return JoinResponse(question, BuildPersonalityQuestionCue(awarenessPolicy, responseLanguage));
        }

        question = BuildMissingFieldQuestion(SelectMissingField(candidate, awarenessPolicy) ?? "desired_outcome", responseLanguage);
        return JoinResponse(question, BuildPersonalityQuestionCue(awarenessPolicy, responseLanguage));
    }

    private static string BuildCollectingNextAction(
        string responseLanguage,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        var nextAction = IsChineseResponse(responseLanguage)
            ? "补上下一项缺失字段、修正这个候选，或者如果现在不应继续就先放置。"
            : "Answer the next missing field, revise the candidate, or park it if it should not continue now.";
        return JoinResponse(nextAction, BuildPersonalityNextActionCue(awarenessPolicy, responseLanguage));
    }

    private static bool ShouldEmitQuestion(RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        return awarenessPolicy.MaxQuestions > 0
            && !string.Equals(
                awarenessPolicy.QuestionMode,
                RuntimeTurnAwarenessProfileQuestionModes.Off,
                StringComparison.Ordinal);
    }

    private static RuntimeGuidanceCandidateReadinessBlock? SelectReadinessBlock(
        RuntimeGuidanceCandidate? candidate,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        if (candidate is null || candidate.ReadinessBlocks.Count == 0)
        {
            return null;
        }

        foreach (var priority in awarenessPolicy.FieldPriority)
        {
            var block = candidate.ReadinessBlocks.FirstOrDefault(item => string.Equals(item.Field, priority, StringComparison.Ordinal));
            if (block is not null)
            {
                return block;
            }
        }

        return candidate.ReadinessBlocks.FirstOrDefault();
    }

    private static string? SelectMissingField(
        RuntimeGuidanceCandidate? candidate,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        if (candidate is null || candidate.MissingFields.Count == 0)
        {
            return null;
        }

        foreach (var priority in awarenessPolicy.FieldPriority)
        {
            var missing = candidate.MissingFields.FirstOrDefault(item => string.Equals(item, priority, StringComparison.Ordinal));
            if (!string.IsNullOrWhiteSpace(missing))
            {
                return missing;
            }
        }

        return candidate.MissingFields.FirstOrDefault();
    }

    private static string BuildMissingFieldQuestion(string missingField, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return missingField switch
            {
                "topic" => "这个候选要覆盖什么主题？",
                "desired_outcome" => "这个候选要保留什么目标？",
                "scope" => "这个候选要用什么范围来约束？",
                "constraint" or "constraints" => "这个候选必须保留什么约束？",
                "non_goal" or "non_goals" => "这个候选明确不做什么？",
                "success_signal" => "什么成功信号能说明这个候选可以复核？",
                "owner" => "这个候选需要谁负责或跟进？",
                "review_gate" => "这个候选要经过什么复核点再落案？",
                "risk" or "risks" => "这个候选需要先看见什么风险？",
                _ => $"应该用什么值填入{DisplayFieldName(missingField, responseLanguage)}？",
            };
        }

        return missingField switch
        {
            "topic" => "What topic should this candidate cover?",
            "desired_outcome" => "What desired outcome should this candidate preserve?",
            "scope" => "What scope should bound this candidate?",
            "constraint" or "constraints" => "What constraint should this candidate keep visible?",
            "non_goal" or "non_goals" => "What non-goal should this candidate keep out of scope?",
            "success_signal" => "What success signal should show this candidate is ready for review?",
            "owner" => "Who should own or follow up on this candidate?",
            "review_gate" => "What review gate should this candidate pass before landing?",
            "risk" or "risks" => "What risk should this candidate surface before review?",
            _ => $"What value should fill {missingField}?",
        };
    }

    private static string BuildCandidateAwarenessLine(
        RuntimeTurnExpressionStyle expressionStyle,
        RuntimeGuidanceCandidate? candidate,
        RuntimeGuidanceFieldCollectionDiagnostics? fieldDiagnostics,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        if (candidate is null)
        {
            var awareness = IsChineseResponse(responseLanguage)
                ? "我先把这当成一个可能方向，等待第一个具体字段。"
                : "I am treating this as a possible direction and waiting for the first concrete field.";
            return expressionStyle.ApplyAwareness(awareness, responseLanguage);
        }

        if (string.Equals(candidate.State, RuntimeGuidanceCandidateStates.ReviewReady, StringComparison.Ordinal))
        {
            var awareness = IsChineseResponse(responseLanguage)
                ? "我看到必要字段已经存在；现在只等待修正、放置或受治理交接，不是在批准。"
                : "I see the required fields as present; I am holding for correction, parking, or governed handoff, not approval.";
            return expressionStyle.ApplyAwareness(awareness, responseLanguage);
        }

        var readinessBlock = SelectReadinessBlock(candidate, awarenessPolicy);
        if (readinessBlock is not null)
        {
            return expressionStyle.ApplyAwareness(BuildReadinessBlockAwareness(readinessBlock, responseLanguage), responseLanguage);
        }

        var ambiguousField = fieldDiagnostics?.Ambiguities.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(ambiguousField))
        {
            var awareness = IsChineseResponse(responseLanguage)
                ? $"我会暂时放开{DisplayFieldName(ambiguousField, responseLanguage)}，不会硬选；已填字段仍只停留在候选态。"
                : $"I am holding {DisplayFieldName(ambiguousField, responseLanguage)} open instead of forcing it; filled fields stay candidate-only.";
            return expressionStyle.ApplyAwareness(awareness, responseLanguage);
        }

        var missingField = SelectMissingField(candidate, awarenessPolicy);
        return string.IsNullOrWhiteSpace(missingField)
            ? expressionStyle.ApplyAwareness(
                IsChineseResponse(responseLanguage)
                    ? "我会让它继续停在收集态，直到下一次修正让它稳定。"
                    : "I am keeping this in collection until the next correction makes it stable.",
                responseLanguage)
            : expressionStyle.ApplyAwareness(
                IsChineseResponse(responseLanguage)
                    ? $"我正在追踪缺失的{DisplayFieldName(missingField, responseLanguage)}；已填字段仍只停留在候选态。"
                    : $"I am tracking missing {DisplayFieldName(missingField, responseLanguage)}; filled fields stay candidate-only.",
                responseLanguage);
    }

    private static string BuildReadinessBlockAwareness(RuntimeGuidanceCandidateReadinessBlock block, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return block.Reason switch
            {
                RuntimeGuidanceCandidateReadinessBlockCodes.CandidateGroupConflict =>
                    $"我发现多个{DisplayFieldName(block.Field, responseLanguage)}候选，不会替你选择；已填字段仍只停留在候选态。",
                RuntimeGuidanceCandidateReadinessBlockCodes.ConflictingRequiredEvidence =>
                    $"我发现{DisplayFieldName(block.Field, responseLanguage)}证据冲突，不会覆盖当前值；已填字段仍只停留在候选态。",
                RuntimeGuidanceCandidateReadinessBlockCodes.AmbiguousRequiredField =>
                    $"我会暂时放开{DisplayFieldName(block.Field, responseLanguage)}，不会硬选；已填字段仍只停留在候选态。",
                _ =>
                    $"我正在追踪缺失的{DisplayFieldName(block.Field, responseLanguage)}；已填字段仍只停留在候选态。",
            };
        }

        return block.Reason switch
        {
            RuntimeGuidanceCandidateReadinessBlockCodes.CandidateGroupConflict =>
                $"I found multiple {DisplayFieldName(block.Field, responseLanguage)} candidates and will not choose one for you; filled fields stay candidate-only.",
            RuntimeGuidanceCandidateReadinessBlockCodes.ConflictingRequiredEvidence =>
                $"I found conflicting {DisplayFieldName(block.Field, responseLanguage)} evidence and will not overwrite the current value; filled fields stay candidate-only.",
            RuntimeGuidanceCandidateReadinessBlockCodes.AmbiguousRequiredField =>
                $"I am holding {DisplayFieldName(block.Field, responseLanguage)} open instead of forcing it; filled fields stay candidate-only.",
            _ =>
                $"I am tracking missing {DisplayFieldName(block.Field, responseLanguage)}; filled fields stay candidate-only.",
        };
    }

    private static string DescribeReadinessBlock(RuntimeGuidanceCandidateReadinessBlock block, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return block.Reason switch
            {
                RuntimeGuidanceCandidateReadinessBlockCodes.CandidateGroupConflict =>
                    $"多个{DisplayFieldName(block.Field, responseLanguage)}候选",
                RuntimeGuidanceCandidateReadinessBlockCodes.ConflictingRequiredEvidence =>
                    $"{DisplayFieldName(block.Field, responseLanguage)}证据冲突",
                RuntimeGuidanceCandidateReadinessBlockCodes.AmbiguousRequiredField =>
                    $"{DisplayFieldName(block.Field, responseLanguage)}证据不明确",
                _ =>
                    $"缺失{DisplayFieldName(block.Field, responseLanguage)}",
            };
        }

        return block.Reason switch
        {
            RuntimeGuidanceCandidateReadinessBlockCodes.CandidateGroupConflict =>
                $"multiple {DisplayFieldName(block.Field, responseLanguage)} candidates",
            RuntimeGuidanceCandidateReadinessBlockCodes.ConflictingRequiredEvidence =>
                $"conflicting {DisplayFieldName(block.Field, responseLanguage)} evidence",
            RuntimeGuidanceCandidateReadinessBlockCodes.AmbiguousRequiredField =>
                $"ambiguous {DisplayFieldName(block.Field, responseLanguage)} evidence",
            _ =>
                $"missing {DisplayFieldName(block.Field, responseLanguage)}",
        };
    }

    private static string BuildReadinessBlockProjection(IReadOnlyList<RuntimeGuidanceCandidateReadinessBlock> blocks)
    {
        return string.Join(
            ',',
            blocks
                .Where(block => !string.IsNullOrWhiteSpace(block.Field) && !string.IsNullOrWhiteSpace(block.Reason))
                .Select(block => $"{block.Field}:{block.Reason}")
                .Distinct(StringComparer.Ordinal)
                .Take(3));
    }

    private static string DisplayFieldName(string field, string responseLanguage)
    {
        if (IsChineseResponse(responseLanguage))
        {
            return field switch
            {
                "topic" => "主题",
                "desired_outcome" => "目标",
                "scope" => "范围",
                "success_signal" => "成功信号",
                "constraint" or "constraints" => "约束",
                "non_goal" or "non_goals" => "非目标",
                "risk" or "risks" => "风险",
                "owner" => "负责人",
                "review_gate" => "复核点",
                _ => field.Replace('_', ' '),
            };
        }

        return field switch
        {
            "topic" => "topic",
            "desired_outcome" => "desired outcome",
            "scope" => "scope",
            "success_signal" => "success signal",
            "constraint" or "constraints" => "constraint",
            "non_goal" or "non_goals" => "non-goal",
            "risk" or "risks" => "risk",
            _ => field.Replace('_', ' '),
        };
    }

    private static RuntimeTurnPostureGuidanceResult BuildParked(
        RuntimeTurnProjectionSeed projection,
        IReadOnlyDictionary<string, string> fields,
        RuntimeTurnExpressionStyle expressionStyle,
        RuntimeTurnAwarenessPolicy awarenessPolicy,
        string responseLanguage)
    {
        if (projection.PromptSuppressed)
        {
            return new RuntimeTurnPostureGuidanceResult(
                projection,
                fields,
                ShouldGuide: false,
                DirectionSummary: null,
                Question: null,
                RecommendedNextAction: null,
                SynthesizedResponse: null,
                PromptSuppressed: true,
                projection.SuppressionKey,
                projection.ShouldLoadPostureRegistryProse,
                projection.ShouldLoadCustomPersonalityNotes)
            {
                AwarenessPolicy = awarenessPolicy,
            };
        }

        if (string.IsNullOrWhiteSpace(projection.SuppressionKey))
        {
            var summaryText = IsChineseResponse(responseLanguage)
                ? "已先放置；因为本轮没有绑定候选修订，所以没有记录提示抑制。"
                : "Parked for now; no prompt suppression was recorded because this turn is not bound to a candidate revision.";
            var summary = expressionStyle.ApplySummary(
                JoinResponse(summaryText, BuildCustomSummaryCueSentence(awarenessPolicy)),
                responseLanguage);
            var awareness = expressionStyle.ApplyAwareness(
                IsChineseResponse(responseLanguage)
                    ? "我会让它先停着，但要抑制重复提示需要一个具体候选修订。"
                    : "I am leaving this idle, but repeated prompts need a concrete candidate revision to suppress.",
                responseLanguage);
            var nextActionText = IsChineseResponse(responseLanguage)
                ? "等用户提供或重新打开具体候选后，再抑制后续引导。"
                : "Wait for the user to provide or reopen a concrete candidate before suppressing follow-up guidance.";
            var nextAction = expressionStyle.ApplyNextAction(
                JoinResponse(nextActionText, BuildPersonalityNextActionCue(awarenessPolicy, responseLanguage)),
                responseLanguage);
            return new RuntimeTurnPostureGuidanceResult(
                projection,
                fields,
                ShouldGuide: true,
                DirectionSummary: summary,
                Question: null,
                RecommendedNextAction: nextAction,
                SynthesizedResponse: JoinResponse(
                    summary,
                    awareness,
                    nextAction,
                    IsChineseResponse(responseLanguage) ? "本轮没有执行生命周期动作。" : "No lifecycle action was taken."),
                PromptSuppressed: false,
                SuppressionKey: null,
                projection.ShouldLoadPostureRegistryProse,
                projection.ShouldLoadCustomPersonalityNotes)
            {
                AwarenessPolicy = awarenessPolicy,
            };
        }

        var parkedSummaryText = IsChineseResponse(responseLanguage)
            ? "已先放置；这个候选的提示抑制已经生效。"
            : "Parked for now; prompt suppression is active for this candidate.";
        var parkedSummary = expressionStyle.ApplySummary(
            JoinResponse(parkedSummaryText, BuildCustomSummaryCueSentence(awarenessPolicy)),
            responseLanguage);
        var parkedAwareness = expressionStyle.ApplyAwareness(
            IsChineseResponse(responseLanguage)
                ? "我会让这个候选保持安静，直到用户重新打开或修改它。"
                : "I am keeping this candidate quiet until the user reopens or changes it.",
            responseLanguage);
        var parkedNextActionText = IsChineseResponse(responseLanguage)
            ? "等待用户明确重新打开或修改这个候选。"
            : "Wait until the user explicitly reopens or changes the candidate.";
        var parkedNextAction = expressionStyle.ApplyNextAction(
            JoinResponse(parkedNextActionText, BuildPersonalityNextActionCue(awarenessPolicy, responseLanguage)),
            responseLanguage);
        return new RuntimeTurnPostureGuidanceResult(
            projection,
            fields,
            ShouldGuide: true,
            DirectionSummary: parkedSummary,
            Question: null,
            RecommendedNextAction: parkedNextAction,
            SynthesizedResponse: JoinResponse(
                parkedSummary,
                parkedAwareness,
                IsChineseResponse(responseLanguage)
                    ? "在用户明确重新打开或修改前，不会继续发出追问。"
                    : "No follow-up question is emitted until the user explicitly reopens or changes it.",
                parkedNextAction),
            PromptSuppressed: false,
            projection.SuppressionKey,
            projection.ShouldLoadPostureRegistryProse,
            projection.ShouldLoadCustomPersonalityNotes)
        {
            AwarenessPolicy = awarenessPolicy,
        };
    }

    private static RuntimeTurnPostureGuidanceResult BuildOrdinary(
        RuntimeTurnProjectionSeed projection,
        IReadOnlyDictionary<string, string> fields,
        RuntimeTurnAwarenessPolicy awarenessPolicy)
    {
        return new RuntimeTurnPostureGuidanceResult(
            projection,
            fields,
            ShouldGuide: false,
            DirectionSummary: null,
            Question: null,
            RecommendedNextAction: null,
            SynthesizedResponse: null,
            PromptSuppressed: false,
            SuppressionKey: null,
            projection.ShouldLoadPostureRegistryProse,
            projection.ShouldLoadCustomPersonalityNotes)
        {
            AwarenessPolicy = awarenessPolicy,
        };
    }

    internal static string JoinResponse(params string?[] parts)
    {
        return string.Join(
            ' ',
            parts
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));
    }

}

public sealed record RuntimeTurnStyleProfile
{
    public const string CurrentVersion = "runtime-turn-style-profile.v1";
    public const int CustomPersonalityNoteMaxUtf8Bytes = 240;
    public const int HardMaxQuestionsPerTurn = 1;

    public string StyleProfileId { get; init; } = "runtime-default";

    public string Version { get; init; } = CurrentVersion;

    public string Scope { get; init; } = "runtime_default";

    public string DisplayName { get; init; } = "Runtime default";

    public string DefaultPostureId { get; init; } = RuntimeTurnPostureCodes.Posture.None;

    public IReadOnlyList<string> ToneCodes { get; init; } = Array.Empty<string>();

    public string Directness { get; init; } = "balanced";

    public string ChallengeLevel { get; init; } = "medium";

    public string QuestionStyle { get; init; } = "one_clear_question";

    public string SummaryStyle { get; init; } = "decision_led";

    public string RiskSurfaceStyle { get; init; } = "surface_blockers_first";

    public IReadOnlyList<string> PreferredFocusCodes { get; init; } = Array.Empty<string>();

    public int MaxQuestions { get; init; } = HardMaxQuestionsPerTurn;

    public string RevisionHash { get; init; } = "runtime-default";

    public string? CustomPersonalityNote { get; init; }

    public static RuntimeTurnStyleProfile RuntimeDefault { get; } = new();
}

public sealed record RuntimeTurnStyleProfileValidation(
    bool IsValid,
    RuntimeTurnStyleProfile EffectiveProfile,
    IReadOnlyList<string> Issues);

public static class RuntimeAuthorityActionTokens
{
    public const string TaskRun = "task_run";
    public const string HostLifecycle = "host_lifecycle";
    public const string CardLifecycle = "card_lifecycle";
    public const string TaskGraphLifecycle = "taskgraph_lifecycle";
    public const string SyncState = "sync_state";
    public const string CommitMergeRelease = "commit_merge_release";
    public const string ValidationBypass = "validation_bypass";
    public const string WorkerDispatch = "worker_dispatch";
    public const string MemoryMutation = "memory_mutation";
    public const string TruthWrite = "truth_write";
    public const string TaskStatusMutation = "task_status_mutation";
    public const string PolicyOverride = "policy_override";
    public const string AutomaticApprovalOrExecution = "automatic_approval_or_execution";

    public static IReadOnlyList<string> All { get; } =
    [
        TaskRun,
        HostLifecycle,
        CardLifecycle,
        TaskGraphLifecycle,
        SyncState,
        CommitMergeRelease,
        ValidationBypass,
        WorkerDispatch,
        MemoryMutation,
        TruthWrite,
        TaskStatusMutation,
        PolicyOverride,
        AutomaticApprovalOrExecution,
    ];
}

public sealed record RuntimeAuthorityIntentClassification(
    bool HasAuthorityIntent,
    IReadOnlyList<string> ActionTokens);

public static class RuntimeAuthorityIntentClassifier
{
    private static readonly AuthorityIntentPhrase[] DirectAuthorityIntentPhrases =
    [
        new(RuntimeAuthorityActionTokens.TaskRun, "task run"),
        new(RuntimeAuthorityActionTokens.TaskRun, "run task"),
        new(RuntimeAuthorityActionTokens.HostLifecycle, "host start"),
        new(RuntimeAuthorityActionTokens.HostLifecycle, "start host"),
        new(RuntimeAuthorityActionTokens.HostLifecycle, "host stop"),
        new(RuntimeAuthorityActionTokens.HostLifecycle, "stop host"),
        new(RuntimeAuthorityActionTokens.HostLifecycle, "host lifecycle"),
        new(RuntimeAuthorityActionTokens.CardLifecycle, "create card"),
        new(RuntimeAuthorityActionTokens.CardLifecycle, "approve card"),
        new(RuntimeAuthorityActionTokens.CardLifecycle, "create-card"),
        new(RuntimeAuthorityActionTokens.CardLifecycle, "approve-card"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "create taskgraph"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "approve taskgraph"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "create-taskgraph"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "approve-taskgraph"),
        new(RuntimeAuthorityActionTokens.SyncState, "sync-state"),
        new(RuntimeAuthorityActionTokens.SyncState, "sync state"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "git commit"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "commit changes"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "create commit"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "git merge"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "merge pull request"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "merge pr"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "merge branch"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "merge release"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "git rebase"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "release build"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "create release"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "ship release"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "publish release"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "bypass validation"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "skip validation"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "validation bypass"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "bypass safety"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "skip safety"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "safety bypass"),
        new(RuntimeAuthorityActionTokens.PolicyOverride, "bypass policy"),
        new(RuntimeAuthorityActionTokens.PolicyOverride, "skip policy"),
        new(RuntimeAuthorityActionTokens.PolicyOverride, "policy bypass"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "ignore safety"),
        new(RuntimeAuthorityActionTokens.WorkerDispatch, "worker dispatch"),
        new(RuntimeAuthorityActionTokens.WorkerDispatch, "dispatch worker"),
        new(RuntimeAuthorityActionTokens.WorkerDispatch, "dispatch a worker"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "memory write"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "write memory"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "mutate memory"),
        new(RuntimeAuthorityActionTokens.TruthWrite, "write truth"),
        new(RuntimeAuthorityActionTokens.TruthWrite, "truth write"),
        new(RuntimeAuthorityActionTokens.TruthWrite, "truth-write"),
        new(RuntimeAuthorityActionTokens.PolicyOverride, "override policy"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "ai memory"),
        new(RuntimeAuthorityActionTokens.TaskStatusMutation, "task status"),
        new(RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution, "always approve"),
        new(RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution, "always execute"),
        new(RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution, "execute directly"),
        new(RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution, "auto execute"),
        new(RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution, "automatically execute"),
    ];

    private static readonly AuthorityIntentPhrase[] CompactAuthorityIntentPhrases =
    [
        new(RuntimeAuthorityActionTokens.TaskRun, "运行任务"),
        new(RuntimeAuthorityActionTokens.TaskRun, "执行任务"),
        new(RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution, "自动执行"),
        new(RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution, "批准执行"),
        new(RuntimeAuthorityActionTokens.HostLifecycle, "启动host"),
        new(RuntimeAuthorityActionTokens.HostLifecycle, "停止host"),
        new(RuntimeAuthorityActionTokens.CardLifecycle, "创建card"),
        new(RuntimeAuthorityActionTokens.CardLifecycle, "批准card"),
        new(RuntimeAuthorityActionTokens.CardLifecycle, "审批card"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "创建taskgraph"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "创建任务图"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "批准taskgraph"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "审批taskgraph"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "批准任务图"),
        new(RuntimeAuthorityActionTokens.TaskGraphLifecycle, "审批任务图"),
        new(RuntimeAuthorityActionTokens.SyncState, "同步状态"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "直接提交"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "自动提交"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "提交更改"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "合并pr"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "合并pullrequest"),
        new(RuntimeAuthorityActionTokens.CommitMergeRelease, "发布版本"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "绕过验证"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "跳过验证"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "绕过安全"),
        new(RuntimeAuthorityActionTokens.ValidationBypass, "跳过安全"),
        new(RuntimeAuthorityActionTokens.PolicyOverride, "绕过策略"),
        new(RuntimeAuthorityActionTokens.PolicyOverride, "跳过策略"),
        new(RuntimeAuthorityActionTokens.WorkerDispatch, "调度worker"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "写memory"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "写记忆"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "修改memory"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "修改记忆"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "变更memory"),
        new(RuntimeAuthorityActionTokens.MemoryMutation, "变更记忆"),
        new(RuntimeAuthorityActionTokens.TaskStatusMutation, "写任务状态"),
        new(RuntimeAuthorityActionTokens.TruthWrite, "写truth"),
        new(RuntimeAuthorityActionTokens.TruthWrite, "写真相"),
        new(RuntimeAuthorityActionTokens.TaskStatusMutation, "改任务状态"),
        new(RuntimeAuthorityActionTokens.TaskStatusMutation, "更新任务状态"),
        new(RuntimeAuthorityActionTokens.PolicyOverride, "覆盖策略"),
    ];

    private static readonly HashSet<string> AuthorityIntentStopWords = new(StringComparer.Ordinal)
    {
        "a",
        "an",
        "all",
        "and",
        "any",
        "automatically",
        "current",
        "directly",
        "my",
        "now",
        "our",
        "please",
        "that",
        "the",
        "then",
        "this",
        "to",
        "when",
    };

    private static readonly HashSet<string> AuthorityIntentNegationTokens = new(StringComparer.Ordinal)
    {
        "avoid",
        "don",
        "dont",
        "never",
        "no",
        "not",
        "without",
    };

    private static readonly string[] DirectNegationPrefixes =
    [
        "avoid ",
        "do not ",
        "don t ",
        "dont ",
        "never ",
        "no ",
        "not ",
        "without ",
    ];

    private static readonly string[] CompactNegationPrefixes =
    [
        "不要",
        "别",
        "不能",
        "不得",
        "禁止",
        "无需",
        "不需要",
    ];

    private static readonly AuthorityIntentRule[] AuthorityIntentRules =
    [
        new(
            RuntimeAuthorityActionTokens.AutomaticApprovalOrExecution,
            new HashSet<string>(StringComparer.Ordinal) { "approve", "authorize", "grant" },
            new HashSet<string>(StringComparer.Ordinal) { "approval", "execution", "permission", "permissions" }),
        new(
            RuntimeAuthorityActionTokens.TaskRun,
            new HashSet<string>(StringComparer.Ordinal) { "execute", "run" },
            new HashSet<string>(StringComparer.Ordinal) { "command", "execution", "task", "worker" }),
        new(
            RuntimeAuthorityActionTokens.HostLifecycle,
            new HashSet<string>(StringComparer.Ordinal) { "launch", "restart", "start", "stop" },
            new HashSet<string>(StringComparer.Ordinal) { "host", "worker" }),
        new(
            RuntimeAuthorityActionTokens.CardLifecycle,
            new HashSet<string>(StringComparer.Ordinal) { "approve", "create", "draft", "open" },
            new HashSet<string>(StringComparer.Ordinal) { "card" }),
        new(
            RuntimeAuthorityActionTokens.TaskGraphLifecycle,
            new HashSet<string>(StringComparer.Ordinal) { "approve", "create", "draft", "open" },
            new HashSet<string>(StringComparer.Ordinal) { "taskgraph" }),
        new(
            RuntimeAuthorityActionTokens.SyncState,
            new HashSet<string>(StringComparer.Ordinal) { "reconcile", "sync" },
            new HashSet<string>(StringComparer.Ordinal) { "state" }),
        new(
            RuntimeAuthorityActionTokens.CommitMergeRelease,
            new HashSet<string>(StringComparer.Ordinal) { "commit", "merge", "publish", "push", "rebase", "release", "ship" },
            new HashSet<string>(StringComparer.Ordinal) { "branch", "build", "change", "changes", "code", "pr", "release", "request" }),
        new(
            RuntimeAuthorityActionTokens.ValidationBypass,
            new HashSet<string>(StringComparer.Ordinal) { "bypass", "ignore", "override", "skip" },
            new HashSet<string>(StringComparer.Ordinal) { "approval", "guard", "review", "safety", "validation" }),
        new(
            RuntimeAuthorityActionTokens.PolicyOverride,
            new HashSet<string>(StringComparer.Ordinal) { "bypass", "ignore", "override", "skip" },
            new HashSet<string>(StringComparer.Ordinal) { "policy" }),
        new(
            RuntimeAuthorityActionTokens.WorkerDispatch,
            new HashSet<string>(StringComparer.Ordinal) { "dispatch", "launch" },
            new HashSet<string>(StringComparer.Ordinal) { "worker" }),
        new(
            RuntimeAuthorityActionTokens.MemoryMutation,
            new HashSet<string>(StringComparer.Ordinal) { "change", "mutate", "set", "update", "write" },
            new HashSet<string>(StringComparer.Ordinal) { "memory" }),
        new(
            RuntimeAuthorityActionTokens.TruthWrite,
            new HashSet<string>(StringComparer.Ordinal) { "change", "mutate", "set", "update", "write" },
            new HashSet<string>(StringComparer.Ordinal) { "truth" }),
    ];

    public static bool ContainsAuthorityIntent(string? text)
    {
        return Classify(text).HasAuthorityIntent;
    }

    public static RuntimeAuthorityIntentClassification Classify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new RuntimeAuthorityIntentClassification(false, Array.Empty<string>());
        }

        var normalized = NormalizeAuthorityIntentText(text);
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        AddMatchingPhraseTokens(tokens, DirectAuthorityIntentPhrases, normalized);

        var compact = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
        AddMatchingPhraseTokens(tokens, CompactAuthorityIntentPhrases, compact, compact: true);

        var normalizedTokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var rule in AuthorityIntentRules.Where(rule => ContainsAuthorityIntent(normalizedTokens, rule)))
        {
            tokens.Add(rule.ActionToken);
        }

        var actionTokens = RuntimeAuthorityActionTokens.All
            .Where(tokens.Contains)
            .ToArray();
        return new RuntimeAuthorityIntentClassification(actionTokens.Length > 0, actionTokens);
    }

    private static void AddMatchingPhraseTokens(
        ISet<string> tokens,
        IReadOnlyList<AuthorityIntentPhrase> phrases,
        string normalizedText,
        bool compact = false)
    {
        foreach (var phrase in phrases.Where(phrase => ContainsUnnegatedPhrase(normalizedText, phrase.Phrase, compact)))
        {
            tokens.Add(phrase.ActionToken);
        }
    }

    private static bool ContainsUnnegatedPhrase(string normalizedText, string phrase, bool compact)
    {
        var searchStart = 0;
        while (searchStart < normalizedText.Length)
        {
            var index = normalizedText.IndexOf(phrase, searchStart, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            if (!IsNegatedPhrase(normalizedText, index, compact))
            {
                return true;
            }

            searchStart = index + phrase.Length;
        }

        return false;
    }

    private static bool IsNegatedPhrase(string normalizedText, int phraseIndex, bool compact)
    {
        var prefix = normalizedText[..phraseIndex];
        if (compact)
        {
            return CompactNegationPrefixes.Any(prefix.EndsWith);
        }

        return DirectNegationPrefixes.Any(prefix.EndsWith);
    }

    private static string NormalizeAuthorityIntentText(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousWasSpace = true;

        foreach (var character in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder
            .ToString()
            .Trim()
            .Replace("task graph", "taskgraph", StringComparison.Ordinal);
    }

    private static bool ContainsAuthorityIntent(IReadOnlyList<string> tokens, AuthorityIntentRule rule)
    {
        const int maxSignificantTokenDistance = 5;

        for (var index = 0; index < tokens.Count; index++)
        {
            if (!rule.Actions.Contains(tokens[index]))
            {
                continue;
            }

            if (HasNearbyNegation(tokens, index))
            {
                continue;
            }

            var significantTokens = 0;
            for (var lookahead = index + 1;
                 lookahead < tokens.Count && significantTokens < maxSignificantTokenDistance;
                 lookahead++)
            {
                var token = tokens[lookahead];
                if (AuthorityIntentStopWords.Contains(token))
                {
                    continue;
                }

                if (rule.Objects.Contains(token))
                {
                    return true;
                }

                significantTokens++;
            }
        }

        return false;
    }

    private static bool HasNearbyNegation(IReadOnlyList<string> tokens, int actionIndex)
    {
        const int maxNegationDistance = 3;

        var significantTokens = 0;
        for (var lookbehind = actionIndex - 1;
             lookbehind >= 0 && significantTokens < maxNegationDistance;
             lookbehind--)
        {
            var token = tokens[lookbehind];
            if (AuthorityIntentStopWords.Contains(token))
            {
                continue;
            }

            if (AuthorityIntentNegationTokens.Contains(token))
            {
                return true;
            }

            significantTokens++;
        }

        return false;
    }

    private sealed record AuthorityIntentPhrase(
        string ActionToken,
        string Phrase);

    private sealed record AuthorityIntentRule(
        string ActionToken,
        IReadOnlySet<string> Actions,
        IReadOnlySet<string> Objects);
}

public sealed class RuntimeTurnStyleProfileValidator
{
    private const int MaxCodeCount = 6;
    private const int MaxCodeLength = 32;

    private static readonly HashSet<string> AllowedScopes = new(StringComparer.Ordinal)
    {
        "runtime_default",
        "user_default",
        "workspace_default",
        "project_override",
        "session_override",
    };

    private static readonly HashSet<string> AllowedPostures = new(StringComparer.Ordinal)
    {
        RuntimeTurnPostureCodes.Posture.None,
        RuntimeTurnPostureCodes.Posture.ProjectManager,
        RuntimeTurnPostureCodes.Posture.Assistant,
        RuntimeTurnPostureCodes.Posture.Architecture,
        RuntimeTurnPostureCodes.Posture.Guard,
    };

    public RuntimeTurnStyleProfileValidation Validate(
        RuntimeTurnStyleProfile? profile,
        RuntimeTurnStyleProfile? fallback = null)
    {
        var fallbackProfile = IsProfileStructurallyValid(fallback)
            ? fallback!
            : RuntimeTurnStyleProfile.RuntimeDefault;

        if (profile is null)
        {
            return new RuntimeTurnStyleProfileValidation(
                false,
                fallbackProfile,
                ["style_profile_missing"]);
        }

        var issues = CollectIssues(profile);
        return issues.Count == 0
            ? new RuntimeTurnStyleProfileValidation(true, profile, Array.Empty<string>())
            : new RuntimeTurnStyleProfileValidation(false, fallbackProfile, issues);
    }

    private static bool IsProfileStructurallyValid(RuntimeTurnStyleProfile? profile)
    {
        return profile is not null && CollectIssues(profile).Count == 0;
    }

    private static IReadOnlyList<string> CollectIssues(RuntimeTurnStyleProfile profile)
    {
        var issues = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.StyleProfileId))
        {
            issues.Add("style_profile_id_required");
        }

        if (!string.Equals(profile.Version, RuntimeTurnStyleProfile.CurrentVersion, StringComparison.Ordinal))
        {
            issues.Add("unknown_style_profile_version");
        }

        if (!AllowedScopes.Contains(profile.Scope))
        {
            issues.Add("invalid_style_profile_scope");
        }

        if (!AllowedPostures.Contains(profile.DefaultPostureId))
        {
            issues.Add("invalid_default_posture_id");
        }

        if (profile.MaxQuestions is < 0 or > RuntimeTurnStyleProfile.HardMaxQuestionsPerTurn)
        {
            issues.Add("max_questions_exceeds_hard_limit");
        }

        AddCodeListIssues(profile.ToneCodes, "tone_codes", issues);
        AddCodeListIssues(profile.PreferredFocusCodes, "preferred_focus_codes", issues);

        if (profile.CustomPersonalityNote is { } note)
        {
            if (Encoding.UTF8.GetByteCount(note) > RuntimeTurnStyleProfile.CustomPersonalityNoteMaxUtf8Bytes)
            {
                issues.Add("custom_personality_note_too_large");
            }

            if (RuntimeAuthorityIntentClassifier.ContainsAuthorityIntent(note))
            {
                issues.Add("custom_personality_note_attempts_authority_change");
            }
        }

        return issues;
    }

    private static void AddCodeListIssues(IReadOnlyList<string>? values, string fieldName, ICollection<string> issues)
    {
        if (values is null)
        {
            return;
        }

        if (values.Count > MaxCodeCount)
        {
            issues.Add($"{fieldName}_too_many");
        }

        foreach (var value in values)
        {
            if (!IsBoundedCode(value))
            {
                issues.Add($"{fieldName}_invalid");
                return;
            }
        }
    }

    private static bool IsBoundedCode(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= MaxCodeLength
            && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');
    }
}

public sealed record RuntimeTurnStyleProfileLoadResult(
    bool ProfileProvided,
    string Status,
    string Source,
    RuntimeTurnStyleProfile EffectiveProfile,
    IReadOnlyList<string> Issues);

public sealed class RuntimeTurnStyleProfileLoader
{
    private const string RuntimeDefaultStatus = "runtime_default";
    private const string LoadedStatus = "loaded";
    private const string FallbackStatus = "fallback";
    private static readonly RuntimeTurnStyleProfile RuntimeDefault = RuntimeTurnStyleProfile.RuntimeDefault;
    private readonly RuntimeTurnStyleProfileValidator validator;

    public RuntimeTurnStyleProfileLoader(RuntimeTurnStyleProfileValidator? validator = null)
    {
        this.validator = validator ?? new RuntimeTurnStyleProfileValidator();
    }

    public RuntimeTurnStyleProfileLoadResult Load(JsonObject? capabilities, RuntimeTurnStyleProfile? fallback = null)
    {
        var (profileNode, source, sourceIssue) = ResolveProfileNode(capabilities);
        if (profileNode is null)
        {
            return sourceIssue is null
                ? new RuntimeTurnStyleProfileLoadResult(false, RuntimeDefaultStatus, "runtime_default", RuntimeDefault, Array.Empty<string>())
                : new RuntimeTurnStyleProfileLoadResult(true, FallbackStatus, source, ResolveFallback(fallback), [sourceIssue]);
        }

        var profile = ReadProfile(profileNode);
        var validation = validator.Validate(profile, fallback);
        return validation.IsValid
            ? new RuntimeTurnStyleProfileLoadResult(true, LoadedStatus, source, validation.EffectiveProfile, Array.Empty<string>())
            : new RuntimeTurnStyleProfileLoadResult(true, FallbackStatus, source, validation.EffectiveProfile, validation.Issues);
    }

    private static RuntimeTurnStyleProfile ResolveFallback(RuntimeTurnStyleProfile? fallback)
    {
        return fallback ?? RuntimeDefault;
    }

    private static (JsonObject? ProfileNode, string Source, string? SourceIssue) ResolveProfileNode(JsonObject? capabilities)
    {
        if (capabilities is null)
        {
            return (null, "runtime_default", null);
        }

        foreach (var key in new[] { "runtime_turn_style_profile", "style_profile" })
        {
            if (!capabilities.TryGetPropertyValue(key, out var node))
            {
                continue;
            }

            return node is JsonObject profile
                ? (profile, $"client_capabilities.{key}", null)
                : (null, $"client_capabilities.{key}", "style_profile_not_object");
        }

        return (null, "runtime_default", null);
    }

    private static RuntimeTurnStyleProfile ReadProfile(JsonObject profile)
    {
        return new RuntimeTurnStyleProfile
        {
            StyleProfileId = ReadString(profile, "style_profile_id", "id") ?? RuntimeDefault.StyleProfileId,
            Version = ReadString(profile, "version") ?? RuntimeTurnStyleProfile.CurrentVersion,
            Scope = ReadString(profile, "scope") ?? RuntimeDefault.Scope,
            DisplayName = ReadString(profile, "display_name", "name") ?? RuntimeDefault.DisplayName,
            DefaultPostureId = ReadString(profile, "default_posture_id", "posture_id") ?? RuntimeDefault.DefaultPostureId,
            ToneCodes = ReadStringArray(profile, "tone_codes", "tones"),
            Directness = ReadString(profile, "directness") ?? RuntimeDefault.Directness,
            ChallengeLevel = ReadString(profile, "challenge_level") ?? RuntimeDefault.ChallengeLevel,
            QuestionStyle = ReadString(profile, "question_style") ?? RuntimeDefault.QuestionStyle,
            SummaryStyle = ReadString(profile, "summary_style") ?? RuntimeDefault.SummaryStyle,
            RiskSurfaceStyle = ReadString(profile, "risk_surface_style") ?? RuntimeDefault.RiskSurfaceStyle,
            PreferredFocusCodes = ReadStringArray(profile, "preferred_focus_codes", "focus_codes"),
            MaxQuestions = ReadInt(profile, "max_questions") ?? RuntimeDefault.MaxQuestions,
            RevisionHash = ReadString(profile, "revision_hash", "revision") ?? RuntimeDefault.RevisionHash,
            CustomPersonalityNote = ReadString(profile, "custom_personality_note", "personality_note"),
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

    private static int? ReadInt(JsonObject source, string key)
    {
        return source.TryGetPropertyValue(key, out var node)
            && node is JsonValue value
            && value.TryGetValue<int>(out var result)
                ? result
                : null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonObject source, params string[] keys)
    {
        var result = new List<string>();
        foreach (var key in keys)
        {
            if (!source.TryGetPropertyValue(key, out var node) || node is null)
            {
                continue;
            }

            if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    AddString(result, item);
                }
            }
            else
            {
                AddString(result, node);
            }
        }

        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void AddString(ICollection<string> result, JsonNode? node)
    {
        if (node is JsonValue value
            && value.TryGetValue<string>(out var text)
            && !string.IsNullOrWhiteSpace(text))
        {
            result.Add(text.Trim());
        }
    }
}
