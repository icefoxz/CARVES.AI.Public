using System.Text.Json;
using Carves.Matrix.Core;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private const string TestCommandSchemaVersion = "carves-test-entry.v0";

    private static readonly JsonSerializerOptions TestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static int RunTest(string repoRoot, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || IsHelpArgument(arguments[0]))
        {
            WriteTestGuide(arguments.Any(IsJsonArgument));
            return 0;
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "demo" => RunMatrixTrial(repoRoot, ["demo", .. arguments.Skip(1)]),
            "agent" or "play" => RunMatrixTrial(repoRoot, ["play", .. arguments.Skip(1)]),
            "package" => RunMatrixTrial(repoRoot, ["package", .. arguments.Skip(1)]),
            "collect" => RunMatrixTrial(repoRoot, ["collect", .. arguments.Skip(1)]),
            "reset" => RunMatrixTrial(repoRoot, ["reset", .. arguments.Skip(1)]),
            "verify" => RunMatrixTrial(repoRoot, ["verify", .. arguments.Skip(1)]),
            "result" => RunMatrixTrial(repoRoot, ["result", .. arguments.Skip(1)]),
            "latest" or "history" => RunMatrixTrial(repoRoot, ["latest", .. arguments.Skip(1)]),
            "compare" => RunMatrixTrial(repoRoot, ["compare", .. arguments.Skip(1)]),
            _ => WriteUnknownTestCommand(arguments[0]),
        };
    }

    private static int RunMatrixTrial(string repoRoot, IReadOnlyList<string> trialArguments)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(repoRoot);
        try
        {
            return MatrixCliRunner.Run(["trial", .. trialArguments], commandName: "carves test");
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private static bool IsPortablePackageCollectInvocation(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 0
            && (string.Equals(arguments[0], "collect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "reset", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arguments[0], "result", StringComparison.OrdinalIgnoreCase))
            && !arguments.Skip(1).Any(IsAdvancedPathArgument);
    }

    private static int RunPortablePackageTestCollect(IReadOnlyList<string> arguments)
    {
        return MatrixCliRunner.Run(["trial", arguments[0].ToLowerInvariant(), .. arguments.Skip(1)], commandName: "carves test");
    }

    private static bool IsAdvancedPathArgument(string argument)
    {
        return argument is "--workspace" or "--bundle-root" or "--history-root" or "--trial-root" or "--pack-root" or "--output";
    }

    private static void WriteTestGuide(bool json)
    {
        var nonClaims = BuildTestNonClaims();
        if (json)
        {
            var result = new
            {
                schema_version = TestCommandSchemaVersion,
                command = "test",
                status = "ready",
                label = "CARVES Agent Trial local test",
                purpose = "Tests local agent execution evidence posture, not generic project unit tests.",
                default_output_root = "./carves-trials/",
                commands = new[]
                {
                    "carves test demo",
                    "carves test agent",
                    "carves test package --output <package-root>",
                    "carves test package --windows-playable --scorer-root <win-publish-root> --output <package-root> --zip-output <zip>",
                    "carves test collect",
                    "carves test reset",
                    "carves test verify",
                    "carves test result",
                    "carves test history",
                    "carves test compare --baseline <run-id> --target <run-id>",
                },
                implementation_owner = "CARVES.Matrix.Core",
                non_claims = nonClaims,
            };
            Console.WriteLine(JsonSerializer.Serialize(result, TestJsonOptions));
            return;
        }

        Console.WriteLine("CARVES Agent Trial local test");
        Console.WriteLine("Purpose: tests local agent execution evidence posture, not generic project unit tests.");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  carves test demo                         # fully automatic local smoke");
        Console.WriteLine("  carves test agent                        # prepare a real agent-assisted local run");
        Console.WriteLine("  carves test package --output <dir>       # prepare a portable package folder");
        Console.WriteLine("  carves test package --windows-playable --scorer-root <win-publish-root> --output <dir> --zip-output <zip>");
        Console.WriteLine("  carves test collect                      # from package root: score the finished agent-workspace");
        Console.WriteLine("  carves test reset                        # from package root: clear this local attempt for another agent");
        Console.WriteLine("  carves test verify                       # verify the latest local bundle");
        Console.WriteLine("  carves test result                       # show the latest local result card");
        Console.WriteLine("  carves test history                      # show latest local history pointer");
        Console.WriteLine("  carves test compare --baseline <id> --target <id>");
        Console.WriteLine();
        Console.WriteLine("Output: ./carves-trials/");
        Console.WriteLine("Implementation owner: CARVES.Matrix.Core; Runtime only routes this public alias.");
        Console.WriteLine("Non-claims: " + string.Join("; ", nonClaims) + ".");
    }

    private static int WriteUnknownTestCommand(string command)
    {
        Console.Error.WriteLine($"Unknown carves test command: {command}");
        Console.Error.WriteLine("Usage: carves test [demo|agent|package|collect|reset|verify|result|history|compare] [...]");
        Console.Error.WriteLine("This command tests local agent execution evidence posture, not generic project unit tests.");
        return 2;
    }

    private static bool IsHelpArgument(string argument)
    {
        return argument is "help" or "--help" or "-h";
    }

    private static bool IsJsonArgument(string argument)
    {
        return string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> BuildTestNonClaims()
    {
        return
        [
            "local_only",
            "not_generic_unit_test_runner",
            "not_certification",
            "not_benchmark",
            "not_hosted_verification",
            "not_server_receipt",
            "not_leaderboard_submission"
        ];
    }
}
