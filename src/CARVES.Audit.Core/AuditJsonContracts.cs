using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Audit.Core;

public static class AuditJsonContracts
{
    public const string SummarySchemaVersion = "carves-audit-summary.v1";
    public const string TimelineSchemaVersion = "carves-audit-timeline.v1";
    public const string ExplainSchemaVersion = "carves-audit-explain.v1";
    public const string ShieldEvidenceSchemaVersion = "shield-evidence.v0";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static readonly JsonSerializerOptions EvidenceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
