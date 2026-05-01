using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Globalization;

namespace Carves.Runtime.Host;

internal static class GatewayActivityJournal
{
    public const string SchemaVersion = "carves-gateway-activity-journal.v1";
    public const string ManifestSchemaVersion = "carves-gateway-activity-segment-manifest.v1";
    public const string CheckpointSchemaVersion = "carves-gateway-activity-manifest-checkpoint.v1";
    public const string StorageModeSegmentedAppendOnly = "segmented_append_only";
    public const string StorageModeLegacySingleFile = "legacy_single_file";
    public const string RetentionMode = "non_destructive_archive_no_delete";
    public const string RetentionExecutionMode = "automatic_on_append_and_manual_archive";
    public const string WriterLockMode = "bounded_file_lock";
    public const string IntegrityMode = "segment_manifest_sha256_checkpoint_chain";
    public const string DropTelemetrySchemaVersion = "carves-gateway-activity-drop-telemetry.v1";
    public const string MaintenanceSummarySchemaVersion = "carves-gateway-activity-maintenance-summary.v1";
    public const string VerificationSummarySchemaVersion = "carves-gateway-activity-verification-summary.v2";
    public const int DefaultCompactRetainLineCount = 5_000;
    public const int DefaultArchiveBeforeDays = 30;
    public static int StoreLockAcquireTimeoutMilliseconds => (int)StoreLockAcquireTimeout.TotalMilliseconds;
    private const string StoreDirectoryName = "gateway-activity-store";
    private const string SegmentDirectoryName = "segments";
    private const string ArchiveDirectoryName = "archive";
    private const string ManifestFileName = "manifest.json";
    private const string CheckpointFileName = "manifest-checkpoints.jsonl";
    private const string LockFileName = "activity.lock";
    private const string DropTelemetryFileName = "activity-drops.json";
    private const string DropTelemetryLockFileName = "activity-drops.lock";
    private const string MaintenanceSummaryFileName = "maintenance-summary.json";
    private const string VerificationSummaryFileName = "verification-summary.json";
    private static readonly TimeSpan StoreLockAcquireTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DropTelemetryLockAcquireTimeout = TimeSpan.FromSeconds(1);

    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public static void Append(
        string path,
        DateTimeOffset timestamp,
        string eventName,
        IReadOnlyDictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        using var storeLock = TryAcquireStoreLock(path);
        if (storeLock is null)
        {
            RecordDroppedActivity(path, timestamp, eventName, fields, "activity_store_lock_unavailable");
            return;
        }

        var segmentPath = ResolveActiveSegmentPath(path, timestamp);
        Directory.CreateDirectory(Path.GetDirectoryName(segmentPath)!);
        var record = new GatewayActivityJournalRecord
        {
            SchemaVersion = SchemaVersion,
            Timestamp = timestamp,
            Event = eventName,
            Fields = new Dictionary<string, string>(fields, StringComparer.Ordinal),
        };
        try
        {
            AppendLine(segmentPath, JsonSerializer.Serialize(record, JsonOptions));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RecordDroppedActivity(path, timestamp, eventName, fields, "activity_store_append_failed");
            return;
        }

        var archive = ArchiveUnderCurrentLock(path, DefaultArchiveBeforeDays, timestamp);
        if (archive.ArchivedSegmentCount > 0)
        {
            RecordMaintenanceSummary(path, "automatic_archive_on_append", archive, timestamp);
        }

        RefreshManifest(path);
    }

    public static IReadOnlyList<GatewayActivityJournalRecord> ReadTail(string path, int lineCount)
    {
        if (lineCount <= 0 || string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }

        var segmentFiles = ResolveSegmentFiles(path);
        if (segmentFiles.Count > 0)
        {
            return ReadTailFromFiles(segmentFiles, lineCount);
        }

        if (!File.Exists(path))
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }

        return ReadTailFromFiles([path], lineCount);
    }

    public static IReadOnlyList<GatewayActivityJournalRecord> ReadSince(string path, DateTimeOffset sinceUtc)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }

        var segmentFiles = ResolveSegmentFiles(path);
        if (segmentFiles.Count > 0)
        {
            return ReadSinceFromFiles(ResolveSegmentFilesSince(segmentFiles, sinceUtc), sinceUtc);
        }

        if (!File.Exists(path))
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }

        return ReadSinceFromFiles([path], sinceUtc);
    }

    public static IReadOnlyList<GatewayActivityJournalRecord> ReadByRequestId(string path, string requestId)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(requestId))
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }

        var files = ResolveSegmentFiles(path)
            .Concat(ResolveArchivedSegmentFiles(path))
            .ToArray();
        if (files.Length > 0)
        {
            return ReadMatchingFromFiles(
                    files,
                    record => record.Fields.TryGetValue("request_id", out var value)
                              && string.Equals(value, requestId, StringComparison.Ordinal))
                .OrderBy(static record => record.Timestamp)
                .ToArray();
        }

        if (!File.Exists(path))
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }

        return ReadMatchingFromFiles(
                [path],
                record => record.Fields.TryGetValue("request_id", out var value)
                          && string.Equals(value, requestId, StringComparison.Ordinal))
            .OrderBy(static record => record.Timestamp)
            .ToArray();
    }

    public static GatewayActivityJournalStatus Inspect(string path)
    {
        var storeDirectory = ResolveStoreDirectory(path);
        var segmentDirectory = ResolveSegmentDirectory(path);
        var archiveDirectory = ResolveArchiveDirectory(path);
        var activeSegmentPath = ResolveActiveSegmentPath(path, DateTimeOffset.UtcNow);
        var lockPath = ResolveLockPath(path);
        var segmentFiles = ResolveSegmentFiles(path);
        var archivedFiles = ResolveArchivedSegmentFiles(path);
        var archivedMetrics = InspectFiles(archivedFiles);
        var manifest = BuildManifest(path);
        var manifestPath = ResolveManifestPath(path);
        var checkpointStatus = InspectCheckpointChain(path);
        var dropTelemetry = ReadDropTelemetry(path);
        var maintenanceSummary = ReadMaintenanceSummary(path);
        var verificationSummary = ReadVerificationSummary(path);
        var lockStatus = InspectLockStatus(lockPath);
        var manifestExists = !string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath);
        var legacyExists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        var storedManifest = manifestExists ? ReadManifest(manifestPath) : null;

        if (segmentFiles.Count > 0)
        {
            return new GatewayActivityJournalStatus(
                activeSegmentPath,
                Exists: true,
                ByteCount: manifest.ByteCount,
                LineCount: manifest.RecordCount,
                StorageMode: StorageModeSegmentedAppendOnly,
                StoreDirectory: storeDirectory,
                SegmentDirectory: segmentDirectory,
                ArchiveDirectory: archiveDirectory,
                RetentionMode: RetentionMode,
                RetentionExecutionMode: RetentionExecutionMode,
                DefaultArchiveBeforeDays: DefaultArchiveBeforeDays,
                WriterLockMode: WriterLockMode,
                LockPath: lockPath,
                LockExists: lockStatus.FileExists,
                LockFileExists: lockStatus.FileExists,
                LockCurrentlyHeld: lockStatus.CurrentlyHeld,
                LockStatus: lockStatus.Status,
                LockLastHolderProcessId: lockStatus.LastHolderProcessId,
                LockLastAcquiredAtUtc: lockStatus.LastAcquiredAtUtc,
                LockAcquireTimeoutMs: StoreLockAcquireTimeoutMilliseconds,
                IntegrityMode: IntegrityMode,
                DropTelemetryPath: dropTelemetry.Path,
                DropTelemetryExists: dropTelemetry.Exists,
                DroppedActivityCount: dropTelemetry.DroppedActivityCount,
                LastDropAtUtc: dropTelemetry.LastDropAtUtc,
                LastDropReason: dropTelemetry.LastDropReason,
                LastDropEvent: dropTelemetry.LastDropEvent,
                LastDropRequestId: dropTelemetry.LastDropRequestId,
                MaintenanceSummaryPath: maintenanceSummary.Path,
                MaintenanceSummaryExists: maintenanceSummary.Exists,
                MaintenanceSummarySchemaVersion: MaintenanceSummarySchemaVersion,
                LastMaintenanceAtUtc: maintenanceSummary.LastMaintenanceAtUtc,
                LastMaintenanceOperation: maintenanceSummary.LastMaintenanceOperation,
                LastMaintenanceDryRun: maintenanceSummary.LastMaintenanceDryRun,
                LastMaintenanceApplied: maintenanceSummary.LastMaintenanceApplied,
                LastMaintenanceReason: maintenanceSummary.LastMaintenanceReason,
                LastMaintenanceBeforeDays: maintenanceSummary.LastMaintenanceBeforeDays,
                LastMaintenanceArchiveBeforeUtcDate: maintenanceSummary.LastMaintenanceArchiveBeforeUtcDate,
                LastMaintenanceCandidateSegmentCount: maintenanceSummary.LastMaintenanceCandidateSegmentCount,
                LastMaintenanceArchivedSegmentCount: maintenanceSummary.LastMaintenanceArchivedSegmentCount,
                LastMaintenanceArchivedByteCount: maintenanceSummary.LastMaintenanceArchivedByteCount,
                LastMaintenanceArchivedRecordCount: maintenanceSummary.LastMaintenanceArchivedRecordCount,
                LastMaintenanceRecommendedAction: maintenanceSummary.LastMaintenanceRecommendedAction,
                VerificationSummaryPath: verificationSummary.Path,
                VerificationSummaryExists: verificationSummary.Exists,
                VerificationSummarySchemaVersion: VerificationSummarySchemaVersion,
                LastVerificationAtUtc: verificationSummary.LastVerificationAtUtc,
                LastVerificationPassed: verificationSummary.LastVerificationPassed,
                LastVerificationPosture: verificationSummary.LastVerificationPosture,
                LastVerificationPossiblyTransient: verificationSummary.LastVerificationPossiblyTransient,
                LastVerificationConsistencyMode: verificationSummary.LastVerificationConsistencyMode,
                LastVerificationSnapshotLockAcquired: verificationSummary.LastVerificationSnapshotLockAcquired,
                LastVerificationIssueCount: verificationSummary.LastVerificationIssueCount,
                LastVerificationStoredManifestRecordCount: verificationSummary.LastVerificationStoredManifestRecordCount,
                LastVerificationActualManifestRecordCount: verificationSummary.LastVerificationActualManifestRecordCount,
                LastVerificationStoredManifestByteCount: verificationSummary.LastVerificationStoredManifestByteCount,
                LastVerificationActualManifestByteCount: verificationSummary.LastVerificationActualManifestByteCount,
                LastVerificationRecommendedAction: verificationSummary.LastVerificationRecommendedAction,
                LastVerificationManifestGeneratedAtUtc: verificationSummary.LastVerificationManifestGeneratedAtUtc,
                LastVerificationManifestSha256: verificationSummary.LastVerificationManifestSha256,
                LastVerificationCheckpointLatestSequence: verificationSummary.LastVerificationCheckpointLatestSequence,
                LastVerificationCheckpointLatestCheckpointSha256: verificationSummary.LastVerificationCheckpointLatestCheckpointSha256,
                LastVerificationCheckpointLatestManifestSha256: verificationSummary.LastVerificationCheckpointLatestManifestSha256,
                ActiveSegmentPath: activeSegmentPath,
                SegmentCount: segmentFiles.Count,
                ArchiveSegmentCount: archivedFiles.Count,
                ArchiveByteCount: archivedMetrics.ByteCount,
                LegacyJournalPath: path,
                LegacyJournalExists: legacyExists,
                LegacyFallbackUsed: false,
                ManifestPath: manifestPath,
                ManifestExists: manifestExists,
                ManifestSchemaVersion: ManifestSchemaVersion,
                CheckpointChainPath: checkpointStatus.Path,
                CheckpointChainExists: checkpointStatus.Exists,
                CheckpointChainCount: checkpointStatus.CheckpointCount,
                CheckpointChainLatestSequence: checkpointStatus.LatestSequence,
                CheckpointChainLatestCheckpointSha256: checkpointStatus.LatestCheckpointSha256,
                CheckpointChainLatestManifestSha256: checkpointStatus.LatestManifestSha256,
                ManifestGeneratedAtUtc: storedManifest?.GeneratedAtUtc ?? manifest.GeneratedAtUtc,
                ManifestRecordCount: manifest.RecordCount,
                ManifestByteCount: manifest.ByteCount,
                ManifestFirstTimestampUtc: manifest.FirstTimestampUtc,
                ManifestLastTimestampUtc: manifest.LastTimestampUtc,
                Segments: manifest.Segments);
        }

        if (legacyExists)
        {
            var metrics = InspectFiles([path]);
            return new GatewayActivityJournalStatus(
                path,
                Exists: true,
                ByteCount: metrics.ByteCount,
                LineCount: metrics.LineCount,
                StorageMode: StorageModeLegacySingleFile,
                StoreDirectory: storeDirectory,
                SegmentDirectory: segmentDirectory,
                ArchiveDirectory: archiveDirectory,
                RetentionMode: RetentionMode,
                RetentionExecutionMode: RetentionExecutionMode,
                DefaultArchiveBeforeDays: DefaultArchiveBeforeDays,
                WriterLockMode: WriterLockMode,
                LockPath: lockPath,
                LockExists: lockStatus.FileExists,
                LockFileExists: lockStatus.FileExists,
                LockCurrentlyHeld: lockStatus.CurrentlyHeld,
                LockStatus: lockStatus.Status,
                LockLastHolderProcessId: lockStatus.LastHolderProcessId,
                LockLastAcquiredAtUtc: lockStatus.LastAcquiredAtUtc,
                LockAcquireTimeoutMs: StoreLockAcquireTimeoutMilliseconds,
                IntegrityMode: IntegrityMode,
                DropTelemetryPath: dropTelemetry.Path,
                DropTelemetryExists: dropTelemetry.Exists,
                DroppedActivityCount: dropTelemetry.DroppedActivityCount,
                LastDropAtUtc: dropTelemetry.LastDropAtUtc,
                LastDropReason: dropTelemetry.LastDropReason,
                LastDropEvent: dropTelemetry.LastDropEvent,
                LastDropRequestId: dropTelemetry.LastDropRequestId,
                MaintenanceSummaryPath: maintenanceSummary.Path,
                MaintenanceSummaryExists: maintenanceSummary.Exists,
                MaintenanceSummarySchemaVersion: MaintenanceSummarySchemaVersion,
                LastMaintenanceAtUtc: maintenanceSummary.LastMaintenanceAtUtc,
                LastMaintenanceOperation: maintenanceSummary.LastMaintenanceOperation,
                LastMaintenanceDryRun: maintenanceSummary.LastMaintenanceDryRun,
                LastMaintenanceApplied: maintenanceSummary.LastMaintenanceApplied,
                LastMaintenanceReason: maintenanceSummary.LastMaintenanceReason,
                LastMaintenanceBeforeDays: maintenanceSummary.LastMaintenanceBeforeDays,
                LastMaintenanceArchiveBeforeUtcDate: maintenanceSummary.LastMaintenanceArchiveBeforeUtcDate,
                LastMaintenanceCandidateSegmentCount: maintenanceSummary.LastMaintenanceCandidateSegmentCount,
                LastMaintenanceArchivedSegmentCount: maintenanceSummary.LastMaintenanceArchivedSegmentCount,
                LastMaintenanceArchivedByteCount: maintenanceSummary.LastMaintenanceArchivedByteCount,
                LastMaintenanceArchivedRecordCount: maintenanceSummary.LastMaintenanceArchivedRecordCount,
                LastMaintenanceRecommendedAction: maintenanceSummary.LastMaintenanceRecommendedAction,
                VerificationSummaryPath: verificationSummary.Path,
                VerificationSummaryExists: verificationSummary.Exists,
                VerificationSummarySchemaVersion: VerificationSummarySchemaVersion,
                LastVerificationAtUtc: verificationSummary.LastVerificationAtUtc,
                LastVerificationPassed: verificationSummary.LastVerificationPassed,
                LastVerificationPosture: verificationSummary.LastVerificationPosture,
                LastVerificationPossiblyTransient: verificationSummary.LastVerificationPossiblyTransient,
                LastVerificationConsistencyMode: verificationSummary.LastVerificationConsistencyMode,
                LastVerificationSnapshotLockAcquired: verificationSummary.LastVerificationSnapshotLockAcquired,
                LastVerificationIssueCount: verificationSummary.LastVerificationIssueCount,
                LastVerificationStoredManifestRecordCount: verificationSummary.LastVerificationStoredManifestRecordCount,
                LastVerificationActualManifestRecordCount: verificationSummary.LastVerificationActualManifestRecordCount,
                LastVerificationStoredManifestByteCount: verificationSummary.LastVerificationStoredManifestByteCount,
                LastVerificationActualManifestByteCount: verificationSummary.LastVerificationActualManifestByteCount,
                LastVerificationRecommendedAction: verificationSummary.LastVerificationRecommendedAction,
                LastVerificationManifestGeneratedAtUtc: verificationSummary.LastVerificationManifestGeneratedAtUtc,
                LastVerificationManifestSha256: verificationSummary.LastVerificationManifestSha256,
                LastVerificationCheckpointLatestSequence: verificationSummary.LastVerificationCheckpointLatestSequence,
                LastVerificationCheckpointLatestCheckpointSha256: verificationSummary.LastVerificationCheckpointLatestCheckpointSha256,
                LastVerificationCheckpointLatestManifestSha256: verificationSummary.LastVerificationCheckpointLatestManifestSha256,
                ActiveSegmentPath: activeSegmentPath,
                SegmentCount: 0,
                ArchiveSegmentCount: archivedFiles.Count,
                ArchiveByteCount: archivedMetrics.ByteCount,
                LegacyJournalPath: path,
                LegacyJournalExists: true,
                LegacyFallbackUsed: true,
                ManifestPath: manifestPath,
                ManifestExists: manifestExists,
                ManifestSchemaVersion: ManifestSchemaVersion,
                CheckpointChainPath: checkpointStatus.Path,
                CheckpointChainExists: checkpointStatus.Exists,
                CheckpointChainCount: checkpointStatus.CheckpointCount,
                CheckpointChainLatestSequence: checkpointStatus.LatestSequence,
                CheckpointChainLatestCheckpointSha256: checkpointStatus.LatestCheckpointSha256,
                CheckpointChainLatestManifestSha256: checkpointStatus.LatestManifestSha256,
                ManifestGeneratedAtUtc: string.Empty,
                ManifestRecordCount: 0,
                ManifestByteCount: 0,
                ManifestFirstTimestampUtc: string.Empty,
                ManifestLastTimestampUtc: string.Empty,
                Segments: Array.Empty<GatewayActivityJournalSegmentManifest>());
        }

        return new GatewayActivityJournalStatus(
            activeSegmentPath,
            Exists: false,
            ByteCount: 0,
            LineCount: 0,
            StorageMode: StorageModeSegmentedAppendOnly,
            StoreDirectory: storeDirectory,
            SegmentDirectory: segmentDirectory,
            ArchiveDirectory: archiveDirectory,
            RetentionMode: RetentionMode,
            RetentionExecutionMode: RetentionExecutionMode,
            DefaultArchiveBeforeDays: DefaultArchiveBeforeDays,
            WriterLockMode: WriterLockMode,
            LockPath: lockPath,
            LockExists: lockStatus.FileExists,
            LockFileExists: lockStatus.FileExists,
            LockCurrentlyHeld: lockStatus.CurrentlyHeld,
            LockStatus: lockStatus.Status,
            LockLastHolderProcessId: lockStatus.LastHolderProcessId,
            LockLastAcquiredAtUtc: lockStatus.LastAcquiredAtUtc,
            LockAcquireTimeoutMs: StoreLockAcquireTimeoutMilliseconds,
            IntegrityMode: IntegrityMode,
            DropTelemetryPath: dropTelemetry.Path,
            DropTelemetryExists: dropTelemetry.Exists,
            DroppedActivityCount: dropTelemetry.DroppedActivityCount,
            LastDropAtUtc: dropTelemetry.LastDropAtUtc,
            LastDropReason: dropTelemetry.LastDropReason,
            LastDropEvent: dropTelemetry.LastDropEvent,
            LastDropRequestId: dropTelemetry.LastDropRequestId,
            MaintenanceSummaryPath: maintenanceSummary.Path,
            MaintenanceSummaryExists: maintenanceSummary.Exists,
            MaintenanceSummarySchemaVersion: MaintenanceSummarySchemaVersion,
            LastMaintenanceAtUtc: maintenanceSummary.LastMaintenanceAtUtc,
            LastMaintenanceOperation: maintenanceSummary.LastMaintenanceOperation,
            LastMaintenanceDryRun: maintenanceSummary.LastMaintenanceDryRun,
            LastMaintenanceApplied: maintenanceSummary.LastMaintenanceApplied,
            LastMaintenanceReason: maintenanceSummary.LastMaintenanceReason,
            LastMaintenanceBeforeDays: maintenanceSummary.LastMaintenanceBeforeDays,
            LastMaintenanceArchiveBeforeUtcDate: maintenanceSummary.LastMaintenanceArchiveBeforeUtcDate,
            LastMaintenanceCandidateSegmentCount: maintenanceSummary.LastMaintenanceCandidateSegmentCount,
            LastMaintenanceArchivedSegmentCount: maintenanceSummary.LastMaintenanceArchivedSegmentCount,
            LastMaintenanceArchivedByteCount: maintenanceSummary.LastMaintenanceArchivedByteCount,
            LastMaintenanceArchivedRecordCount: maintenanceSummary.LastMaintenanceArchivedRecordCount,
            LastMaintenanceRecommendedAction: maintenanceSummary.LastMaintenanceRecommendedAction,
            VerificationSummaryPath: verificationSummary.Path,
            VerificationSummaryExists: verificationSummary.Exists,
            VerificationSummarySchemaVersion: VerificationSummarySchemaVersion,
            LastVerificationAtUtc: verificationSummary.LastVerificationAtUtc,
            LastVerificationPassed: verificationSummary.LastVerificationPassed,
            LastVerificationPosture: verificationSummary.LastVerificationPosture,
            LastVerificationPossiblyTransient: verificationSummary.LastVerificationPossiblyTransient,
            LastVerificationConsistencyMode: verificationSummary.LastVerificationConsistencyMode,
            LastVerificationSnapshotLockAcquired: verificationSummary.LastVerificationSnapshotLockAcquired,
            LastVerificationIssueCount: verificationSummary.LastVerificationIssueCount,
            LastVerificationStoredManifestRecordCount: verificationSummary.LastVerificationStoredManifestRecordCount,
            LastVerificationActualManifestRecordCount: verificationSummary.LastVerificationActualManifestRecordCount,
            LastVerificationStoredManifestByteCount: verificationSummary.LastVerificationStoredManifestByteCount,
            LastVerificationActualManifestByteCount: verificationSummary.LastVerificationActualManifestByteCount,
            LastVerificationRecommendedAction: verificationSummary.LastVerificationRecommendedAction,
            LastVerificationManifestGeneratedAtUtc: verificationSummary.LastVerificationManifestGeneratedAtUtc,
            LastVerificationManifestSha256: verificationSummary.LastVerificationManifestSha256,
            LastVerificationCheckpointLatestSequence: verificationSummary.LastVerificationCheckpointLatestSequence,
            LastVerificationCheckpointLatestCheckpointSha256: verificationSummary.LastVerificationCheckpointLatestCheckpointSha256,
            LastVerificationCheckpointLatestManifestSha256: verificationSummary.LastVerificationCheckpointLatestManifestSha256,
            ActiveSegmentPath: activeSegmentPath,
            SegmentCount: 0,
            ArchiveSegmentCount: archivedFiles.Count,
            ArchiveByteCount: archivedMetrics.ByteCount,
            LegacyJournalPath: path,
            LegacyJournalExists: false,
            LegacyFallbackUsed: false,
            ManifestPath: manifestPath,
            ManifestExists: manifestExists,
            ManifestSchemaVersion: ManifestSchemaVersion,
            CheckpointChainPath: checkpointStatus.Path,
            CheckpointChainExists: checkpointStatus.Exists,
            CheckpointChainCount: checkpointStatus.CheckpointCount,
            CheckpointChainLatestSequence: checkpointStatus.LatestSequence,
            CheckpointChainLatestCheckpointSha256: checkpointStatus.LatestCheckpointSha256,
            CheckpointChainLatestManifestSha256: checkpointStatus.LatestManifestSha256,
            ManifestGeneratedAtUtc: string.Empty,
            ManifestRecordCount: 0,
            ManifestByteCount: 0,
            ManifestFirstTimestampUtc: string.Empty,
            ManifestLastTimestampUtc: string.Empty,
            Segments: Array.Empty<GatewayActivityJournalSegmentManifest>());
    }

    public static GatewayActivityJournalCompactionResult Compact(string path, int retainLineCount)
    {
        if (retainLineCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retainLineCount), "Retained line count must be positive.");
        }

        using var storeLock = TryAcquireStoreLock(path);
        if (storeLock is null)
        {
            return new GatewayActivityJournalCompactionResult(
                Requested: true,
                Applied: false,
                Reason: "journal_lock_unavailable",
                RetainLineLimit: retainLineCount,
                OriginalLineCount: 0,
                RetainedLineCount: 0);
        }

        var status = Inspect(path);
        if (status.SegmentCount > 0)
        {
            return new GatewayActivityJournalCompactionResult(
                Requested: true,
                Applied: false,
                Reason: "segment_store_append_only",
                RetainLineLimit: retainLineCount,
                OriginalLineCount: status.LineCount,
                RetainedLineCount: status.LineCount);
        }

        if (!status.LegacyJournalExists)
        {
            return new GatewayActivityJournalCompactionResult(
                Requested: true,
                Applied: false,
                Reason: "journal_missing",
                RetainLineLimit: retainLineCount,
                OriginalLineCount: 0,
                RetainedLineCount: 0);
        }

        try
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length <= retainLineCount)
            {
                return new GatewayActivityJournalCompactionResult(
                    Requested: true,
                    Applied: false,
                    Reason: "journal_within_limit",
                    RetainLineLimit: retainLineCount,
                    OriginalLineCount: lines.Length,
                    RetainedLineCount: lines.Length);
            }

            var retainedLines = lines.Skip(lines.Length - retainLineCount).ToArray();
            var temporaryPath = $"{path}.compact-{Guid.NewGuid():N}.tmp";
            File.WriteAllLines(temporaryPath, retainedLines, Utf8WithoutBom);
            File.Move(temporaryPath, path, overwrite: true);
            return new GatewayActivityJournalCompactionResult(
                Requested: true,
                Applied: true,
                Reason: "journal_compacted",
                RetainLineLimit: retainLineCount,
                OriginalLineCount: lines.Length,
                RetainedLineCount: retainedLines.Length);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GatewayActivityJournalCompactionResult(
                Requested: true,
                Applied: false,
                Reason: "journal_compaction_failed",
                RetainLineLimit: retainLineCount,
                OriginalLineCount: 0,
                RetainedLineCount: 0);
        }
    }

    public static GatewayActivityJournalCompactionResult NotRequested(string path, int retainLineCount)
    {
        var status = Inspect(path);
        return new GatewayActivityJournalCompactionResult(
            Requested: false,
            Applied: false,
            Reason: "not_requested",
            RetainLineLimit: retainLineCount,
            OriginalLineCount: status.LineCount,
            RetainedLineCount: status.LineCount);
    }

    public static GatewayActivityJournalArchiveResult Archive(
        string path,
        int beforeDays,
        DateTimeOffset? now = null)
    {
        if (beforeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beforeDays), "Archive before-days must be positive.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        var archiveDirectory = ResolveArchiveDirectory(path);
        using var storeLock = TryAcquireStoreLock(path);
        if (storeLock is null)
        {
            return new GatewayActivityJournalArchiveResult(
                Requested: true,
                Applied: false,
                Reason: "segment_archive_lock_unavailable",
                BeforeDays: beforeDays,
                ArchiveBeforeUtcDate: timestamp.UtcDateTime.Date.AddDays(-beforeDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ArchiveDirectory: archiveDirectory,
                CandidateSegmentCount: 0,
                ArchivedSegmentCount: 0,
                ArchivedSegments: Array.Empty<GatewayActivityJournalArchivedSegment>());
        }

        var result = ArchiveUnderCurrentLock(path, beforeDays, timestamp);
        if (result.ArchivedSegmentCount > 0)
        {
            RefreshManifest(path);
        }

        if (!string.Equals(result.Reason, "segment_store_missing", StringComparison.Ordinal))
        {
            RecordMaintenanceSummary(path, "manual_archive", result, timestamp);
        }

        return result;
    }

    public static GatewayActivityJournalMaintenancePlan PlanMaintenance(
        string path,
        int beforeDays,
        DateTimeOffset? now = null)
    {
        if (beforeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beforeDays), "Maintenance before-days must be positive.");
        }

        var timestamp = now ?? DateTimeOffset.UtcNow;
        var archiveDirectory = ResolveArchiveDirectory(path);
        var archiveBeforeUtcDate = timestamp.UtcDateTime.Date.AddDays(-beforeDays);
        var segmentFiles = ResolveSegmentFiles(path);
        if (segmentFiles.Count == 0)
        {
            return new GatewayActivityJournalMaintenancePlan(
                DryRun: true,
                MaintenanceMode: "read_only_dry_run",
                WillModifyStore: false,
                MaintenanceNeeded: false,
                Reason: "segment_store_missing",
                RecommendedAction: "no maintenance action needed",
                BeforeDays: beforeDays,
                ArchiveBeforeUtcDate: archiveBeforeUtcDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ArchiveDirectory: archiveDirectory,
                ArchiveCandidateSegmentCount: 0,
                ArchiveCandidateByteCount: 0,
                ArchiveCandidateRecordCount: 0,
                ArchiveCandidateSegments: Array.Empty<GatewayActivityJournalArchivedSegment>());
        }

        var candidateSegments = ResolveArchiveCandidates(path, beforeDays, timestamp);
        var plannedSegments = candidateSegments
            .Select(candidate =>
            {
                var manifest = BuildSegmentManifest(candidate.Path);
                var destinationDirectory = Path.Combine(
                    archiveDirectory,
                    candidate.SegmentDateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                var archivePath = ResolveUniqueArchivePath(destinationDirectory, Path.GetFileName(candidate.Path));
                return new GatewayActivityJournalArchivedSegment(
                    SourcePath: candidate.Path,
                    ArchivePath: archivePath,
                    FileName: manifest.FileName,
                    SegmentDateUtc: candidate.SegmentDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ByteCount: manifest.ByteCount,
                    RecordCount: manifest.RecordCount,
                    Sha256: manifest.Sha256);
            })
            .ToArray();
        var maintenanceNeeded = plannedSegments.Length > 0;

        return new GatewayActivityJournalMaintenancePlan(
            DryRun: true,
            MaintenanceMode: "read_only_dry_run",
            WillModifyStore: false,
            MaintenanceNeeded: maintenanceNeeded,
            Reason: maintenanceNeeded ? "archive_candidates_found" : "no_archive_candidates",
            RecommendedAction: maintenanceNeeded
                ? $"gateway activity archive --before-days {beforeDays.ToString(CultureInfo.InvariantCulture)}"
                : "no maintenance action needed",
            BeforeDays: beforeDays,
            ArchiveBeforeUtcDate: archiveBeforeUtcDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ArchiveDirectory: archiveDirectory,
            ArchiveCandidateSegmentCount: plannedSegments.Length,
            ArchiveCandidateByteCount: plannedSegments.Sum(static segment => segment.ByteCount),
            ArchiveCandidateRecordCount: plannedSegments.Sum(static segment => segment.RecordCount),
            ArchiveCandidateSegments: plannedSegments);
    }

    private static GatewayActivityJournalArchiveResult ArchiveUnderCurrentLock(
        string path,
        int beforeDays,
        DateTimeOffset timestamp)
    {
        var archiveDirectory = ResolveArchiveDirectory(path);
        var segmentFiles = ResolveSegmentFiles(path);
        if (segmentFiles.Count == 0)
        {
            return new GatewayActivityJournalArchiveResult(
                Requested: true,
                Applied: false,
                Reason: "segment_store_missing",
                BeforeDays: beforeDays,
                ArchiveBeforeUtcDate: timestamp.UtcDateTime.Date.AddDays(-beforeDays).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ArchiveDirectory: archiveDirectory,
                CandidateSegmentCount: 0,
                ArchivedSegmentCount: 0,
                ArchivedSegments: Array.Empty<GatewayActivityJournalArchivedSegment>());
        }

        var archiveBeforeUtcDate = timestamp.UtcDateTime.Date.AddDays(-beforeDays);
        var candidateSegments = ResolveArchiveCandidates(path, beforeDays, timestamp);

        if (candidateSegments.Length == 0)
        {
            return new GatewayActivityJournalArchiveResult(
                Requested: true,
                Applied: false,
                Reason: "no_archive_candidates",
                BeforeDays: beforeDays,
                ArchiveBeforeUtcDate: archiveBeforeUtcDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ArchiveDirectory: archiveDirectory,
                CandidateSegmentCount: 0,
                ArchivedSegmentCount: 0,
                ArchivedSegments: Array.Empty<GatewayActivityJournalArchivedSegment>());
        }

        var archivedSegments = new List<GatewayActivityJournalArchivedSegment>();
        var archiveFailed = false;
        foreach (var candidate in candidateSegments)
        {
            try
            {
                var manifest = BuildSegmentManifest(candidate.Path);
                var destinationDirectory = Path.Combine(
                    archiveDirectory,
                    candidate.SegmentDateUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(destinationDirectory);
                var archivePath = ResolveUniqueArchivePath(destinationDirectory, Path.GetFileName(candidate.Path));
                File.Move(candidate.Path, archivePath, overwrite: false);
                archivedSegments.Add(new GatewayActivityJournalArchivedSegment(
                    SourcePath: candidate.Path,
                    ArchivePath: archivePath,
                    FileName: manifest.FileName,
                    SegmentDateUtc: candidate.SegmentDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    ByteCount: manifest.ByteCount,
                    RecordCount: manifest.RecordCount,
                    Sha256: manifest.Sha256));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                archiveFailed = true;
                break;
            }
        }

        return new GatewayActivityJournalArchiveResult(
            Requested: true,
            Applied: archivedSegments.Count > 0,
            Reason: archiveFailed ? "segment_archive_partial_failed" : "segments_archived",
            BeforeDays: beforeDays,
            ArchiveBeforeUtcDate: archiveBeforeUtcDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ArchiveDirectory: archiveDirectory,
            CandidateSegmentCount: candidateSegments.Length,
            ArchivedSegmentCount: archivedSegments.Count,
            ArchivedSegments: archivedSegments);
    }

    private static GatewayActivityJournalArchiveCandidate[] ResolveArchiveCandidates(
        string path,
        int beforeDays,
        DateTimeOffset timestamp)
    {
        var activeSegmentPath = ResolveActiveSegmentPath(path, timestamp);
        var activeFullPath = Path.GetFullPath(activeSegmentPath);
        var archiveBeforeUtcDate = timestamp.UtcDateTime.Date.AddDays(-beforeDays);
        return ResolveSegmentFiles(path)
            .Select(segmentPath => new
            {
                Path = segmentPath,
                Date = TryParseSegmentDateUtc(segmentPath),
            })
            .Where(segment => segment.Date is not null)
            .Where(segment => segment.Date!.Value < archiveBeforeUtcDate)
            .Where(segment => !string.Equals(Path.GetFullPath(segment.Path), activeFullPath, StringComparison.Ordinal))
            .Select(segment => new GatewayActivityJournalArchiveCandidate(segment.Path, segment.Date!.Value))
            .ToArray();
    }

    public static GatewayActivityJournalVerificationResult Verify(string path)
    {
        var manifestPath = ResolveManifestPath(path);
        var lockPath = ResolveLockPath(path);
        using var storeLock = TryAcquireStoreLock(path);
        if (storeLock is null)
        {
            var transientIssues = new List<GatewayActivityJournalVerificationIssue>
            {
                new(
                    "verification_lock_unavailable",
                    lockPath,
                    "exclusive_activity_store_lock",
                    $"unavailable_after_{StoreLockAcquireTimeoutMilliseconds.ToString(CultureInfo.InvariantCulture)}ms"),
            };
            return new GatewayActivityJournalVerificationResult(
                Passed: false,
                Posture: "possibly_transient",
                PossiblyTransient: true,
                ConsistencyMode: "store_lock_consistent_snapshot",
                SnapshotLockAcquired: false,
                SnapshotLockStatus: "lock_unavailable",
                SnapshotLockPath: lockPath,
                SnapshotLockAcquireTimeoutMs: StoreLockAcquireTimeoutMilliseconds,
                JournalPath: path,
                ManifestPath: manifestPath,
                SegmentDirectory: ResolveSegmentDirectory(path),
                CheckpointChainPath: ResolveCheckpointPath(path),
                CheckpointChainExists: false,
                CheckpointChainCount: 0,
                CheckpointChainLatestSequence: 0,
                CheckpointChainLatestCheckpointSha256: string.Empty,
                CheckpointChainLatestManifestSha256: string.Empty,
                StoredManifest: null,
                ActualManifest: BuildEmptyManifest(path),
                Issues: transientIssues);
        }

        var status = Inspect(path);
        var issues = new List<GatewayActivityJournalVerificationIssue>();
        var actualManifest = BuildManifest(path);
        var checkpointVerification = VerifyCheckpointChain(path, manifestPath);
        issues.AddRange(checkpointVerification.Issues);
        GatewayActivityJournalManifest? storedManifest = null;

        if (actualManifest.SegmentCount == 0)
        {
            if (status.LegacyJournalExists)
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "legacy_single_file_unverifiable",
                    path,
                    "segmented_append_only",
                    "legacy_single_file"));
            }
            else
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "segment_store_missing",
                    ResolveSegmentDirectory(path),
                    "at_least_one_segment",
                    "none"));
            }
        }

        if (!File.Exists(manifestPath))
        {
            issues.Add(new GatewayActivityJournalVerificationIssue(
                "manifest_missing",
                manifestPath,
                ManifestSchemaVersion,
                "missing"));
        }
        else
        {
            try
            {
                storedManifest = JsonSerializer.Deserialize<GatewayActivityJournalManifest>(
                    File.ReadAllText(manifestPath),
                    JsonOptions);
                if (storedManifest is null)
                {
                    issues.Add(new GatewayActivityJournalVerificationIssue(
                        "manifest_empty",
                        manifestPath,
                        ManifestSchemaVersion,
                        "null"));
                }
                else
                {
                    CompareManifests(issues, manifestPath, storedManifest, actualManifest);
                }
            }
            catch (JsonException exception)
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "manifest_invalid_json",
                    manifestPath,
                    "valid_json",
                    exception.GetType().Name));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "manifest_read_failed",
                    manifestPath,
                    "readable",
                    exception.GetType().Name));
            }
        }

        var result = new GatewayActivityJournalVerificationResult(
            Passed: issues.Count == 0,
            Posture: issues.Count == 0 ? "verified" : "failed",
            PossiblyTransient: false,
            ConsistencyMode: "store_lock_consistent_snapshot",
            SnapshotLockAcquired: true,
            SnapshotLockStatus: "acquired",
            SnapshotLockPath: lockPath,
            SnapshotLockAcquireTimeoutMs: StoreLockAcquireTimeoutMilliseconds,
            JournalPath: path,
            ManifestPath: manifestPath,
            SegmentDirectory: ResolveSegmentDirectory(path),
            CheckpointChainPath: checkpointVerification.Path,
            CheckpointChainExists: checkpointVerification.Exists,
            CheckpointChainCount: checkpointVerification.CheckpointCount,
            CheckpointChainLatestSequence: checkpointVerification.LatestSequence,
            CheckpointChainLatestCheckpointSha256: checkpointVerification.LatestCheckpointSha256,
            CheckpointChainLatestManifestSha256: checkpointVerification.LatestManifestSha256,
            StoredManifest: storedManifest,
            ActualManifest: actualManifest,
            Issues: issues);
        RecordVerificationSummary(path, result, DateTimeOffset.UtcNow);
        return result;
    }

    private static IReadOnlyList<GatewayActivityJournalRecord> ReadTailFromFiles(
        IReadOnlyList<string> paths,
        int lineCount)
    {
        try
        {
            var tail = new Queue<string>(capacity: lineCount);
            foreach (var filePath in paths)
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    if (tail.Count == lineCount)
                    {
                        tail.Dequeue();
                    }

                    tail.Enqueue(line);
                }
            }

            var records = new List<GatewayActivityJournalRecord>();
            foreach (var line in tail)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<GatewayActivityJournalRecord>(line, JsonOptions);
                    if (record is not null)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return records;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }
    }

    private static IReadOnlyList<GatewayActivityJournalRecord> ReadSinceFromFiles(
        IReadOnlyList<string> paths,
        DateTimeOffset sinceUtc)
    {
        try
        {
            var records = new List<GatewayActivityJournalRecord>();
            foreach (var filePath in paths)
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var record = JsonSerializer.Deserialize<GatewayActivityJournalRecord>(line, JsonOptions);
                        if (record is not null && record.Timestamp >= sinceUtc)
                        {
                            records.Add(record);
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }
            }

            return records;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }
    }

    private static IReadOnlyList<GatewayActivityJournalRecord> ReadMatchingFromFiles(
        IReadOnlyList<string> paths,
        Func<GatewayActivityJournalRecord, bool> predicate)
    {
        try
        {
            var records = new List<GatewayActivityJournalRecord>();
            foreach (var filePath in paths)
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var record = JsonSerializer.Deserialize<GatewayActivityJournalRecord>(line, JsonOptions);
                        if (record is not null && predicate(record))
                        {
                            records.Add(record);
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }
            }

            return records;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<GatewayActivityJournalRecord>();
        }
    }

    private static GatewayActivityJournalFileMetrics InspectFiles(IReadOnlyList<string> paths)
    {
        long byteCount = 0;
        long lineCount = 0;
        foreach (var filePath in paths)
        {
            try
            {
                var file = new FileInfo(filePath);
                byteCount += file.Length;
                lineCount += File.ReadLines(filePath).LongCount();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return new GatewayActivityJournalFileMetrics(byteCount, lineCount);
    }

    private static GatewayActivityJournalCheckpointChainStatus InspectCheckpointChain(string legacyPath)
    {
        var checkpointPath = ResolveCheckpointPath(legacyPath);
        if (string.IsNullOrWhiteSpace(checkpointPath) || !File.Exists(checkpointPath))
        {
            return new GatewayActivityJournalCheckpointChainStatus(
                Path: checkpointPath,
                Exists: false,
                CheckpointCount: 0,
                LatestSequence: 0,
                LatestCheckpointSha256: string.Empty,
                LatestManifestSha256: string.Empty);
        }

        var checkpoints = ReadCheckpointRecords(checkpointPath);
        var latest = checkpoints.LastOrDefault();
        return new GatewayActivityJournalCheckpointChainStatus(
            Path: checkpointPath,
            Exists: true,
            CheckpointCount: checkpoints.Count,
            LatestSequence: latest?.Sequence ?? 0,
            LatestCheckpointSha256: latest?.CheckpointSha256 ?? string.Empty,
            LatestManifestSha256: latest?.ManifestSha256 ?? string.Empty);
    }

    private static GatewayActivityJournalCheckpointChainVerification VerifyCheckpointChain(
        string legacyPath,
        string manifestPath)
    {
        var checkpointPath = ResolveCheckpointPath(legacyPath);
        var issues = new List<GatewayActivityJournalVerificationIssue>();
        if (!File.Exists(checkpointPath))
        {
            if (File.Exists(manifestPath))
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "manifest_checkpoint_chain_missing",
                    checkpointPath,
                    CheckpointSchemaVersion,
                    "missing"));
            }

            return new GatewayActivityJournalCheckpointChainVerification(
                Path: checkpointPath,
                Exists: false,
                CheckpointCount: 0,
                LatestSequence: 0,
                LatestCheckpointSha256: string.Empty,
                LatestManifestSha256: string.Empty,
                Issues: issues);
        }

        var checkpoints = new List<GatewayActivityJournalCheckpoint>();
        try
        {
            var expectedSequence = 1L;
            var expectedPreviousCheckpointSha256 = string.Empty;
            foreach (var line in File.ReadLines(checkpointPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                GatewayActivityJournalCheckpoint? checkpoint;
                try
                {
                    checkpoint = JsonSerializer.Deserialize<GatewayActivityJournalCheckpoint>(line, JsonOptions);
                }
                catch (JsonException exception)
                {
                    issues.Add(new GatewayActivityJournalVerificationIssue(
                        "manifest_checkpoint_invalid_json",
                        checkpointPath,
                        "valid_json",
                        exception.GetType().Name));
                    continue;
                }

                if (checkpoint is null)
                {
                    issues.Add(new GatewayActivityJournalVerificationIssue(
                        "manifest_checkpoint_empty",
                        checkpointPath,
                        CheckpointSchemaVersion,
                        "null"));
                    continue;
                }

                checkpoints.Add(checkpoint);
                AddIssueIfNotEqual(issues, "manifest_checkpoint_schema_mismatch", checkpointPath, CheckpointSchemaVersion, checkpoint.SchemaVersion);
                AddIssueIfNotEqual(issues, "manifest_checkpoint_sequence_mismatch", checkpointPath, expectedSequence.ToString(CultureInfo.InvariantCulture), checkpoint.Sequence.ToString(CultureInfo.InvariantCulture));
                AddIssueIfNotEqual(issues, "manifest_checkpoint_previous_hash_mismatch", checkpointPath, expectedPreviousCheckpointSha256, checkpoint.PreviousCheckpointSha256);

                var expectedCheckpointSha256 = ComputeCheckpointSha256(checkpoint);
                AddIssueIfNotEqual(issues, "manifest_checkpoint_hash_mismatch", checkpointPath, expectedCheckpointSha256, checkpoint.CheckpointSha256);

                expectedPreviousCheckpointSha256 = expectedCheckpointSha256;
                expectedSequence++;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            issues.Add(new GatewayActivityJournalVerificationIssue(
                "manifest_checkpoint_chain_read_failed",
                checkpointPath,
                "readable",
                exception.GetType().Name));
        }

        if (checkpoints.Count == 0)
        {
            issues.Add(new GatewayActivityJournalVerificationIssue(
                "manifest_checkpoint_chain_empty",
                checkpointPath,
                CheckpointSchemaVersion,
                "empty"));
        }

        var latest = checkpoints.LastOrDefault();
        if (latest is not null && File.Exists(manifestPath))
        {
            AddIssueIfNotEqual(issues, "manifest_checkpoint_path_mismatch", checkpointPath, manifestPath, latest.ManifestPath);
            AddIssueIfNotEqual(issues, "manifest_checkpoint_manifest_sha256_mismatch", manifestPath, ComputeSha256(manifestPath), latest.ManifestSha256);
        }

        return new GatewayActivityJournalCheckpointChainVerification(
            Path: checkpointPath,
            Exists: true,
            CheckpointCount: checkpoints.Count,
            LatestSequence: latest?.Sequence ?? 0,
            LatestCheckpointSha256: latest?.CheckpointSha256 ?? string.Empty,
            LatestManifestSha256: latest?.ManifestSha256 ?? string.Empty,
            Issues: issues);
    }

    private static GatewayActivityJournalManifest BuildManifest(string legacyPath)
    {
        var storeDirectory = ResolveStoreDirectory(legacyPath);
        var segmentDirectory = ResolveSegmentDirectory(legacyPath);
        var segments = ResolveSegmentFiles(legacyPath)
            .Select(BuildSegmentManifest)
            .ToArray();
        var firstTimestamp = segments
            .Select(static segment => ParseTimestamp(segment.FirstTimestampUtc))
            .Where(static timestamp => timestamp is not null)
            .MinBy(static timestamp => timestamp!.Value);
        var lastTimestamp = segments
            .Select(static segment => ParseTimestamp(segment.LastTimestampUtc))
            .Where(static timestamp => timestamp is not null)
            .MaxBy(static timestamp => timestamp!.Value);

        return new GatewayActivityJournalManifest(
            ManifestSchemaVersion,
            storeDirectory,
            segmentDirectory,
            DateTimeOffset.UtcNow.ToString("O"),
            segments.Length,
            segments.Sum(static segment => segment.RecordCount),
            segments.Sum(static segment => segment.ByteCount),
            firstTimestamp?.ToString("O") ?? string.Empty,
            lastTimestamp?.ToString("O") ?? string.Empty,
            segments);
    }

    private static GatewayActivityJournalManifest? ReadManifest(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GatewayActivityJournalManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static GatewayActivityJournalManifest BuildEmptyManifest(string legacyPath)
    {
        return new GatewayActivityJournalManifest(
            ManifestSchemaVersion,
            ResolveStoreDirectory(legacyPath),
            ResolveSegmentDirectory(legacyPath),
            string.Empty,
            SegmentCount: 0,
            RecordCount: 0,
            ByteCount: 0,
            FirstTimestampUtc: string.Empty,
            LastTimestampUtc: string.Empty,
            Segments: Array.Empty<GatewayActivityJournalSegmentManifest>());
    }

    private static void CompareManifests(
        List<GatewayActivityJournalVerificationIssue> issues,
        string manifestPath,
        GatewayActivityJournalManifest stored,
        GatewayActivityJournalManifest actual)
    {
        AddIssueIfNotEqual(issues, "manifest_schema_mismatch", manifestPath, ManifestSchemaVersion, stored.SchemaVersion);
        AddIssueIfNotEqual(issues, "segment_count_mismatch", manifestPath, actual.SegmentCount.ToString(), stored.SegmentCount.ToString());
        AddIssueIfNotEqual(issues, "record_count_mismatch", manifestPath, actual.RecordCount.ToString(), stored.RecordCount.ToString());
        AddIssueIfNotEqual(issues, "byte_count_mismatch", manifestPath, actual.ByteCount.ToString(), stored.ByteCount.ToString());
        AddIssueIfNotEqual(issues, "first_timestamp_mismatch", manifestPath, actual.FirstTimestampUtc, stored.FirstTimestampUtc);
        AddIssueIfNotEqual(issues, "last_timestamp_mismatch", manifestPath, actual.LastTimestampUtc, stored.LastTimestampUtc);

        var actualSegments = actual.Segments ?? Array.Empty<GatewayActivityJournalSegmentManifest>();
        var storedSegments = stored.Segments ?? Array.Empty<GatewayActivityJournalSegmentManifest>();
        var actualByName = actualSegments
            .GroupBy(static segment => segment.FileName, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var storedByName = new Dictionary<string, GatewayActivityJournalSegmentManifest>(StringComparer.Ordinal);
        foreach (var storedSegment in storedSegments)
        {
            if (storedByName.ContainsKey(storedSegment.FileName))
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "segment_duplicate_in_manifest",
                    storedSegment.Path,
                    "unique_file_name",
                    storedSegment.FileName));
                continue;
            }

            storedByName[storedSegment.FileName] = storedSegment;
        }

        foreach (var storedSegment in storedSegments)
        {
            if (!actualByName.TryGetValue(storedSegment.FileName, out var actualSegment))
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "segment_missing",
                    storedSegment.Path,
                    "present",
                    "missing"));
                continue;
            }

            AddIssueIfNotEqual(issues, "segment_path_mismatch", storedSegment.FileName, actualSegment.Path, storedSegment.Path);
            AddIssueIfNotEqual(issues, "segment_record_count_mismatch", storedSegment.Path, actualSegment.RecordCount.ToString(), storedSegment.RecordCount.ToString());
            AddIssueIfNotEqual(issues, "segment_byte_count_mismatch", storedSegment.Path, actualSegment.ByteCount.ToString(), storedSegment.ByteCount.ToString());
            AddIssueIfNotEqual(issues, "segment_first_timestamp_mismatch", storedSegment.Path, actualSegment.FirstTimestampUtc, storedSegment.FirstTimestampUtc);
            AddIssueIfNotEqual(issues, "segment_last_timestamp_mismatch", storedSegment.Path, actualSegment.LastTimestampUtc, storedSegment.LastTimestampUtc);
            AddIssueIfNotEqual(issues, "segment_sha256_mismatch", storedSegment.Path, actualSegment.Sha256, storedSegment.Sha256);
        }

        foreach (var actualSegment in actualSegments)
        {
            if (!storedByName.ContainsKey(actualSegment.FileName))
            {
                issues.Add(new GatewayActivityJournalVerificationIssue(
                    "segment_unindexed",
                    actualSegment.Path,
                    "indexed",
                    "unindexed"));
            }
        }
    }

    private static void AddIssueIfNotEqual(
        List<GatewayActivityJournalVerificationIssue> issues,
        string code,
        string path,
        string expected,
        string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(new GatewayActivityJournalVerificationIssue(code, path, expected, actual));
    }

    private static GatewayActivityJournalSegmentManifest BuildSegmentManifest(string path)
    {
        try
        {
            var file = new FileInfo(path);
            var recordCount = 0L;
            DateTimeOffset? firstTimestamp = null;
            DateTimeOffset? lastTimestamp = null;
            foreach (var line in File.ReadLines(path))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                recordCount++;
                try
                {
                    var record = JsonSerializer.Deserialize<GatewayActivityJournalRecord>(line, JsonOptions);
                    if (record is null)
                    {
                        continue;
                    }

                    firstTimestamp ??= record.Timestamp;
                    lastTimestamp = record.Timestamp;
                }
                catch (JsonException)
                {
                }
            }

            return new GatewayActivityJournalSegmentManifest(
                FileName: file.Name,
                Path: path,
                ByteCount: file.Exists ? file.Length : 0,
                RecordCount: recordCount,
                FirstTimestampUtc: firstTimestamp?.ToString("O") ?? string.Empty,
                LastTimestampUtc: lastTimestamp?.ToString("O") ?? string.Empty,
                Sha256: ComputeSha256(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GatewayActivityJournalSegmentManifest(
                FileName: Path.GetFileName(path),
                Path: path,
                ByteCount: 0,
                RecordCount: 0,
                FirstTimestampUtc: string.Empty,
                LastTimestampUtc: string.Empty,
                Sha256: string.Empty);
        }
    }

    private static DateTimeOffset? ParseTimestamp(string value)
    {
        return DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : null;
    }

    private static string ComputeSha256(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string ComputeSha256FromText(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    private static string ComputeCheckpointSha256(GatewayActivityJournalCheckpoint checkpoint)
    {
        var payload = new GatewayActivityJournalCheckpointPayload(
            checkpoint.SchemaVersion,
            checkpoint.Sequence,
            checkpoint.GeneratedAtUtc,
            checkpoint.ManifestPath,
            checkpoint.ManifestSha256,
            checkpoint.PreviousCheckpointSha256);
        return ComputeSha256FromText(JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static void RefreshManifest(string legacyPath)
    {
        var manifestPath = ResolveManifestPath(legacyPath);
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var temporaryPath = $"{manifestPath}.tmp-{Guid.NewGuid():N}";
        var manifest = BuildManifest(legacyPath);
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(manifest, JsonOptions), Utf8WithoutBom);
        File.Move(temporaryPath, manifestPath, overwrite: true);
        AppendManifestCheckpoint(legacyPath, manifestPath);
    }

    private static void AppendManifestCheckpoint(string legacyPath, string manifestPath)
    {
        var checkpointPath = ResolveCheckpointPath(legacyPath);
        Directory.CreateDirectory(Path.GetDirectoryName(checkpointPath)!);
        var previousCheckpoint = ReadLatestCheckpoint(checkpointPath);
        var checkpoint = new GatewayActivityJournalCheckpoint
        {
            SchemaVersion = CheckpointSchemaVersion,
            Sequence = (previousCheckpoint?.Sequence ?? 0) + 1,
            GeneratedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            ManifestPath = manifestPath,
            ManifestSha256 = ComputeSha256(manifestPath),
            PreviousCheckpointSha256 = previousCheckpoint?.CheckpointSha256 ?? string.Empty,
        };
        checkpoint.CheckpointSha256 = ComputeCheckpointSha256(checkpoint);
        AppendLine(checkpointPath, JsonSerializer.Serialize(checkpoint, JsonOptions));
    }

    private static GatewayActivityJournalCheckpoint? ReadLatestCheckpoint(string checkpointPath)
    {
        return ReadCheckpointRecords(checkpointPath).LastOrDefault();
    }

    private static IReadOnlyList<GatewayActivityJournalCheckpoint> ReadCheckpointRecords(string checkpointPath)
    {
        if (string.IsNullOrWhiteSpace(checkpointPath) || !File.Exists(checkpointPath))
        {
            return Array.Empty<GatewayActivityJournalCheckpoint>();
        }

        try
        {
            var checkpoints = new List<GatewayActivityJournalCheckpoint>();
            foreach (var line in File.ReadLines(checkpointPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var checkpoint = JsonSerializer.Deserialize<GatewayActivityJournalCheckpoint>(line, JsonOptions);
                    if (checkpoint is not null)
                    {
                        checkpoints.Add(checkpoint);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return checkpoints;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<GatewayActivityJournalCheckpoint>();
        }
    }

    private static void AppendLine(string path, string line)
    {
        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, Utf8WithoutBom);
        writer.WriteLine(line);
    }

    private static FileStream? TryAcquireStoreLock(string legacyPath)
    {
        var lockPath = ResolveLockPath(legacyPath);
        return TryAcquireFileLock(lockPath, StoreLockAcquireTimeout);
    }

    private static FileStream? TryAcquireDropTelemetryLock(string legacyPath)
    {
        var lockPath = ResolveDropTelemetryLockPath(legacyPath);
        return TryAcquireFileLock(lockPath, DropTelemetryLockAcquireTimeout);
    }

    private static FileStream? TryAcquireFileLock(string lockPath, TimeSpan timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        var deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (true)
        {
            try
            {
                var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var payload = $"{Environment.ProcessId} {DateTimeOffset.UtcNow:O}{Environment.NewLine}";
                stream.SetLength(0);
                stream.Write(Utf8WithoutBom.GetBytes(payload));
                stream.Flush(flushToDisk: true);
                return stream;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    return null;
                }

                Thread.Sleep(25);
            }
        }
    }

    private static GatewayActivityLockStatus InspectLockStatus(string lockPath)
    {
        if (string.IsNullOrWhiteSpace(lockPath) || !File.Exists(lockPath))
        {
            return new GatewayActivityLockStatus(
                Path: lockPath,
                FileExists: false,
                CurrentlyHeld: false,
                Status: "missing",
                LastHolderProcessId: string.Empty,
                LastAcquiredAtUtc: string.Empty);
        }

        try
        {
            using (new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }

            var metadata = ReadLockMetadata(lockPath);
            return new GatewayActivityLockStatus(
                Path: lockPath,
                FileExists: true,
                CurrentlyHeld: false,
                Status: "available",
                LastHolderProcessId: metadata.ProcessId,
                LastAcquiredAtUtc: metadata.AcquiredAtUtc);
        }
        catch (UnauthorizedAccessException)
        {
            return new GatewayActivityLockStatus(
                Path: lockPath,
                FileExists: true,
                CurrentlyHeld: false,
                Status: "unknown",
                LastHolderProcessId: string.Empty,
                LastAcquiredAtUtc: string.Empty);
        }
        catch (IOException)
        {
            return new GatewayActivityLockStatus(
                Path: lockPath,
                FileExists: File.Exists(lockPath),
                CurrentlyHeld: File.Exists(lockPath),
                Status: File.Exists(lockPath) ? "held" : "missing",
                LastHolderProcessId: string.Empty,
                LastAcquiredAtUtc: string.Empty);
        }
    }

    private static GatewayActivityLockMetadata ReadLockMetadata(string lockPath)
    {
        try
        {
            var payload = File.ReadAllText(lockPath, Utf8WithoutBom).Trim();
            var parts = payload.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return new GatewayActivityLockMetadata(
                ProcessId: parts.Length > 0 ? parts[0] : string.Empty,
                AcquiredAtUtc: parts.Length > 1 ? parts[1] : string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new GatewayActivityLockMetadata(string.Empty, string.Empty);
        }
    }

    private static void RecordDroppedActivity(
        string legacyPath,
        DateTimeOffset timestamp,
        string eventName,
        IReadOnlyDictionary<string, string> fields,
        string reason)
    {
        try
        {
            using var dropLock = TryAcquireDropTelemetryLock(legacyPath);
            if (dropLock is null)
            {
                return;
            }

            var current = ReadDropTelemetry(legacyPath);
            var telemetryPath = ResolveDropTelemetryPath(legacyPath);
            var telemetry = new GatewayActivityDropTelemetry
            {
                SchemaVersion = DropTelemetrySchemaVersion,
                DroppedActivityCount = current.DroppedActivityCount + 1,
                LastDropAtUtc = timestamp.ToString("O"),
                LastDropReason = reason,
                LastDropEvent = eventName,
                LastDropRequestId = fields.TryGetValue("request_id", out var requestId) ? requestId : string.Empty,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(telemetryPath)!);
            var temporaryPath = $"{telemetryPath}.tmp-{Guid.NewGuid():N}";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(telemetry, JsonOptions), Utf8WithoutBom);
            File.Move(temporaryPath, telemetryPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    private static GatewayActivityDropTelemetryStatus ReadDropTelemetry(string legacyPath)
    {
        var telemetryPath = ResolveDropTelemetryPath(legacyPath);
        if (string.IsNullOrWhiteSpace(telemetryPath) || !File.Exists(telemetryPath))
        {
            return new GatewayActivityDropTelemetryStatus(
                telemetryPath,
                Exists: false,
                DroppedActivityCount: 0,
                LastDropAtUtc: string.Empty,
                LastDropReason: string.Empty,
                LastDropEvent: string.Empty,
                LastDropRequestId: string.Empty);
        }

        try
        {
            var telemetry = JsonSerializer.Deserialize<GatewayActivityDropTelemetry>(
                File.ReadAllText(telemetryPath),
                JsonOptions);
            return new GatewayActivityDropTelemetryStatus(
                telemetryPath,
                Exists: true,
                DroppedActivityCount: telemetry?.DroppedActivityCount ?? 0,
                LastDropAtUtc: telemetry?.LastDropAtUtc ?? string.Empty,
                LastDropReason: telemetry?.LastDropReason ?? string.Empty,
                LastDropEvent: telemetry?.LastDropEvent ?? string.Empty,
                LastDropRequestId: telemetry?.LastDropRequestId ?? string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new GatewayActivityDropTelemetryStatus(
                telemetryPath,
                Exists: true,
                DroppedActivityCount: 0,
                LastDropAtUtc: string.Empty,
                LastDropReason: "drop_telemetry_unreadable",
                LastDropEvent: string.Empty,
                LastDropRequestId: string.Empty);
        }
    }

    private static void RecordMaintenanceSummary(
        string legacyPath,
        string operation,
        GatewayActivityJournalArchiveResult result,
        DateTimeOffset timestamp)
    {
        try
        {
            var summaryPath = ResolveMaintenanceSummaryPath(legacyPath);
            var summary = new GatewayActivityMaintenanceSummary
            {
                SchemaVersion = MaintenanceSummarySchemaVersion,
                UpdatedAtUtc = timestamp.ToString("O"),
                Operation = operation,
                DryRun = false,
                Applied = result.Applied,
                Reason = result.Reason,
                BeforeDays = result.BeforeDays,
                ArchiveBeforeUtcDate = result.ArchiveBeforeUtcDate,
                ArchiveDirectory = result.ArchiveDirectory,
                CandidateSegmentCount = result.CandidateSegmentCount,
                ArchivedSegmentCount = result.ArchivedSegmentCount,
                ArchivedByteCount = result.ArchivedSegments.Sum(static segment => segment.ByteCount),
                ArchivedRecordCount = result.ArchivedSegments.Sum(static segment => segment.RecordCount),
                RecommendedAction = ResolveMaintenanceRecommendedAction(result),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
            var temporaryPath = $"{summaryPath}.tmp-{Guid.NewGuid():N}";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(summary, JsonOptions), Utf8WithoutBom);
            File.Move(temporaryPath, summaryPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    private static GatewayActivityMaintenanceSummaryStatus ReadMaintenanceSummary(string legacyPath)
    {
        var summaryPath = ResolveMaintenanceSummaryPath(legacyPath);
        if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
        {
            return GatewayActivityMaintenanceSummaryStatus.Empty(summaryPath);
        }

        try
        {
            var summary = JsonSerializer.Deserialize<GatewayActivityMaintenanceSummary>(
                File.ReadAllText(summaryPath),
                JsonOptions);
            if (summary is null)
            {
                return GatewayActivityMaintenanceSummaryStatus.Unreadable(summaryPath);
            }

            return new GatewayActivityMaintenanceSummaryStatus(
                Path: summaryPath,
                Exists: true,
                LastMaintenanceAtUtc: summary.UpdatedAtUtc,
                LastMaintenanceOperation: summary.Operation,
                LastMaintenanceDryRun: summary.DryRun,
                LastMaintenanceApplied: summary.Applied,
                LastMaintenanceReason: summary.Reason,
                LastMaintenanceBeforeDays: summary.BeforeDays,
                LastMaintenanceArchiveBeforeUtcDate: summary.ArchiveBeforeUtcDate,
                LastMaintenanceCandidateSegmentCount: summary.CandidateSegmentCount,
                LastMaintenanceArchivedSegmentCount: summary.ArchivedSegmentCount,
                LastMaintenanceArchivedByteCount: summary.ArchivedByteCount,
                LastMaintenanceArchivedRecordCount: summary.ArchivedRecordCount,
                LastMaintenanceRecommendedAction: summary.RecommendedAction);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return GatewayActivityMaintenanceSummaryStatus.Unreadable(summaryPath);
        }
    }

    private static string ResolveMaintenanceRecommendedAction(GatewayActivityJournalArchiveResult result)
    {
        if (result.ArchivedSegmentCount > 0)
        {
            return "gateway activity verify";
        }

        return string.Equals(result.Reason, "no_archive_candidates", StringComparison.Ordinal)
            ? "no maintenance action needed"
            : "inspect gateway activity archive failure";
    }

    private static void RecordVerificationSummary(
        string legacyPath,
        GatewayActivityJournalVerificationResult result,
        DateTimeOffset timestamp)
    {
        try
        {
            var summaryPath = ResolveVerificationSummaryPath(legacyPath);
            var summary = new GatewayActivityVerificationSummary
            {
                SchemaVersion = VerificationSummarySchemaVersion,
                UpdatedAtUtc = timestamp.ToString("O"),
                Passed = result.Passed,
                Posture = result.Posture,
                PossiblyTransient = result.PossiblyTransient,
                ConsistencyMode = result.ConsistencyMode,
                SnapshotLockAcquired = result.SnapshotLockAcquired,
                IssueCount = result.Issues.Count,
                StoredManifestRecordCount = result.StoredManifest?.RecordCount ?? 0,
                ActualManifestRecordCount = result.ActualManifest.RecordCount,
                StoredManifestByteCount = result.StoredManifest?.ByteCount ?? 0,
                ActualManifestByteCount = result.ActualManifest.ByteCount,
                RecommendedAction = ResolveVerificationRecommendedAction(result),
                ManifestGeneratedAtUtc = result.StoredManifest?.GeneratedAtUtc ?? string.Empty,
                ManifestSha256 = result.CheckpointChainLatestManifestSha256,
                CheckpointLatestSequence = result.CheckpointChainLatestSequence,
                CheckpointLatestCheckpointSha256 = result.CheckpointChainLatestCheckpointSha256,
                CheckpointLatestManifestSha256 = result.CheckpointChainLatestManifestSha256,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
            var temporaryPath = $"{summaryPath}.tmp-{Guid.NewGuid():N}";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(summary, JsonOptions), Utf8WithoutBom);
            File.Move(temporaryPath, summaryPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    private static GatewayActivityVerificationSummaryStatus ReadVerificationSummary(string legacyPath)
    {
        var summaryPath = ResolveVerificationSummaryPath(legacyPath);
        if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
        {
            return GatewayActivityVerificationSummaryStatus.Empty(summaryPath);
        }

        try
        {
            var summary = JsonSerializer.Deserialize<GatewayActivityVerificationSummary>(
                File.ReadAllText(summaryPath),
                JsonOptions);
            if (summary is null)
            {
                return GatewayActivityVerificationSummaryStatus.Unreadable(summaryPath);
            }

            return new GatewayActivityVerificationSummaryStatus(
                Path: summaryPath,
                Exists: true,
                LastVerificationAtUtc: summary.UpdatedAtUtc,
                LastVerificationPassed: summary.Passed,
                LastVerificationPosture: summary.Posture,
                LastVerificationPossiblyTransient: summary.PossiblyTransient,
                LastVerificationConsistencyMode: summary.ConsistencyMode,
                LastVerificationSnapshotLockAcquired: summary.SnapshotLockAcquired,
                LastVerificationIssueCount: summary.IssueCount,
                LastVerificationStoredManifestRecordCount: summary.StoredManifestRecordCount,
                LastVerificationActualManifestRecordCount: summary.ActualManifestRecordCount,
                LastVerificationStoredManifestByteCount: summary.StoredManifestByteCount,
                LastVerificationActualManifestByteCount: summary.ActualManifestByteCount,
                LastVerificationRecommendedAction: summary.RecommendedAction,
                LastVerificationManifestGeneratedAtUtc: summary.ManifestGeneratedAtUtc,
                LastVerificationManifestSha256: summary.ManifestSha256,
                LastVerificationCheckpointLatestSequence: summary.CheckpointLatestSequence,
                LastVerificationCheckpointLatestCheckpointSha256: summary.CheckpointLatestCheckpointSha256,
                LastVerificationCheckpointLatestManifestSha256: summary.CheckpointLatestManifestSha256);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return GatewayActivityVerificationSummaryStatus.Unreadable(summaryPath);
        }
    }

    private static string ResolveVerificationRecommendedAction(GatewayActivityJournalVerificationResult result)
    {
        if (result.PossiblyTransient)
        {
            return "retry gateway activity verify";
        }

        return result.Passed
            ? "gateway activity verified"
            : "inspect gateway activity verify issues";
    }

    private static IReadOnlyList<string> ResolveSegmentFiles(string legacyPath)
    {
        var segmentDirectory = ResolveSegmentDirectory(legacyPath);
        if (string.IsNullOrWhiteSpace(segmentDirectory) || !Directory.Exists(segmentDirectory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory
                .EnumerateFiles(segmentDirectory, "activity-*.jsonl")
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyList<string> ResolveSegmentFilesSince(
        IReadOnlyList<string> segmentFiles,
        DateTimeOffset sinceUtc)
    {
        var sinceDate = sinceUtc.UtcDateTime.Date;
        return segmentFiles
            .Where(segmentPath =>
            {
                var segmentDate = TryParseSegmentDateUtc(segmentPath);
                return segmentDate is null || segmentDate.Value >= sinceDate;
            })
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveArchivedSegmentFiles(string legacyPath)
    {
        var archiveDirectory = ResolveArchiveDirectory(legacyPath);
        if (string.IsNullOrWhiteSpace(archiveDirectory) || !Directory.Exists(archiveDirectory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory
                .EnumerateFiles(archiveDirectory, "activity-*.jsonl", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static DateTime? TryParseSegmentDateUtc(string path)
    {
        const string prefix = "activity-";
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (!fileName.StartsWith(prefix, StringComparison.Ordinal)
            || fileName.Length < prefix.Length + 8)
        {
            return null;
        }

        var dateText = fileName.Substring(prefix.Length, 8);
        return DateTime.TryParseExact(
            dateText,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date)
            ? date.Date
            : null;
    }

    private static string ResolveUniqueArchivePath(string directory, string fileName)
    {
        var archivePath = Path.Combine(directory, fileName);
        if (!File.Exists(archivePath))
        {
            return archivePath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var index = 1; index <= 1_000; index++)
        {
            var candidate = Path.Combine(directory, $"{name}.archive-{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{name}.archive-{Guid.NewGuid():N}{extension}");
    }

    private static string ResolveActiveSegmentPath(string legacyPath, DateTimeOffset timestamp)
    {
        return Path.Combine(
            ResolveSegmentDirectory(legacyPath),
            $"activity-{timestamp.UtcDateTime:yyyyMMdd}-0001.jsonl");
    }

    private static string ResolveSegmentDirectory(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), SegmentDirectoryName);
    }

    private static string ResolveArchiveDirectory(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), ArchiveDirectoryName);
    }

    private static string ResolveStoreDirectory(string legacyPath)
    {
        var runtimeDirectory = string.IsNullOrWhiteSpace(legacyPath)
            ? Path.GetTempPath()
            : Path.GetDirectoryName(legacyPath) ?? Path.GetTempPath();
        return Path.Combine(runtimeDirectory, StoreDirectoryName);
    }

    private static string ResolveLockPath(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), LockFileName);
    }

    private static string ResolveDropTelemetryPath(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), DropTelemetryFileName);
    }

    private static string ResolveDropTelemetryLockPath(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), DropTelemetryLockFileName);
    }

    private static string ResolveMaintenanceSummaryPath(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), MaintenanceSummaryFileName);
    }

    private static string ResolveVerificationSummaryPath(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), VerificationSummaryFileName);
    }

    private static string ResolveManifestPath(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), ManifestFileName);
    }

    private static string ResolveCheckpointPath(string legacyPath)
    {
        return Path.Combine(ResolveStoreDirectory(legacyPath), CheckpointFileName);
    }
}

internal sealed class GatewayActivityJournalRecord
{
    public string SchemaVersion { get; init; } = GatewayActivityJournal.SchemaVersion;

    public DateTimeOffset Timestamp { get; init; }

    public string Event { get; init; } = string.Empty;

    public Dictionary<string, string> Fields { get; init; } = new(StringComparer.Ordinal);
}

internal sealed class GatewayActivityJournalCheckpoint
{
    public string SchemaVersion { get; set; } = GatewayActivityJournal.CheckpointSchemaVersion;

    public long Sequence { get; set; }

    public string GeneratedAtUtc { get; set; } = string.Empty;

    public string ManifestPath { get; set; } = string.Empty;

    public string ManifestSha256 { get; set; } = string.Empty;

    public string PreviousCheckpointSha256 { get; set; } = string.Empty;

    public string CheckpointSha256 { get; set; } = string.Empty;
}

internal sealed class GatewayActivityDropTelemetry
{
    public string SchemaVersion { get; set; } = GatewayActivityJournal.DropTelemetrySchemaVersion;

    public long DroppedActivityCount { get; set; }

    public string LastDropAtUtc { get; set; } = string.Empty;

    public string LastDropReason { get; set; } = string.Empty;

    public string LastDropEvent { get; set; } = string.Empty;

    public string LastDropRequestId { get; set; } = string.Empty;
}

internal sealed class GatewayActivityMaintenanceSummary
{
    public string SchemaVersion { get; set; } = GatewayActivityJournal.MaintenanceSummarySchemaVersion;

    public string UpdatedAtUtc { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public bool DryRun { get; set; }

    public bool Applied { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int BeforeDays { get; set; }

    public string ArchiveBeforeUtcDate { get; set; } = string.Empty;

    public string ArchiveDirectory { get; set; } = string.Empty;

    public int CandidateSegmentCount { get; set; }

    public int ArchivedSegmentCount { get; set; }

    public long ArchivedByteCount { get; set; }

    public long ArchivedRecordCount { get; set; }

    public string RecommendedAction { get; set; } = string.Empty;
}

internal sealed class GatewayActivityVerificationSummary
{
    public string SchemaVersion { get; set; } = GatewayActivityJournal.VerificationSummarySchemaVersion;

    public string UpdatedAtUtc { get; set; } = string.Empty;

    public bool Passed { get; set; }

    public string Posture { get; set; } = string.Empty;

    public bool PossiblyTransient { get; set; }

    public string ConsistencyMode { get; set; } = string.Empty;

    public bool SnapshotLockAcquired { get; set; }

    public int IssueCount { get; set; }

    public long StoredManifestRecordCount { get; set; }

    public long ActualManifestRecordCount { get; set; }

    public long StoredManifestByteCount { get; set; }

    public long ActualManifestByteCount { get; set; }

    public string RecommendedAction { get; set; } = string.Empty;

    public string ManifestGeneratedAtUtc { get; set; } = string.Empty;

    public string ManifestSha256 { get; set; } = string.Empty;

    public long CheckpointLatestSequence { get; set; }

    public string CheckpointLatestCheckpointSha256 { get; set; } = string.Empty;

    public string CheckpointLatestManifestSha256 { get; set; } = string.Empty;
}

internal sealed record GatewayActivityJournalCheckpointPayload(
    string SchemaVersion,
    long Sequence,
    string GeneratedAtUtc,
    string ManifestPath,
    string ManifestSha256,
    string PreviousCheckpointSha256);

internal sealed record GatewayActivityJournalStatus(
    string Path,
    bool Exists,
    long ByteCount,
    long LineCount,
    string StorageMode,
    string StoreDirectory,
    string SegmentDirectory,
    string ArchiveDirectory,
    string RetentionMode,
    string RetentionExecutionMode,
    int DefaultArchiveBeforeDays,
    string WriterLockMode,
    string LockPath,
    bool LockExists,
    bool LockFileExists,
    bool LockCurrentlyHeld,
    string LockStatus,
    string LockLastHolderProcessId,
    string LockLastAcquiredAtUtc,
    int LockAcquireTimeoutMs,
    string IntegrityMode,
    string DropTelemetryPath,
    bool DropTelemetryExists,
    long DroppedActivityCount,
    string LastDropAtUtc,
    string LastDropReason,
    string LastDropEvent,
    string LastDropRequestId,
    string MaintenanceSummaryPath,
    bool MaintenanceSummaryExists,
    string MaintenanceSummarySchemaVersion,
    string LastMaintenanceAtUtc,
    string LastMaintenanceOperation,
    bool LastMaintenanceDryRun,
    bool LastMaintenanceApplied,
    string LastMaintenanceReason,
    int LastMaintenanceBeforeDays,
    string LastMaintenanceArchiveBeforeUtcDate,
    int LastMaintenanceCandidateSegmentCount,
    int LastMaintenanceArchivedSegmentCount,
    long LastMaintenanceArchivedByteCount,
    long LastMaintenanceArchivedRecordCount,
    string LastMaintenanceRecommendedAction,
    string VerificationSummaryPath,
    bool VerificationSummaryExists,
    string VerificationSummarySchemaVersion,
    string LastVerificationAtUtc,
    bool LastVerificationPassed,
    string LastVerificationPosture,
    bool LastVerificationPossiblyTransient,
    string LastVerificationConsistencyMode,
    bool LastVerificationSnapshotLockAcquired,
    int LastVerificationIssueCount,
    long LastVerificationStoredManifestRecordCount,
    long LastVerificationActualManifestRecordCount,
    long LastVerificationStoredManifestByteCount,
    long LastVerificationActualManifestByteCount,
    string LastVerificationRecommendedAction,
    string LastVerificationManifestGeneratedAtUtc,
    string LastVerificationManifestSha256,
    long LastVerificationCheckpointLatestSequence,
    string LastVerificationCheckpointLatestCheckpointSha256,
    string LastVerificationCheckpointLatestManifestSha256,
    string ActiveSegmentPath,
    int SegmentCount,
    int ArchiveSegmentCount,
    long ArchiveByteCount,
    string LegacyJournalPath,
    bool LegacyJournalExists,
    bool LegacyFallbackUsed,
    string ManifestPath,
    bool ManifestExists,
    string ManifestSchemaVersion,
    string CheckpointChainPath,
    bool CheckpointChainExists,
    int CheckpointChainCount,
    long CheckpointChainLatestSequence,
    string CheckpointChainLatestCheckpointSha256,
    string CheckpointChainLatestManifestSha256,
    string ManifestGeneratedAtUtc,
    long ManifestRecordCount,
    long ManifestByteCount,
    string ManifestFirstTimestampUtc,
    string ManifestLastTimestampUtc,
    IReadOnlyList<GatewayActivityJournalSegmentManifest> Segments);

internal sealed record GatewayActivityDropTelemetryStatus(
    string Path,
    bool Exists,
    long DroppedActivityCount,
    string LastDropAtUtc,
    string LastDropReason,
    string LastDropEvent,
    string LastDropRequestId);

internal sealed record GatewayActivityMaintenanceSummaryStatus(
    string Path,
    bool Exists,
    string LastMaintenanceAtUtc,
    string LastMaintenanceOperation,
    bool LastMaintenanceDryRun,
    bool LastMaintenanceApplied,
    string LastMaintenanceReason,
    int LastMaintenanceBeforeDays,
    string LastMaintenanceArchiveBeforeUtcDate,
    int LastMaintenanceCandidateSegmentCount,
    int LastMaintenanceArchivedSegmentCount,
    long LastMaintenanceArchivedByteCount,
    long LastMaintenanceArchivedRecordCount,
    string LastMaintenanceRecommendedAction)
{
    public static GatewayActivityMaintenanceSummaryStatus Empty(string path)
    {
        return new GatewayActivityMaintenanceSummaryStatus(
            path,
            Exists: false,
            LastMaintenanceAtUtc: string.Empty,
            LastMaintenanceOperation: string.Empty,
            LastMaintenanceDryRun: false,
            LastMaintenanceApplied: false,
            LastMaintenanceReason: string.Empty,
            LastMaintenanceBeforeDays: 0,
            LastMaintenanceArchiveBeforeUtcDate: string.Empty,
            LastMaintenanceCandidateSegmentCount: 0,
            LastMaintenanceArchivedSegmentCount: 0,
            LastMaintenanceArchivedByteCount: 0,
            LastMaintenanceArchivedRecordCount: 0,
            LastMaintenanceRecommendedAction: string.Empty);
    }

    public static GatewayActivityMaintenanceSummaryStatus Unreadable(string path)
    {
        return Empty(path) with
        {
            Exists = true,
            LastMaintenanceReason = "maintenance_summary_unreadable",
            LastMaintenanceRecommendedAction = "inspect gateway activity status",
        };
    }
}

internal sealed record GatewayActivityVerificationSummaryStatus(
    string Path,
    bool Exists,
    string LastVerificationAtUtc,
    bool LastVerificationPassed,
    string LastVerificationPosture,
    bool LastVerificationPossiblyTransient,
    string LastVerificationConsistencyMode,
    bool LastVerificationSnapshotLockAcquired,
    int LastVerificationIssueCount,
    long LastVerificationStoredManifestRecordCount,
    long LastVerificationActualManifestRecordCount,
    long LastVerificationStoredManifestByteCount,
    long LastVerificationActualManifestByteCount,
    string LastVerificationRecommendedAction,
    string LastVerificationManifestGeneratedAtUtc,
    string LastVerificationManifestSha256,
    long LastVerificationCheckpointLatestSequence,
    string LastVerificationCheckpointLatestCheckpointSha256,
    string LastVerificationCheckpointLatestManifestSha256)
{
    public static GatewayActivityVerificationSummaryStatus Empty(string path)
    {
        return new GatewayActivityVerificationSummaryStatus(
            path,
            Exists: false,
            LastVerificationAtUtc: string.Empty,
            LastVerificationPassed: false,
            LastVerificationPosture: string.Empty,
            LastVerificationPossiblyTransient: false,
            LastVerificationConsistencyMode: string.Empty,
            LastVerificationSnapshotLockAcquired: false,
            LastVerificationIssueCount: 0,
            LastVerificationStoredManifestRecordCount: 0,
            LastVerificationActualManifestRecordCount: 0,
            LastVerificationStoredManifestByteCount: 0,
            LastVerificationActualManifestByteCount: 0,
            LastVerificationRecommendedAction: string.Empty,
            LastVerificationManifestGeneratedAtUtc: string.Empty,
            LastVerificationManifestSha256: string.Empty,
            LastVerificationCheckpointLatestSequence: 0,
            LastVerificationCheckpointLatestCheckpointSha256: string.Empty,
            LastVerificationCheckpointLatestManifestSha256: string.Empty);
    }

    public static GatewayActivityVerificationSummaryStatus Unreadable(string path)
    {
        return Empty(path) with
        {
            Exists = true,
            LastVerificationPosture = "verification_summary_unreadable",
            LastVerificationIssueCount = 1,
            LastVerificationRecommendedAction = "inspect gateway activity status",
        };
    }
}

internal sealed record GatewayActivityLockStatus(
    string Path,
    bool FileExists,
    bool CurrentlyHeld,
    string Status,
    string LastHolderProcessId,
    string LastAcquiredAtUtc);

internal sealed record GatewayActivityLockMetadata(
    string ProcessId,
    string AcquiredAtUtc);

internal sealed record GatewayActivityJournalCompactionResult(
    bool Requested,
    bool Applied,
    string Reason,
    int RetainLineLimit,
    long OriginalLineCount,
    long RetainedLineCount);

internal sealed record GatewayActivityJournalArchiveResult(
    bool Requested,
    bool Applied,
    string Reason,
    int BeforeDays,
    string ArchiveBeforeUtcDate,
    string ArchiveDirectory,
    int CandidateSegmentCount,
    int ArchivedSegmentCount,
    IReadOnlyList<GatewayActivityJournalArchivedSegment> ArchivedSegments);

internal sealed record GatewayActivityJournalMaintenancePlan(
    bool DryRun,
    string MaintenanceMode,
    bool WillModifyStore,
    bool MaintenanceNeeded,
    string Reason,
    string RecommendedAction,
    int BeforeDays,
    string ArchiveBeforeUtcDate,
    string ArchiveDirectory,
    int ArchiveCandidateSegmentCount,
    long ArchiveCandidateByteCount,
    long ArchiveCandidateRecordCount,
    IReadOnlyList<GatewayActivityJournalArchivedSegment> ArchiveCandidateSegments);

internal sealed record GatewayActivityJournalArchiveCandidate(
    string Path,
    DateTime SegmentDateUtc);

internal sealed record GatewayActivityJournalArchivedSegment(
    string SourcePath,
    string ArchivePath,
    string FileName,
    string SegmentDateUtc,
    long ByteCount,
    long RecordCount,
    string Sha256);

internal sealed record GatewayActivityJournalFileMetrics(
    long ByteCount,
    long LineCount);

internal sealed record GatewayActivityJournalCheckpointChainStatus(
    string Path,
    bool Exists,
    int CheckpointCount,
    long LatestSequence,
    string LatestCheckpointSha256,
    string LatestManifestSha256);

internal sealed record GatewayActivityJournalManifest(
    string SchemaVersion,
    string StoreDirectory,
    string SegmentDirectory,
    string GeneratedAtUtc,
    int SegmentCount,
    long RecordCount,
    long ByteCount,
    string FirstTimestampUtc,
    string LastTimestampUtc,
    IReadOnlyList<GatewayActivityJournalSegmentManifest> Segments);

internal sealed record GatewayActivityJournalSegmentManifest(
    string FileName,
    string Path,
    long ByteCount,
    long RecordCount,
    string FirstTimestampUtc,
    string LastTimestampUtc,
    string Sha256);

internal sealed record GatewayActivityJournalVerificationResult(
    bool Passed,
    string Posture,
    bool PossiblyTransient,
    string ConsistencyMode,
    bool SnapshotLockAcquired,
    string SnapshotLockStatus,
    string SnapshotLockPath,
    int SnapshotLockAcquireTimeoutMs,
    string JournalPath,
    string ManifestPath,
    string SegmentDirectory,
    string CheckpointChainPath,
    bool CheckpointChainExists,
    int CheckpointChainCount,
    long CheckpointChainLatestSequence,
    string CheckpointChainLatestCheckpointSha256,
    string CheckpointChainLatestManifestSha256,
    GatewayActivityJournalManifest? StoredManifest,
    GatewayActivityJournalManifest ActualManifest,
    IReadOnlyList<GatewayActivityJournalVerificationIssue> Issues);

internal sealed record GatewayActivityJournalVerificationIssue(
    string Code,
    string Path,
    string Expected,
    string Actual);

internal sealed record GatewayActivityJournalCheckpointChainVerification(
    string Path,
    bool Exists,
    int CheckpointCount,
    long LatestSequence,
    string LatestCheckpointSha256,
    string LatestManifestSha256,
    IReadOnlyList<GatewayActivityJournalVerificationIssue> Issues);
