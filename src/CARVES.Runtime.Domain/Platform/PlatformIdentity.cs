using System.Security.Cryptography;
using System.Text;

namespace Carves.Runtime.Domain.Platform;

public static class PlatformIdentity
{
    public static string CreateHostId(string repoRoot, string machineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineId);
        return $"host-{Hash($"{machineId}|{NormalizePath(repoRoot)}")}";
    }

    public static string CreateRepoRuntimeId(string repoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repoPath);
        return $"repo-{Hash(NormalizePath(repoPath))}";
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant()[..12];
    }

    private static string NormalizePath(string path)
    {
        var normalized = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return OperatingSystem.IsWindows()
            ? normalized.ToUpperInvariant()
            : normalized;
    }
}
