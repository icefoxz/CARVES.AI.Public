namespace Carves.Handoff.Tests;

internal sealed class TemporaryWorkspace : IDisposable
{
    public TemporaryWorkspace()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "carves-handoff-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public string WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
        }
    }
}

internal static class HandoffTestPackets
{
    public static string ValidPacket(
        string handoffId = "HND-TEST-001",
        string resumeStatus = "ready",
        string confidence = "medium",
        string objective = "Test incoming handoff inspection.",
        string decisionRefs = "")
    {
        var decisionRefsJson = string.IsNullOrWhiteSpace(decisionRefs)
            ? "[]"
            : decisionRefs;
        var createdAtUtc = DateTimeOffset.UtcNow.ToString("O");

        return $$"""
        {
          "schema_version": "carves-continuity-handoff.v1",
          "handoff_id": "{{handoffId}}",
          "created_at_utc": "{{createdAtUtc}}",
          "producer": {
            "agent": "test"
          },
          "repo": {
            "name": "external-target"
          },
          "resume_status": "{{resumeStatus}}",
          "current_objective": "{{objective}}",
          "current_cursor": {
            "kind": "manual",
            "id": "TEST",
            "title": "Handoff test",
            "status": "in_progress",
            "scope": [
              "src/example.ts"
            ]
          },
          "completed_facts": [
            {
              "statement": "A bounded handoff packet exists.",
              "evidence_refs": [
                "docs/example.md"
              ],
              "confidence": "high"
            }
          ],
          "remaining_work": [
            {
              "action": "Inspect the packet."
            }
          ],
          "blocked_reasons": [],
          "must_not_repeat": [
            {
              "item": "Do not rediscover the same context.",
              "reason": "The packet already lists it."
            }
          ],
          "open_questions": [],
          "decision_refs": {{decisionRefsJson}},
          "evidence_refs": [
            {
              "kind": "doc",
              "ref": "docs/example.md",
              "summary": "Example evidence."
            }
          ],
          "context_refs": [
            {
              "ref": "docs/example.md",
              "reason": "Provides bounded context.",
              "priority": 1
            }
          ],
          "recommended_next_action": {
            "action": "Read the listed context refs.",
            "rationale": "Avoid broad rediscovery."
          },
          "confidence": "{{confidence}}",
          "confidence_notes": [
            "Test fixture."
          ]
        }
        """;
    }
}
