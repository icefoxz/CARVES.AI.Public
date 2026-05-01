using System.Text.Json;

namespace Carves.Handoff.Core;

public sealed class HandoffGuardDecisionReferenceResolver
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public IReadOnlyList<HandoffDecisionReference> Resolve(
        string repoRoot,
        JsonElement packetRoot,
        ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        var references = ReadDecisionReferences(packetRoot);
        if (references.Count == 0)
        {
            return references;
        }

        var guardReferences = references
            .Where(reference => reference.IsGuardCandidate)
            .ToArray();
        if (guardReferences.Length == 0)
        {
            return references;
        }

        var decisionsPath = Path.Combine(repoRoot, HandoffDefaults.GuardDecisionsPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(decisionsPath))
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "decision_refs.guard_decisions_missing",
                "warning",
                $"Guard decisions file was not found at {HandoffDefaults.GuardDecisionsPath}; Guard references remain unresolved."));

            return references
                .Select(reference => reference.IsGuardCandidate
                    ? reference with { Status = "unresolved", Message = "Guard decisions file is missing." }
                    : reference)
                .ToArray();
        }

        var knownRunIds = ReadGuardRunIds(decisionsPath, diagnostics);
        return references
            .Select(reference =>
            {
                if (!reference.IsGuardCandidate)
                {
                    return reference;
                }

                return knownRunIds.Contains(reference.Ref)
                    ? reference with { Status = "linked", MatchedRunId = reference.Ref, Message = "Guard decision reference resolved." }
                    : MarkUnresolved(reference, diagnostics);
            })
            .ToArray();
    }

    private static HandoffDecisionReference MarkUnresolved(
        HandoffDecisionReference reference,
        ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        diagnostics.Add(new HandoffInspectionDiagnostic(
            "decision_refs.guard_unresolved",
            "warning",
            $"Guard decision reference '{reference.Ref}' was not found in {HandoffDefaults.GuardDecisionsPath}."));

        return reference with { Status = "unresolved", Message = "Guard decision reference was not found." };
    }

    private static HashSet<string> ReadGuardRunIds(
        string decisionsPath,
        ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var malformed = 0;
        foreach (var line in File.ReadLines(decisionsPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line, DocumentOptions);
                var root = document.RootElement;
                var runId = ReadString(root, "run_id") ?? ReadString(root, "RunId");
                if (!string.IsNullOrWhiteSpace(runId))
                {
                    result.Add(runId);
                }
            }
            catch (JsonException)
            {
                malformed++;
            }
            catch (IOException)
            {
                malformed++;
            }
        }

        if (malformed > 0)
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "decision_refs.guard_decisions_malformed",
                "warning",
                $"Skipped {malformed} malformed Guard decision record(s)."));
        }

        return result;
    }

    private static IReadOnlyList<HandoffDecisionReference> ReadDecisionReferences(JsonElement packetRoot)
    {
        if (!packetRoot.TryGetProperty("decision_refs", out var refs) || refs.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<HandoffDecisionReference>();
        foreach (var reference in refs.EnumerateArray())
        {
            if (reference.ValueKind == JsonValueKind.String)
            {
                var raw = reference.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    result.Add(CreateReference(null, raw, null));
                }

                continue;
            }

            if (reference.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = ReadString(reference, "kind");
            var rawRef = ReadString(reference, "ref")
                ?? ReadString(reference, "run_id")
                ?? ReadString(reference, "decision_id")
                ?? ReadString(reference, "id");
            if (string.IsNullOrWhiteSpace(rawRef))
            {
                continue;
            }

            result.Add(CreateReference(kind, rawRef, ReadString(reference, "summary")));
        }

        return result;
    }

    private static HandoffDecisionReference CreateReference(string? kind, string rawRef, string? summary)
    {
        var normalized = NormalizeGuardRef(rawRef);
        var isGuard = IsGuardKind(kind) || IsGuardPrefixed(rawRef);
        return new HandoffDecisionReference(
            kind,
            normalized,
            summary,
            isGuard,
            isGuard ? "unresolved" : "unvalidated",
            null,
            isGuard ? "Guard decision reference has not been resolved yet." : "Non-Guard decision reference is preserved without validation.");
    }

    private static bool IsGuardKind(string? kind)
    {
        return !string.IsNullOrWhiteSpace(kind)
            && kind.Contains("guard", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGuardPrefixed(string value)
    {
        return value.StartsWith("guard-run:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("guard-decision:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("guard:", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeGuardRef(string value)
    {
        var trimmed = value.Trim();
        foreach (var prefix in new[] { "guard-run:", "guard-decision:", "guard:" })
        {
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[prefix.Length..].Trim();
            }
        }

        return trimmed;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }
}

public sealed record HandoffDecisionReference(
    string? Kind,
    string Ref,
    string? Summary,
    bool IsGuardCandidate,
    string Status,
    string? MatchedRunId,
    string? Message);
