using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IPrivilegedActorRoleResolver
{
    IReadOnlyList<string> ResolveRoles(
        ActorSessionRecord session,
        string repoId,
        string operationId,
        string targetKind,
        string targetId,
        string targetHash,
        DateTimeOffset? now = null);
}

public sealed class PrivilegedActorRolePolicyService : IPrivilegedActorRoleResolver
{
    public const string Schema = "carves.privileged_actor_roles.policy.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;

    public PrivilegedActorRolePolicyService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public string PolicyPath => Path.Combine(paths.PlatformPoliciesRoot, "privileged-actor-roles.policy.json");

    public IReadOnlyList<string> ResolveRoles(
        ActorSessionRecord session,
        string repoId,
        string operationId,
        string targetKind,
        string targetId,
        string targetHash,
        DateTimeOffset? now = null)
    {
        var roles = new List<string>();
        if (session.Kind == ActorSessionKind.Operator)
        {
            roles.Add("operator");
        }

        var policy = LoadPolicy();
        var effectiveNow = now ?? DateTimeOffset.UtcNow;
        foreach (var grant in policy.Grants)
        {
            if (!GrantMatches(grant, session, repoId, operationId, targetKind, targetId, targetHash, effectiveNow))
            {
                continue;
            }

            AddRole(roles, grant.Role);
        }

        return roles.Distinct(StringComparer.Ordinal).ToArray();
    }

    private PrivilegedActorRolePolicy LoadPolicy()
    {
        if (!File.Exists(PolicyPath))
        {
            return new PrivilegedActorRolePolicy();
        }

        try
        {
            return JsonSerializer.Deserialize<PrivilegedActorRolePolicy>(File.ReadAllText(PolicyPath), JsonOptions)
                ?? new PrivilegedActorRolePolicy();
        }
        catch (JsonException)
        {
            return new PrivilegedActorRolePolicy();
        }
    }

    private static bool GrantMatches(
        PrivilegedActorRoleGrant grant,
        ActorSessionRecord session,
        string repoId,
        string operationId,
        string targetKind,
        string targetId,
        string targetHash,
        DateTimeOffset now)
    {
        if (!HasHostActorBinding(grant))
        {
            return false;
        }

        if (grant.ExpiresAtUtc is not null && grant.ExpiresAtUtc <= now)
        {
            return false;
        }

        return Matches(grant.ActorSessionId, session.ActorSessionId)
            && Matches(grant.ActorIdentity, session.ActorIdentity)
            && Matches(grant.RuntimeSessionId, session.RuntimeSessionId ?? string.Empty)
            && MatchesActorKind(grant.ActorKind, session.Kind)
            && MatchesOwnershipScope(grant.OwnershipScope, session.CurrentOwnershipScope)
            && Matches(grant.OwnershipTargetId, session.CurrentOwnershipTargetId ?? string.Empty)
            && Matches(grant.RepoId, repoId)
            && Matches(grant.OperationId, operationId)
            && Matches(grant.TargetKind, targetKind)
            && Matches(grant.TargetId, targetId)
            && Matches(grant.TargetHash, targetHash)
            && !string.IsNullOrWhiteSpace(grant.Role);
    }

    private static bool HasHostActorBinding(PrivilegedActorRoleGrant grant)
    {
        return !string.IsNullOrWhiteSpace(grant.ActorSessionId)
            || !string.IsNullOrWhiteSpace(grant.ActorIdentity)
            || !string.IsNullOrWhiteSpace(grant.RuntimeSessionId)
            || grant.OwnershipScope is not null
            || !string.IsNullOrWhiteSpace(grant.OwnershipTargetId);
    }

    private static bool Matches(string? expected, string actual)
    {
        return string.IsNullOrWhiteSpace(expected)
            || string.Equals(expected, "*", StringComparison.Ordinal)
            || string.Equals(expected.Trim(), actual, StringComparison.Ordinal);
    }

    private static bool MatchesActorKind(ActorSessionKind? expected, ActorSessionKind actual)
    {
        return expected is null || expected.Value == actual;
    }

    private static bool MatchesOwnershipScope(OwnershipScope? expected, OwnershipScope? actual)
    {
        return expected is null || actual == expected.Value;
    }

    private static void AddRole(ICollection<string> roles, string role)
    {
        if (!string.IsNullOrWhiteSpace(role))
        {
            roles.Add(role.Trim());
        }
    }
}

public sealed record PrivilegedActorRolePolicy
{
    public string Schema { get; init; } = PrivilegedActorRolePolicyService.Schema;

    public IReadOnlyList<PrivilegedActorRoleGrant> Grants { get; init; } = [];
}

public sealed record PrivilegedActorRoleGrant
{
    public string ActorSessionId { get; init; } = string.Empty;

    public string ActorIdentity { get; init; } = string.Empty;

    public ActorSessionKind? ActorKind { get; init; }

    public string RuntimeSessionId { get; init; } = string.Empty;

    public OwnershipScope? OwnershipScope { get; init; }

    public string OwnershipTargetId { get; init; } = string.Empty;

    public string RepoId { get; init; } = string.Empty;

    public string OperationId { get; init; } = string.Empty;

    public string TargetKind { get; init; } = string.Empty;

    public string TargetId { get; init; } = string.Empty;

    public string TargetHash { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
