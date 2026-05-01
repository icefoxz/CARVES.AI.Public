using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.Interaction;

namespace Carves.Runtime.Application.Planning;

public sealed record PlanningCardInvariantBlock(
    string BlockId,
    string FieldPath,
    int LineIndex,
    string LiteralText,
    string Ownership,
    string CompareRule,
    string RemediationAction);

public sealed record PlanningCardInvariantViolation(
    string ViolationKind,
    string BlockId,
    string FieldPath,
    string ExpectedText,
    string? ActualText,
    string Summary,
    string RemediationAction);

public sealed record PlanningCardInvariantReport(
    string State,
    bool CanExportGovernedTruth,
    string Summary,
    string RemediationAction,
    string ExpectedDigest,
    string? ActualDigest,
    string? SystemDerivedDigest,
    IReadOnlyList<PlanningCardInvariantBlock> Blocks,
    IReadOnlyList<PlanningCardInvariantViolation> Violations);

public static class PlanningCardInvariantService
{
    public const string ValidState = "valid";
    public const string DriftedState = "drifted";
    public const string NoActivePlanningCardState = "no_active_planning_card";
    public const string CompareRule = "literal_and_digest";

    public static IReadOnlyList<PlanningCardInvariantBlock> BuildCanonicalBlocks(string? sourceCandidateCardId)
    {
        var candidateCardId = string.IsNullOrWhiteSpace(sourceCandidateCardId)
            ? "(none)"
            : sourceCandidateCardId.Trim();
        return
        [
            Block(
                "runtime_issuer_and_edit_boundary",
                0,
                "CARVES Runtime issues the active planning card; agents only assist within editable fields."),
            Block(
                "single_active_planning_slot",
                1,
                $"Exactly one active planning card may exist for planning slot '{ActivePlanningSlotProjectionResolver.PrimaryFormalPlanningSlotId}'."),
            Block(
                "source_candidate_lineage",
                2,
                $"The formal planning source candidate remains '{candidateCardId}' until a new `plan init` is issued."),
            Block(
                "literal_digest_validation",
                3,
                "Locked doctrine is validated by literal compare and digest compare before export."),
            Block(
                "host_routed_truth_lineage",
                4,
                "Official card, taskgraph, and execution truth remain host-routed and must preserve planning lineage."),
        ];
    }

    public static IReadOnlyList<string> BuildCanonicalLiteralLines(string? sourceCandidateCardId)
    {
        return BuildCanonicalBlocks(sourceCandidateCardId).Select(block => block.LiteralText).ToArray();
    }

    public static PlanningCardInvariantReport Evaluate(IntentDiscoveryDraft? draft, ActivePlanningCard? activePlanningCard)
    {
        if (activePlanningCard is null)
        {
            return new PlanningCardInvariantReport(
                NoActivePlanningCardState,
                false,
                "No active planning card exists, so planning-card invariant blocks cannot be evaluated.",
                "Run `plan init [candidate-card-id]` before exporting card truth.",
                ComputeDigest([]),
                null,
                null,
                [],
                []);
        }

        var sourceCandidateCardId = activePlanningCard.SourceCandidateCardId ?? draft?.FocusCardId;
        var blocks = BuildCanonicalBlocks(sourceCandidateCardId);
        var expectedLines = blocks.Select(block => block.LiteralText).ToArray();
        var expectedDigest = ComputeDigest(expectedLines);
        var actualLines = activePlanningCard.LockedDoctrine.LiteralLines;
        var violations = new List<PlanningCardInvariantViolation>();

        for (var index = 0; index < Math.Max(blocks.Count, actualLines.Count); index++)
        {
            if (index >= blocks.Count)
            {
                violations.Add(new PlanningCardInvariantViolation(
                    "extra_invariant_line",
                    "unknown_extra_line",
                    $"locked_doctrine.literal_lines[{index}]",
                    string.Empty,
                    actualLines[index],
                    $"Active planning card contains an extra locked doctrine line at index {index}.",
                    BuildRemediationAction(activePlanningCard)));
                continue;
            }

            var block = blocks[index];
            if (index >= actualLines.Count)
            {
                violations.Add(new PlanningCardInvariantViolation(
                    "missing_invariant_line",
                    block.BlockId,
                    block.FieldPath,
                    block.LiteralText,
                    null,
                    $"Canonical planning-card invariant block '{block.BlockId}' is missing.",
                    BuildRemediationAction(activePlanningCard)));
                continue;
            }

            if (!string.Equals(actualLines[index], block.LiteralText, StringComparison.Ordinal))
            {
                violations.Add(new PlanningCardInvariantViolation(
                    "altered_invariant_line",
                    block.BlockId,
                    block.FieldPath,
                    block.LiteralText,
                    actualLines[index],
                    $"Canonical planning-card invariant block '{block.BlockId}' was altered.",
                    BuildRemediationAction(activePlanningCard)));
            }
        }

        if (!string.Equals(activePlanningCard.LockedDoctrine.CompareRule, CompareRule, StringComparison.Ordinal))
        {
            violations.Add(new PlanningCardInvariantViolation(
                "invalid_compare_rule",
                "locked_doctrine_compare_rule",
                "locked_doctrine.compare_rule",
                CompareRule,
                activePlanningCard.LockedDoctrine.CompareRule,
                "Active planning card locked doctrine compare rule drifted from the canonical policy.",
                BuildRemediationAction(activePlanningCard)));
        }

        if (!string.Equals(activePlanningCard.LockedDoctrine.Digest, expectedDigest, StringComparison.Ordinal))
        {
            violations.Add(new PlanningCardInvariantViolation(
                "locked_doctrine_digest_mismatch",
                "locked_doctrine_digest",
                "locked_doctrine.digest",
                expectedDigest,
                activePlanningCard.LockedDoctrine.Digest,
                "Active planning card locked doctrine digest does not match canonical invariant blocks.",
                BuildRemediationAction(activePlanningCard)));
        }

        if (!string.Equals(activePlanningCard.SystemDerived.LockedDoctrineDigest, expectedDigest, StringComparison.Ordinal))
        {
            violations.Add(new PlanningCardInvariantViolation(
                "system_derived_digest_mismatch",
                "system_derived_locked_doctrine_digest",
                "system_derived.locked_doctrine_digest",
                expectedDigest,
                activePlanningCard.SystemDerived.LockedDoctrineDigest,
                "Active planning card system-derived doctrine digest does not match canonical invariant blocks.",
                BuildRemediationAction(activePlanningCard)));
        }

        var state = violations.Count == 0 ? ValidState : DriftedState;
        return new PlanningCardInvariantReport(
            state,
            violations.Count == 0,
            violations.Count == 0
                ? $"Planning card invariant check passed across {blocks.Count} canonical locked doctrine block(s)."
                : $"Active planning card locked doctrine drifted from {violations.Count} canonical invariant check(s).",
            violations.Count == 0
                ? "Continue with `plan export-card <json-path>` when editable planning fields are ready."
                : BuildRemediationAction(activePlanningCard),
            expectedDigest,
            activePlanningCard.LockedDoctrine.Digest,
            activePlanningCard.SystemDerived.LockedDoctrineDigest,
            blocks,
            violations);
    }

    public static void ValidateOrThrow(IntentDiscoveryDraft draft, ActivePlanningCard activePlanningCard)
    {
        var report = Evaluate(draft, activePlanningCard);
        if (report.CanExportGovernedTruth)
        {
            return;
        }

        var digestOnly = report.Violations.Count > 0
            && report.Violations.All(item => item.ViolationKind.Contains("digest", StringComparison.Ordinal));
        var prefix = digestOnly
            ? "Active planning card locked doctrine digest is invalid."
            : "Active planning card locked doctrine drifted from the canonical Runtime text.";
        throw new InvalidOperationException($"{prefix} {report.Summary} {report.RemediationAction}");
    }

    public static string ComputeDigest(IReadOnlyList<string> literalLines)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(string.Join('\n', literalLines));
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static PlanningCardInvariantBlock Block(string blockId, int lineIndex, string literalText)
    {
        return new PlanningCardInvariantBlock(
            blockId,
            $"locked_doctrine.literal_lines[{lineIndex}]",
            lineIndex,
            literalText,
            "system",
            "literal_compare",
            "Run `plan init [candidate-card-id]` to reissue canonical locked doctrine before exporting governed card truth.");
    }

    private static string BuildRemediationAction(ActivePlanningCard activePlanningCard)
    {
        var candidate = string.IsNullOrWhiteSpace(activePlanningCard.SourceCandidateCardId)
            ? "[candidate-card-id]"
            : activePlanningCard.SourceCandidateCardId;
        return $"Run `plan status` to inspect the drift, then run `plan init {candidate}` to reissue canonical locked doctrine; reapply only legitimate editable-field content after reevaluation.";
    }
}
