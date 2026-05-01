using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

internal sealed record WorkerValidationOutcome(
    bool Passed,
    IReadOnlyList<IReadOnlyList<string>> Commands,
    IReadOnlyList<CommandExecutionRecord> Results);
