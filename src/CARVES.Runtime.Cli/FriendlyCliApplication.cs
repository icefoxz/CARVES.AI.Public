using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Host;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    public static int Run(IReadOnlyList<string> args)
    {
        var parsed = ParsedArguments.Parse(args);
        if (!string.IsNullOrWhiteSpace(parsed.Error))
        {
            Console.Error.WriteLine(parsed.Error);
            return 2;
        }

        if (parsed.ShowHelp)
        {
            WriteHelp(parsed.CommandArguments);
            return 0;
        }

        if (string.Equals(parsed.Command, "doctor", StringComparison.OrdinalIgnoreCase))
        {
            return RunDoctor(parsed.RepoRootOverride, parsed.CommandArguments);
        }

        if (string.Equals(parsed.Command, "init", StringComparison.OrdinalIgnoreCase))
        {
            return RunInit(parsed.RepoRootOverride, parsed.RuntimeRootOverride, parsed.CommandArguments);
        }

        if (string.Equals(parsed.Command, "up", StringComparison.OrdinalIgnoreCase))
        {
            return RunUp(parsed.RepoRootOverride, parsed.RuntimeRootOverride, parsed.CommandArguments);
        }

        if (string.Equals(parsed.Command, "shim", StringComparison.OrdinalIgnoreCase))
        {
            WriteSubcommandHelp("shim");
            return 0;
        }

        if (string.Equals(parsed.Command, "test", StringComparison.OrdinalIgnoreCase)
            && IsPortablePackageCollectInvocation(parsed.CommandArguments))
        {
            return RunPortablePackageTestCollect(parsed.CommandArguments);
        }

        if (string.Equals(parsed.Command, "adoption", StringComparison.OrdinalIgnoreCase))
        {
            return RunAdoption(parsed.RepoRootOverride, parsed.CommandArguments);
        }

        var repoRoot = RepoLocator.Resolve(parsed.RepoRootOverride);
        if (repoRoot is null || !RepoLocator.IsRepositoryWorkspace(repoRoot))
        {
            Console.Error.WriteLine("Not a git repository.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Next:");
            Console.Error.WriteLine("  run git init or switch to a project folder");
            return 1;
        }

        return parsed.Command switch
        {
            "attach" or "start" => RunAttach(repoRoot, parsed.RuntimeRootOverride),
            "status" => RunStatus(repoRoot, parsed.CommandArguments, parsed.Transport),
            "inspect" => RunInspect(repoRoot, parsed.CommandArguments, parsed.Transport),
            "plan" => RunPlan(repoRoot, parsed.CommandArguments, parsed.Transport),
            "run" => RunRun(repoRoot, parsed.CommandArguments, parsed.Transport),
            "review" => RunReview(repoRoot, parsed.CommandArguments, parsed.Transport),
            "guard" => RunGuard(repoRoot, parsed.CommandArguments, parsed.Transport),
            "shield" => RunShield(repoRoot, parsed.CommandArguments),
            "handoff" => RunHandoff(repoRoot, parsed.CommandArguments),
            "audit" => RunAudit(repoRoot, parsed.CommandArguments, parsed.Transport),
            "matrix" => RunMatrix(parsed.CommandArguments),
            "test" => RunTest(repoRoot, parsed.CommandArguments),
            "maintain" => RunMaintain(repoRoot, parsed.CommandArguments, parsed.Transport),
            "workbench" => RunWorkbench(repoRoot, parsed.CommandArguments, parsed.Transport),
            "search" => RunSearch(repoRoot, parsed.CommandArguments),
            "host" or "gateway" => Delegate(repoRoot, parsed.Command, parsed.CommandArguments, parsed.Transport),
            "repair" => Delegate(repoRoot, "repair", transport: parsed.Transport),
            "card" => Delegate(repoRoot, "card", parsed.CommandArguments, parsed.Transport),
            "task" => Delegate(repoRoot, "task", parsed.CommandArguments, parsed.Transport),
            "runtime" => Delegate(repoRoot, "runtime", parsed.CommandArguments, parsed.Transport),
            "worker" => Delegate(repoRoot, "worker", parsed.CommandArguments, parsed.Transport),
            _ => Delegate(repoRoot, parsed.Command, parsed.CommandArguments, parsed.Transport),
        };
    }


    private enum TransportPreference
    {
        Auto,
        Cold,
        Host,
    }

    private sealed record ParsedArguments(
        string Command,
        IReadOnlyList<string> CommandArguments,
        string? RepoRootOverride,
        string? RuntimeRootOverride,
        bool ShowHelp,
        TransportPreference Transport,
        string? Error)
    {
        public static ParsedArguments Parse(IReadOnlyList<string> args)
        {
            string? repoRoot = null;
            string? runtimeRoot = null;
            var remaining = new List<string>();
            var transport = TransportPreference.Auto;

            for (var index = 0; index < args.Count; index++)
            {
                if (string.Equals(args[index], "--repo-root", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
                {
                    repoRoot = args[index + 1];
                    index++;
                    continue;
                }

                if (string.Equals(args[index], "--runtime-root", StringComparison.OrdinalIgnoreCase) && index + 1 < args.Count)
                {
                    runtimeRoot = args[index + 1];
                    index++;
                    continue;
                }

                if (string.Equals(args[index], "--cold", StringComparison.OrdinalIgnoreCase))
                {
                    transport = transport == TransportPreference.Host ? TransportPreference.Auto : TransportPreference.Cold;
                    if (transport == TransportPreference.Auto)
                    {
                        return new ParsedArguments("help", Array.Empty<string>(), repoRoot, runtimeRoot, ShowHelp: false, TransportPreference.Auto, "Choose either --cold or --host, not both.");
                    }

                    continue;
                }

                if (string.Equals(args[index], "--host", StringComparison.OrdinalIgnoreCase))
                {
                    transport = transport == TransportPreference.Cold ? TransportPreference.Auto : TransportPreference.Host;
                    if (transport == TransportPreference.Auto)
                    {
                        return new ParsedArguments("help", Array.Empty<string>(), repoRoot, runtimeRoot, ShowHelp: false, TransportPreference.Auto, "Choose either --cold or --host, not both.");
                    }

                    continue;
                }

                remaining.Add(args[index]);
            }

            if (remaining.Count == 0)
            {
                return new ParsedArguments("help", Array.Empty<string>(), repoRoot, runtimeRoot, ShowHelp: true, transport, null);
            }

            var command = remaining[0].ToLowerInvariant();
            var showHelp = command is "help" or "--help" or "-h";
            return new ParsedArguments(command, remaining.Skip(1).ToArray(), repoRoot, runtimeRoot, showHelp, transport, null);
        }
    }
}
