using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Infrastructure.Persistence;

internal sealed class TaskGraphDocument
{
    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    [JsonPropertyName("tasks")]
    public List<TaskSummaryDocument> Tasks { get; init; } = [];

    [JsonPropertyName("cards")]
    public List<string> Cards { get; init; } = [];
}

internal sealed class TaskSummaryDocument
{
    [JsonPropertyName("task_id")]
    public string? TaskId { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    [JsonPropertyName("card_id")]
    public string? CardId { get; init; }

    [JsonPropertyName("dependencies")]
    public string[]? Dependencies { get; init; }

    [JsonPropertyName("node_file")]
    public string? NodeFile { get; init; }
}

internal sealed class TaskNodeDocument
{
    [JsonPropertyName("schema_version")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("task_type")]
    public string? TaskType { get; init; }

    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("card_id")]
    public string? CardId { get; init; }

    [JsonPropertyName("proposal_source")]
    public string? ProposalSource { get; init; }

    [JsonPropertyName("proposal_reason")]
    public string? ProposalReason { get; init; }

    [JsonPropertyName("proposal_confidence")]
    public double? ProposalConfidence { get; init; }

    [JsonPropertyName("proposal_priority_hint")]
    public string? ProposalPriorityHint { get; init; }

    [JsonPropertyName("base_commit")]
    public string? BaseCommit { get; init; }

    [JsonPropertyName("result_commit")]
    public string? ResultCommit { get; init; }

    [JsonPropertyName("dependencies")]
    public string[]? Dependencies { get; init; }

    [JsonPropertyName("scope")]
    public string[]? Scope { get; init; }

    [JsonPropertyName("acceptance")]
    public string[]? Acceptance { get; init; }

    [JsonPropertyName("constraints")]
    public string[]? Constraints { get; init; }

    [JsonPropertyName("acceptance_contract")]
    public AcceptanceContractDocument? AcceptanceContract { get; init; }

    [JsonPropertyName("validation")]
    public ValidationPlanDocument? Validation { get; init; }

    [JsonPropertyName("retry_count")]
    public int RetryCount { get; init; }

    [JsonPropertyName("capabilities")]
    public string[]? Capabilities { get; init; }

    [JsonPropertyName("metadata")]
    [JsonConverter(typeof(LenientStringDictionaryJsonConverter))]
    public Dictionary<string, string>? Metadata { get; init; }

    [JsonPropertyName("last_worker_run_id")]
    public string? LastWorkerRunId { get; init; }

    [JsonPropertyName("last_worker_backend")]
    public string? LastWorkerBackend { get; init; }

    [JsonPropertyName("last_worker_failure_kind")]
    public string? LastWorkerFailureKind { get; init; }

    [JsonPropertyName("last_worker_retryable")]
    [JsonConverter(typeof(LenientBooleanJsonConverter))]
    public bool LastWorkerRetryable { get; init; }

    [JsonPropertyName("last_worker_summary")]
    public string? LastWorkerSummary { get; init; }

    [JsonPropertyName("last_worker_detail_ref")]
    public string? LastWorkerDetailRef { get; init; }

    [JsonPropertyName("last_provider_detail_ref")]
    public string? LastProviderDetailRef { get; init; }

    [JsonPropertyName("last_recovery_action")]
    public string? LastRecoveryAction { get; init; }

    [JsonPropertyName("last_recovery_reason")]
    public string? LastRecoveryReason { get; init; }

    [JsonPropertyName("retry_not_before")]
    public string? RetryNotBefore { get; init; }

    [JsonPropertyName("planner_review")]
    public PlannerReviewDocument? PlannerReview { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

internal sealed class LenientBooleanJsonConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.True)
        {
            return true;
        }

        if (reader.TokenType == JsonTokenType.False)
        {
            return false;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (bool.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        if (reader.TokenType == JsonTokenType.Null)
        {
            return false;
        }

        throw new JsonException("Expected boolean or boolean-like string value.");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }
}

internal sealed class LenientStringDictionaryJsonConverter : JsonConverter<Dictionary<string, string>?>
{
    public override Dictionary<string, string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected metadata object.");
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return metadata;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected metadata property name.");
            }

            var key = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Expected metadata value.");
            }

            var value = ReadValue(ref reader);
            if (!string.IsNullOrEmpty(key) && value is not null)
            {
                metadata[key] = value;
            }
        }

        throw new JsonException("Expected metadata object terminator.");
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach (var pair in value)
        {
            writer.WriteString(pair.Key, pair.Value);
        }

        writer.WriteEndObject();
    }

    private static string? ReadValue(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? string.Empty,
            JsonTokenType.Number => ReadRawJsonValue(ref reader),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => null,
            JsonTokenType.StartObject or JsonTokenType.StartArray => ReadRawJsonValue(ref reader),
            _ => throw new JsonException("Expected metadata value.")
        };
    }

    private static string ReadRawJsonValue(ref Utf8JsonReader reader)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }
}

internal sealed class ValidationPlanDocument
{
    [JsonPropertyName("commands")]
    public List<string[]> Commands { get; init; } = [];

    [JsonPropertyName("checks")]
    public string[]? Checks { get; init; }

    [JsonPropertyName("expected_evidence")]
    public string[]? ExpectedEvidence { get; init; }
}

internal sealed class AcceptanceContractDocument
{
    [JsonPropertyName("contract_id")]
    public string? ContractId { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("owner")]
    public string? Owner { get; init; }

    [JsonPropertyName("created_at_utc")]
    public string? CreatedAtUtc { get; init; }

    [JsonPropertyName("intent")]
    public AcceptanceContractIntentDocument? Intent { get; init; }

    [JsonPropertyName("acceptance_examples")]
    public AcceptanceContractExampleDocument[]? AcceptanceExamples { get; init; }

    [JsonPropertyName("checks")]
    public AcceptanceContractChecksDocument? Checks { get; init; }

    [JsonPropertyName("constraints")]
    public AcceptanceContractConstraintSetDocument? Constraints { get; init; }

    [JsonPropertyName("non_goals")]
    public string[]? NonGoals { get; init; }

    [JsonPropertyName("auto_complete_allowed")]
    public bool? AutoCompleteAllowed { get; init; }

    [JsonPropertyName("evidence_required")]
    public AcceptanceContractEvidenceRequirementDocument[]? EvidenceRequired { get; init; }

    [JsonPropertyName("human_review")]
    public AcceptanceContractHumanReviewPolicyDocument? HumanReview { get; init; }

    [JsonPropertyName("traceability")]
    public AcceptanceContractTraceabilityDocument? Traceability { get; init; }
}

internal sealed class AcceptanceContractIntentDocument
{
    [JsonPropertyName("goal")]
    public string? Goal { get; init; }

    [JsonPropertyName("business_value")]
    public string? BusinessValue { get; init; }
}

internal sealed class AcceptanceContractExampleDocument
{
    [JsonPropertyName("given")]
    public string? Given { get; init; }

    [JsonPropertyName("when")]
    public string? When { get; init; }

    [JsonPropertyName("then")]
    public string? Then { get; init; }
}

internal sealed class AcceptanceContractChecksDocument
{
    [JsonPropertyName("unit_tests")]
    public string[]? UnitTests { get; init; }

    [JsonPropertyName("integration_tests")]
    public string[]? IntegrationTests { get; init; }

    [JsonPropertyName("regression_tests")]
    public string[]? RegressionTests { get; init; }

    [JsonPropertyName("policy_checks")]
    public string[]? PolicyChecks { get; init; }

    [JsonPropertyName("additional_checks")]
    public string[]? AdditionalChecks { get; init; }
}

internal sealed class AcceptanceContractConstraintSetDocument
{
    [JsonPropertyName("must_not")]
    public string[]? MustNot { get; init; }

    [JsonPropertyName("architecture")]
    public string[]? Architecture { get; init; }

    [JsonPropertyName("scope_limit")]
    [JsonConverter(typeof(AcceptanceContractScopeLimitDocumentJsonConverter))]
    public AcceptanceContractScopeLimitDocument? ScopeLimit { get; init; }
}

internal sealed class AcceptanceContractScopeLimitDocument
{
    [JsonPropertyName("max_files_changed")]
    public int? MaxFilesChanged { get; init; }

    [JsonPropertyName("max_lines_changed")]
    public int? MaxLinesChanged { get; init; }
}

internal sealed class AcceptanceContractScopeLimitDocumentJsonConverter : JsonConverter<AcceptanceContractScopeLimitDocument>
{
    public override AcceptanceContractScopeLimitDocument? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            _ = reader.GetString();
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected scope_limit to be null, an object, or a legacy string value.");
        }

        int? maxFilesChanged = null;
        int? maxLinesChanged = null;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new AcceptanceContractScopeLimitDocument
                {
                    MaxFilesChanged = maxFilesChanged,
                    MaxLinesChanged = maxLinesChanged,
                };
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected scope_limit object property.");
            }

            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of scope_limit object.");
            }

            switch (propertyName)
            {
                case "max_files_changed":
                    maxFilesChanged = ReadNullableInt32(ref reader);
                    break;
                case "max_lines_changed":
                    maxLinesChanged = ReadNullableInt32(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unexpected end of scope_limit object.");
    }

    public override void Write(Utf8JsonWriter writer, AcceptanceContractScopeLimitDocument value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        WriteNullableInt32(writer, "max_files_changed", value.MaxFilesChanged);
        WriteNullableInt32(writer, "max_lines_changed", value.MaxLinesChanged);
        writer.WriteEndObject();
    }

    private static int? ReadNullableInt32(ref Utf8JsonReader reader)
    {
        return reader.TokenType == JsonTokenType.Null
            ? null
            : reader.GetInt32();
    }

    private static void WriteNullableInt32(Utf8JsonWriter writer, string propertyName, int? value)
    {
        writer.WritePropertyName(propertyName);
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value.Value);
    }
}

internal sealed class AcceptanceContractEvidenceRequirementDocument
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

internal sealed class AcceptanceContractHumanReviewPolicyDocument
{
    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("provisional_allowed")]
    public bool ProvisionalAllowed { get; init; }

    [JsonPropertyName("decisions")]
    public string[]? Decisions { get; init; }
}

internal sealed class AcceptanceContractTraceabilityDocument
{
    [JsonPropertyName("source_card_id")]
    public string? SourceCardId { get; init; }

    [JsonPropertyName("source_task_id")]
    public string? SourceTaskId { get; init; }

    [JsonPropertyName("derived_task_ids")]
    public string[]? DerivedTaskIds { get; init; }

    [JsonPropertyName("related_artifacts")]
    public string[]? RelatedArtifacts { get; init; }
}

internal sealed class PlannerReviewDocument
{
    [JsonPropertyName("verdict")]
    public string? Verdict { get; init; }

    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("decision_status")]
    public string? DecisionStatus { get; init; }

    [JsonPropertyName("acceptance_met")]
    public bool AcceptanceMet { get; init; }

    [JsonPropertyName("boundary_preserved")]
    public bool BoundaryPreserved { get; init; }

    [JsonPropertyName("scope_drift_detected")]
    public bool ScopeDriftDetected { get; init; }

    [JsonPropertyName("follow_up_suggestions")]
    public string[]? FollowUpSuggestions { get; init; }

    [JsonPropertyName("decision_debt")]
    public ReviewDecisionDebtDocument? DecisionDebt { get; init; }
}

internal sealed class ReviewDecisionDebtDocument
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("follow_up_actions")]
    public string[]? FollowUpActions { get; init; }

    [JsonPropertyName("requires_follow_up_review")]
    public bool RequiresFollowUpReview { get; init; }

    [JsonPropertyName("recorded_at")]
    public string? RecordedAt { get; init; }
}
