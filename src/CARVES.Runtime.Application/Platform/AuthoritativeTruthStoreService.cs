using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class AuthoritativeTruthStoreService
{
    private static readonly JsonSerializerOptions MirrorSyncReceiptJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public const string AuthoritativeTruthWriterScope = "authoritative-truth-writer";

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public AuthoritativeTruthStoreService(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public string BaseRoot => ResolveBaseRoot();

    public string RepoKey => BuildRepoKey(paths.RepoRoot);

    public string AuthoritativeRoot => Path.Combine(BaseRoot, RepoKey);

    public string TaskGraphRoot => Path.Combine(AuthoritativeRoot, "tasks");

    public string TaskGraphFile => Path.Combine(TaskGraphRoot, "graph.json");

    public string TaskNodesRoot => Path.Combine(TaskGraphRoot, "nodes");

    public string RuntimeRoot => Path.Combine(AuthoritativeRoot, "runtime");

    public string RuntimeManifestFile => Path.Combine(RuntimeRoot, "runtime.json");

    public string ExecutionPacketsRoot => Path.Combine(RuntimeRoot, "execution-packets");

    public string MirrorSyncReceiptsRoot => Path.Combine(RuntimeRoot, "mirror-sync-receipts");

    public bool ExternalToRepo => !IsSameOrDescendant(AuthoritativeRoot, paths.RepoRoot);

    public string GetTaskNodePath(string taskId)
    {
        return Path.Combine(TaskNodesRoot, $"{taskId}.json");
    }

    public string GetExecutionPacketPath(string taskId)
    {
        return Path.Combine(ExecutionPacketsRoot, $"{taskId}.json");
    }

    public void EnsureInitialized()
    {
        Directory.CreateDirectory(TaskGraphRoot);
        Directory.CreateDirectory(TaskNodesRoot);
        Directory.CreateDirectory(RuntimeRoot);
        Directory.CreateDirectory(ExecutionPacketsRoot);
        Directory.CreateDirectory(MirrorSyncReceiptsRoot);
    }

    public string? ReadAuthoritativeFirst(string authoritativePath, string mirrorPath)
    {
        if (File.Exists(authoritativePath))
        {
            return SharedFileAccess.ReadAllText(authoritativePath);
        }

        return File.Exists(mirrorPath)
            ? SharedFileAccess.ReadAllText(mirrorPath)
            : null;
    }

    public T WithWriterLease<T>(string resource, string operation, Func<T> action, TimeSpan? timeout = null)
    {
        var options = new ControlPlaneLockOptions
        {
            Resource = NormalizePath(resource),
            Operation = operation,
            Mode = "write",
        };
        using var _ = lockService.Acquire(AuthoritativeTruthWriterScope, timeout, options);
        return action();
    }

    public void WriteAuthoritativeThenMirror(string authoritativePath, string mirrorPath, string payload, bool writerLockHeld = false)
    {
        if (!writerLockHeld)
        {
            WithWriterLease(authoritativePath, "authoritative-truth-write", () =>
            {
                WriteAuthoritativeThenMirror(authoritativePath, mirrorPath, payload, writerLockHeld: true);
                return 0;
            });
            return;
        }

        EnsureInitialized();
        Directory.CreateDirectory(Path.GetDirectoryName(authoritativePath)!);
        var familyId = ResolveFamilyId(authoritativePath, mirrorPath);
        var authoritativeWriteAt = DateTimeOffset.UtcNow;
        WriteAllTextAtomically(authoritativePath, payload);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(mirrorPath)!);
            var mirrorSyncAttemptAt = DateTimeOffset.UtcNow;
            EnsureExclusiveMirrorWriteAccess(mirrorPath);
            WriteAllTextAtomically(mirrorPath, payload);
            PersistMirrorSyncReceipt(
                familyId,
                authoritativePath,
                mirrorPath,
                authoritativeWriteAt,
                mirrorSyncAttemptAt,
                mirrorSyncAttemptAt,
                "in_sync",
                "Repo mirror synchronized from authoritative truth under the governed writer lane.");
        }
        catch (IOException exception)
        {
            if (string.IsNullOrWhiteSpace(familyId))
            {
                throw;
            }

            PersistMirrorSyncReceipt(
                familyId,
                authoritativePath,
                mirrorPath,
                authoritativeWriteAt,
                DateTimeOffset.UtcNow,
                null,
                ClassifyMirrorSyncOutcome(exception),
                BuildMirrorSyncFailureSummary(mirrorPath, exception));
        }
        catch (UnauthorizedAccessException exception)
        {
            if (string.IsNullOrWhiteSpace(familyId))
            {
                throw;
            }

            PersistMirrorSyncReceipt(
                familyId,
                authoritativePath,
                mirrorPath,
                authoritativeWriteAt,
                DateTimeOffset.UtcNow,
                null,
                "drifted",
                BuildMirrorSyncFailureSummary(mirrorPath, exception));
        }
    }

    public AuthoritativeTruthStoreSurface BuildSurface()
    {
        var writerLock = lockService.InspectLease(AuthoritativeTruthWriterScope);
        return new AuthoritativeTruthStoreSurface
        {
            RepoRoot = NormalizePath(paths.RepoRoot),
            AuthoritativeRoot = NormalizePath(AuthoritativeRoot),
            MirrorRoot = NormalizePath(paths.AiRoot),
            ExternalToRepo = ExternalToRepo,
            Summary = "Task graph and selected runtime truth are persisted to an external authoritative store first and synchronized back into repo .ai mirrors through governed writes.",
            WriterLock = writerLock is null
                ? null
                : new AuthoritativeTruthWriterLockSurface
                {
                    Scope = writerLock.Scope,
                    LeasePath = writerLock.LeasePath,
                    State = writerLock.State,
                    Resource = writerLock.Resource,
                    Operation = writerLock.Operation,
                    Mode = writerLock.Mode,
                    OwnerId = writerLock.OwnerId,
                    OwnerProcessId = writerLock.OwnerProcessId,
                    OwnerProcessName = writerLock.OwnerProcessName,
                    AcquiredAt = writerLock.AcquiredAt,
                    LastHeartbeat = writerLock.LastHeartbeat,
                    TtlSeconds = writerLock.Ttl?.TotalSeconds,
                    Summary = writerLock.Summary,
                },
            Families =
            [
                BuildFamily(
                    "task_graph",
                    "Authoritative task graph JSON with repo .ai graph mirror.",
                    TaskGraphFile,
                    paths.TaskGraphFile),
                BuildFamily(
                    "task_nodes",
                    "Authoritative task node documents with repo .ai node mirrors.",
                    TaskNodesRoot,
                    paths.TaskNodesRoot),
                BuildFamily(
                    "execution_packets",
                    "Authoritative execution packets with repo runtime packet mirrors.",
                    ExecutionPacketsRoot,
                    Path.Combine(paths.RuntimeRoot, "execution-packets")),
                BuildFamily(
                    "runtime_manifest",
                    "Authoritative runtime manifest with repo .ai/runtime.json mirror.",
                    RuntimeManifestFile,
                    Path.Combine(paths.AiRoot, "runtime.json")),
            ],
        };
    }

    private AuthoritativeTruthFamilyBinding BuildFamily(string familyId, string summary, string authoritativePath, string mirrorPath)
    {
        var authoritativeExists = File.Exists(authoritativePath) || Directory.Exists(authoritativePath);
        var mirrorExists = File.Exists(mirrorPath) || Directory.Exists(mirrorPath);
        var mirrorState = ResolveMirrorState(authoritativePath, mirrorPath, authoritativeExists, mirrorExists, out var mirrorDriftDetected, out var mirrorSummary);

        return new AuthoritativeTruthFamilyBinding
        {
            FamilyId = familyId,
            Summary = summary,
            AuthoritativePath = NormalizePath(authoritativePath),
            MirrorPath = NormalizePath(mirrorPath),
            AuthoritativeExists = authoritativeExists,
            MirrorExists = mirrorExists,
            MirrorDriftDetected = mirrorDriftDetected,
            MirrorState = mirrorState,
            MirrorSummary = mirrorSummary,
            MirrorSync = BuildMirrorSyncStatus(familyId),
        };
    }

    private AuthoritativeTruthMirrorSyncStatus BuildMirrorSyncStatus(string familyId)
    {
        var receiptPath = GetMirrorSyncReceiptPath(familyId);
        var receipt = TryReadMirrorSyncReceipt(receiptPath);
        if (receipt is null)
        {
            return new AuthoritativeTruthMirrorSyncStatus
            {
                ReceiptPath = NormalizePath(receiptPath),
            };
        }

        return new AuthoritativeTruthMirrorSyncStatus
        {
            ReceiptPath = NormalizePath(receiptPath),
            Outcome = string.IsNullOrWhiteSpace(receipt.Outcome) ? "not_recorded" : receipt.Outcome,
            Summary = string.IsNullOrWhiteSpace(receipt.Summary)
                ? "Mirror synchronization receipt did not provide a summary."
                : receipt.Summary,
            Resource = receipt.Resource,
            LastAuthoritativeWriteAt = receipt.LastAuthoritativeWriteAt,
            LastMirrorSyncAttemptAt = receipt.LastMirrorSyncAttemptAt,
            LastSuccessfulMirrorSyncAt = receipt.LastSuccessfulMirrorSyncAt,
        };
    }

    private static string ResolveMirrorState(
        string authoritativePath,
        string mirrorPath,
        bool authoritativeExists,
        bool mirrorExists,
        out bool mirrorDriftDetected,
        out string mirrorSummary)
    {
        mirrorDriftDetected = false;

        if (!authoritativeExists && !mirrorExists)
        {
            mirrorSummary = "Both authoritative and mirror paths are missing.";
            return "missing";
        }

        if (authoritativeExists && !mirrorExists)
        {
            mirrorSummary = "Authoritative truth is present but the repo mirror is missing.";
            return "authoritative_only";
        }

        if (!authoritativeExists && mirrorExists)
        {
            mirrorDriftDetected = true;
            mirrorSummary = "Repo mirror is present without authoritative truth.";
            return "mirror_only";
        }

        var authoritativeIsFile = File.Exists(authoritativePath);
        var mirrorIsFile = File.Exists(mirrorPath);
        var authoritativeIsDirectory = Directory.Exists(authoritativePath);
        var mirrorIsDirectory = Directory.Exists(mirrorPath);

        if (authoritativeIsFile != mirrorIsFile || authoritativeIsDirectory != mirrorIsDirectory)
        {
            mirrorDriftDetected = true;
            mirrorSummary = "Authoritative truth and repo mirror use different filesystem shapes.";
            return "drifted";
        }

        var authoritativeFingerprint = ComputeFingerprint(authoritativePath);
        var mirrorFingerprint = ComputeFingerprint(mirrorPath);
        if (!string.Equals(authoritativeFingerprint, mirrorFingerprint, StringComparison.Ordinal))
        {
            mirrorDriftDetected = true;
            mirrorSummary = "Repo mirror content has drifted from authoritative truth.";
            return "drifted";
        }

        mirrorSummary = "Repo mirror matches authoritative truth.";
        return "in_sync";
    }

    private static string ComputeFingerprint(string path)
    {
        if (File.Exists(path))
        {
            using var sha = SHA256.Create();
            using var stream = SharedFileAccess.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(stream));
        }

        if (!Directory.Exists(path))
        {
            return "missing";
        }

        using var aggregate = SHA256.Create();
        var builder = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(path, file).Replace('\\', '/');
            using var fileSha = SHA256.Create();
            using var stream = SharedFileAccess.OpenRead(file);
            var fileHash = Convert.ToHexString(fileSha.ComputeHash(stream));
            builder.Append(relativePath).Append(':').Append(fileHash).AppendLine();
        }

        return Convert.ToHexString(aggregate.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private string GetMirrorSyncReceiptPath(string familyId)
    {
        return Path.Combine(MirrorSyncReceiptsRoot, $"{familyId}.json");
    }

    private void PersistMirrorSyncReceipt(
        string? familyId,
        string authoritativePath,
        string mirrorPath,
        DateTimeOffset authoritativeWriteAt,
        DateTimeOffset mirrorSyncAttemptAt,
        DateTimeOffset? successfulMirrorSyncAt,
        string outcome,
        string summary)
    {
        if (string.IsNullOrWhiteSpace(familyId))
        {
            return;
        }

        var receiptPath = GetMirrorSyncReceiptPath(familyId);
        var existing = TryReadMirrorSyncReceipt(receiptPath);
        var receipt = new MirrorSyncReceiptDocument
        {
            SchemaVersion = 1,
            FamilyId = familyId,
            Resource = NormalizePath(mirrorPath),
            AuthoritativePath = NormalizePath(authoritativePath),
            MirrorPath = NormalizePath(mirrorPath),
            LastAuthoritativeWriteAt = authoritativeWriteAt,
            LastMirrorSyncAttemptAt = mirrorSyncAttemptAt,
            LastSuccessfulMirrorSyncAt = successfulMirrorSyncAt ?? existing?.LastSuccessfulMirrorSyncAt,
            Outcome = outcome,
            Summary = summary,
        };

        WriteAllTextAtomically(
            receiptPath,
            JsonSerializer.Serialize(receipt, MirrorSyncReceiptJsonOptions));
    }

    private MirrorSyncReceiptDocument? TryReadMirrorSyncReceipt(string receiptPath)
    {
        return File.Exists(receiptPath)
            ? JsonSerializer.Deserialize<MirrorSyncReceiptDocument>(
                SharedFileAccess.ReadAllText(receiptPath),
                MirrorSyncReceiptJsonOptions)
            : null;
    }

    private string? ResolveFamilyId(string authoritativePath, string mirrorPath)
    {
        if (IsSameOrDescendant(authoritativePath, TaskGraphFile) && IsSameOrDescendant(mirrorPath, paths.TaskGraphFile))
        {
            return "task_graph";
        }

        if (IsSameOrDescendant(authoritativePath, TaskNodesRoot) && IsSameOrDescendant(mirrorPath, paths.TaskNodesRoot))
        {
            return "task_nodes";
        }

        if (IsSameOrDescendant(authoritativePath, ExecutionPacketsRoot)
            && IsSameOrDescendant(mirrorPath, Path.Combine(paths.RuntimeRoot, "execution-packets")))
        {
            return "execution_packets";
        }

        if (IsSameOrDescendant(authoritativePath, RuntimeManifestFile)
            && IsSameOrDescendant(mirrorPath, Path.Combine(paths.AiRoot, "runtime.json")))
        {
            return "runtime_manifest";
        }

        return null;
    }

    private static string ClassifyMirrorSyncOutcome(IOException exception)
    {
        return IsMirrorContention(exception) ? "contention" : "drifted";
    }

    private static bool IsMirrorContention(IOException exception)
    {
        var code = exception.HResult & 0xFFFF;
        return code is 32 or 33
               || exception.Message.Contains("another process", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("being used", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("sharing violation", StringComparison.OrdinalIgnoreCase)
               || exception.Message.Contains("lock violation", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildMirrorSyncFailureSummary(string mirrorPath, Exception exception)
    {
        var failureKind = exception switch
        {
            IOException io when IsMirrorContention(io) => "contention",
            _ => "drifted",
        };

        return $"Mirror synchronization ended in {failureKind} after authoritative write succeeded for {NormalizePath(mirrorPath)}; repo mirror needs a later governed sync. {exception.GetType().Name}: {exception.Message}";
    }

    private static string ResolveBaseRoot()
    {
        var configured = Environment.GetEnvironmentVariable("CARVES_AUTHORITATIVE_TRUTH_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            localApplicationData = Path.Combine(Path.GetTempPath(), "CARVES.Runtime", "authoritative-truth");
        }

        return Path.Combine(localApplicationData, "CARVES.Runtime", "authoritative-truth");
    }

    private static string BuildRepoKey(string repoRoot)
    {
        var fullPath = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var repoName = Path.GetFileName(fullPath);
        var sanitizedRepoName = string.Concat(repoName.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(sanitizedRepoName))
        {
            sanitizedRepoName = "repo";
        }

        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(fullPath.ToLowerInvariant())));
        return $"{sanitizedRepoName}-{hash[..12].ToLowerInvariant()}";
    }

    private static bool IsSameOrDescendant(string candidatePath, string rootPath)
    {
        var candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void WriteAllTextAtomically(string path, string payload)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new InvalidOperationException($"Path '{path}' has no parent directory.");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempPath, payload);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (IOException)
        {
            TryDeleteTemp(tempPath);
            WriteAllTextShared(path, payload);
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteTemp(tempPath);
            WriteAllTextShared(path, payload);
        }
    }

    private static void EnsureExclusiveMirrorWriteAccess(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private static void WriteAllTextShared(string path, string payload)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write(payload);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static void TryDeleteTemp(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class MirrorSyncReceiptDocument
    {
        public int SchemaVersion { get; init; } = 1;

        public string FamilyId { get; init; } = string.Empty;

        public string Resource { get; init; } = string.Empty;

        public string AuthoritativePath { get; init; } = string.Empty;

        public string MirrorPath { get; init; } = string.Empty;

        public DateTimeOffset LastAuthoritativeWriteAt { get; init; }

        public DateTimeOffset LastMirrorSyncAttemptAt { get; init; }

        public DateTimeOffset? LastSuccessfulMirrorSyncAt { get; init; }

        public string Outcome { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;
    }
}
