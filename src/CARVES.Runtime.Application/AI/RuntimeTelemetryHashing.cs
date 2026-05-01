using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.AI;

internal static class RuntimeTelemetryHashing
{
    internal const string HashMode = "hmac_sha256_env_scoped";
    internal const string HashSaltScope = "runtime_live_state";
    internal const string HmacKeyId = "token_telemetry_hmac.key";
    internal const string HashAlgorithm = "hmac_sha256";
    internal const string NormalizationVersion = "token_text_normalization.v1";

    public static string Normalize(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd();
    }

    public static string Compute(string normalized, ControlPlanePaths paths)
    {
        var keyPath = Path.Combine(paths.RuntimeLiveStateRoot, HmacKeyId);
        Directory.CreateDirectory(paths.RuntimeLiveStateRoot);
        byte[] key;
        if (File.Exists(keyPath))
        {
            key = File.ReadAllBytes(keyPath);
        }
        else
        {
            key = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(keyPath, key);
        }

        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }
}
