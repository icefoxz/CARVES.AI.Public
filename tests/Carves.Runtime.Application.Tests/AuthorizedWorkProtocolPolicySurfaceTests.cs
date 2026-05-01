using System.Text.Json;

namespace Carves.Runtime.Application.Tests;

public sealed class AuthorizedWorkProtocolPolicySurfaceTests
{
    [Fact]
    public void ExecutionIntakePolicy_DeclaresAuthorizedWorkProtocolAsFrozenV1Truth()
    {
        var repoRoot = ResolveRepoRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "product",
            "policy",
            "CARVES_EXECUTION_INTAKE_POLICY_V0.json")));
        var awop = document.RootElement.GetProperty("authorized_work_protocol");

        Assert.Equal("frozen_v1", awop.GetProperty("status").GetString());
        Assert.True(awop.GetProperty("runtime_authority").GetBoolean());
        Assert.Equal(
            "docs/runtime/authorized-work-protocol-active-contract.md",
            awop.GetProperty("operator_companion").GetString());
        Assert.Equal(
            "docs/runtime/authorized-work-protocol-v1-freeze-validation-bundle.md",
            awop.GetProperty("freeze_validation_bundle_surface").GetString());
        Assert.Equal(
            "none",
            awop.GetProperty("acceptance_surface").GetProperty("remaining_gate").GetString());
    }

    [Fact]
    public void ActiveContract_StatesFrozenV1ForBoundedRuntimeExecution()
    {
        var repoRoot = ResolveRepoRoot();
        var text = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "authorized-work-protocol-active-contract.md"));

        Assert.Contains("Truth role: frozen protocol truth for bounded Runtime execution.", text, StringComparison.Ordinal);
        Assert.Contains("Protocol status: frozen v1.", text, StringComparison.Ordinal);
        Assert.Contains("Freeze status: validation bundle passed.", text, StringComparison.Ordinal);
        Assert.Contains("docs/product/policy/CARVES_EXECUTION_INTAKE_POLICY_V0.json:authorized_work_protocol", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FreezeValidationBundle_RecordsBoundedFrozenProofCommands()
    {
        var repoRoot = ResolveRepoRoot();
        var text = File.ReadAllText(Path.Combine(
            repoRoot,
            "docs",
            "runtime",
            "authorized-work-protocol-v1-freeze-validation-bundle.md"));

        Assert.Contains("# CARVES Authorized Work Protocol v1 Freeze Validation Bundle", text, StringComparison.Ordinal);
        Assert.Contains("dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj --filter \"FullyQualifiedName~TypedExecutionCoreServiceTests", text, StringComparison.Ordinal);
        Assert.Contains("dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj --filter \"FullyQualifiedName~SessionGatewayHostContractTests\"", text, StringComparison.Ordinal);
        Assert.Contains("AWOP is frozen v1 for bounded Runtime-governed execution;", text, StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
