namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record MatrixOptions(
        string? RuntimeRoot,
        string? WorkRoot,
        string? ArtifactRoot,
        string Configuration,
        string? ToolMode,
        string? GuardCommand,
        string? HandoffCommand,
        string? AuditCommand,
        string? ShieldCommand,
        string? MatrixCommand,
        string? GuardVersion,
        string? HandoffVersion,
        string? AuditVersion,
        string? ShieldVersion,
        string? MatrixVersion,
        MatrixProofLane Lane,
        bool Keep,
        bool Json,
        string? Error)
    {
        public static MatrixOptions Parse(IReadOnlyList<string> arguments)
        {
            string? runtimeRoot = null;
            string? workRoot = null;
            string? artifactRoot = null;
            var configuration = "Release";
            string? toolMode = null;
            string? guardCommand = null;
            string? handoffCommand = null;
            string? auditCommand = null;
            string? shieldCommand = null;
            string? matrixCommand = null;
            string? guardVersion = null;
            string? handoffVersion = null;
            string? auditVersion = null;
            string? shieldVersion = null;
            string? matrixVersion = null;
            var lane = MatrixProofLane.Compatibility;
            var keep = false;
            var json = false;

            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                if (string.Equals(argument, "--keep", StringComparison.OrdinalIgnoreCase))
                {
                    keep = true;
                    continue;
                }

                if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
                {
                    json = true;
                    continue;
                }

                var target = argument.ToLowerInvariant() switch
                {
                    "--runtime-root" => nameof(runtimeRoot),
                    "--work-root" => nameof(workRoot),
                    "--artifact-root" => nameof(artifactRoot),
                    "--configuration" => nameof(configuration),
                    "--tool-mode" => nameof(toolMode),
                    "--guard-command" => nameof(guardCommand),
                    "--handoff-command" => nameof(handoffCommand),
                    "--audit-command" => nameof(auditCommand),
                    "--shield-command" => nameof(shieldCommand),
                    "--matrix-command" => nameof(matrixCommand),
                    "--guard-version" => nameof(guardVersion),
                    "--handoff-version" => nameof(handoffVersion),
                    "--audit-version" => nameof(auditVersion),
                    "--shield-version" => nameof(shieldVersion),
                    "--matrix-version" => nameof(matrixVersion),
                    "--lane" => nameof(lane),
                    _ => null,
                };

                if (target is null)
                {
                    return new MatrixOptions(null, null, null, configuration, null, null, null, null, null, null, null, null, null, null, null, lane, keep, json, $"Unknown option: {argument}");
                }

                if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return new MatrixOptions(null, null, null, configuration, null, null, null, null, null, null, null, null, null, null, null, lane, keep, json, $"{argument} requires a value.");
                }

                var value = arguments[++index];
                switch (target)
                {
                    case nameof(runtimeRoot):
                        runtimeRoot = value;
                        break;
                    case nameof(workRoot):
                        workRoot = value;
                        break;
                    case nameof(artifactRoot):
                        artifactRoot = value;
                        break;
                    case nameof(configuration):
                        configuration = value;
                        break;
                    case nameof(toolMode):
                        toolMode = value;
                        break;
                    case nameof(guardCommand):
                        guardCommand = value;
                        break;
                    case nameof(handoffCommand):
                        handoffCommand = value;
                        break;
                    case nameof(auditCommand):
                        auditCommand = value;
                        break;
                    case nameof(shieldCommand):
                        shieldCommand = value;
                        break;
                    case nameof(matrixCommand):
                        matrixCommand = value;
                        break;
                    case nameof(guardVersion):
                        guardVersion = value;
                        break;
                    case nameof(handoffVersion):
                        handoffVersion = value;
                        break;
                    case nameof(auditVersion):
                        auditVersion = value;
                        break;
                    case nameof(shieldVersion):
                        shieldVersion = value;
                        break;
                    case nameof(matrixVersion):
                        matrixVersion = value;
                        break;
                    case nameof(lane):
                        if (!TryParseProofLane(value, out lane))
                        {
                            return new MatrixOptions(null, null, null, configuration, null, null, null, null, null, null, null, null, null, null, null, MatrixProofLane.Compatibility, keep, json, $"Invalid --lane value: {value}. Expected native-minimal, native-full-release, or full-release.");
                        }

                        break;
                }
            }

            return new MatrixOptions(runtimeRoot, workRoot, artifactRoot, configuration, toolMode, guardCommand, handoffCommand, auditCommand, shieldCommand, matrixCommand, guardVersion, handoffVersion, auditVersion, shieldVersion, matrixVersion, lane, keep, json, null);
        }

        private static bool TryParseProofLane(string value, out MatrixProofLane lane)
        {
            lane = value.ToLowerInvariant() switch
            {
                "native-minimal" => MatrixProofLane.NativeMinimal,
                "native-full-release" => MatrixProofLane.NativeFullRelease,
                "full-release" => MatrixProofLane.FullRelease,
                _ => MatrixProofLane.Compatibility,
            };

            return lane is not MatrixProofLane.Compatibility;
        }
    }
}
