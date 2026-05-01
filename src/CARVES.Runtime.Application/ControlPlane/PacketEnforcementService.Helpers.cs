using System.Text;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class PacketEnforcementService
{
    private static IReadOnlyList<string> ValidatePacketContract(ExecutionPacket packet)
    {
        var issues = new List<string>();
        if (packet.PlannerIntent == PlannerIntent.Execution
            && !packet.WorkerAllowedActions.Any(action => ActionMatches(action, "carves.submit_result")))
        {
            issues.Add("packet_missing_submit_result");
        }

        foreach (var workerAction in packet.WorkerAllowedActions)
        {
            if (packet.PlannerOnlyActions.Any(plannerAction => ActionMatches(workerAction, plannerAction)))
            {
                issues.Add("packet_worker_planner_overlap");
                break;
            }
        }

        return issues;
    }

    private sealed record ChangedFileProvenance(
        string[] ResultEnvelopeFiles,
        string[] WorkerReportedFiles,
        string[] WorkerObservedFiles,
        string[] EvidenceFiles,
        string[] EffectiveFiles);

    private static ChangedFileProvenance BuildChangedFileProvenance(ResultEnvelope? envelope, WorkerExecutionArtifact? workerArtifact)
    {
        var resultEnvelopeFiles = NormalizeDistinct(
            envelope is null
                ? Array.Empty<string>()
                : envelope.Changes.FilesModified.Concat(envelope.Changes.FilesAdded));
        var workerReportedFiles = NormalizeDistinct(workerArtifact?.Result.ChangedFiles ?? Array.Empty<string>());
        var workerObservedFiles = NormalizeDistinct(workerArtifact?.Result.ObservedChangedFiles ?? Array.Empty<string>());
        var evidenceFiles = NormalizeDistinct(workerArtifact?.Evidence.FilesWritten ?? Array.Empty<string>());
        var effectiveFiles = NormalizeDistinct(
            resultEnvelopeFiles
                .Concat(workerReportedFiles)
                .Concat(workerObservedFiles)
                .Concat(evidenceFiles));

        return new ChangedFileProvenance(
            resultEnvelopeFiles,
            workerReportedFiles,
            workerObservedFiles,
            evidenceFiles,
            effectiveFiles);
    }

    private static string[] NormalizeDistinct(IEnumerable<string> paths)
    {
        return paths
            .Select(NormalizePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveRequestedAction(string? suggestedAction, ExecutionPacket packet)
    {
        if (string.IsNullOrWhiteSpace(suggestedAction))
        {
            return "none";
        }

        foreach (var action in packet.PlannerOnlyActions.Concat(packet.WorkerAllowedActions).Concat(LocalEphemeralActions))
        {
            if (ActionMatches(suggestedAction, action))
            {
                return action;
            }
        }

        return suggestedAction.Trim();
    }

    private static string ResolveRequestedActionClass(string requestedAction, ExecutionPacket packet)
    {
        if (string.Equals(requestedAction, "none", StringComparison.Ordinal))
        {
            return "none";
        }

        if (packet.PlannerOnlyActions.Any(action => ActionMatches(requestedAction, action)))
        {
            return "planner_only";
        }

        if (packet.WorkerAllowedActions.Any(action => ActionMatches(requestedAction, action)))
        {
            return "worker_allowed";
        }

        if (LocalEphemeralActions.Any(action => ActionMatches(requestedAction, action)))
        {
            return "local_ephemeral";
        }

        return "unclassified";
    }

    private static IReadOnlyList<string> BuildEvidencePaths(string packetPath, string resultPath, WorkerExecutionArtifact? workerArtifact)
    {
        var evidencePaths = new List<string>
        {
            NormalizePath(packetPath),
            NormalizePath(resultPath),
        };
        if (!string.IsNullOrWhiteSpace(workerArtifact?.Evidence.EvidencePath))
        {
            evidencePaths.Add(NormalizePath(workerArtifact.Evidence.EvidencePath!));
        }

        return evidencePaths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsUnderRoots(string path, IReadOnlyList<string> roots)
    {
        if (roots.Count == 0)
        {
            return false;
        }

        var normalizedPath = NormalizePath(path);
        foreach (var root in roots)
        {
            var normalizedRoot = NormalizePath(root);
            if (string.IsNullOrWhiteSpace(normalizedRoot))
            {
                continue;
            }

            if (string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var prefix = normalizedRoot.EndsWith("/", StringComparison.Ordinal)
                ? normalizedRoot
                : $"{normalizedRoot}/";
            if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTruthWritePath(string path, IReadOnlyList<string> repoMirrorRoots)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath.StartsWith(".ai/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.StartsWith(".carves-platform/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return repoMirrorRoots.Any(root => IsUnderRoots(normalizedPath, [root]));
    }

    private string ToRepoRelative(string path)
    {
        return NormalizePath(Path.GetRelativePath(paths.RepoRoot, path));
    }

    private static bool ActionMatches(string? value, string candidate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalizedValue = NormalizeAction(value);
        if (normalizedValue.Length == 0)
        {
            return false;
        }

        foreach (var alias in BuildActionAliases(candidate))
        {
            if (alias.Length == 0)
            {
                continue;
            }

            if (normalizedValue.Contains(alias, StringComparison.Ordinal)
                || alias.Contains(normalizedValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> BuildActionAliases(string action)
    {
        var aliases = new List<string>();
        var normalized = NormalizeAction(action);
        if (normalized.Length > 0)
        {
            aliases.Add(normalized);
        }

        if (action.IndexOf('.', StringComparison.Ordinal) >= 0)
        {
            var suffix = action[(action.LastIndexOf('.') + 1)..];
            var normalizedSuffix = NormalizeAction(suffix);
            if (normalizedSuffix.Length > 0)
            {
                aliases.Add(normalizedSuffix);
            }
        }

        return aliases.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string NormalizeAction(string value)
    {
        var builder = new StringBuilder(value.Length);
        var lastWasSeparator = false;
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append(' ');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
