using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static OperatorCommandResult RunQualificationCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: qualification <run|materialize-candidate|promote-candidate> [...]");
        }

        return arguments[0] switch
        {
            "run" => services.OperatorSurfaceService.RunQualification(ResolveOptionalInt(arguments, "--attempts")),
            "materialize-candidate" => services.OperatorSurfaceService.MaterializeQualificationCandidate(),
            "promote-candidate" => services.OperatorSurfaceService.PromoteQualificationCandidate(arguments.Count >= 2 ? arguments[1] : null),
            _ => OperatorCommandResult.Failure("Usage: qualification <run|materialize-candidate|promote-candidate> [...]"),
        };
    }

    private static OperatorCommandResult RunValidationCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: validation <run|run-suite|summary> [...]");
        }

        return arguments[0] switch
        {
            "run" => arguments.Count < 2
                ? OperatorCommandResult.Failure("Usage: validation run <task-id> [--mode <baseline|routing|forced-fallback>] [--repo-id <repo-id>]")
                : services.OperatorSurfaceService.RunValidationTask(arguments[1], ParseRoutingValidationMode(ResolveOption(arguments, "--mode")), ResolveOption(arguments, "--repo-id")),
            "run-suite" => services.OperatorSurfaceService.RunValidationSuite(
                ParseRoutingValidationMode(ResolveOption(arguments, "--mode")),
                ResolveOptionalInt(arguments, "--limit"),
                ResolveOption(arguments, "--repo-id")),
            "summary" => services.OperatorSurfaceService.InspectValidationSummary(ResolveOption(arguments, "--run-id")),
            _ => OperatorCommandResult.Failure("Usage: validation <run|run-suite|summary> [...]"),
        };
    }
}
