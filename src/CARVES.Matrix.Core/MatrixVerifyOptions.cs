namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record MatrixVerifyOptions(
        string? ArtifactRoot,
        bool Json,
        bool RequireTrial,
        string? Error)
    {
        public static MatrixVerifyOptions Parse(IReadOnlyList<string> arguments)
        {
            string? artifactRoot = null;
            var json = false;
            var requireTrial = false;
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                {
                    json = true;
                    continue;
                }

                if (string.Equals(argument, "--trial", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(argument, "--require-trial", StringComparison.OrdinalIgnoreCase))
                {
                    requireTrial = true;
                    continue;
                }

                if (string.Equals(argument, "--artifact-root", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
                    {
                        return new MatrixVerifyOptions(null, json, requireTrial, "--artifact-root requires a value.");
                    }

                    if (!string.IsNullOrWhiteSpace(artifactRoot))
                    {
                        return new MatrixVerifyOptions(null, json, requireTrial, "Only one artifact root can be supplied.");
                    }

                    artifactRoot = arguments[++index];
                    continue;
                }

                if (argument.StartsWith("--", StringComparison.Ordinal))
                {
                    return new MatrixVerifyOptions(null, json, requireTrial, $"Unknown option: {argument}");
                }

                if (!string.IsNullOrWhiteSpace(artifactRoot))
                {
                    return new MatrixVerifyOptions(null, json, requireTrial, "Only one artifact root can be supplied.");
                }

                artifactRoot = argument;
            }

            return string.IsNullOrWhiteSpace(artifactRoot)
                ? new MatrixVerifyOptions(null, json, requireTrial, "An artifact root is required.")
                : new MatrixVerifyOptions(artifactRoot, json, requireTrial, null);
        }
    }
}
