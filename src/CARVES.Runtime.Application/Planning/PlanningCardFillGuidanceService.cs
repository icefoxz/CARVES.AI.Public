using Carves.Runtime.Application.Interaction;

namespace Carves.Runtime.Application.Planning;

public sealed record PlanningCardFillFieldStatus(
    string FieldPath,
    string GroupId,
    string Label,
    bool Required,
    bool IsMissing,
    string Summary,
    string RecommendedFillAction);

public sealed record PlanningCardFillGuidanceReport(
    string State,
    string CompletionPosture,
    bool ReadyForRecommendedExport,
    string Summary,
    string RecommendedNextFillAction,
    string? NextMissingFieldPath,
    int RequiredFieldCount,
    int MissingRequiredFieldCount,
    IReadOnlyList<PlanningCardFillFieldStatus> Fields,
    IReadOnlyList<PlanningCardFillFieldStatus> MissingRequiredFields);

public static class PlanningCardFillGuidanceService
{
    public const string NoActivePlanningCardState = "no_active_planning_card";
    public const string BlockedByInvariantDriftState = "blocked_by_invariant_drift";
    public const string NeedsFillState = "needs_fill";
    public const string ReadyToExportState = "ready_to_export";

    public static PlanningCardFillGuidanceReport Evaluate(
        ActivePlanningCard? activePlanningCard,
        PlanningCardInvariantReport? invariantReport = null)
    {
        if (activePlanningCard is null)
        {
            return new PlanningCardFillGuidanceReport(
                NoActivePlanningCardState,
                "plan_init_required",
                false,
                "No active planning card exists, so editable planning fields cannot be evaluated.",
                "Run `plan init [candidate-card-id]` before filling active planning-card fields.",
                null,
                0,
                0,
                [],
                []);
        }

        invariantReport ??= PlanningCardInvariantService.Evaluate(null, activePlanningCard);
        if (!invariantReport.CanExportGovernedTruth)
        {
            return new PlanningCardFillGuidanceReport(
                BlockedByInvariantDriftState,
                "blocked_by_planning_card_invariant_drift",
                false,
                "Active planning-card fill guidance is blocked until Runtime-owned invariant doctrine is reissued.",
                invariantReport.RemediationAction,
                null,
                0,
                0,
                [],
                []);
        }

        var fields = BuildFieldStatuses(activePlanningCard);
        var missing = fields
            .Where(field => field.Required && field.IsMissing)
            .ToArray();
        if (missing.Length > 0)
        {
            var next = missing[0];
            return new PlanningCardFillGuidanceReport(
                NeedsFillState,
                "missing_required_editable_fields",
                false,
                $"Active planning card is missing {missing.Length} required editable field(s). This report only reflects blank or empty editable fields.",
                next.RecommendedFillAction,
                next.FieldPath,
                fields.Count(field => field.Required),
                missing.Length,
                fields,
                missing);
        }

        return new PlanningCardFillGuidanceReport(
            ReadyToExportState,
            "editable_fields_ready",
            true,
            "Active planning-card editable fields are populated and invariant doctrine is valid.",
            "Review the editable fields with the operator, then run `plan export-card <json-path>` to create the governed card payload.",
            null,
            fields.Count(field => field.Required),
            0,
            fields,
            []);
    }

    private static IReadOnlyList<PlanningCardFillFieldStatus> BuildFieldStatuses(ActivePlanningCard activePlanningCard)
    {
        return
        [
            RequiredString(
                "operator_intent.title",
                "operator_intent",
                "Title",
                activePlanningCard.OperatorIntent.Title,
                "Fill `operator_intent.title` with a concise bounded-card title, then rerun `plan status`."),
            RequiredString(
                "operator_intent.goal",
                "operator_intent",
                "Goal",
                activePlanningCard.OperatorIntent.Goal,
                "Fill `operator_intent.goal` with the user-confirmed outcome, then rerun `plan status`."),
            RequiredString(
                "operator_intent.validation_artifact",
                "operator_intent",
                "Validation artifact",
                activePlanningCard.OperatorIntent.ValidationArtifact,
                "Fill `operator_intent.validation_artifact` with the evidence artifact or command that proves the slice, then rerun `plan status`."),
            RequiredList(
                "operator_intent.acceptance_outline",
                "operator_intent",
                "Acceptance outline",
                activePlanningCard.OperatorIntent.AcceptanceOutline,
                "Fill `operator_intent.acceptance_outline` with concrete acceptance criteria, then rerun `plan status`."),
            RequiredList(
                "operator_intent.constraints",
                "operator_intent",
                "Constraints",
                activePlanningCard.OperatorIntent.Constraints,
                "Fill `operator_intent.constraints` with explicit scope, boundary, or safety constraints, then rerun `plan status`."),
            OptionalList(
                "operator_intent.non_goals",
                "operator_intent",
                "Non-goals",
                activePlanningCard.OperatorIntent.NonGoals,
                "Optionally fill `operator_intent.non_goals` with explicit out-of-scope items."),
            RequiredString(
                "agent_proposal.candidate_summary",
                "agent_proposal",
                "Candidate summary",
                activePlanningCard.AgentProposal.CandidateSummary,
                "Fill `agent_proposal.candidate_summary` with a compact summary grounded in the active candidate, then rerun `plan status`."),
            RequiredList(
                "agent_proposal.decomposition_candidates",
                "agent_proposal",
                "Decomposition candidates",
                activePlanningCard.AgentProposal.DecompositionCandidates,
                "Fill `agent_proposal.decomposition_candidates` with bounded implementation slices or questions, then rerun `plan status`."),
            OptionalList(
                "agent_proposal.open_questions",
                "agent_proposal",
                "Open questions",
                activePlanningCard.AgentProposal.OpenQuestions,
                "Optionally fill `agent_proposal.open_questions` with unresolved planning questions."),
            RequiredString(
                "agent_proposal.suggested_next_action",
                "agent_proposal",
                "Suggested next action",
                activePlanningCard.AgentProposal.SuggestedNextAction,
                "Fill `agent_proposal.suggested_next_action` with the next operator-visible planning action, then rerun `plan status`."),
        ];
    }

    private static PlanningCardFillFieldStatus RequiredString(
        string fieldPath,
        string groupId,
        string label,
        string? value,
        string fillAction)
    {
        var missing = string.IsNullOrWhiteSpace(value);
        return Field(fieldPath, groupId, label, required: true, missing, fillAction);
    }

    private static PlanningCardFillFieldStatus RequiredList(
        string fieldPath,
        string groupId,
        string label,
        IReadOnlyList<string>? value,
        string fillAction)
    {
        var missing = value is null || value.Count == 0 || value.All(string.IsNullOrWhiteSpace);
        return Field(fieldPath, groupId, label, required: true, missing, fillAction);
    }

    private static PlanningCardFillFieldStatus OptionalList(
        string fieldPath,
        string groupId,
        string label,
        IReadOnlyList<string>? value,
        string fillAction)
    {
        var missing = value is null || value.Count == 0 || value.All(string.IsNullOrWhiteSpace);
        return Field(fieldPath, groupId, label, required: false, missing, fillAction);
    }

    private static PlanningCardFillFieldStatus Field(
        string fieldPath,
        string groupId,
        string label,
        bool required,
        bool missing,
        string fillAction)
    {
        var requirement = required ? "required" : "optional";
        var state = missing ? "missing" : "populated";
        return new PlanningCardFillFieldStatus(
            fieldPath,
            groupId,
            label,
            required,
            missing,
            $"{label} is {state} ({requirement}).",
            fillAction);
    }
}
