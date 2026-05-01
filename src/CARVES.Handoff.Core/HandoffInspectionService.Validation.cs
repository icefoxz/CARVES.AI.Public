using System.Text.Json;

namespace Carves.Handoff.Core;

public sealed partial class HandoffInspectionService
{
    private static void ValidateRepoShape(JsonElement root, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("repo", out var repo) || repo.ValueKind == JsonValueKind.Object)
        {
            return;
        }

        diagnostics.Add(new HandoffInspectionDiagnostic(
            "packet.repo_not_object",
            "error",
            "Packet field 'repo' must be an object."));
    }
    private static void ValidateListShape(JsonElement root, string propertyName, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                $"packet.{propertyName}_not_array",
                "error",
                $"Packet field '{propertyName}' must be an array."));
        }
    }

    private static void ValidateCompletedFactEvidence(JsonElement root, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("completed_facts", out var facts) || facts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var fact in facts.EnumerateArray())
        {
            if (fact.ValueKind != JsonValueKind.Object
                || !fact.TryGetProperty("evidence_refs", out var refs)
                || refs.ValueKind != JsonValueKind.Array
                || refs.GetArrayLength() == 0)
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "completed_facts.missing_evidence_refs",
                    "warning",
                    $"Completed fact at index {index} has no evidence_refs."));
            }

            index++;
        }
    }

    private static void ValidateContextRefs(JsonElement root, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("context_refs", out var refs) || refs.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var reference in refs.EnumerateArray())
        {
            if (reference.ValueKind != JsonValueKind.Object
                || string.IsNullOrWhiteSpace(ReadString(reference, "ref"))
                || string.IsNullOrWhiteSpace(ReadString(reference, "reason"))
                || !reference.TryGetProperty("priority", out var priority)
                || priority.ValueKind != JsonValueKind.Number)
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "context_refs.incomplete",
                    "warning",
                    $"Context ref at index {index} must include ref, reason, and numeric priority."));
            }

            index++;
        }
    }

    private static void ValidateMustNotRepeat(JsonElement root, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("must_not_repeat", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                index++;
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                var text = ReadString(item, "item") ?? ReadString(item, "text");
                if (string.IsNullOrWhiteSpace(text))
                {
                    diagnostics.Add(new HandoffInspectionDiagnostic(
                        "must_not_repeat.missing_item",
                        "warning",
                        $"Must-not-repeat entry at index {index} has no item or text."));
                }

                if (string.IsNullOrWhiteSpace(ReadString(item, "reason")))
                {
                    diagnostics.Add(new HandoffInspectionDiagnostic(
                        "must_not_repeat.missing_reason",
                        "warning",
                        $"Must-not-repeat entry at index {index} has no reason."));
                }
            }
            else
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "must_not_repeat.invalid_item",
                    "warning",
                    $"Must-not-repeat entry at index {index} must be a string or object."));
            }

            index++;
        }
    }

    private static bool ValidatePacketFreshness(JsonElement root, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        var createdAt = ReadString(root, "created_at_utc");
        if (!DateTimeOffset.TryParse(createdAt, out var createdAtUtc))
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "packet.invalid_created_at",
                "error",
                "Packet created_at_utc must be a valid timestamp."));
            return false;
        }

        var age = DateTimeOffset.UtcNow - createdAtUtc;
        if (age.TotalDays <= DefaultFreshnessThresholdDays)
        {
            return false;
        }

        diagnostics.Add(new HandoffInspectionDiagnostic(
            "packet.age_stale",
            "warning",
            $"Packet is older than {DefaultFreshnessThresholdDays} days; reorient before using it as next-session guidance."));
        return true;
    }

    private static void ValidateBlockedReasons(JsonElement root, string resumeStatus, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        if (!string.Equals(resumeStatus, "blocked", StringComparison.Ordinal))
        {
            return;
        }

        if (!root.TryGetProperty("blocked_reasons", out var reasons)
            || reasons.ValueKind != JsonValueKind.Array
            || reasons.GetArrayLength() == 0)
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "blocked_reasons.missing",
                "warning",
                "Blocked packet must include blocked_reasons."));
            return;
        }

        var index = 0;
        foreach (var reason in reasons.EnumerateArray())
        {
            if (reason.ValueKind != JsonValueKind.Object
                || string.IsNullOrWhiteSpace(ReadString(reason, "reason"))
                || string.IsNullOrWhiteSpace(ReadString(reason, "unblock_condition")))
            {
                diagnostics.Add(new HandoffInspectionDiagnostic(
                    "blocked_reasons.incomplete",
                    "warning",
                    $"Blocked reason at index {index} must include reason and unblock_condition."));
            }

            index++;
        }
    }

    private bool ValidateRepoOrientation(JsonElement root, string repoRoot, ICollection<HandoffInspectionDiagnostic> diagnostics)
    {
        if (!root.TryGetProperty("repo", out var repo) || repo.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var packetBranch = NormalizeOrientationValue(ReadString(repo, "branch"));
        var packetBaseCommit = NormalizeOrientationValue(ReadString(repo, "base_commit"));
        var packetDirtyState = NormalizeOrientationValue(ReadString(repo, "dirty_state"));
        if (packetBranch is null && packetBaseCommit is null && packetDirtyState is null)
        {
            return false;
        }

        var current = orientationReader.Read(repoRoot);
        if (!current.Available)
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "repo.orientation_unavailable",
                "warning",
                current.Diagnostic ?? "Unable to read current repo orientation."));
            return true;
        }

        var stale = false;
        if (packetBranch is not null
            && !string.IsNullOrWhiteSpace(current.Branch)
            && !string.Equals(packetBranch, current.Branch, StringComparison.Ordinal))
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "repo.branch_mismatch",
                "warning",
                $"Packet branch '{packetBranch}' does not match current branch '{current.Branch}'."));
            stale = true;
        }

        if (packetBaseCommit is not null
            && !string.IsNullOrWhiteSpace(current.HeadCommit)
            && !string.Equals(packetBaseCommit, current.HeadCommit, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "repo.base_commit_mismatch",
                "warning",
                $"Packet base_commit '{packetBaseCommit}' does not match current HEAD '{current.HeadCommit}'."));
            stale = true;
        }

        if (packetDirtyState is not null
            && !string.Equals(current.DirtyState, "unknown", StringComparison.Ordinal)
            && !string.Equals(packetDirtyState, current.DirtyState, StringComparison.Ordinal))
        {
            diagnostics.Add(new HandoffInspectionDiagnostic(
                "repo.dirty_state_mismatch",
                "warning",
                $"Packet dirty_state '{packetDirtyState}' does not match current dirty_state '{current.DirtyState}'."));
            stale = true;
        }

        return stale;
    }
}
