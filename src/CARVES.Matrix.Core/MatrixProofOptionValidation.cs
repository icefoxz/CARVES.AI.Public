namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string? ValidateProofOptions(MatrixOptions options, MatrixProofLane lane)
    {
        if (!string.Equals(options.Configuration, "Debug", StringComparison.Ordinal)
            && !string.Equals(options.Configuration, "Release", StringComparison.Ordinal))
        {
            return $"Invalid --configuration value: {options.Configuration}. Expected Debug or Release.";
        }

        var unsupportedProofOption = FirstUnsupportedProofOption(options);
        if (!string.IsNullOrWhiteSpace(unsupportedProofOption))
        {
            return $"{unsupportedProofOption} is not supported by proof. Use e2e or packaged for script passthrough options.";
        }

        if (lane == MatrixProofLane.NativeMinimal && !string.IsNullOrWhiteSpace(options.RuntimeRoot))
        {
            return "--runtime-root is not supported by proof --lane native-minimal. The native lane runs a bounded in-process proof repository.";
        }

        if (lane is MatrixProofLane.FullRelease or MatrixProofLane.NativeFullRelease)
        {
            if (!string.IsNullOrWhiteSpace(options.WorkRoot))
            {
                return "--work-root is only supported by proof --lane native-minimal.";
            }

            if (options.Keep)
            {
                return "--keep is only supported by proof --lane native-minimal.";
            }
        }

        return null;
    }

    private static string? FirstUnsupportedProofOption(MatrixOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ToolMode))
        {
            return "--tool-mode";
        }

        if (!string.IsNullOrWhiteSpace(options.GuardCommand))
        {
            return "--guard-command";
        }

        if (!string.IsNullOrWhiteSpace(options.HandoffCommand))
        {
            return "--handoff-command";
        }

        if (!string.IsNullOrWhiteSpace(options.AuditCommand))
        {
            return "--audit-command";
        }

        if (!string.IsNullOrWhiteSpace(options.ShieldCommand))
        {
            return "--shield-command";
        }

        if (!string.IsNullOrWhiteSpace(options.MatrixCommand))
        {
            return "--matrix-command";
        }

        if (!string.IsNullOrWhiteSpace(options.GuardVersion))
        {
            return "--guard-version";
        }

        if (!string.IsNullOrWhiteSpace(options.HandoffVersion))
        {
            return "--handoff-version";
        }

        if (!string.IsNullOrWhiteSpace(options.AuditVersion))
        {
            return "--audit-version";
        }

        if (!string.IsNullOrWhiteSpace(options.ShieldVersion))
        {
            return "--shield-version";
        }

        return !string.IsNullOrWhiteSpace(options.MatrixVersion)
            ? "--matrix-version"
            : null;
    }
}
