using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public static class RuntimeDocumentRootResolver
{
    private const string GovernedAgentHandoffProofPath = "docs/runtime/runtime-governed-agent-handoff-proof.md";
    private const string FirstRunOperatorPacketPath = "docs/runtime/runtime-first-run-operator-packet.md";

    public static RuntimeDocumentRootResolution Resolve(string repoRoot, ControlPlanePaths paths)
    {
        var localRoot = Path.GetFullPath(repoRoot);
        if (HasRuntimeDocumentBundle(localRoot))
        {
            return new RuntimeDocumentRootResolution(localRoot, "repo_local");
        }

        var handshake = new AttachHandshakeService(paths).Load();
        if (TryResolveCandidate(handshake?.Request.RuntimeRoot, out var handshakeRoot))
        {
            return new RuntimeDocumentRootResolution(handshakeRoot!, "attach_handshake_runtime_root");
        }

        var manifest = new RuntimeManifestService(paths).Load();
        if (TryResolveCandidate(manifest?.RuntimeRoot, out var manifestRoot))
        {
            return new RuntimeDocumentRootResolution(manifestRoot!, "runtime_manifest_root");
        }

        return new RuntimeDocumentRootResolution(localRoot, "repo_local_missing_runtime_docs");
    }

    private static bool TryResolveCandidate(string? candidate, out string? resolvedRoot)
    {
        resolvedRoot = null;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(candidate);
        if (!HasRuntimeDocumentBundle(fullPath))
        {
            return false;
        }

        resolvedRoot = fullPath;
        return true;
    }

    private static bool HasRuntimeDocumentBundle(string root)
    {
        return File.Exists(Path.Combine(root, GovernedAgentHandoffProofPath.Replace('/', Path.DirectorySeparatorChar)))
            && File.Exists(Path.Combine(root, FirstRunOperatorPacketPath.Replace('/', Path.DirectorySeparatorChar)));
    }
}

public sealed record RuntimeDocumentRootResolution(string DocumentRoot, string Mode);
