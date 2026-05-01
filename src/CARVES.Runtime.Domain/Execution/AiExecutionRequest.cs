namespace Carves.Runtime.Domain.Execution;

public sealed record AiExecutionRequest(
    string TaskId,
    string Title,
    string Instructions,
    string Input,
    int MaxOutputTokens,
    string? ModelOverride = null);
