namespace Carves.Runtime.Domain.Runtime;

public enum RuntimeActionability
{
    None,
    PlannerActionable,
    WorkerActionable,
    HumanActionable,
    Terminal,
}
