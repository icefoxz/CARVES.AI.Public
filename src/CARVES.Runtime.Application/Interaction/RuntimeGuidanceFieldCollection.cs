using System.Text;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Interaction;

public static class RuntimeGuidanceFieldOperationCompilerContract
{
    public const string CurrentVersion = "runtime-guidance-field-ops.v1";
}

public static class RuntimeGuidanceFieldOperationNames
{
    public const string Preserve = "preserve";
    public const string Set = "set";
    public const string Append = "append";
    public const string Clear = "clear";

    internal static string FromKind(RuntimeGuidanceFieldOperationKind kind)
    {
        return kind switch
        {
            RuntimeGuidanceFieldOperationKind.Set => Set,
            RuntimeGuidanceFieldOperationKind.Append => Append,
            RuntimeGuidanceFieldOperationKind.Clear => Clear,
            _ => Preserve,
        };
    }
}

public static class RuntimeGuidanceFieldEvidenceKinds
{
    public const string ExplicitField = "explicit_field";
    public const string AmbientTopic = "ambient_topic";
    public const string AmbientScope = "ambient_scope";
    public const string CandidateAlternative = "candidate_alternative";
    public const string NegativeScope = "negative_scope";
    public const string ListField = "list_field";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            ExplicitField,
            AmbientTopic,
            AmbientScope,
            CandidateAlternative,
            NegativeScope,
            ListField,
        ],
        StringComparer.Ordinal);
}

public static class RuntimeGuidanceFieldEvidenceCandidateGroups
{
    public const string Primary = "candidate_primary";
    public const string Alternative = "candidate_alternative";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            Primary,
            Alternative,
        ],
        StringComparer.Ordinal);
}

public static class RuntimeGuidanceFieldEvidenceIssueCodes
{
    public const string AmbiguousLanguage = "ambiguous_language";
    public const string ConflictingEvidence = "conflicting_evidence";
    public const string CandidateAlternative = "candidate_alternative";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(
        [
            AmbiguousLanguage,
            ConflictingEvidence,
            CandidateAlternative,
        ],
        StringComparer.Ordinal);
}

public sealed record RuntimeGuidanceFieldOperationCompilerInput(
    string? UserText,
    RuntimeGuidanceCandidate? ExistingCandidate = null,
    RuntimeGuidanceAdmissionDecision? Admission = null,
    RuntimeTurnAwarenessAdmissionHints? AdmissionHints = null);

public sealed record RuntimeGuidanceCompiledFieldOperation(
    [property: JsonPropertyName("field")]
    string Field,
    [property: JsonPropertyName("op")]
    string Operation,
    [property: JsonPropertyName("value")]
    string? Value,
    [property: JsonPropertyName("values")]
    IReadOnlyList<string> Values,
    [property: JsonPropertyName("confidence")]
    double Confidence,
    [property: JsonPropertyName("evidence")]
    string? Evidence);

public sealed record RuntimeGuidanceFieldOperationCompilerResult
{
    [JsonPropertyName("contract_version")]
    public string ContractVersion { get; init; } = RuntimeGuidanceFieldOperationCompilerContract.CurrentVersion;

    [JsonPropertyName("extraction_contract_version")]
    public string ExtractionContractVersion { get; init; } = RuntimeGuidanceFieldExtractionContract.CurrentVersion;

    [JsonPropertyName("admission_kind")]
    public string AdmissionKind { get; init; } = RuntimeGuidanceAdmissionKinds.ChatOnly;

    [JsonPropertyName("operations")]
    public IReadOnlyList<RuntimeGuidanceCompiledFieldOperation> Operations { get; init; } = Array.Empty<RuntimeGuidanceCompiledFieldOperation>();

    [JsonPropertyName("field_evidence")]
    public IReadOnlyList<RuntimeGuidanceExtractedFieldEvidence> FieldEvidence { get; init; } = Array.Empty<RuntimeGuidanceExtractedFieldEvidence>();

    [JsonPropertyName("ambiguities")]
    public IReadOnlyList<string> Ambiguities { get; init; } = Array.Empty<string>();

    [JsonPropertyName("resets_candidate")]
    public bool ResetsCandidate { get; init; }

    [JsonPropertyName("blocked_reasons")]
    public IReadOnlyList<string> BlockedReasons { get; init; } = Array.Empty<string>();

    [JsonIgnore]
    internal RuntimeGuidanceFieldOperationSet OperationSet { get; init; } = RuntimeGuidanceFieldOperationSet.Empty;

    [JsonIgnore]
    public bool ShouldCompile => BlockedReasons.Count == 0;
}

public sealed class RuntimeGuidanceFieldOperationCompiler
{
    private readonly RuntimeGuidanceFieldExtractor fieldExtractor;

    public RuntimeGuidanceFieldOperationCompiler()
        : this(new RuntimeGuidanceFieldExtractor())
    {
    }

    internal RuntimeGuidanceFieldOperationCompiler(RuntimeGuidanceFieldExtractor fieldExtractor)
    {
        this.fieldExtractor = fieldExtractor;
    }

    public RuntimeGuidanceFieldOperationCompilerResult Compile(RuntimeGuidanceFieldOperationCompilerInput input)
    {
        var text = string.IsNullOrWhiteSpace(input.UserText) ? string.Empty : input.UserText.Trim();
        var admission = input.Admission ?? RuntimeGuidanceAdmission.Decide(text, input.ExistingCandidate, input.AdmissionHints);
        if (!admission.CanBuildCandidate)
        {
            return new RuntimeGuidanceFieldOperationCompilerResult
            {
                AdmissionKind = admission.Kind,
                BlockedReasons = admission.ReasonCodes.Count == 0
                    ? [RuntimeGuidanceAdmissionReasonCodes.NoAdmissionSignal]
                    : admission.ReasonCodes,
            };
        }

        var extraction = fieldExtractor.Extract(new RuntimeGuidanceFieldExtractionInput(text, input.ExistingCandidate));
        var operationSet = extraction.OperationSet;
        return new RuntimeGuidanceFieldOperationCompilerResult
        {
            AdmissionKind = admission.Kind,
            OperationSet = operationSet,
            Operations = ProjectOperations(operationSet),
            FieldEvidence = extraction.FieldEvidence,
            Ambiguities = extraction.Ambiguities,
            ResetsCandidate = extraction.ResetsCandidate,
        };
    }

    private static IReadOnlyList<RuntimeGuidanceCompiledFieldOperation> ProjectOperations(
        RuntimeGuidanceFieldOperationSet operationSet)
    {
        return
        [
            ProjectScalar("topic", operationSet.Topic),
            ProjectScalar("desired_outcome", operationSet.DesiredOutcome),
            ProjectScalar("scope", operationSet.Scope),
            ProjectList("constraints", operationSet.Constraints),
            ProjectList("non_goals", operationSet.NonGoals),
            ProjectScalar("success_signal", operationSet.SuccessSignal),
            ProjectScalar("owner", operationSet.Owner),
            ProjectScalar("review_gate", operationSet.ReviewGate),
            ProjectList("risks", operationSet.Risks),
        ];
    }

    private static RuntimeGuidanceCompiledFieldOperation ProjectScalar(
        string field,
        RuntimeGuidanceScalarFieldOperation operation)
    {
        var value = operation.Kind == RuntimeGuidanceFieldOperationKind.Set ? operation.Value : null;
        return new RuntimeGuidanceCompiledFieldOperation(
            field,
            RuntimeGuidanceFieldOperationNames.FromKind(operation.Kind),
            value,
            Array.Empty<string>(),
            ToOperationConfidence(operation.Kind),
            value);
    }

    private static RuntimeGuidanceCompiledFieldOperation ProjectList(
        string field,
        RuntimeGuidanceListFieldOperation operation)
    {
        var values = operation.Kind is RuntimeGuidanceFieldOperationKind.Set
            or RuntimeGuidanceFieldOperationKind.Append
            or RuntimeGuidanceFieldOperationKind.Clear
            ? operation.Values
            : Array.Empty<string>();
        return new RuntimeGuidanceCompiledFieldOperation(
            field,
            RuntimeGuidanceFieldOperationNames.FromKind(operation.Kind),
            null,
            values,
            ToOperationConfidence(operation.Kind),
            values.Count == 0 ? null : string.Join("; ", values));
    }

    private static double ToOperationConfidence(RuntimeGuidanceFieldOperationKind kind)
    {
        return kind switch
        {
            RuntimeGuidanceFieldOperationKind.Set => 0.84d,
            RuntimeGuidanceFieldOperationKind.Append => 0.8d,
            RuntimeGuidanceFieldOperationKind.Clear => 0.88d,
            _ => 0.55d,
        };
    }
}

public sealed class RuntimeGuidanceCandidateBuilder
{
    private readonly RuntimeGuidanceFieldOperationCompiler fieldOperationCompiler;
    private readonly RuntimeGuidanceCandidateReducer candidateReducer;

    public RuntimeGuidanceCandidateBuilder(
        RuntimeGuidanceFieldOperationCompiler? fieldOperationCompiler = null,
        RuntimeGuidanceCandidateReducer? candidateReducer = null)
    {
        this.fieldOperationCompiler = fieldOperationCompiler ?? new RuntimeGuidanceFieldOperationCompiler();
        this.candidateReducer = candidateReducer ?? new RuntimeGuidanceCandidateReducer();
    }

    public RuntimeGuidanceCandidateBuildResult Build(RuntimeGuidanceCandidateInput input)
    {
        var text = string.IsNullOrWhiteSpace(input.UserText) ? string.Empty : input.UserText.Trim();
        var admission = RuntimeGuidanceAdmission.Decide(text, input.ExistingCandidate, input.AdmissionHints);
        if (!admission.CanBuildCandidate)
        {
            return new RuntimeGuidanceCandidateBuildResult(false, null);
        }

        var topicReset = RuntimeGuidanceFieldExtractor.ShouldResetCandidate(text, input.ExistingCandidate);
        var existingCandidate = topicReset ? null : input.ExistingCandidate;
        var compiledOperations = fieldOperationCompiler.Compile(new RuntimeGuidanceFieldOperationCompilerInput(
            text,
            existingCandidate,
            admission,
            input.AdmissionHints));
        var fieldDiagnostics = RuntimeGuidanceFieldCollectionDiagnostics.FromCompilerResult(compiledOperations) with
        {
            ResetsCandidate = topicReset || compiledOperations.ResetsCandidate,
        };
        if (!compiledOperations.ShouldCompile)
        {
            return new RuntimeGuidanceCandidateBuildResult(false, null)
            {
                FieldDiagnostics = fieldDiagnostics,
            };
        }

        var reduced = candidateReducer.Reduce(new RuntimeGuidanceCandidateReducerInput(
            existingCandidate,
            compiledOperations,
            input.CandidateId,
            input.SourceTurnRef,
            topicReset,
            input.FieldPriority,
            input.ReviewGateFields));

        return new RuntimeGuidanceCandidateBuildResult(reduced.ShouldProjectCandidate, reduced.Candidate)
        {
            FieldDiagnostics = fieldDiagnostics,
        };
    }

    public static bool HasCandidateSignal(string? userText)
    {
        return RuntimeGuidanceAdmission.HasCandidateSignal(userText);
    }
}

internal sealed record RuntimeGuidanceFieldExtractionInput(
    string? UserText,
    RuntimeGuidanceCandidate? ExistingCandidate = null);

public static class RuntimeGuidanceFieldExtractionContract
{
    public const string CurrentVersion = "runtime-guidance-field-extraction.v1";
}

public sealed record RuntimeGuidanceExtractedFieldEvidence(
    [property: JsonPropertyName("field")]
    string Field,
    [property: JsonPropertyName("op")]
    string Operation,
    [property: JsonPropertyName("values")]
    IReadOnlyList<string> Values,
    [property: JsonPropertyName("ambiguous")]
    bool Ambiguous,
    [property: JsonPropertyName("issue_codes")]
    IReadOnlyList<string> IssueCodes,
    [property: JsonPropertyName("sources")]
    IReadOnlyList<RuntimeGuidanceExtractedFieldEvidenceSource> Sources);

public sealed record RuntimeGuidanceExtractedFieldEvidenceSource(
    [property: JsonPropertyName("source_ref")]
    string SourceRef,
    [property: JsonPropertyName("kind")]
    string Kind,
    [property: JsonPropertyName("candidate_group")]
    string CandidateGroup,
    [property: JsonPropertyName("confidence")]
    double Confidence,
    [property: JsonPropertyName("negated")]
    bool Negated,
    [property: JsonPropertyName("ambiguous")]
    bool Ambiguous);

internal sealed record RuntimeGuidanceFieldExtractionResult(
    [property: JsonPropertyName("contract_version")]
    string ContractVersion,
    [property: JsonIgnore]
    RuntimeGuidanceFieldOperationSet OperationSet,
    [property: JsonPropertyName("field_evidence")]
    IReadOnlyList<RuntimeGuidanceExtractedFieldEvidence> FieldEvidence,
    [property: JsonPropertyName("ambiguities")]
    IReadOnlyList<string> Ambiguities,
    [property: JsonPropertyName("resets_candidate")]
    bool ResetsCandidate);

internal sealed record RuntimeGuidanceCollectedFieldEvidence(
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> Topics,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> DesiredOutcomes,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> Scopes,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> Constraints,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> NonGoals,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> SuccessSignals,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> Owners,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> ReviewGates,
    IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> Risks,
    IReadOnlyList<string> Ambiguities)
{
    public string ScalarValue(string field)
    {
        return field switch
        {
            "topic" => FirstScalar(Topics),
            "desired_outcome" => FirstScalar(DesiredOutcomes),
            "scope" => FirstScalar(Scopes),
            "success_signal" => FirstScalar(SuccessSignals),
            "owner" => FirstScalar(Owners),
            "review_gate" => FirstScalar(ReviewGates),
            _ => string.Empty,
        };
    }

    public IReadOnlyList<string> ListValues(string field)
    {
        return field switch
        {
            "constraint" => Values(Constraints),
            "non_goal" => Values(NonGoals),
            "risk" => Values(Risks),
            _ => Array.Empty<string>(),
        };
    }

    public IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> EvidenceFor(string field)
    {
        return field switch
        {
            "topic" => Topics,
            "desired_outcome" => DesiredOutcomes,
            "scope" => Scopes,
            "constraint" or "constraints" => Constraints,
            "non_goal" or "non_goals" => NonGoals,
            "success_signal" => SuccessSignals,
            "owner" => Owners,
            "review_gate" => ReviewGates,
            "risk" or "risks" => Risks,
            _ => Array.Empty<RuntimeGuidanceFieldEvidenceItem>(),
        };
    }

    public bool IsAmbiguous(string field)
    {
        return Ambiguities.Contains(field, StringComparer.Ordinal);
    }

    private static string FirstScalar(IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> values)
    {
        return values.Count == 0 ? string.Empty : values[0].Value;
    }

    private static IReadOnlyList<string> Values(IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> values)
    {
        return values.Select(value => value.Value).ToArray();
    }
}

internal sealed record RuntimeGuidanceFieldEvidenceItem(
    string Field,
    string Value,
    string SourceClause,
    string SourceRef,
    string Kind,
    double Confidence,
    bool Negated,
    bool Ambiguous,
    string CandidateGroup);

internal sealed class RuntimeGuidanceFieldExtractor
{
    private const int MaxTextLength = 96;
    private const int MaxClauseCount = 12;
    private const int MaxListItems = 3;
    private static readonly char[] ClauseSeparators = ['\r', '\n', '.', '。', '!', '！', '?', '？', ';', '；'];
    private static readonly string[] InlineFieldMarkers =
    [
        "换一个方向：",
        "换一个方向:",
        "换个方向：",
        "换个方向:",
        "另一个方向是",
        "另一个方向:",
        "另一个方向：",
        "项目方向是",
        "项目方向:",
        "项目方向：",
        "功能方向是",
        "功能方向:",
        "功能方向：",
        "目标不是",
        "目标改成",
        "目标改为",
        "目标应该是",
        "目标可能是",
        "目标是",
        "目标:",
        "目标：",
        "范围改成",
        "范围改为",
        "范围不是",
        "范围不包括",
        "范围不包含",
        "范围可能是",
        "范围是",
        "范围:",
        "范围：",
        "成功标准是",
        "成功标准:",
        "成功标准：",
        "验收是",
        "验收:",
        "验收：",
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
        "约束改成",
        "约束改为",
        "约束是",
        "约束:",
        "约束：",
        "非目标是",
        "非目标:",
        "非目标：",
        "不纳入范围是",
        "不包括范围是",
        "风险改成",
        "风险改为",
        "风险可能是",
        "风险是",
        "风险:",
        "风险：",
        "project direction is",
        "feature direction is",
        "topic is",
        "goal should be",
        "goal is",
        "outcome should be",
        "outcome is",
        "scope should be",
        "scope is not",
        "scope isn't",
        "scope does not include",
        "scope doesn't include",
        "scope is",
        "success means",
        "ready when",
        "acceptance should be",
        "acceptance is",
        "owner is",
        "owner:",
        "assignee is",
        "assignee:",
        "review gate is",
        "review gate:",
        "review checkpoint is",
        "review checkpoint:",
        "constraint is",
        "non-goal is",
        "non goal is",
        "out of scope is",
        "not in scope is",
        "risk is",
    ];

    public RuntimeGuidanceFieldExtractionResult Extract(RuntimeGuidanceFieldExtractionInput input)
    {
        var text = NormalizeText(input.UserText);
        var clauses = SplitClauses(text);
        var evidence = CollectFieldEvidence(
            clauses,
            allowTopicFallback: input.ExistingCandidate is null,
            allowAmbientScope: input.ExistingCandidate is null);
        var operationSet = BuildFieldOperations(text, input.ExistingCandidate, evidence);
        var ambiguities = evidence.Ambiguities;
        return new RuntimeGuidanceFieldExtractionResult(
            RuntimeGuidanceFieldExtractionContract.CurrentVersion,
            operationSet,
            ProjectFieldEvidence(operationSet, evidence, ambiguities),
            ambiguities,
            ShouldResetCandidate(text, input.ExistingCandidate));
    }

    private static IReadOnlyList<RuntimeGuidanceExtractedFieldEvidence> ProjectFieldEvidence(
        RuntimeGuidanceFieldOperationSet operationSet,
        RuntimeGuidanceCollectedFieldEvidence collectedEvidence,
        IReadOnlyList<string> ambiguities)
    {
        var ambiguousFields = ambiguities.ToHashSet(StringComparer.Ordinal);
        return
        [
            ProjectScalarEvidence("topic", operationSet.Topic, collectedEvidence.EvidenceFor("topic"), ambiguousFields),
            ProjectScalarEvidence("desired_outcome", operationSet.DesiredOutcome, collectedEvidence.EvidenceFor("desired_outcome"), ambiguousFields),
            ProjectScalarEvidence("scope", operationSet.Scope, collectedEvidence.EvidenceFor("scope"), ambiguousFields),
            ProjectListEvidence("constraints", operationSet.Constraints, "constraint", collectedEvidence.EvidenceFor("constraint"), ambiguousFields),
            ProjectListEvidence("non_goals", operationSet.NonGoals, "non_goal", collectedEvidence.EvidenceFor("non_goal"), ambiguousFields),
            ProjectScalarEvidence("success_signal", operationSet.SuccessSignal, collectedEvidence.EvidenceFor("success_signal"), ambiguousFields),
            ProjectScalarEvidence("owner", operationSet.Owner, collectedEvidence.EvidenceFor("owner"), ambiguousFields),
            ProjectScalarEvidence("review_gate", operationSet.ReviewGate, collectedEvidence.EvidenceFor("review_gate"), ambiguousFields),
            ProjectListEvidence("risks", operationSet.Risks, "risk", collectedEvidence.EvidenceFor("risk"), ambiguousFields),
        ];
    }

    private static RuntimeGuidanceExtractedFieldEvidence ProjectScalarEvidence(
        string field,
        RuntimeGuidanceScalarFieldOperation operation,
        IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> evidence,
        IReadOnlySet<string> ambiguousFields)
    {
        IReadOnlyList<string> values = operation.Kind == RuntimeGuidanceFieldOperationKind.Set && !string.IsNullOrWhiteSpace(operation.Value)
            ? [operation.Value]
            : Array.Empty<string>();
        return new RuntimeGuidanceExtractedFieldEvidence(
            field,
            RuntimeGuidanceFieldOperationNames.FromKind(operation.Kind),
            values,
            ambiguousFields.Contains(field),
            ProjectIssueCodes(field, evidence, ambiguousFields.Contains(field), allowMultipleValues: false),
            ProjectEvidenceSources(evidence, ambiguousFields.Contains(field)));
    }

    private static RuntimeGuidanceExtractedFieldEvidence ProjectListEvidence(
        string field,
        RuntimeGuidanceListFieldOperation operation,
        string ambiguityField,
        IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> evidence,
        IReadOnlySet<string> ambiguousFields)
    {
        IReadOnlyList<string> values = operation.Kind is RuntimeGuidanceFieldOperationKind.Set
            or RuntimeGuidanceFieldOperationKind.Append
            or RuntimeGuidanceFieldOperationKind.Clear
            ? operation.Values
            : Array.Empty<string>();
        return new RuntimeGuidanceExtractedFieldEvidence(
            field,
            RuntimeGuidanceFieldOperationNames.FromKind(operation.Kind),
            values,
            ambiguousFields.Contains(ambiguityField),
            ProjectIssueCodes(field, evidence, ambiguousFields.Contains(ambiguityField), allowMultipleValues: true),
            ProjectEvidenceSources(evidence, ambiguousFields.Contains(ambiguityField)));
    }

    private static IReadOnlyList<string> ProjectIssueCodes(
        string field,
        IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> evidence,
        bool fieldAmbiguous,
        bool allowMultipleValues)
    {
        if (!fieldAmbiguous)
        {
            return Array.Empty<string>();
        }

        var issues = new List<string>(3);
        if (evidence.Any(item => item.Ambiguous))
        {
            issues.Add(RuntimeGuidanceFieldEvidenceIssueCodes.AmbiguousLanguage);
        }

        if (evidence.Any(item => string.Equals(
                item.CandidateGroup,
                RuntimeGuidanceFieldEvidenceCandidateGroups.Alternative,
                StringComparison.Ordinal)))
        {
            issues.Add(RuntimeGuidanceFieldEvidenceIssueCodes.CandidateAlternative);
        }

        if (!allowMultipleValues && HasConflictingEvidence(evidence.Select(item => item.Value).ToArray()))
        {
            issues.Add(RuntimeGuidanceFieldEvidenceIssueCodes.ConflictingEvidence);
        }

        if (issues.Count == 0)
        {
            issues.Add(RuntimeGuidanceFieldEvidenceIssueCodes.AmbiguousLanguage);
        }

        return issues
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<RuntimeGuidanceExtractedFieldEvidenceSource> ProjectEvidenceSources(
        IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> evidence,
        bool fieldAmbiguous)
    {
        return evidence
            .Select(item => new RuntimeGuidanceExtractedFieldEvidenceSource(
                item.SourceRef,
                item.Kind,
                item.CandidateGroup,
                Math.Clamp(Math.Round(item.Confidence, 2), 0d, 1d),
                item.Negated,
                fieldAmbiguous || item.Ambiguous))
            .Take(MaxListItems)
            .ToArray();
    }

    internal static RuntimeGuidanceFieldOperationSet BuildFieldOperations(string text, RuntimeGuidanceCandidate? existingCandidate)
    {
        var clauses = SplitClauses(text);
        var evidence = CollectFieldEvidence(
            clauses,
            allowTopicFallback: existingCandidate is null,
            allowAmbientScope: existingCandidate is null);
        return BuildFieldOperations(text, existingCandidate, evidence);
    }

    private static RuntimeGuidanceFieldOperationSet BuildFieldOperations(
        string text,
        RuntimeGuidanceCandidate? existingCandidate,
        RuntimeGuidanceCollectedFieldEvidence evidence)
    {
        var isRemoval = IsRemovalText(text);

        return new RuntimeGuidanceFieldOperationSet(
            ToScalarOperation(
                evidence.ScalarValue("topic"),
                IsCorrectionForField(text, "topic"),
                IsRemovalForField(text, "topic"),
                evidence.IsAmbiguous("topic")),
            ToScalarOperation(
                evidence.ScalarValue("desired_outcome"),
                IsCorrectionForField(text, "desired_outcome"),
                IsRemovalForField(text, "desired_outcome"),
                evidence.IsAmbiguous("desired_outcome")),
            ToScalarOperation(
                evidence.ScalarValue("scope"),
                IsCorrectionForField(text, "scope"),
                IsRemovalForField(text, "scope"),
                evidence.IsAmbiguous("scope")),
            ToListOperation(
                existingCandidate?.Constraints,
                evidence.ListValues("constraint"),
                text,
                isRemoval,
                "constraint",
                evidence.IsAmbiguous("constraint")),
            ToListOperation(
                existingCandidate?.NonGoals,
                evidence.ListValues("non_goal"),
                text,
                isRemoval,
                "non_goal",
                evidence.IsAmbiguous("non_goal")),
            ToScalarOperation(
                evidence.ScalarValue("success_signal"),
                IsCorrectionForField(text, "success_signal"),
                IsRemovalForField(text, "success_signal"),
                evidence.IsAmbiguous("success_signal")),
            ToScalarOperation(
                evidence.ScalarValue("owner"),
                IsCorrectionForField(text, "owner"),
                IsRemovalForField(text, "owner"),
                evidence.IsAmbiguous("owner")),
            ToScalarOperation(
                evidence.ScalarValue("review_gate"),
                IsCorrectionForField(text, "review_gate"),
                IsRemovalForField(text, "review_gate"),
                evidence.IsAmbiguous("review_gate")),
            ToListOperation(
                existingCandidate?.Risks,
                evidence.ListValues("risk"),
                text,
                isRemoval,
                "risk",
                evidence.IsAmbiguous("risk")));
    }

    internal static IReadOnlyList<string> BuildFieldOperationAmbiguities(string text)
    {
        var clauses = SplitClauses(text);
        return CollectFieldEvidence(clauses, allowTopicFallback: true, allowAmbientScope: true).Ambiguities;
    }

    private static RuntimeGuidanceCollectedFieldEvidence CollectFieldEvidence(
        IReadOnlyList<string> clauses,
        bool allowTopicFallback,
        bool allowAmbientScope)
    {
        var metaFieldReferenceClauseIndexes = DetectMetaFieldReferenceClauseIndexes(clauses);
        var topics = CollectTopicEvidence(clauses, allowTopicFallback, metaFieldReferenceClauseIndexes);
        var desiredOutcomes = CollectScalarEvidence(
            clauses,
            "desired_outcome",
            clause => !IsTopicCreationClause(clause)
                && !IsConstraintClause(clause)
                && !IsRiskClause(clause)
                && IsDesiredOutcomeClause(clause),
            RuntimeGuidanceFieldEvidenceKinds.ExplicitField,
            metaFieldReferenceClauseIndexes);
        var scopes = CollectScopeEvidence(clauses, allowAmbientScope, metaFieldReferenceClauseIndexes);
        var constraints = ExtractList(clauses, "constraint", IsConstraintClause);
        var nonGoals = ExtractList(clauses, "non_goal", IsNonGoalClause, metaFieldReferenceClauseIndexes);
        var successSignals = CollectScalarEvidence(clauses, "success_signal", IsSuccessClause, RuntimeGuidanceFieldEvidenceKinds.ExplicitField, metaFieldReferenceClauseIndexes);
        var owners = CollectScalarEvidence(clauses, "owner", IsOwnerClause, RuntimeGuidanceFieldEvidenceKinds.ExplicitField, metaFieldReferenceClauseIndexes);
        var reviewGates = CollectScalarEvidence(clauses, "review_gate", IsReviewGateClause, RuntimeGuidanceFieldEvidenceKinds.ExplicitField, metaFieldReferenceClauseIndexes);
        var risks = ExtractList(clauses, "risk", IsRiskClause, metaFieldReferenceClauseIndexes);
        var ambiguities = new List<string>(9);
        AddAmbiguityIfNeeded(ambiguities, "topic", topics, clauses, allowMultipleValues: false);
        AddAmbiguityIfNeeded(ambiguities, "desired_outcome", desiredOutcomes, clauses, allowMultipleValues: false);
        AddAmbiguityIfNeeded(ambiguities, "scope", scopes, clauses, allowMultipleValues: false);
        AddAmbiguityIfNeeded(ambiguities, "constraint", constraints, clauses, allowMultipleValues: true);
        AddAmbiguityIfNeeded(ambiguities, "non_goal", nonGoals, clauses, allowMultipleValues: true);
        AddAmbiguityIfNeeded(ambiguities, "success_signal", successSignals, clauses, allowMultipleValues: false);
        AddAmbiguityIfNeeded(ambiguities, "owner", owners, clauses, allowMultipleValues: false);
        AddAmbiguityIfNeeded(ambiguities, "review_gate", reviewGates, clauses, allowMultipleValues: false);
        AddAmbiguityIfNeeded(ambiguities, "risk", risks, clauses, allowMultipleValues: true);

        return new RuntimeGuidanceCollectedFieldEvidence(
            topics,
            desiredOutcomes,
            scopes,
            constraints,
            nonGoals,
            successSignals,
            owners,
            reviewGates,
            risks,
            ambiguities.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlySet<int> DetectMetaFieldReferenceClauseIndexes(IReadOnlyList<string> clauses)
    {
        var indexes = new HashSet<int>();
        for (var startIndex = 0; startIndex < clauses.Count; startIndex++)
        {
            var window = new StringBuilder();
            var maxEndIndex = Math.Min(clauses.Count, startIndex + 4);
            for (var endIndex = startIndex; endIndex < maxEndIndex; endIndex++)
            {
                if (window.Length > 0)
                {
                    window.Append(' ');
                }

                window.Append(clauses[endIndex]);
                if (!IsMetaFieldReferenceText(window.ToString()))
                {
                    continue;
                }

                for (var index = startIndex; index <= endIndex; index++)
                {
                    indexes.Add(index);
                }
            }
        }

        return indexes;
    }

    private static bool IsMetaFieldReferenceText(string text)
    {
        var normalized = text.ToLowerInvariant();
        return !ContainsExplicitFieldAssignmentMarker(normalized)
            && CountMetaFieldReferences(normalized) >= 2
            && (HasMetaCompletenessSignal(normalized) || HasMetaConfirmationSignal(normalized));
    }

    private static bool ContainsExplicitFieldAssignmentMarker(string normalizedText)
    {
        return InlineFieldMarkers.Any(marker => normalizedText.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountMetaFieldReferences(string normalizedText)
    {
        var count = 0;
        count += ContainsAny(normalizedText, "目标", "goal", "outcome") ? 1 : 0;
        count += ContainsAny(normalizedText, "范围", "边界", "scope", "boundary") ? 1 : 0;
        count += ContainsAny(normalizedText, "验收", "成功标准", "acceptance", "success signal") ? 1 : 0;
        count += ContainsAny(normalizedText, "约束", "constraint") ? 1 : 0;
        count += ContainsAny(normalizedText, "风险", "risk") ? 1 : 0;
        count += ContainsAny(normalizedText, "负责人", "跟进人", "owner", "assignee") ? 1 : 0;
        count += ContainsAny(normalizedText, "复核点", "复核门槛", "复核条件", "review gate", "review checkpoint") ? 1 : 0;
        return count;
    }

    private static bool HasMetaCompletenessSignal(string normalizedText)
    {
        return ContainsAny(
            normalizedText,
            "说清楚",
            "讲清楚",
            "信息够",
            "信息足够",
            "字段满足",
            "字段清楚",
            "enough",
            "clear enough",
            "fields are clear",
            "fields are complete",
            "has enough");
    }

    private static bool HasMetaConfirmationSignal(string normalizedText)
    {
        return ContainsAny(
            normalizedText,
            "提醒我确认",
            "提示我确认",
            "确认要不要",
            "确认是否",
            "用户确认",
            "人工确认",
            "ask for confirmation",
            "asks for confirmation",
            "ask me to confirm",
            "asks me to confirm",
            "user confirmation",
            "human confirmation",
            "confirm before");
    }

    private static IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> CollectTopicEvidence(
        IReadOnlyList<string> clauses,
        bool allowFallback,
        IReadOnlySet<int> excludedClauseIndexes)
    {
        var explicitTopics = CollectScalarEvidence(
            clauses,
            "topic",
            IsExplicitTopicClause,
            RuntimeGuidanceFieldEvidenceKinds.ExplicitField,
            excludedClauseIndexes);
        if (explicitTopics.Count > 0)
        {
            var alternatives = HasAmbiguousTopicChoice(clauses)
                ? CollectScalarEvidence(
                    clauses,
                    "topic",
                    IsAmbiguousTopicAlternativeClause,
                    RuntimeGuidanceFieldEvidenceKinds.CandidateAlternative,
                    excludedClauseIndexes)
                : Array.Empty<RuntimeGuidanceFieldEvidenceItem>();
            return explicitTopics
                .Concat(alternatives)
                .DistinctBy(item => item.Value, StringComparer.Ordinal)
                .Take(MaxListItems)
                .ToArray();
        }

        if (!allowFallback)
        {
            return explicitTopics;
        }

        return CollectScalarEvidence(
            clauses,
            "topic",
            IsFallbackTopicClause,
            RuntimeGuidanceFieldEvidenceKinds.AmbientTopic,
            excludedClauseIndexes);
    }

    private static IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> CollectScopeEvidence(
        IReadOnlyList<string> clauses,
        bool allowAmbient,
        IReadOnlySet<int> excludedClauseIndexes)
    {
        var explicitScopes = CollectScalarEvidence(
            clauses,
            "scope",
            IsScopeClause,
            RuntimeGuidanceFieldEvidenceKinds.ExplicitField,
            excludedClauseIndexes);
        if (explicitScopes.Count > 0 || !allowAmbient)
        {
            return explicitScopes;
        }

        return CollectScalarEvidence(
            clauses,
            "scope",
            clause =>
            {
                var text = clause.ToLowerInvariant();
                return !IsNonGoalClause(clause)
                    && ContainsAny(text, "runtime", "session gateway", "聊天", "系统", "功能");
            },
            RuntimeGuidanceFieldEvidenceKinds.AmbientScope,
            excludedClauseIndexes)
            .Take(1)
            .ToArray();
    }

    private static bool IsExplicitTopicClause(string clause)
    {
        if (IsNonTopicFieldClause(clause))
        {
            return false;
        }

        var text = clause.ToLowerInvariant();
        return ContainsAny(
            text,
            "功能方向",
            "项目方向",
            "另一个方向",
            "新方向",
            "主题",
            "topic",
            "feature direction",
            "project direction",
            "another direction",
            "new direction");
    }

    private static bool IsFallbackTopicClause(string clause)
    {
        return !IsNonGoalClause(clause)
            && !IsConstraintClause(clause)
            && !IsRiskClause(clause)
            && !IsSuccessClause(clause)
            && !IsOwnerClause(clause)
            && !IsReviewGateClause(clause)
            && !IsScopeClause(clause)
            && !IsLifecycleInstructionClause(clause)
            && (!IsDesiredOutcomeClause(clause) || IsTopicCreationClause(clause));
    }

    private static bool IsAmbiguousTopicAlternativeClause(string clause)
    {
        if (IsExplicitTopicClause(clause)
            || IsNonGoalClause(clause)
            || IsConstraintClause(clause)
            || IsRiskClause(clause)
            || IsSuccessClause(clause)
            || IsOwnerClause(clause)
            || IsReviewGateClause(clause)
            || IsScopeClause(clause)
            || IsLifecycleInstructionClause(clause)
            || (IsDesiredOutcomeClause(clause) && !IsTopicCreationClause(clause)))
        {
            return false;
        }

        var text = clause.ToLowerInvariant();
        return IsAmbiguousOrUncertainClause(text)
            && ContainsAny(text, "功能", "项目", "系统", "方向", "feature", "project", "system", "direction");
    }

    private static bool IsNonTopicFieldClause(string clause)
    {
        return IsNonGoalClause(clause)
            || IsConstraintClause(clause)
            || IsRiskClause(clause)
            || IsDesiredOutcomeClause(clause)
            || IsSuccessClause(clause)
            || IsOwnerClause(clause)
            || IsReviewGateClause(clause)
            || IsScopeClause(clause);
    }

    private static bool IsLifecycleInstructionClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(
            text,
            "落案",
            "平铺计划",
            "提交方案",
            "提交计划",
            "提交这个",
            "并提交",
            "开始执行",
            "直接执行",
            "执行 phase",
            "执行phase",
            "推进 phase",
            "推进phase",
            "approve",
            "handoff",
            "land it",
            "land this",
            "planning intake",
            "submit this",
            "submit the plan",
            "execute phase",
            "start execution",
            "run phase");
    }

    private static bool IsTopicCreationClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(text, "我想", "我希望", "希望", "需要", "i want", "we need", "want to", "need a")
            && ContainsAny(text, "carves", "runtime", "引导", "整理", "方向", "功能", "系统", "feature", "project", "system", "guidance");
    }

    private static bool IsScopeClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return !IsNonGoalClause(clause) && ContainsAny(text, "范围", "scope");
    }

    private static IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> CollectScalarEvidence(
        IReadOnlyList<string> clauses,
        string field,
        Func<string, bool> predicate,
        string evidenceKind,
        IReadOnlySet<int>? excludedClauseIndexes = null)
    {
        return clauses
            .Select((clause, index) => new { Clause = clause, Index = index })
            .Where(item => excludedClauseIndexes is null || !excludedClauseIndexes.Contains(item.Index))
            .Where(item => predicate(item.Clause))
            .Select(item => BuildEvidenceItem(field, item.Clause, item.Index, ResolveEvidenceKind(field, item.Clause, evidenceKind)))
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .DistinctBy(item => item.Value, StringComparer.Ordinal)
            .Take(MaxListItems)
            .ToArray();
    }

    private static RuntimeGuidanceFieldEvidenceItem BuildEvidenceItem(
        string field,
        string clause,
        int clauseIndex,
        string evidenceKind)
    {
        var compact = CompactText(clause);
        if (string.Equals(field, "constraint", StringComparison.Ordinal))
        {
            compact = CompactConstraintText(compact);
        }

        var normalizedClause = clause.ToLowerInvariant();
        var negated = string.Equals(field, "non_goal", StringComparison.Ordinal)
            || IsNegativeScopeClause(normalizedClause);
        var ambiguous = IsAmbiguousOrUncertainClause(normalizedClause);
        var candidateGroup = ResolveCandidateGroup(normalizedClause);
        return new RuntimeGuidanceFieldEvidenceItem(
            field,
            compact,
            clause,
            $"clause:{clauseIndex + 1:00}",
            evidenceKind,
            ResolveEvidenceConfidence(evidenceKind, ambiguous),
            negated,
            ambiguous,
            candidateGroup);
    }

    private static string ResolveEvidenceKind(string field, string clause, string defaultKind)
    {
        var normalizedClause = clause.ToLowerInvariant();
        if (string.Equals(field, "non_goal", StringComparison.Ordinal)
            && IsNegativeScopeClause(normalizedClause))
        {
            return RuntimeGuidanceFieldEvidenceKinds.NegativeScope;
        }

        if (ContainsAny(normalizedClause, "另一个方向", "another direction", "new direction")
            || string.Equals(defaultKind, RuntimeGuidanceFieldEvidenceKinds.CandidateAlternative, StringComparison.Ordinal))
        {
            return RuntimeGuidanceFieldEvidenceKinds.CandidateAlternative;
        }

        return defaultKind;
    }

    private static string ResolveCandidateGroup(string normalizedClause)
    {
        return ContainsAny(normalizedClause, "另一个方向", "another direction", "new direction")
            ? RuntimeGuidanceFieldEvidenceCandidateGroups.Alternative
            : RuntimeGuidanceFieldEvidenceCandidateGroups.Primary;
    }

    private static double ResolveEvidenceConfidence(string evidenceKind, bool ambiguous)
    {
        var baseConfidence = evidenceKind switch
        {
            RuntimeGuidanceFieldEvidenceKinds.ExplicitField => 0.86d,
            RuntimeGuidanceFieldEvidenceKinds.NegativeScope => 0.84d,
            RuntimeGuidanceFieldEvidenceKinds.ListField => 0.8d,
            RuntimeGuidanceFieldEvidenceKinds.CandidateAlternative => 0.72d,
            RuntimeGuidanceFieldEvidenceKinds.AmbientTopic => 0.62d,
            RuntimeGuidanceFieldEvidenceKinds.AmbientScope => 0.6d,
            _ => 0.55d,
        };
        return ambiguous ? Math.Max(0.4d, baseConfidence - 0.18d) : baseConfidence;
    }

    private static void AddAmbiguityIfNeeded(
        ICollection<string> ambiguities,
        string fieldKind,
        IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> evidence,
        IReadOnlyList<string> clauses,
        bool allowMultipleValues)
    {
        var values = evidence.Select(item => item.Value).ToArray();
        if ((!allowMultipleValues && HasConflictingEvidence(values))
            || evidence.Any(item => item.Ambiguous)
            || HasAmbiguousOrUncertainFieldText(clauses, fieldKind))
        {
            ambiguities.Add(fieldKind);
        }
    }

    private static bool HasConflictingEvidence(IReadOnlyList<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .Count() > 1;
    }

    private static RuntimeGuidanceScalarFieldOperation ToScalarOperation(
        string next,
        bool isCorrectionForField,
        bool isRemovalForField,
        bool preserveAmbiguousText)
    {
        if (preserveAmbiguousText)
        {
            return RuntimeGuidanceScalarFieldOperation.Preserve();
        }

        if (isRemovalForField)
        {
            return RuntimeGuidanceScalarFieldOperation.Clear();
        }

        if (isCorrectionForField)
        {
            return string.IsNullOrWhiteSpace(next)
                ? RuntimeGuidanceScalarFieldOperation.Clear()
                : RuntimeGuidanceScalarFieldOperation.Set(next);
        }

        return string.IsNullOrWhiteSpace(next)
            ? RuntimeGuidanceScalarFieldOperation.Preserve()
            : RuntimeGuidanceScalarFieldOperation.Set(next);
    }

    private static RuntimeGuidanceListFieldOperation ToListOperation(
        IReadOnlyList<string>? existing,
        IReadOnlyList<string> next,
        string userText,
        bool isRemoval,
        string fieldKind,
        bool preserveAmbiguousText)
    {
        if (preserveAmbiguousText)
        {
            return RuntimeGuidanceListFieldOperation.Preserve();
        }

        if (isRemoval && IsRemovalForField(userText, fieldKind))
        {
            return RuntimeGuidanceListFieldOperation.Clear(RemoveMentionedValues(existing ?? Array.Empty<string>(), userText));
        }

        if (IsCorrectionForField(userText, fieldKind))
        {
            return next.Count == 0
                ? RuntimeGuidanceListFieldOperation.Clear(Array.Empty<string>())
                : RuntimeGuidanceListFieldOperation.Set(next);
        }

        return next.Count == 0
            ? RuntimeGuidanceListFieldOperation.Preserve()
            : RuntimeGuidanceListFieldOperation.Append(next);
    }

    private static bool HasAmbiguousOrUncertainFieldText(IReadOnlyList<string> clauses, string fieldKind)
    {
        if (HasAmbiguousTopicChoice(clauses)
            && string.Equals(fieldKind, "topic", StringComparison.Ordinal))
        {
            return true;
        }

        return clauses.Any(clause =>
        {
            var text = clause.ToLowerInvariant();
            return IsAmbiguousOrUncertainClause(text) && MentionsFieldKind(text, fieldKind);
        });
    }

    private static bool HasAmbiguousTopicChoice(IReadOnlyList<string> clauses)
    {
        var explicitTopicClauses = clauses.Where(IsExplicitTopicClause).ToArray();
        if (explicitTopicClauses.Length > 1
            && clauses.Any(clause => IsAmbiguousOrUncertainClause(clause.ToLowerInvariant())))
        {
            return true;
        }

        return explicitTopicClauses.Length > 0
            && clauses.Any(IsAmbiguousTopicAlternativeClause);
    }

    private static bool IsAmbiguousOrUncertainClause(string text)
    {
        return ContainsAny(text, "或者", "还是", "也想", "另一个", "另外", "可能", "不确定", "先看", " or ", "also", "another", "maybe", "not sure");
    }

    private static bool IsRemovalForField(string userText, string fieldKind)
    {
        return SplitClauses(userText)
            .Any(clause => IsRemovalText(clause) && MentionsFieldKind(clause, fieldKind));
    }

    private static bool IsCorrectionForField(string userText, string fieldKind)
    {
        return SplitClauses(userText)
            .Any(clause => IsCorrectionText(clause)
                && MentionsFieldKind(clause, fieldKind)
                && !IsNegativeScopeClause(clause.ToLowerInvariant()));
    }

    internal static bool ShouldResetCandidate(string text, RuntimeGuidanceCandidate? existingCandidate)
    {
        if (existingCandidate is null)
        {
            return false;
        }

        var normalized = text.ToLowerInvariant();
        return ContainsAny(
            normalized,
            "换一个方向",
            "换个方向",
            "换一个项目",
            "换个项目",
            "另一个方向",
            "另一个项目",
            "新的方向",
            "新方向",
            "新项目",
            "重新开始",
            "改聊",
            "new topic",
            "new direction",
            "different topic",
            "different direction",
            "different project",
            "switch topic",
            "switch direction");
    }

    private static bool IsCorrectionText(string text)
    {
        var normalized = text.ToLowerInvariant();
        return ContainsAny(
            normalized,
            "修正",
            "改成",
            "改为",
            "更新为",
            "不是",
            "应该是",
            "correction",
            "correct",
            "replace",
            "change to",
            "should be",
            "instead");
    }

    private static bool IsRemovalText(string text)
    {
        var normalized = text.ToLowerInvariant();
        return ContainsAny(
            normalized,
            "去掉",
            "移除",
            "删除",
            "不再包含",
            "不用包含",
            "不要包含",
            "取消",
            "remove",
            "drop",
            "delete",
            "no longer include",
            "do not include");
    }

    private static string NormalizeText(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Trim();
    }

    private static string[] SplitClauses(string text)
    {
        var clauses = text.Split(ClauseSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(clause => clause.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .SelectMany(ExpandInlineFieldClauses)
            .Where(clause => !string.IsNullOrWhiteSpace(clause))
            .ToArray();

        return clauses
            .Where(IsGuidanceRelevantClause)
            .Concat(clauses.Where(clause => !IsGuidanceRelevantClause(clause)))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxClauseCount)
            .ToArray();
    }

    private static IReadOnlyList<string> ExpandInlineFieldClauses(string clause)
    {
        var matches = InlineFieldMarkers
            .Select(marker => new InlineFieldMarkerMatch(
                clause.IndexOf(marker, StringComparison.OrdinalIgnoreCase),
                marker.Length))
            .Where(match => match.Index >= 0)
            .GroupBy(match => match.Index)
            .Select(group => group.OrderByDescending(match => match.Length).First())
            .OrderBy(match => match.Index)
            .ToArray();
        if (matches.Length <= 1)
        {
            return [clause];
        }

        var starts = new List<int>();
        foreach (var match in matches)
        {
            if (starts.Count > 0)
            {
                var previous = matches.First(item => item.Index == starts[^1]);
                if (match.Index < previous.Index + previous.Length)
                {
                    continue;
                }
            }

            starts.Add(match.Index);
        }

        if (starts.Count <= 1)
        {
            return [clause];
        }

        var fragments = new List<string>();
        if (starts[0] > 0)
        {
            fragments.Add(clause[..starts[0]].Trim());
        }

        for (var index = 0; index < starts.Count; index++)
        {
            var start = starts[index];
            var end = index + 1 < starts.Count ? starts[index + 1] : clause.Length;
            fragments.Add(clause[start..end].Trim());
        }

        return fragments
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment))
            .ToArray();
    }

    private sealed record InlineFieldMarkerMatch(int Index, int Length);

    private static bool IsGuidanceRelevantClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(
                text,
                "目标",
                "为了",
                "形成",
                "帮助",
                "希望",
                "范围",
                "成功",
                "验收",
                "负责人",
                "跟进人",
                "复核点",
                "复核门槛",
                "复核条件",
                "标准",
                "约束",
                "不能",
                "不要",
                "禁止",
                "不得",
                "风险",
                "担心",
                "我怕",
                "功能方向",
                "项目方向",
                "goal",
                "outcome",
                "i want to build",
                "we need a",
                "scope",
                "success",
                "acceptance",
                "ready when",
                "owner",
                "assignee",
                "review gate",
                "review checkpoint",
                "constraint",
                "must not",
                "cannot",
                "risk",
                "concern")
            || IsCorrectionText(text)
            || IsRemovalText(text)
            || ContainsAny(
                text,
                "换一个方向",
                "换个方向",
                "另一个方向",
                "新方向",
                "new topic",
                "new direction",
                "different direction");
    }

    private static bool IsConstraintClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return !IsChoiceQuestionClause(text)
            && ContainsAny(text, "约束", "不能", "不要", "禁止", "不得", "must not", "do not", "cannot", "without", "constraint");
    }

    private static bool IsChoiceQuestionClause(string text)
    {
        return ContainsAny(
            text,
            "要不要",
            "需不需要",
            "是否",
            "whether",
            "should we",
            "should it");
    }

    private static bool IsNonGoalClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(
            text,
            "非目标",
            "不做",
            "不纳入",
            "不包括",
            "不需要做",
            "不应该做",
            "non-goal",
            "non goal",
            "out of scope",
            "not in scope",
            "will not include")
            || IsNegativeScopeClause(text);
    }

    private static bool IsNegativeScopeClause(string text)
    {
        if (HasPositiveReplacement(text))
        {
            return false;
        }

        return ContainsAny(
            text,
            "范围不是",
            "范围不包括",
            "范围不包含",
            "scope is not",
            "scope isn't",
            "scope does not include",
            "scope doesn't include");
    }

    private static bool HasPositiveReplacement(string text)
    {
        return ContainsAny(text, "而是", "instead", "should be");
    }

    private static bool IsDesiredOutcomeClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return !IsNonGoalClause(clause)
            && !IsOwnerClause(clause)
            && !IsReviewGateClause(clause)
            && ContainsAny(text, "目标", "为了", "形成", "帮助", "完成", "希望", "goal", "outcome", "so that", "help");
    }

    private static bool IsOwnerClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(text, "负责人", "跟进人", "owner", "assignee");
    }

    private static bool IsReviewGateClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(text, "复核点", "复核门槛", "复核条件", "review gate", "review checkpoint");
    }

    private static bool IsRiskClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(text, "风险", "担心", "我怕", "污染", "误触发", "爆炸", "risk", "concern");
    }

    private static bool IsSuccessClause(string clause)
    {
        var text = clause.ToLowerInvariant();
        return ContainsAny(text, "成功", "验收", "success", "acceptance", "ready when");
    }

    private static IReadOnlyList<RuntimeGuidanceFieldEvidenceItem> ExtractList(
        IReadOnlyList<string> clauses,
        string field,
        Func<string, bool> predicate,
        IReadOnlySet<int>? excludedClauseIndexes = null)
    {
        return CollectScalarEvidence(
            clauses,
            field,
            predicate,
            RuntimeGuidanceFieldEvidenceKinds.ListField,
            excludedClauseIndexes);
    }

    private static bool MentionsFieldKind(string userText, string fieldKind)
    {
        var text = userText.ToLowerInvariant();
        return fieldKind switch
        {
            "topic" => ContainsAny(text, "功能方向", "项目方向", "主题", "topic", "feature direction", "project direction"),
            "desired_outcome" => !IsNonGoalClause(text) && ContainsAny(text, "目标", "希望", "outcome", "goal"),
            "scope" => !IsNonGoalClause(text) && ContainsAny(text, "范围", "scope"),
            "success_signal" => IsSuccessClause(text),
            "owner" => IsOwnerClause(text),
            "review_gate" => IsReviewGateClause(text),
            "constraint" => ContainsAny(text, "约束", "constraint", "must not", "cannot", "不能"),
            "non_goal" => IsNonGoalClause(text),
            "risk" => ContainsAny(text, "风险", "risk", "concern"),
            _ => false,
        };
    }

    private static IReadOnlyList<string> RemoveMentionedValues(IReadOnlyList<string> existing, string userText)
    {
        var normalizedText = NormalizeForMatching(userText);
        return existing
            .Where(value =>
            {
                var normalizedValue = NormalizeForMatching(value);
                return normalizedValue.Length == 0
                    || (!normalizedText.Contains(normalizedValue, StringComparison.Ordinal)
                        && !TokenOverlap(normalizedText, normalizedValue));
            })
            .Take(MaxListItems)
            .ToArray();
    }

    private static bool TokenOverlap(string normalizedText, string normalizedValue)
    {
        var tokens = normalizedValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .ToArray();
        return tokens.Length > 0 && tokens.Any(token => normalizedText.Contains(token, StringComparison.Ordinal));
    }

    private static string CompactText(string text)
    {
        var compact = text.Trim();
        var prefixes = new[]
        {
            "换一个方向：",
            "换一个方向:",
            "换个方向：",
            "换个方向:",
            "另一个方向是",
            "另一个方向：",
            "另一个方向:",
            "新方向：",
            "新方向:",
            "项目方向是",
            "项目方向:",
            "项目方向：",
            "项目方向",
            "功能方向是",
            "功能方向:",
            "功能方向：",
            "功能方向",
            "topic is",
            "topic:",
            "project direction is",
            "project direction:",
            "feature direction is",
            "feature direction:",
            "修正：",
            "修正:",
            "修正",
            "改成",
            "改为",
            "更新为",
            "我想让",
            "我想",
            "我希望",
            "希望",
            "需要",
            "i want to build",
            "we need a",
            "goal is",
            "goal should be",
            "goal:",
            "outcome is",
            "outcome should be",
            "outcome:",
            "owner is",
            "owner:",
            "assignee is",
            "assignee:",
            "负责人是",
            "负责人",
            "跟进人是",
            "跟进人",
            "review gate is",
            "review gate:",
            "review checkpoint is",
            "review checkpoint:",
            "复核点是",
            "复核点",
            "复核门槛是",
            "复核门槛",
            "复核条件是",
            "复核条件",
            "目标改成",
            "目标改为",
            "目标应该是",
            "目标可能是",
            "目标是",
            "目标",
            "scope is not",
            "scope isn't",
            "scope does not include",
            "scope doesn't include",
            "scope is",
            "scope should be",
            "scope:",
            "范围改成",
            "范围改为",
            "范围不是",
            "范围不包括",
            "范围不包含",
            "范围可能是",
            "范围是",
            "范围",
            "constraint is",
            "constraint:",
            "约束改成",
            "约束改为",
            "约束是",
            "约束",
            "非目标是",
            "非目标:",
            "非目标：",
            "非目标",
            "不纳入范围是",
            "不纳入范围:",
            "不纳入范围：",
            "不纳入范围",
            "不包括范围是",
            "不包括范围:",
            "不包括范围：",
            "不包括范围",
            "non-goal is",
            "non-goal:",
            "non goal is",
            "non goal:",
            "out of scope is",
            "out of scope:",
            "not in scope is",
            "not in scope:",
            "risk is",
            "risk:",
            "风险改成",
            "风险改为",
            "风险是",
            "风险",
            "success means",
            "ready when",
            "acceptance is",
            "acceptance should be",
            "acceptance:",
            "成功标准是",
            "成功标准",
            "验收是",
            "验收",
        };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var prefix in prefixes)
            {
                if (compact.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    compact = compact[prefix.Length..].Trim([' ', ':', '：']);
                    changed = true;
                    break;
                }
            }
        }

        var positiveCorrectionIndex = compact.IndexOf("而是", StringComparison.OrdinalIgnoreCase);
        if (positiveCorrectionIndex >= 0)
        {
            compact = compact[(positiveCorrectionIndex + "而是".Length)..].Trim([' ', ':', '：']);
        }

        var englishCorrectionIndex = compact.IndexOf("instead", StringComparison.OrdinalIgnoreCase);
        if (englishCorrectionIndex >= 0)
        {
            compact = compact[(englishCorrectionIndex + "instead".Length)..].Trim([' ', ':', '：']);
        }

        if (compact.Length > MaxTextLength)
        {
            compact = compact[..MaxTextLength].Trim();
        }

        return compact;
    }

    private static string CompactConstraintText(string text)
    {
        var compact = text.Trim();
        var markers = new[]
        {
            "不能",
            "不要",
            "禁止",
            "不得",
            "must not",
            "cannot",
            "do not",
            "without",
        };
        var markerIndex = markers
            .Select(marker => new
            {
                Marker = marker,
                Index = compact.IndexOf(marker, StringComparison.OrdinalIgnoreCase),
            })
            .Where(match => match.Index >= 0)
            .OrderBy(match => match.Index)
            .FirstOrDefault();
        if (markerIndex is null || markerIndex.Index <= 0)
        {
            return compact;
        }

        return compact[markerIndex.Index..].Trim([' ', ':', '：', ',', '，', '。', ';', '；', '而']);
    }

    private static string NormalizeForMatching(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return string.Join(
            ' ',
            builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }
}

internal enum RuntimeGuidanceFieldOperationKind
{
    Preserve,
    Set,
    Append,
    Clear,
}

internal sealed record RuntimeGuidanceScalarFieldOperation(RuntimeGuidanceFieldOperationKind Kind, string? Value = null)
{
    public static RuntimeGuidanceScalarFieldOperation Preserve() => new(RuntimeGuidanceFieldOperationKind.Preserve);

    public static RuntimeGuidanceScalarFieldOperation Set(string value) => new(RuntimeGuidanceFieldOperationKind.Set, value.Trim());

    public static RuntimeGuidanceScalarFieldOperation Clear() => new(RuntimeGuidanceFieldOperationKind.Clear);
}

internal sealed record RuntimeGuidanceListFieldOperation(RuntimeGuidanceFieldOperationKind Kind, IReadOnlyList<string> Values)
{
    public static RuntimeGuidanceListFieldOperation Preserve() => new(RuntimeGuidanceFieldOperationKind.Preserve, Array.Empty<string>());

    public static RuntimeGuidanceListFieldOperation Set(IReadOnlyList<string> values) => new(RuntimeGuidanceFieldOperationKind.Set, values);

    public static RuntimeGuidanceListFieldOperation Append(IReadOnlyList<string> values) => new(RuntimeGuidanceFieldOperationKind.Append, values);

    public static RuntimeGuidanceListFieldOperation Clear(IReadOnlyList<string> preservedValues) => new(RuntimeGuidanceFieldOperationKind.Clear, preservedValues);
}

internal sealed record RuntimeGuidanceFieldOperationSet(
    RuntimeGuidanceScalarFieldOperation Topic,
    RuntimeGuidanceScalarFieldOperation DesiredOutcome,
    RuntimeGuidanceScalarFieldOperation Scope,
    RuntimeGuidanceListFieldOperation Constraints,
    RuntimeGuidanceListFieldOperation NonGoals,
    RuntimeGuidanceScalarFieldOperation SuccessSignal,
    RuntimeGuidanceScalarFieldOperation Owner,
    RuntimeGuidanceScalarFieldOperation ReviewGate,
    RuntimeGuidanceListFieldOperation Risks)
{
    public static RuntimeGuidanceFieldOperationSet Empty { get; } = new(
        RuntimeGuidanceScalarFieldOperation.Preserve(),
        RuntimeGuidanceScalarFieldOperation.Preserve(),
        RuntimeGuidanceScalarFieldOperation.Preserve(),
        RuntimeGuidanceListFieldOperation.Preserve(),
        RuntimeGuidanceListFieldOperation.Preserve(),
        RuntimeGuidanceScalarFieldOperation.Preserve(),
        RuntimeGuidanceScalarFieldOperation.Preserve(),
        RuntimeGuidanceScalarFieldOperation.Preserve(),
        RuntimeGuidanceListFieldOperation.Preserve());
}
