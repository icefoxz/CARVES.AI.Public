using Carves.Runtime.Application.Workers;
using Microsoft.Data.Sqlite;

namespace Carves.Runtime.Infrastructure.Audit;

public sealed class SqliteWorkerExecutionAuditReadModel : IWorkerExecutionAuditQueryReadModel
{
    public SqliteWorkerExecutionAuditReadModel(string storagePath)
    {
        StoragePath = Path.GetFullPath(storagePath);
    }

    public string StoragePath { get; }

    public bool StorageExists => File.Exists(StoragePath);

    public void AppendExecution(WorkerExecutionAuditEntry entry)
    {
        var directory = Path.GetDirectoryName(StoragePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenConnection(SqliteOpenMode.ReadWriteCreate);
        EnsureSchema(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO execution_audit
            (
                task_id,
                run_id,
                event_type,
                backend_id,
                provider_id,
                adapter_id,
                protocol_family,
                status,
                failure_kind,
                failure_layer,
                changed_files_count,
                observed_changed_files_count,
                permission_request_count,
                input_tokens,
                output_tokens,
                provider_latency_ms,
                safety_outcome,
                safety_allowed,
                summary,
                occurred_at_utc
            )
            VALUES
            (
                $task_id,
                $run_id,
                $event_type,
                $backend_id,
                $provider_id,
                $adapter_id,
                $protocol_family,
                $status,
                $failure_kind,
                $failure_layer,
                $changed_files_count,
                $observed_changed_files_count,
                $permission_request_count,
                $input_tokens,
                $output_tokens,
                $provider_latency_ms,
                $safety_outcome,
                $safety_allowed,
                $summary,
                $occurred_at_utc
            );
            """;
        AddParameters(command, entry);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<WorkerExecutionAuditEntry> QueryRecent(int limit)
    {
        if (!StorageExists)
        {
            return [];
        }

        using var connection = OpenConnection(SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                id,
                task_id,
                run_id,
                event_type,
                backend_id,
                provider_id,
                adapter_id,
                protocol_family,
                status,
                failure_kind,
                failure_layer,
                changed_files_count,
                observed_changed_files_count,
                permission_request_count,
                input_tokens,
                output_tokens,
                provider_latency_ms,
                safety_outcome,
                safety_allowed,
                summary,
                occurred_at_utc
            FROM execution_audit
            ORDER BY id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Max(1, limit));

        var entries = new List<WorkerExecutionAuditEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return entries;
    }

    public WorkerExecutionAuditQueryResult Query(WorkerExecutionAuditQuery query)
    {
        if (!StorageExists)
        {
            return new WorkerExecutionAuditQueryResult
            {
                Query = query,
                Summary = new WorkerExecutionAuditSummary(),
                Entries = [],
                QueryMode = "indexed_sqlite",
            };
        }

        using var connection = OpenConnection(SqliteOpenMode.ReadOnly);
        var filter = BuildFilter(query);
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                id,
                task_id,
                run_id,
                event_type,
                backend_id,
                provider_id,
                adapter_id,
                protocol_family,
                status,
                failure_kind,
                failure_layer,
                changed_files_count,
                observed_changed_files_count,
                permission_request_count,
                input_tokens,
                output_tokens,
                provider_latency_ms,
                safety_outcome,
                safety_allowed,
                summary,
                occurred_at_utc
            FROM execution_audit
            {filter.WhereClause}
            ORDER BY id DESC
            LIMIT $limit;
            """;
        AddFilterParameters(command, filter.Parameters);
        command.Parameters.AddWithValue("$limit", Math.Max(1, query.Limit));

        var entries = new List<WorkerExecutionAuditEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(ReadEntry(reader));
        }

        return new WorkerExecutionAuditQueryResult
        {
            Query = query,
            Summary = GetSummary(connection, filter),
            Entries = entries,
            QueryMode = "indexed_sqlite",
        };
    }

    public WorkerExecutionAuditSummary GetSummary()
    {
        if (!StorageExists)
        {
            return new WorkerExecutionAuditSummary();
        }

        using var connection = OpenConnection(SqliteOpenMode.ReadOnly);
        return GetSummary(connection, AuditFilter.Empty);
    }

    private static WorkerExecutionAuditSummary GetSummary(SqliteConnection connection, AuditFilter filter)
    {
        using var summaryCommand = connection.CreateCommand();
        summaryCommand.CommandText = $"""
            SELECT
                COUNT(*) AS total_executions,
                SUM(CASE WHEN status = 'Succeeded' THEN 1 ELSE 0 END) AS succeeded_executions,
                SUM(CASE WHEN status = 'Failed' THEN 1 ELSE 0 END) AS failed_executions,
                SUM(CASE WHEN status = 'Blocked' THEN 1 ELSE 0 END) AS blocked_executions,
                SUM(CASE WHEN status = 'Skipped' THEN 1 ELSE 0 END) AS skipped_executions,
                SUM(CASE WHEN status = 'ApprovalWait' THEN 1 ELSE 0 END) AS approval_wait_executions,
                SUM(CASE WHEN safety_allowed = 0 THEN 1 ELSE 0 END) AS safety_blocked_executions,
                SUM(permission_request_count) AS permission_request_count,
                SUM(changed_files_count) AS changed_files_count
            FROM execution_audit
            {filter.WhereClause};
            """;
        AddFilterParameters(summaryCommand, filter.Parameters);

        using var reader = summaryCommand.ExecuteReader();
        var summary = reader.Read()
            ? new WorkerExecutionAuditSummary
            {
                TotalExecutions = ReadInt32(reader, 0),
                SucceededExecutions = ReadInt32(reader, 1),
                FailedExecutions = ReadInt32(reader, 2),
                BlockedExecutions = ReadInt32(reader, 3),
                SkippedExecutions = ReadInt32(reader, 4),
                ApprovalWaitExecutions = ReadInt32(reader, 5),
                SafetyBlockedExecutions = ReadInt32(reader, 6),
                PermissionRequestCount = ReadInt32(reader, 7),
                ChangedFilesCount = ReadInt32(reader, 8),
            }
            : new WorkerExecutionAuditSummary();

        using var latestCommand = connection.CreateCommand();
        latestCommand.CommandText = $"""
            SELECT task_id, occurred_at_utc
            FROM execution_audit
            {filter.WhereClause}
            ORDER BY id DESC
            LIMIT 1;
            """;
        AddFilterParameters(latestCommand, filter.Parameters);
        using var latestReader = latestCommand.ExecuteReader();
        if (!latestReader.Read())
        {
            return summary;
        }

        return summary with
        {
            LatestTaskId = latestReader.GetString(0),
            LatestOccurrenceUtc = DateTimeOffset.Parse(latestReader.GetString(1)),
        };
    }

    private static AuditFilter BuildFilter(WorkerExecutionAuditQuery query)
    {
        var clauses = new List<string>();
        var parameters = new List<AuditFilterParameter>();

        AddStringFilter(clauses, parameters, "task_id", "$task_id", query.TaskId);
        AddStringFilter(clauses, parameters, "run_id", "$run_id", query.RunId);
        AddStringFilter(clauses, parameters, "status", "$status", query.Status);
        AddStringFilter(clauses, parameters, "event_type", "$event_type", query.EventType);
        AddStringFilter(clauses, parameters, "backend_id", "$backend_id", query.BackendId);
        AddStringFilter(clauses, parameters, "provider_id", "$provider_id", query.ProviderId);

        if (query.SafetyAllowed is not null)
        {
            clauses.Add("safety_allowed = $safety_allowed");
            parameters.Add(new AuditFilterParameter("$safety_allowed", query.SafetyAllowed.Value ? 1 : 0));
        }

        return clauses.Count == 0
            ? AuditFilter.Empty
            : new AuditFilter($"WHERE {string.Join(" AND ", clauses)}", parameters);
    }

    private static void AddStringFilter(
        List<string> clauses,
        List<AuditFilterParameter> parameters,
        string column,
        string parameterName,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        clauses.Add($"{column} = {parameterName} COLLATE NOCASE");
        parameters.Add(new AuditFilterParameter(parameterName, value));
    }

    private static void AddFilterParameters(SqliteCommand command, IReadOnlyList<AuditFilterParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }
    }

    private SqliteConnection OpenConnection(SqliteOpenMode mode)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = StoragePath,
            Mode = mode,
        };
        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        pragma.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS execution_audit (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_id TEXT NOT NULL,
                run_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                backend_id TEXT,
                provider_id TEXT,
                adapter_id TEXT,
                protocol_family TEXT,
                status TEXT NOT NULL,
                failure_kind TEXT,
                failure_layer TEXT,
                changed_files_count INTEGER NOT NULL DEFAULT 0,
                observed_changed_files_count INTEGER NOT NULL DEFAULT 0,
                permission_request_count INTEGER NOT NULL DEFAULT 0,
                input_tokens INTEGER,
                output_tokens INTEGER,
                provider_latency_ms INTEGER,
                safety_outcome TEXT,
                safety_allowed INTEGER NOT NULL DEFAULT 1,
                summary TEXT,
                occurred_at_utc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_worker_execution_audit_task
                ON execution_audit(task_id);

            CREATE INDEX IF NOT EXISTS idx_worker_execution_audit_occurred
                ON execution_audit(occurred_at_utc);

            CREATE INDEX IF NOT EXISTS idx_worker_execution_audit_status
                ON execution_audit(status);
            """;
        command.ExecuteNonQuery();
    }

    private static void AddParameters(SqliteCommand command, WorkerExecutionAuditEntry entry)
    {
        command.Parameters.AddWithValue("$task_id", entry.TaskId);
        command.Parameters.AddWithValue("$run_id", entry.RunId);
        command.Parameters.AddWithValue("$event_type", entry.EventType);
        command.Parameters.AddWithValue("$backend_id", entry.BackendId);
        command.Parameters.AddWithValue("$provider_id", entry.ProviderId);
        command.Parameters.AddWithValue("$adapter_id", entry.AdapterId);
        command.Parameters.AddWithValue("$protocol_family", ToDbValue(entry.ProtocolFamily));
        command.Parameters.AddWithValue("$status", entry.Status);
        command.Parameters.AddWithValue("$failure_kind", entry.FailureKind);
        command.Parameters.AddWithValue("$failure_layer", entry.FailureLayer);
        command.Parameters.AddWithValue("$changed_files_count", entry.ChangedFilesCount);
        command.Parameters.AddWithValue("$observed_changed_files_count", entry.ObservedChangedFilesCount);
        command.Parameters.AddWithValue("$permission_request_count", entry.PermissionRequestCount);
        command.Parameters.AddWithValue("$input_tokens", ToDbValue(entry.InputTokens));
        command.Parameters.AddWithValue("$output_tokens", ToDbValue(entry.OutputTokens));
        command.Parameters.AddWithValue("$provider_latency_ms", ToDbValue(entry.ProviderLatencyMs));
        command.Parameters.AddWithValue("$safety_outcome", entry.SafetyOutcome);
        command.Parameters.AddWithValue("$safety_allowed", entry.SafetyAllowed ? 1 : 0);
        command.Parameters.AddWithValue("$summary", entry.Summary);
        command.Parameters.AddWithValue("$occurred_at_utc", entry.OccurredAtUtc.ToUniversalTime().ToString("O"));
    }

    private static WorkerExecutionAuditEntry ReadEntry(SqliteDataReader reader)
    {
        return new WorkerExecutionAuditEntry
        {
            SequenceId = reader.GetInt64(0),
            TaskId = reader.GetString(1),
            RunId = reader.GetString(2),
            EventType = reader.GetString(3),
            BackendId = reader.GetString(4),
            ProviderId = reader.GetString(5),
            AdapterId = reader.GetString(6),
            ProtocolFamily = ReadNullableString(reader, 7),
            Status = reader.GetString(8),
            FailureKind = reader.GetString(9),
            FailureLayer = reader.GetString(10),
            ChangedFilesCount = reader.GetInt32(11),
            ObservedChangedFilesCount = reader.GetInt32(12),
            PermissionRequestCount = reader.GetInt32(13),
            InputTokens = ReadNullableInt32(reader, 14),
            OutputTokens = ReadNullableInt32(reader, 15),
            ProviderLatencyMs = ReadNullableInt64(reader, 16),
            SafetyOutcome = reader.GetString(17),
            SafetyAllowed = reader.GetInt32(18) != 0,
            Summary = reader.GetString(19),
            OccurredAtUtc = DateTimeOffset.Parse(reader.GetString(20)),
        };
    }

    private static int ReadInt32(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadNullableInt32(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static long? ReadNullableInt64(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);
    }

    private static string? ReadNullableString(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static object ToDbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }

    private sealed record AuditFilter(string WhereClause, IReadOnlyList<AuditFilterParameter> Parameters)
    {
        public static AuditFilter Empty { get; } = new(string.Empty, []);
    }

    private sealed record AuditFilterParameter(string Name, object Value);
}
