using Carves.Runtime.Application.Platform;
using Carves.Runtime.Infrastructure.Persistence;

namespace Carves.Runtime.Application.Tests;

public sealed class PlatformGovernanceServiceTests
{
    [Fact]
    public void GetSnapshot_DoesNotRewriteCanonicalGovernanceWhenNoSemanticChangeExists()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new PlatformGovernanceService(new JsonPlatformGovernanceRepository(workspace.Paths));

        var first = service.GetSnapshot();
        var firstPayload = File.ReadAllText(workspace.Paths.PlatformGovernanceFile);

        var second = service.GetSnapshot();
        var secondPayload = File.ReadAllText(workspace.Paths.PlatformGovernanceFile);

        Assert.Equal(first.Version, second.Version);
        Assert.Equal(first.UpdatedAt, second.UpdatedAt);
        Assert.Equal(firstPayload, secondPayload);
    }
}
