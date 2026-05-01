using System.Security.Cryptography;
using System.Text;

namespace Carves.Runtime.Application.ControlPlane;

public static class ControlPlaneLockLeasePaths
{
    public static string GetLeaseRoot(string repoRoot)
    {
        return Path.Combine(ControlPlanePaths.FromRepoRoot(repoRoot).PlatformRuntimeStateRoot, "control-plane-locks");
    }

    public static string GetLeasePath(string repoRoot, string scope)
    {
        var leaseRoot = GetLeaseRoot(repoRoot);
        var sanitizedScope = string.Concat(scope.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(sanitizedScope))
        {
            sanitizedScope = "lock";
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope))).ToLowerInvariant();
        return Path.Combine(leaseRoot, $"{sanitizedScope}-{hash[..12]}.json");
    }
}
