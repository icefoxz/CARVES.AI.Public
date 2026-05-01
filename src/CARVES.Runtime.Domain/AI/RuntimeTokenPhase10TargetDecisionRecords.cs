namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenPhase10TargetDecisionResult
{
    public string SchemaVersion { get; init; } = "runtime-token-phase10-target-decision-result.v1";

    public DateOnly ResultDate { get; init; }

    public DateTimeOffset EvaluatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string CohortId { get; init; } = string.Empty;

    public string MarkdownArtifactPath { get; init; } = string.Empty;

    public string JsonArtifactPath { get; init; } = string.Empty;

    public string EvidenceMarkdownArtifactPath { get; init; } = string.Empty;

    public string EvidenceJsonArtifactPath { get; init; } = string.Empty;

    public string TrustMarkdownArtifactPath { get; init; } = string.Empty;

    public string TrustJsonArtifactPath { get; init; } = string.Empty;

    public string TrustLineClassification { get; init; } = "recomputed_but_insufficient_data_for_phase_1_target_decision";

    public bool Phase10TargetDecisionMayReferenceThisLine { get; init; }

    public string Decision { get; init; } = "insufficient_data";

    public string NextTrack { get; init; } = "insufficient_data";

    public string? TargetSegment { get; init; }

    public string? TargetSegmentClass { get; init; }

    public double? TargetShareP95 { get; init; }

    public double? TrimmedShareProxyP95 { get; init; }

    public string? HardCapTriggerSegment { get; init; }

    public IReadOnlyList<string> DominanceBasis { get; init; } = Array.Empty<string>();

    public string Confidence { get; init; } = "low";

    public IReadOnlyList<RuntimeTokenPhase10ContributorSummary> TopP95Contributors { get; init; } = Array.Empty<RuntimeTokenPhase10ContributorSummary>();

    public IReadOnlyList<RuntimeTokenPhase10TrimmedContributorSummary> TopTrimmedContributors { get; init; } = Array.Empty<RuntimeTokenPhase10TrimmedContributorSummary>();

    public RuntimeTokenPhase10DecisionInputs DecisionInputs { get; init; } = new();

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WhatMustNotHappenNext { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();
}
