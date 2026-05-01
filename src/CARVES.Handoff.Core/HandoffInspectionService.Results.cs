using System.Text.Json;

namespace Carves.Handoff.Core;

public sealed partial class HandoffInspectionService
{
    private static HandoffInspectionResult Invalid(string packetPath, string status, string code, string message)
    {
        return new HandoffInspectionResult(
            InspectionSchemaVersion,
            packetPath,
            null,
            status,
            null,
            null,
            new HandoffInspectionReadiness("invalid", message),
            [new HandoffInspectionDiagnostic(code, "error", message)],
            [],
            [],
            [],
            [],
            [],
            null);
    }

    private HandoffInspectionResult BuildResult(
        string repoRoot,
        string packetPath,
        JsonElement root,
        string inspectionStatus,
        HandoffInspectionReadiness readiness,
        List<HandoffInspectionDiagnostic> diagnostics)
    {
        return new HandoffInspectionResult(
            InspectionSchemaVersion,
            packetPath,
            ReadString(root, "handoff_id"),
            inspectionStatus,
            ReadString(root, "resume_status"),
            ReadString(root, "confidence"),
            readiness,
            diagnostics,
            ReadReferences(root, "context_refs"),
            ReadReferences(root, "evidence_refs"),
            ReadTextItems(root, "must_not_repeat"),
            ReadTextItems(root, "blocked_reasons"),
            decisionReferenceResolver.Resolve(repoRoot, root, diagnostics),
            ReadRecommendedNextAction(root));
    }
}
