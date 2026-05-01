using System.Text.Json;

namespace Carves.Handoff.Core;

public sealed class HandoffProjectionService
{
    public const string ProjectionSchemaVersion = HandoffJsonContracts.NextSchemaVersion;
    private readonly HandoffInspectionService inspectionService;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public HandoffProjectionService()
        : this(new HandoffInspectionService())
    {
    }

    public HandoffProjectionService(HandoffInspectionService inspectionService)
    {
        this.inspectionService = inspectionService;
    }

    public HandoffProjectionResult Project(string repoRoot, string packetPath)
    {
        var inspection = inspectionService.Inspect(repoRoot, packetPath);
        var packet = TryReadPacket(repoRoot, packetPath);
        return new HandoffProjectionResult(
            ProjectionSchemaVersion,
            packetPath,
            inspection.HandoffId,
            DeriveAction(inspection.Readiness.Decision),
            inspection.InspectionStatus,
            inspection.ResumeStatus,
            inspection.Confidence,
            inspection.Readiness,
            packet.CurrentObjective,
            packet.CurrentCursor,
            inspection.RecommendedNextAction,
            inspection.ContextRefs,
            inspection.EvidenceRefs,
            inspection.DecisionRefs,
            inspection.MustNotRepeat,
            inspection.BlockedReasons,
            inspection.Diagnostics);
    }

    private static HandoffProjectionPacketFields TryReadPacket(string repoRoot, string packetPath)
    {
        if (string.IsNullOrWhiteSpace(packetPath))
        {
            return HandoffProjectionPacketFields.Empty;
        }

        var resolvedPath = ResolvePath(repoRoot, packetPath);
        if (!File.Exists(resolvedPath))
        {
            return HandoffProjectionPacketFields.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(resolvedPath), DocumentOptions);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return HandoffProjectionPacketFields.Empty;
            }

            return new HandoffProjectionPacketFields(
                ReadString(root, "current_objective"),
                ReadCurrentCursor(root));
        }
        catch (JsonException)
        {
            return HandoffProjectionPacketFields.Empty;
        }
        catch (IOException)
        {
            return HandoffProjectionPacketFields.Empty;
        }
    }

    private static HandoffProjectionCursor? ReadCurrentCursor(JsonElement root)
    {
        if (!root.TryGetProperty("current_cursor", out var cursor) || cursor.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new HandoffProjectionCursor(
            ReadString(cursor, "kind"),
            ReadString(cursor, "id"),
            ReadString(cursor, "title"),
            ReadString(cursor, "status"),
            ReadStringArray(cursor, "scope"));
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return items.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string DeriveAction(string readinessDecision)
    {
        return readinessDecision switch
        {
            "ready" => "continue",
            "done" => "no_action",
            "operator_review_required" => "operator_review_first",
            "reorient_first" => "reorient_first",
            "blocked" => "blocked",
            _ => "invalid",
        };
    }

    private static string ResolvePath(string repoRoot, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(repoRoot, path));
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private sealed record HandoffProjectionPacketFields(
        string? CurrentObjective,
        HandoffProjectionCursor? CurrentCursor)
    {
        public static HandoffProjectionPacketFields Empty { get; } = new(null, null);
    }
}

public sealed record HandoffProjectionResult(
    string SchemaVersion,
    string PacketPath,
    string? HandoffId,
    string Action,
    string InspectionStatus,
    string? ResumeStatus,
    string? Confidence,
    HandoffInspectionReadiness Readiness,
    string? CurrentObjective,
    HandoffProjectionCursor? CurrentCursor,
    HandoffInspectionNextAction? RecommendedNextAction,
    IReadOnlyList<HandoffInspectionReference> ContextRefs,
    IReadOnlyList<HandoffInspectionReference> EvidenceRefs,
    IReadOnlyList<HandoffDecisionReference> DecisionRefs,
    IReadOnlyList<HandoffInspectionTextItem> MustNotRepeat,
    IReadOnlyList<HandoffInspectionTextItem> BlockedReasons,
    IReadOnlyList<HandoffInspectionDiagnostic> Diagnostics);

public sealed record HandoffProjectionCursor(
    string? Kind,
    string? Id,
    string? Title,
    string? Status,
    IReadOnlyList<string> Scope);
