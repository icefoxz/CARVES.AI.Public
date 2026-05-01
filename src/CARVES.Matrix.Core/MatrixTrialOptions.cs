namespace Carves.Matrix.Core;

internal sealed record MatrixTrialOptions
{
    public string Command { get; init; } = "plan";

    public string? WorkspaceRoot { get; init; }

    public string? PackRoot { get; init; }

    public string? BundleRoot { get; init; }

    public string? HistoryRoot { get; init; }

    public string? TrialRoot { get; init; }

    public string? OutputRoot { get; init; }

    public string? ScorerRoot { get; init; }

    public string? ZipOutput { get; init; }

    public string? RuntimeIdentifier { get; init; }

    public string? BuildLabel { get; init; }

    public string? CarvesVersion { get; init; }

    public string? RunId { get; init; }

    public string? BaselineRunId { get; init; }

    public string? TargetRunId { get; init; }

    public bool Json { get; init; }

    public bool NoWait { get; init; }

    public bool DemoAgent { get; init; }

    public bool Force { get; init; }

    public bool WindowsPlayable { get; init; }

    public string? Error { get; init; }

    public static MatrixTrialOptions Parse(IReadOnlyList<string> arguments)
    {
        var options = new MatrixTrialOptions();
        var index = 0;
        if (arguments.Count > 0 && !arguments[0].StartsWith("-", StringComparison.Ordinal))
        {
            var command = arguments[0].ToLowerInvariant();
            if (command is not ("plan" or "prepare" or "collect" or "verify" or "local" or "record" or "compare" or "demo" or "play" or "latest" or "result" or "package" or "reset"))
            {
                return options with { Error = $"Unknown trial command: {arguments[0]}" };
            }

            options = options with { Command = command };
            index = 1;
        }

        for (; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            switch (argument)
            {
                case "--json":
                    options = options with { Json = true };
                    break;
                case "--no-wait":
                    options = options with { NoWait = true };
                    break;
                case "--demo-agent":
                    options = options with { DemoAgent = true };
                    break;
                case "--force":
                    options = options with { Force = true };
                    break;
                case "--windows-playable":
                    options = options with { WindowsPlayable = true };
                    break;
                case "--workspace":
                    if (!TryReadValue(arguments, ref index, argument, out var workspace, out var workspaceError))
                    {
                        return options with { Error = workspaceError };
                    }

                    options = options with { WorkspaceRoot = workspace };
                    break;
                case "--pack-root":
                    if (!TryReadValue(arguments, ref index, argument, out var packRoot, out var packRootError))
                    {
                        return options with { Error = packRootError };
                    }

                    options = options with { PackRoot = packRoot };
                    break;
                case "--bundle-root":
                    if (!TryReadValue(arguments, ref index, argument, out var bundleRoot, out var bundleRootError))
                    {
                        return options with { Error = bundleRootError };
                    }

                    options = options with { BundleRoot = bundleRoot };
                    break;
                case "--history-root":
                    if (!TryReadValue(arguments, ref index, argument, out var historyRoot, out var historyRootError))
                    {
                        return options with { Error = historyRootError };
                    }

                    options = options with { HistoryRoot = historyRoot };
                    break;
                case "--trial-root":
                    if (!TryReadValue(arguments, ref index, argument, out var trialRoot, out var trialRootError))
                    {
                        return options with { Error = trialRootError };
                    }

                    options = options with { TrialRoot = trialRoot };
                    break;
                case "--output":
                    if (!TryReadValue(arguments, ref index, argument, out var outputRoot, out var outputRootError))
                    {
                        return options with { Error = outputRootError };
                    }

                    options = options with { OutputRoot = outputRoot };
                    break;
                case "--scorer-root":
                    if (!TryReadValue(arguments, ref index, argument, out var scorerRoot, out var scorerRootError))
                    {
                        return options with { Error = scorerRootError };
                    }

                    options = options with { ScorerRoot = scorerRoot };
                    break;
                case "--zip":
                case "--zip-output":
                    if (!TryReadValue(arguments, ref index, argument, out var zipOutput, out var zipOutputError))
                    {
                        return options with { Error = zipOutputError };
                    }

                    options = options with { ZipOutput = zipOutput };
                    break;
                case "--runtime-identifier":
                    if (!TryReadValue(arguments, ref index, argument, out var runtimeIdentifier, out var runtimeIdentifierError))
                    {
                        return options with { Error = runtimeIdentifierError };
                    }

                    options = options with { RuntimeIdentifier = runtimeIdentifier };
                    break;
                case "--build-label":
                    if (!TryReadValue(arguments, ref index, argument, out var buildLabel, out var buildLabelError))
                    {
                        return options with { Error = buildLabelError };
                    }

                    options = options with { BuildLabel = buildLabel };
                    break;
                case "--carves-version":
                    if (!TryReadValue(arguments, ref index, argument, out var carvesVersion, out var carvesVersionError))
                    {
                        return options with { Error = carvesVersionError };
                    }

                    options = options with { CarvesVersion = carvesVersion };
                    break;
                case "--run-id":
                    if (!TryReadValue(arguments, ref index, argument, out var runId, out var runIdError))
                    {
                        return options with { Error = runIdError };
                    }

                    options = options with { RunId = runId };
                    break;
                case "--baseline":
                    if (!TryReadValue(arguments, ref index, argument, out var baseline, out var baselineError))
                    {
                        return options with { Error = baselineError };
                    }

                    options = options with { BaselineRunId = baseline };
                    break;
                case "--target":
                    if (!TryReadValue(arguments, ref index, argument, out var target, out var targetError))
                    {
                        return options with { Error = targetError };
                    }

                    options = options with { TargetRunId = target };
                    break;
                default:
                    return options with { Error = $"Unknown trial option: {argument}" };
            }
        }

        return Validate(options);
    }

    private static MatrixTrialOptions Validate(MatrixTrialOptions options)
    {
        if (options.Command is "prepare" or "local" && string.IsNullOrWhiteSpace(options.WorkspaceRoot))
        {
            return options with { Error = $"trial {options.Command} requires --workspace <path>." };
        }

        if (options.Command == "package"
            && (!string.IsNullOrWhiteSpace(options.WorkspaceRoot)
                || !string.IsNullOrWhiteSpace(options.BundleRoot)
                || !string.IsNullOrWhiteSpace(options.HistoryRoot)
                || !string.IsNullOrWhiteSpace(options.TrialRoot)))
        {
            return options with { Error = "trial package uses --output <package-root>; do not pass --workspace, --bundle-root, --history-root, or --trial-root." };
        }

        if (options.Command == "package" && options.WindowsPlayable && string.IsNullOrWhiteSpace(options.ScorerRoot))
        {
            return options with { Error = "trial package --windows-playable requires --scorer-root <self-contained-windows-publish-root>." };
        }

        if (options.Command == "package"
            && !options.WindowsPlayable
            && (!string.IsNullOrWhiteSpace(options.ScorerRoot)
                || !string.IsNullOrWhiteSpace(options.ZipOutput)
                || !string.IsNullOrWhiteSpace(options.RuntimeIdentifier)
                || !string.IsNullOrWhiteSpace(options.BuildLabel)
                || !string.IsNullOrWhiteSpace(options.CarvesVersion)))
        {
            return options with { Error = "trial package scorer bundle options require --windows-playable." };
        }

        if (options.Command != "package"
            && (options.WindowsPlayable
                || !string.IsNullOrWhiteSpace(options.ScorerRoot)
                || !string.IsNullOrWhiteSpace(options.ZipOutput)
                || !string.IsNullOrWhiteSpace(options.RuntimeIdentifier)
                || !string.IsNullOrWhiteSpace(options.BuildLabel)
                || !string.IsNullOrWhiteSpace(options.CarvesVersion)))
        {
            return options with { Error = "Portable scorer bundle options are only supported by trial package." };
        }

        if (options.Command == "record")
        {
            if (string.IsNullOrWhiteSpace(options.BundleRoot))
            {
                return options with { Error = "trial record requires --bundle-root <path>." };
            }

            if (string.IsNullOrWhiteSpace(options.HistoryRoot))
            {
                return options with { Error = "trial record requires --history-root <path>." };
            }
        }

        if (options.Command == "compare")
        {
            if (string.IsNullOrWhiteSpace(options.BaselineRunId))
            {
                return options with { Error = "trial compare requires --baseline <run-id>." };
            }

            if (string.IsNullOrWhiteSpace(options.TargetRunId))
            {
                return options with { Error = "trial compare requires --target <run-id>." };
            }
        }

        return options;
    }

    private static bool TryReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string optionName,
        out string? value,
        out string? error)
    {
        value = null;
        error = null;
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            error = $"{optionName} requires a value.";
            return false;
        }

        index++;
        value = arguments[index];
        return true;
    }
}
