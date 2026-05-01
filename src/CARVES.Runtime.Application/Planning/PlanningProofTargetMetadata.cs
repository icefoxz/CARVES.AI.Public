using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Planning;

public static class PlanningProofTargetMetadata
{
    public const string KindKey = "proof_target_kind";
    public const string DescriptionKey = "proof_target_description";

    public static IReadOnlyDictionary<string, string> Merge(IReadOnlyDictionary<string, string> metadata, RealityProofTarget? proofTarget)
    {
        var merged = new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        if (proofTarget is null)
        {
            merged.Remove(KindKey);
            merged.Remove(DescriptionKey);
            return merged;
        }

        merged[KindKey] = ToSnakeCase(proofTarget.Kind);
        merged[DescriptionKey] = proofTarget.Description;
        return merged;
    }

    public static RealityProofTarget? TryRead(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null
            || !metadata.TryGetValue(KindKey, out var kindValue)
            || string.IsNullOrWhiteSpace(kindValue)
            || !metadata.TryGetValue(DescriptionKey, out var description)
            || string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return new RealityProofTarget
        {
            Kind = ParseKind(kindValue),
            Description = description.Trim(),
        };
    }

    public static bool RequiresProofTarget(TaskType taskType, IReadOnlyList<string> scope)
    {
        return taskType == TaskType.Execution && scope.Count > 0;
    }

    private static ProofTargetKind ParseKind(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim();
        return Enum.TryParse<ProofTargetKind>(normalized, ignoreCase: true, out var parsed)
            ? parsed
            : ProofTargetKind.Boundary;
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
