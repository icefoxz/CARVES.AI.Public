namespace Carves.Runtime.Domain.Platform;

public sealed class ProviderProfileBinding
{
    public string ProfileId { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
