using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class PrivilegedActorRolePolicyServiceTests
{
    [Fact]
    public void ResolveRoles_IgnoresGrantWithoutHostActorBinding()
    {
        using var workspace = new TemporaryWorkspace();
        var repoId = ResolveTestRepoId(workspace.RootPath);
        workspace.WriteFile(
            ".carves-platform/policies/privileged-actor-roles.policy.json",
            $$"""
            {
              "schema": "carves.privileged_actor_roles.policy.v1",
              "grants": [
                {
                  "repo_id": "{{repoId}}",
                  "operation_id": "release_channel",
                  "target_kind": "card",
                  "target_id": "CARD-P9",
                  "target_hash": "hash-card-p9",
                  "role": "release-manager",
                  "expires_at_utc": "{{DateTimeOffset.UtcNow.AddHours(1):O}}"
                }
              ]
            }
            """);

        var service = new PrivilegedActorRolePolicyService(ControlPlanePaths.FromRepoRoot(workspace.RootPath));
        var roles = service.ResolveRoles(
            new ActorSessionRecord
            {
                ActorSessionId = "actor-session-p9-no-binding",
                Kind = ActorSessionKind.Operator,
                ActorIdentity = "operator:p9-no-binding",
                RepoId = repoId,
                RuntimeSessionId = "runtime-p9-no-binding",
            },
            repoId,
            "release_channel",
            "card",
            "CARD-P9",
            "hash-card-p9");

        Assert.Equal(["operator"], roles);
    }

    [Fact]
    public void ResolveRoles_IgnoresGrantWithActorKindOnlyBinding()
    {
        using var workspace = new TemporaryWorkspace();
        var repoId = ResolveTestRepoId(workspace.RootPath);
        workspace.WriteFile(
            ".carves-platform/policies/privileged-actor-roles.policy.json",
            $$"""
            {
              "schema": "carves.privileged_actor_roles.policy.v1",
              "grants": [
                {
                  "actor_kind": "operator",
                  "repo_id": "{{repoId}}",
                  "operation_id": "release_channel",
                  "target_kind": "card",
                  "target_id": "CARD-P9",
                  "target_hash": "hash-card-p9",
                  "role": "release-manager",
                  "expires_at_utc": "{{DateTimeOffset.UtcNow.AddHours(1):O}}"
                }
              ]
            }
            """);

        var service = new PrivilegedActorRolePolicyService(ControlPlanePaths.FromRepoRoot(workspace.RootPath));
        var roles = service.ResolveRoles(
            new ActorSessionRecord
            {
                ActorSessionId = "actor-session-p9-kind-only",
                Kind = ActorSessionKind.Operator,
                ActorIdentity = "operator:p9-kind-only",
                RepoId = repoId,
                RuntimeSessionId = "runtime-p9-kind-only",
            },
            repoId,
            "release_channel",
            "card",
            "CARD-P9",
            "hash-card-p9");

        Assert.Equal(["operator"], roles);
    }

    [Fact]
    public void ResolveRoles_GrantsPrivilegedRoleWhenHostBindingsMatchRuntimeSessionAndOwnership()
    {
        using var workspace = new TemporaryWorkspace();
        var repoId = ResolveTestRepoId(workspace.RootPath);
        workspace.WriteFile(
            ".carves-platform/policies/privileged-actor-roles.policy.json",
            $$"""
            {
              "schema": "carves.privileged_actor_roles.policy.v1",
              "grants": [
                {
                  "actor_identity": "operator:p9-owned",
                  "actor_kind": "operator",
                  "runtime_session_id": "runtime-p9-owned",
                  "ownership_scope": "runtime_control",
                  "ownership_target_id": "CARD-P9",
                  "repo_id": "{{repoId}}",
                  "operation_id": "release_channel",
                  "target_kind": "card",
                  "target_id": "CARD-P9",
                  "target_hash": "hash-card-p9",
                  "role": "release-manager",
                  "expires_at_utc": "{{DateTimeOffset.UtcNow.AddHours(1):O}}"
                }
              ]
            }
            """);

        var service = new PrivilegedActorRolePolicyService(ControlPlanePaths.FromRepoRoot(workspace.RootPath));
        var roles = service.ResolveRoles(
            new ActorSessionRecord
            {
                ActorSessionId = "actor-session-p9-owned",
                Kind = ActorSessionKind.Operator,
                ActorIdentity = "operator:p9-owned",
                RepoId = repoId,
                RuntimeSessionId = "runtime-p9-owned",
                CurrentOwnershipScope = OwnershipScope.RuntimeControl,
                CurrentOwnershipTargetId = "CARD-P9",
            },
            repoId,
            "release_channel",
            "card",
            "CARD-P9",
            "hash-card-p9");

        Assert.Equal(["operator", "release-manager"], roles.OrderBy(static item => item, StringComparer.Ordinal).ToArray());
    }

    private static string ResolveTestRepoId(string repoRoot)
    {
        var normalizedRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(normalizedRoot);
    }
}
