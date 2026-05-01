using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

public static partial class Program
{
    private static OperatorCommandResult RunApiCommandCore(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return LocalHostCommandDispatcher.Dispatch(services, "api", arguments);
    }
}
