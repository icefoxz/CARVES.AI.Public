namespace Carves.Matrix.Core;

internal sealed record MatrixJsonSchemaValidationIssue(string InstancePath, string Message)
{
    public override string ToString()
    {
        return $"{InstancePath}: {Message}";
    }
}
