namespace Carves.Runtime.Application.ControlPlane;

public sealed record OperatorCommandResult(int ExitCode, IReadOnlyList<string> Lines)
{
    public static OperatorCommandResult Success(params string[] lines)
    {
        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult Failure(params string[] lines)
    {
        return new OperatorCommandResult(1, lines);
    }
}
