using System.Text.Json;

namespace Carves.Handoff.Core;

public static class HandoffJsonContracts
{
    public const string PacketSchemaVersion = "carves-continuity-handoff.v1";
    public const string InspectionSchemaVersion = "carves-continuity-handoff-inspection.v1";
    public const string DraftSchemaVersion = "carves-continuity-handoff-draft.v1";
    public const string NextSchemaVersion = "carves-continuity-handoff-next.v1";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };
}
