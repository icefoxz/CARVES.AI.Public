using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Host;

internal sealed class InteractiveOperatorConsoleService
{
    private readonly RuntimeServices? services;
    private readonly LocalHostClient? hostClient;
    private bool autoRefresh;

    public InteractiveOperatorConsoleService(RuntimeServices services)
    {
        this.services = services;
    }

    public InteractiveOperatorConsoleService(LocalHostClient hostClient)
    {
        this.hostClient = hostClient;
    }

    public Carves.Runtime.Application.ControlPlane.OperatorCommandResult Run(string? script)
    {
        var steps = string.IsNullOrWhiteSpace(script)
            ? null
            : script.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Console.WriteLine("CARVES Operator Console");
        Console.WriteLine();
        WriteMenu();

        if (steps is not null)
        {
            foreach (var step in steps)
            {
                if (HandleStep(step))
                {
                    return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Success("Operator console exited.");
                }
            }

            return SafeExit();
        }

        while (true)
        {
            if (autoRefresh)
            {
                WriteResult(ExecuteStatus());
            }

            Console.Write("> ");
            var input = Console.ReadLine();
            if (HandleStep(input))
            {
                return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Success("Operator console exited.");
            }
        }
    }

    private bool HandleStep(string? step)
    {
        var normalized = string.IsNullOrWhiteSpace(step) ? "0" : step.Trim();
        var result = normalized switch
        {
            "1" => ExecuteStatus(),
            "2" => Execute("repo", "list"),
            "3" => Execute("runtime", "list"),
            "4" => Execute("show-opportunities"),
            "5" => Execute("worker", "providers"),
            "6" => Execute("provider", "list"),
            "7" => Execute("governance", "show"),
            "8" => Execute("session", "pause", "Paused through operator console."),
            "9" => Execute("session", "stop", "Stopped through operator console."),
            "p" or "P" => Execute("planner", "status"),
            "i" or "I" => Execute("intent", "status"),
            "r" or "R" => Execute("protocol", "status"),
            "k" or "K" => Execute("prompt", "kernel"),
            "w" or "W" => Execute("planner", "wake", "explicit-human-wake", "Planner wake requested through operator console."),
            "b" or "B" => Execute("discuss", "blocked"),
            "d" or "D" => ExecuteDashboard(),
            "a" or "A" => ToggleAutoRefresh(),
            _ when normalized.StartsWith("card ", StringComparison.OrdinalIgnoreCase) => Execute("card", "inspect", normalized[5..].Trim()),
            _ when normalized.StartsWith("task ", StringComparison.OrdinalIgnoreCase) => Execute("task", "inspect", normalized[5..].Trim()),
            "0" or "exit" or "quit" => SafeExit(),
            _ => Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure($"Unknown console selection '{normalized}'."),
        };

        WriteResult(result);
        return normalized is "0" or "exit" or "quit";
    }

    private Carves.Runtime.Application.ControlPlane.OperatorCommandResult SafeExit()
    {
        if (hostClient is not null)
        {
            var sessionStatus = Execute("session", "status");
            if (sessionStatus.ExitCode != 0)
            {
                return sessionStatus;
            }

            if (sessionStatus.Lines.Any(line => line.Contains("No runtime session is attached.", StringComparison.Ordinal)))
            {
                return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Success("Console exit left runtime state unchanged.");
            }

            return Execute("session", "pause", "Paused through operator console exit.");
        }

        var session = services!.DevLoopService.GetSession();
        if (session is not null && session.Status is RuntimeSessionStatus.Idle or RuntimeSessionStatus.Scheduling or RuntimeSessionStatus.Executing or RuntimeSessionStatus.ReviewWait)
        {
            return services.OperatorSurfaceService.PauseSession("Paused through operator console exit.");
        }

        return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Success("Console exit left runtime state unchanged.");
    }

    private static void WriteMenu()
    {
        Console.WriteLine("1. Platform status");
        Console.WriteLine("2. Repositories");
        Console.WriteLine("3. Runtime sessions");
        Console.WriteLine("4. Opportunities");
        Console.WriteLine("5. Worker providers");
        Console.WriteLine("6. Providers");
        Console.WriteLine("7. Governance");
        Console.WriteLine("8. Pause runtime");
        Console.WriteLine("9. Stop runtime");
        Console.WriteLine("P. Planner status");
        Console.WriteLine("I. Intent status");
        Console.WriteLine("R. Protocol status");
        Console.WriteLine("K. Prompt kernel");
        Console.WriteLine("W. Wake planner");
        Console.WriteLine("B. Blocked summary");
        Console.WriteLine("D. Dashboard");
        Console.WriteLine("A. Toggle auto-refresh");
        Console.WriteLine("card <CARD-ID>");
        Console.WriteLine("task <TASK-ID>");
        Console.WriteLine("0. Exit safely");
    }

    private Carves.Runtime.Application.ControlPlane.OperatorCommandResult ExecuteStatus()
    {
        return Execute("status");
    }

    private Carves.Runtime.Application.ControlPlane.OperatorCommandResult ExecuteDashboard()
    {
        if (hostClient is null)
        {
            return services!.OperatorSurfaceService.Dashboard();
        }

        var discovery = hostClient.Discover("dashboard");
        return !discovery.HostRunning || discovery.Summary is null
            ? Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Failure(discovery.Message)
            : Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Success(
                $"Dashboard: {discovery.Summary.DashboardUrl}",
                "Use the localhost dashboard for the visual operator surface.");
    }

    private Carves.Runtime.Application.ControlPlane.OperatorCommandResult ToggleAutoRefresh()
    {
        autoRefresh = !autoRefresh;
        return Carves.Runtime.Application.ControlPlane.OperatorCommandResult.Success($"Auto-refresh {(autoRefresh ? "enabled" : "disabled")}.");
    }

    private Carves.Runtime.Application.ControlPlane.OperatorCommandResult Execute(string command, params string[] arguments)
    {
        if (hostClient is not null)
        {
            return hostClient.Invoke(command, arguments);
        }

        return LocalHostCommandDispatcher.Dispatch(services!, command, arguments);
    }

    private static void WriteResult(Carves.Runtime.Application.ControlPlane.OperatorCommandResult result)
    {
        foreach (var line in result.Lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
    }
}
