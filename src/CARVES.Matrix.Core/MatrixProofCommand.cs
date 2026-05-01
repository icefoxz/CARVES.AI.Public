using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int RunProof(IReadOnlyList<string> arguments)
    {
        var options = MatrixOptions.Parse(arguments);
        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            Console.Error.WriteLine(options.Error);
            return 2;
        }

        var lane = ResolveProofLane(options);
        var validationError = ValidateProofOptions(options, lane);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            Console.Error.WriteLine(validationError);
            return 2;
        }

        if (lane == MatrixProofLane.NativeMinimal)
        {
            return RunNativeMinimalProof(options);
        }

        if (lane == MatrixProofLane.NativeFullRelease)
        {
            return RunNativeFullReleaseProof(options);
        }

        var runtimeRoot = ResolveRuntimeRoot(options.RuntimeRoot);
        if (runtimeRoot is null)
        {
            Console.Error.WriteLine("Unable to locate CARVES.Runtime root. Run from the source repository or pass --runtime-root <path>.");
            return 2;
        }

        var artifactRoot = ResolveArtifactRoot(runtimeRoot, options.ArtifactRoot);
        Directory.CreateDirectory(artifactRoot);

        var projectArtifactRoot = Path.Combine(artifactRoot, "project");
        var packagedArtifactRoot = Path.Combine(artifactRoot, "packaged");
        var e2eScript = ResolveMatrixScript(runtimeRoot, "matrix-e2e-smoke.ps1");
        var packagedScript = ResolveMatrixScript(runtimeRoot, "matrix-packaged-install-smoke.ps1");

        var projectResult = InvokeScript(
            e2eScript,
            [
                "-RuntimeRoot",
                runtimeRoot,
                "-ArtifactRoot",
                projectArtifactRoot,
                "-Configuration",
                options.Configuration,
            ],
            runtimeRoot,
            Path.Combine(artifactRoot, "project-matrix-output.json"));
        if (projectResult.ExitCode != 0)
        {
            WriteFailedCommand(projectResult);
            return projectResult.ExitCode;
        }

        var packagedResult = InvokeScript(
            packagedScript,
            [
                "-RuntimeRoot",
                runtimeRoot,
                "-ArtifactRoot",
                packagedArtifactRoot,
                "-Configuration",
                options.Configuration,
            ],
            runtimeRoot,
            Path.Combine(artifactRoot, "packaged-matrix-output.json"));
        if (packagedResult.ExitCode != 0)
        {
            WriteFailedCommand(packagedResult);
            return packagedResult.ExitCode;
        }

        var projectJson = ParseJson(projectResult.Stdout, "project matrix smoke");
        var packagedJson = ParseJson(packagedResult.Stdout, "packaged matrix smoke");
        if (projectJson is null || packagedJson is null)
        {
            return 1;
        }

        return CompleteFullReleaseProof(
            artifactRoot,
            projectJson.RootElement,
            packagedJson.RootElement,
            BuildFullReleaseProofCapabilities());
    }

    private static MatrixProofLane ResolveProofLane(MatrixOptions options)
    {
        return options.Lane == MatrixProofLane.Compatibility
            ? (options.Json ? MatrixProofLane.NativeMinimal : MatrixProofLane.FullRelease)
            : options.Lane;
    }
}
