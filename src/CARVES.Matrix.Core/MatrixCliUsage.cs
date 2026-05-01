namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int WriteUsage(string commandName, int exitCode = 2)
    {
        var output = exitCode == 0 ? Console.Out : Console.Error;
        output.WriteLine($"Usage: {commandName} <proof|verify|trial|e2e|packaged> [options]");
        output.WriteLine($"       {commandName} proof --lane native-minimal [--artifact-root <path>] [--work-root <path>] [--configuration <Debug|Release>] [--keep] [--json]");
        output.WriteLine($"       {commandName} proof --lane full-release [--runtime-root <path>] [--artifact-root <path>] [--configuration <Debug|Release>] [--json]");
        output.WriteLine($"       {commandName} proof --lane native-full-release [--runtime-root <path>] [--artifact-root <path>] [--configuration <Debug|Release>] [--json]  # explicit native full-release lane");
        output.WriteLine($"       {commandName} proof [--json]  # compatibility shorthand: --json selects native-minimal; omitted --json selects full-release");
        output.WriteLine($"       {commandName} verify <artifact-root> [--trial|--require-trial] [--json]  # native .NET artifact recheck; no PowerShell");
        output.WriteLine($"       {commandName} trial demo [--trial-root <path>] [--run-id <id>] [--json]  # one-command local demo; no server");
        output.WriteLine($"       {commandName} trial play [--trial-root <path>] [--run-id <id>] [--no-wait|--demo-agent] [--json]  # prepare a local agent test run");
        output.WriteLine($"       {commandName} trial package [--output <package-root>] [--force] [--json]  # prepare a portable agent-workspace package");
        output.WriteLine($"       {commandName} trial package --windows-playable --scorer-root <win-publish-root> [--zip-output <zip>] [--runtime-identifier win-x64] [--build-label <label>] [--force] [--json]  # assemble a Windows playable zip");
        output.WriteLine($"       {commandName} trial plan [--workspace <path>] [--bundle-root <path>] [--json]  # offline Agent Trial first-run sequence");
        output.WriteLine($"       {commandName} trial prepare --workspace <path> [--pack-root <path>] [--json]  # copy the local starter pack; no server");
        output.WriteLine($"       {commandName} trial collect [--workspace <path>] [--bundle-root <path>] [--json]  # collect local evidence; no args scores a portable package root");
        output.WriteLine($"       {commandName} trial reset [--json]  # reset a portable package root for another local run");
        output.WriteLine($"       {commandName} trial verify [--bundle-root <path>|--trial-root <path>] [--json]  # strict verify of a bundle, or latest local trial");
        output.WriteLine($"       {commandName} trial latest [--trial-root <path>] [--json]  # show the UX-only latest local trial pointer");
        output.WriteLine($"       {commandName} trial local --workspace <path> [--bundle-root <path>] [--json]  # collect then verify after the agent run");
        output.WriteLine($"       {commandName} trial record --bundle-root <path> --history-root <path> [--run-id <id>] [--json]  # save a local-only summary outside the bundle");
        output.WriteLine($"       {commandName} trial compare [--history-root <path>|--trial-root <path>] --baseline <run-id> --target <run-id> [--json]  # compare local history entries");
        output.WriteLine($"       {commandName} e2e [--runtime-root <path>] [--tool-mode <Project|Installed>] [--guard-command <path>] [--handoff-command <path>] [--audit-command <path>] [--shield-command <path>]");
        output.WriteLine($"       {commandName} packaged [--runtime-root <path>] [--artifact-root <path>] [--configuration <Debug|Release>]");
        output.WriteLine("       Proof --lane native-minimal runs the native minimal Guard -> Handoff -> Audit -> Shield local consistency proof lane without invoking PowerShell.");
        output.WriteLine("       Proof --lane full-release runs the full source-repo PowerShell proof lane for release compatibility.");
        output.WriteLine("       Proof --lane native-full-release runs the opt-in native full-release lane without making it the default.");
        output.WriteLine("       Compatibility: proof --json still selects native-minimal when --lane is omitted; proof without --json still selects full-release.");
        output.WriteLine("       Verify exit codes: 0 verified, 1 verification failed, 2 usage.");
        output.WriteLine("       Verify reads an existing summary-only proof bundle and does not invoke PowerShell proof scripts.");
        output.WriteLine("       Verify --trial requires all Agent Trial artifacts to be manifest-covered and verified; ordinary verify only reports loose trial files as readback.");
        output.WriteLine("       Trial commands are offline local playtest helpers; they never submit data or create leaderboard eligibility.");
        output.WriteLine("       Trial history is local-only, remains outside the verified bundle root, and is not a server receipt.");
        output.WriteLine("       Matrix composes Guard, Handoff, Audit, and Shield outputs as a local workflow self-check; it is not a fifth safety engine.");
        output.WriteLine("       Non-claims: no producer identity, signatures, transparency log, hosted verification, certification, benchmarking, OS sandbox, or semantic correctness proof.");
        return exitCode;
    }
}
