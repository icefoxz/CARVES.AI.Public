using System.Text.Json;

namespace Carves.Handoff.Core;

public sealed partial class HandoffInspectionService
{
    public const string InspectionSchemaVersion = HandoffJsonContracts.InspectionSchemaVersion;
    public const string SupportedPacketSchemaVersion = HandoffJsonContracts.PacketSchemaVersion;
    public const int DefaultFreshnessThresholdDays = 14;
    private readonly IHandoffRepoOrientationReader orientationReader;
    private readonly HandoffGuardDecisionReferenceResolver decisionReferenceResolver;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> AllowedResumeStatuses = new(StringComparer.Ordinal)
    {
        "ready",
        "blocked",
        "operator_review_required",
        "done_no_next_action",
        "low_confidence",
    };

    private static readonly HashSet<string> AllowedConfidence = new(StringComparer.Ordinal)
    {
        "high",
        "medium",
        "low",
    };

    private static readonly string[] RequiredTopLevelFields =
    [
        "schema_version",
        "handoff_id",
        "created_at_utc",
        "producer",
        "repo",
        "resume_status",
        "current_objective",
        "current_cursor",
        "completed_facts",
        "remaining_work",
        "blocked_reasons",
        "must_not_repeat",
        "open_questions",
        "decision_refs",
        "evidence_refs",
        "context_refs",
        "recommended_next_action",
        "confidence",
        "confidence_notes",
    ];

    public HandoffInspectionService()
        : this(new GitHandoffRepoOrientationReader(), new HandoffGuardDecisionReferenceResolver())
    {
    }

    public HandoffInspectionService(IHandoffRepoOrientationReader orientationReader)
        : this(orientationReader, new HandoffGuardDecisionReferenceResolver())
    {
    }

    public HandoffInspectionService(
        IHandoffRepoOrientationReader orientationReader,
        HandoffGuardDecisionReferenceResolver decisionReferenceResolver)
    {
        this.orientationReader = orientationReader;
        this.decisionReferenceResolver = decisionReferenceResolver;
    }

    public HandoffInspectionResult Inspect(string repoRoot, string packetPath)
    {
        if (string.IsNullOrWhiteSpace(packetPath))
        {
            return Invalid(packetPath, "missing", "packet.path_missing", "Packet path is required.");
        }

        var resolvedPath = ResolvePath(repoRoot, packetPath);
        if (!File.Exists(resolvedPath))
        {
            return Invalid(
                packetPath,
                "missing",
                "packet.missing",
                "Packet path was not found.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(resolvedPath), DocumentOptions);
        }
        catch (JsonException exception)
        {
            return Invalid(
                packetPath,
                "malformed",
                "packet.malformed",
                $"Packet is not valid JSON: {exception.Message}");
        }
        catch (IOException exception)
        {
            return Invalid(
                packetPath,
                "missing",
                "packet.read_failed",
                $"Packet could not be read: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Invalid(
                    packetPath,
                    "malformed",
                    "packet.not_object",
                    "Packet root must be a JSON object.");
            }

            var diagnostics = new List<HandoffInspectionDiagnostic>();
            var missingFields = RequiredTopLevelFields
                .Where(field => !root.TryGetProperty(field, out _))
                .ToArray();
            if (missingFields.Length > 0)
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "packet.incomplete",
                    "error",
                    $"Packet is missing required fields: {string.Join(", ", missingFields)}."));
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "incomplete",
                    new HandoffInspectionReadiness("invalid", "Packet is missing required top-level fields."),
                    diagnostics);
            }

            var schemaVersion = ReadString(root, "schema_version");
            if (!string.Equals(schemaVersion, SupportedPacketSchemaVersion, StringComparison.Ordinal))
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "packet.unsupported_schema",
                    "error",
                    $"Unsupported packet schema '{schemaVersion ?? "(missing)"}'."));
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "unsupported_schema",
                    new HandoffInspectionReadiness("invalid", "Packet schema is not supported."),
                    diagnostics);
            }

            var resumeStatus = ReadString(root, "resume_status");
            if (resumeStatus is null || !AllowedResumeStatuses.Contains(resumeStatus))
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "packet.invalid_resume_status",
                    "error",
                    $"Invalid resume_status '{resumeStatus ?? "(missing)"}'."));
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "incomplete",
                    new HandoffInspectionReadiness("invalid", "Packet resume_status is invalid."),
                    diagnostics);
            }

            var confidence = ReadString(root, "confidence");
            if (confidence is null || !AllowedConfidence.Contains(confidence))
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "packet.invalid_confidence",
                    "error",
                    $"Invalid confidence '{confidence ?? "(missing)"}'."));
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "incomplete",
                    new HandoffInspectionReadiness("invalid", "Packet confidence is invalid."),
                    diagnostics);
            }

            ValidateListShape(root, "completed_facts", diagnostics);
            ValidateListShape(root, "remaining_work", diagnostics);
            ValidateListShape(root, "blocked_reasons", diagnostics);
            ValidateListShape(root, "must_not_repeat", diagnostics);
            ValidateListShape(root, "open_questions", diagnostics);
            ValidateListShape(root, "decision_refs", diagnostics);
            ValidateListShape(root, "evidence_refs", diagnostics);
            ValidateListShape(root, "context_refs", diagnostics);
            ValidateListShape(root, "confidence_notes", diagnostics);
            ValidateRepoShape(root, diagnostics);

            var structuralErrors = diagnostics
                .Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal));
            if (structuralErrors)
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "incomplete",
                    new HandoffInspectionReadiness("invalid", "Packet list fields are malformed."),
                    diagnostics);
            }

            ValidateCompletedFactEvidence(root, diagnostics);
            ValidateContextRefs(root, diagnostics);
            ValidateMustNotRepeat(root, diagnostics);
            ValidateBlockedReasons(root, resumeStatus, diagnostics);
            var ageStale = ValidatePacketFreshness(root, diagnostics);
            var stale = ValidateRepoOrientation(root, repoRoot, diagnostics);
            var validationErrors = diagnostics
                .Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal));
            if (validationErrors)
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "incomplete",
                    new HandoffInspectionReadiness("invalid", "Packet contains invalid values."),
                    diagnostics);
            }

            var missingEvidence = diagnostics.Any(diagnostic =>
                diagnostic.Code.StartsWith("completed_facts.", StringComparison.Ordinal)
                || diagnostic.Code.StartsWith("context_refs.", StringComparison.Ordinal));
            if (missingEvidence)
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "missing_evidence",
                    new HandoffInspectionReadiness("operator_review_required", "Packet has missing or incomplete evidence/context references."),
                    diagnostics);
            }

            if (ageStale || stale)
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "stale",
                    new HandoffInspectionReadiness("reorient_first", "Packet is stale and should be reoriented before use."),
                    diagnostics);
            }

            if (string.Equals(resumeStatus, "blocked", StringComparison.Ordinal))
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "blocked",
                    new HandoffInspectionReadiness("blocked", "Packet resume_status is blocked."),
                    diagnostics);
            }

            if (string.Equals(resumeStatus, "operator_review_required", StringComparison.Ordinal))
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "usable",
                    new HandoffInspectionReadiness("operator_review_required", "Packet requires operator review before action."),
                    diagnostics);
            }

            if (string.Equals(resumeStatus, "done_no_next_action", StringComparison.Ordinal))
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "done",
                    new HandoffInspectionReadiness("done", "Packet marks the work complete and has no next action."),
                    diagnostics);
            }

            if (string.Equals(resumeStatus, "low_confidence", StringComparison.Ordinal)
                || string.Equals(confidence, "low", StringComparison.Ordinal))
            {
                return BuildResult(
                    repoRoot,
                    packetPath,
                    root,
                    "low_confidence",
                    new HandoffInspectionReadiness("reorient_first", "Packet confidence is low."),
                    diagnostics);
            }

            return BuildResult(
                repoRoot,
                packetPath,
                root,
                "usable",
                new HandoffInspectionReadiness("ready", "Packet is well-formed and can guide an incoming agent."),
                diagnostics);
        }
    }
}

public sealed record HandoffInspectionResult(
    string SchemaVersion,
    string PacketPath,
    string? HandoffId,
    string InspectionStatus,
    string? ResumeStatus,
    string? Confidence,
    HandoffInspectionReadiness Readiness,
    IReadOnlyList<HandoffInspectionDiagnostic> Diagnostics,
    IReadOnlyList<HandoffInspectionReference> ContextRefs,
    IReadOnlyList<HandoffInspectionReference> EvidenceRefs,
    IReadOnlyList<HandoffInspectionTextItem> MustNotRepeat,
    IReadOnlyList<HandoffInspectionTextItem> BlockedReasons,
    IReadOnlyList<HandoffDecisionReference> DecisionRefs,
    HandoffInspectionNextAction? RecommendedNextAction);

public sealed record HandoffInspectionReadiness(string Decision, string Reason);

public sealed record HandoffInspectionDiagnostic(string Code, string Severity, string Message);

public sealed record HandoffInspectionReference(
    string? Kind,
    string? Ref,
    string? Reason,
    string? Summary,
    int? Priority);

public sealed record HandoffInspectionTextItem(string Text, string? Reason, string? UnblockCondition);

public sealed record HandoffInspectionNextAction(string Action, string? Rationale);
