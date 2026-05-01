using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static JsonDocument? ParseJson(string stdout, string stepName)
    {
        var text = stdout.Trim();
        if (!text.StartsWith("{", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"{stepName} did not emit JSON on stdout.");
            Console.Error.WriteLine(text);
            return null;
        }

        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"{stepName} emitted invalid JSON: {ex.Message}");
            return null;
        }
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, params string[] path)
    {
        if (!TryGet(element, out var current, path))
        {
            return null;
        }

        return current.ValueKind == JsonValueKind.Number && current.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool? GetBool(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? current.GetBoolean()
            : null;
    }

    private static object BuildTrustChainHardeningEvidence(JsonElement element, params string[] matrixPath)
    {
        return new
        {
            audit_evidence_integrity = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "audit_evidence_integrity")),
            guard_deletion_replacement_honesty = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "guard_deletion_replacement_honesty")),
            shield_evidence_contract_alignment = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "shield_evidence_contract_alignment")),
            guard_audit_store_multiprocess_durability = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "guard_audit_store_multiprocess_durability")),
            handoff_completed_state_semantics = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "handoff_completed_state_semantics")),
            matrix_shield_proof_bridge_claim_boundary = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "matrix_shield_proof_bridge_claim_boundary")),
            large_log_streaming_output_boundaries = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "large_log_streaming_output_boundaries")),
            handoff_reference_freshness_portability = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "handoff_reference_freshness_portability")),
            usability_coverage_cleanup = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "usability_coverage_cleanup")),
            release_checkpoint = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "release_checkpoint")),
            public_rating_claim = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "public_rating_claim")),
            public_rating_claims_allowed = GetString(element, ConcatPath(matrixPath, "trust_chain_hardening", "public_rating_claims_allowed")),
        };
    }

    private static string[] ConcatPath(string[] prefix, params string[] suffix)
    {
        var path = new string[prefix.Length + suffix.Length];
        Array.Copy(prefix, path, prefix.Length);
        Array.Copy(suffix, 0, path, prefix.Length, suffix.Length);
        return path;
    }

    private static bool TryGet(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteFailedCommand(ScriptResult result)
    {
        Console.Error.WriteLine($"Command failed with exit code {result.ExitCode}: {result.Command}");
        if (result.TimedOut)
        {
            Console.Error.WriteLine("Command timed out and was terminated.");
        }

        if (!string.IsNullOrWhiteSpace(result.Stdout))
        {
            Console.Error.WriteLine("STDOUT:");
            Console.Error.WriteLine(result.Stdout);
            if (result.StdoutTruncated)
            {
                Console.Error.WriteLine("(stdout truncated by Matrix process capture limit)");
            }
        }

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            Console.Error.WriteLine("STDERR:");
            Console.Error.WriteLine(result.Stderr);
            if (result.StderrTruncated)
            {
                Console.Error.WriteLine("(stderr truncated by Matrix process capture limit)");
            }
        }
    }
}
