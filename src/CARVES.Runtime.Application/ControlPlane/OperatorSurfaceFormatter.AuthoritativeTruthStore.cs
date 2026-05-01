using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static IReadOnlyList<string> FormatAuthoritativeTruthStore(AuthoritativeTruthStoreSurface surface)
    {
        var lines = new List<string>
        {
            "Authoritative truth store",
            $"Repo root: {surface.RepoRoot}",
            $"Authoritative root: {surface.AuthoritativeRoot}",
            $"Mirror root: {surface.MirrorRoot}",
            $"External to repo: {surface.ExternalToRepo}",
            $"Summary: {surface.Summary}",
            $"Writer lock: {(surface.WriterLock is null ? "none" : $"{surface.WriterLock.State} ({surface.WriterLock.Operation ?? "unknown"})")}",
            "Families:",
        };

        if (surface.WriterLock is not null)
        {
            lines.Add($"Writer lock scope: {surface.WriterLock.Scope}");
            lines.Add($"Writer lock resource: {surface.WriterLock.Resource ?? "(unknown)"}");
            lines.Add($"Writer lock operation: {surface.WriterLock.Operation ?? "(unknown)"}");
            lines.Add($"Writer lock owner: {surface.WriterLock.OwnerId}");
            lines.Add($"Writer lock heartbeat: {surface.WriterLock.LastHeartbeat:O}");
            lines.Add($"Writer lock summary: {surface.WriterLock.Summary}");
        }

        foreach (var family in surface.Families)
        {
            lines.Add($"- {family.FamilyId}: {family.Summary}");
            lines.Add($"  Read mode: {family.ReadMode}");
            lines.Add($"  Write mode: {family.WriteMode}");
            lines.Add($"  Authoritative: {family.AuthoritativePath} ({(family.AuthoritativeExists ? "present" : "missing")})");
            lines.Add($"  Mirror: {family.MirrorPath} ({(family.MirrorExists ? "present" : "missing")})");
            lines.Add($"  Mirror state: {family.MirrorState}");
            lines.Add($"  Mirror drift detected: {family.MirrorDriftDetected}");
            lines.Add($"  Mirror summary: {family.MirrorSummary}");
            lines.Add($"  Mirror sync receipt: {family.MirrorSync.ReceiptPath}");
            lines.Add($"  Mirror sync outcome: {family.MirrorSync.Outcome}");
            lines.Add($"  Mirror sync resource: {(string.IsNullOrWhiteSpace(family.MirrorSync.Resource) ? "(none)" : family.MirrorSync.Resource)}");
            lines.Add($"  Mirror sync attempt: {family.MirrorSync.LastMirrorSyncAttemptAt?.ToString("O") ?? "(none)"}");
            lines.Add($"  Last successful mirror sync: {family.MirrorSync.LastSuccessfulMirrorSyncAt?.ToString("O") ?? "(none)"}");
            lines.Add($"  Mirror sync summary: {family.MirrorSync.Summary}");
        }

        return lines;
    }
}
