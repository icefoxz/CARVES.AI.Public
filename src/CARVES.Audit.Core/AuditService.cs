namespace Carves.Audit.Core;

public sealed class AuditService
{
    private readonly AuditInputReader reader;

    public AuditService()
        : this(new AuditInputReader())
    {
    }

    public AuditService(AuditInputReader reader)
    {
        this.reader = reader;
    }

    public AuditSummaryResult BuildSummary(AuditInputOptions options)
    {
        var snapshot = reader.Read(options);
        var events = BuildEvents(snapshot);
        return new AuditSummaryResult(
            AuditJsonContracts.SummarySchemaVersion,
            DateTimeOffset.UtcNow,
            snapshot.ConfidencePosture,
            events.Count,
            BuildGuardSummary(snapshot.Guard),
            BuildHandoffSummary(snapshot.HandoffPackets));
    }

    public AuditTimelineResult BuildTimeline(AuditInputOptions options)
    {
        var snapshot = reader.Read(options);
        var events = BuildEvents(snapshot)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.SourceProduct, StringComparer.Ordinal)
            .ThenBy(item => item.SubjectId, StringComparer.Ordinal)
            .ToArray();
        return new AuditTimelineResult(
            AuditJsonContracts.TimelineSchemaVersion,
            DateTimeOffset.UtcNow,
            snapshot.ConfidencePosture,
            events.Length,
            events);
    }

    public AuditExplainResult Explain(AuditInputOptions options, string id)
    {
        var snapshot = reader.Read(options);
        var matches = new List<AuditExplainMatch>();
        matches.AddRange(snapshot.Guard.Records
            .Where(record => string.Equals(record.RunId, id, StringComparison.Ordinal))
            .Select(record => new AuditExplainMatch(
                "guard",
                snapshot.Guard.InputPath,
                record.RunId!,
                record.RecordedAtUtc,
                NormalizeStatus(record.Outcome, "unknown"),
                record.Summary,
                record.EvidenceRefs,
                [],
                record.Violations.Concat(record.Warnings).Select(issue => new AuditExplainIssue(
                    issue.RuleId,
                    issue.Severity,
                    issue.Message,
                    issue.FilePath,
                    issue.EvidenceRef)).ToArray())));

        matches.AddRange(snapshot.HandoffPackets
            .Where(input => input.Packet is not null && string.Equals(input.Packet.HandoffId, id, StringComparison.Ordinal))
            .Select(input => new AuditExplainMatch(
                "handoff",
                input.InputPath,
                input.Packet!.HandoffId,
                input.Packet.CreatedAtUtc,
                NormalizeStatus(input.Packet.ResumeStatus, "unknown"),
                input.Packet.CurrentObjective,
                input.Packet.EvidenceRefs,
                input.Packet.ContextRefs,
                [])));

        return new AuditExplainResult(
            AuditJsonContracts.ExplainSchemaVersion,
            DateTimeOffset.UtcNow,
            id,
            Found: matches.Count > 0,
            Ambiguous: matches.Count > 1,
            snapshot.ConfidencePosture,
            matches);
    }

    public AuditInputSnapshot ReadInputs(AuditInputOptions options)
    {
        return reader.Read(options);
    }

    private static AuditGuardSummary BuildGuardSummary(GuardAuditInput input)
    {
        return new AuditGuardSummary(
            input.InputPath,
            input.InputStatus,
            input.Records.Count,
            input.Records.Count(record => string.Equals(record.Outcome, "allow", StringComparison.OrdinalIgnoreCase)),
            input.Records.Count(record =>
                string.Equals(record.Outcome, "review", StringComparison.OrdinalIgnoreCase)
                || string.Equals(record.Outcome, "needs_review", StringComparison.OrdinalIgnoreCase)),
            input.Records.Count(record => string.Equals(record.Outcome, "block", StringComparison.OrdinalIgnoreCase)),
            CountBy(input.Records.Select(record => NormalizeStatus(record.Source, "unknown"))),
            input.Records.OrderByDescending(record => record.RecordedAtUtc).FirstOrDefault()?.RunId,
            input.Diagnostics);
    }

    private static AuditHandoffSummary BuildHandoffSummary(IReadOnlyList<HandoffAuditInput> inputs)
    {
        var packets = inputs
            .Where(input => input.Packet is not null)
            .Select(input => input.Packet!)
            .ToArray();

        return new AuditHandoffSummary(
            inputs.Count,
            packets.Length,
            inputs.Count(input => input.InputStatus == "input_error"),
            CountBy(packets.Select(packet => NormalizeStatus(packet.ResumeStatus, "unknown"))),
            CountBy(packets.Select(packet => NormalizeStatus(packet.Confidence, "unknown"))),
            packets.OrderByDescending(packet => packet.CreatedAtUtc).FirstOrDefault()?.HandoffId,
            inputs.Select(input => new AuditHandoffInputSummary(
                input.InputPath,
                input.InputStatus,
                input.Packet?.HandoffId,
                input.Packet?.ResumeStatus,
                input.Packet?.Confidence,
                input.Diagnostics)).ToArray());
    }

    private static IReadOnlyList<AuditEvent> BuildEvents(AuditInputSnapshot snapshot)
    {
        var events = new List<AuditEvent>();
        events.AddRange(snapshot.Guard.Records.Select(record => new AuditEvent(
            "guard_decision",
            "guard",
            snapshot.Guard.InputPath,
            record.SchemaVersion.ToString(),
            record.RunId!,
            record.RecordedAtUtc,
            NormalizeStatus(record.Outcome, "unknown"),
            snapshot.Guard.Diagnostics.IsDegraded ? "degraded" : "known",
            record.Summary,
            record.EvidenceRefs)));

        events.AddRange(snapshot.HandoffPackets
            .Where(input => input.Packet is not null)
            .Select(input => new AuditEvent(
                "handoff_packet",
                "handoff",
                input.InputPath,
                input.Packet!.SchemaVersion,
                input.Packet.HandoffId,
                input.Packet.CreatedAtUtc,
                NormalizeStatus(input.Packet.ResumeStatus, "unknown"),
                NormalizeStatus(input.Packet.Confidence, "unknown"),
                input.Packet.CurrentObjective,
                input.Packet.EvidenceRefs.Count > 0 ? input.Packet.EvidenceRefs : input.Packet.ContextRefs)));

        return events;
    }

    private static IReadOnlyDictionary<string, int> CountBy(IEnumerable<string> values)
    {
        return values
            .GroupBy(value => value, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static string NormalizeStatus(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }
}
