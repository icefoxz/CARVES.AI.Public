using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Safety;

public sealed record SafetyContext(
    TaskNode Task,
    TaskRunReport Report,
    SafetyValidationMode ValidationMode,
    SafetyRules Rules,
    ModuleDependencyMap ModuleDependencyMap);
