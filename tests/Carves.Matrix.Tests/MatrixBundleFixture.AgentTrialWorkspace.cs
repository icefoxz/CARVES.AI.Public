namespace Carves.Matrix.Tests;

internal sealed partial class MatrixBundleFixture
{
    public void AddTrialArtifactsFromWorkspaceAndRewriteManifest(string workspaceRoot)
    {
        CopyWorkspaceTrialArtifact(workspaceRoot, ".carves/trial/task-contract.json", "trial/task-contract.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/agent-report.json", "trial/agent-report.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/diff-scope-summary.json", "trial/diff-scope-summary.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/test-evidence.json", "trial/test-evidence.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/carves-agent-trial-result.json", "trial/carves-agent-trial-result.json");

        WriteProofManifest(includeTrialArtifacts: true);
        WriteProofSummary();
    }

    public void CopyWorkspaceTrialArtifactsWithoutManifestCoverage(string workspaceRoot)
    {
        CopyWorkspaceTrialArtifact(workspaceRoot, ".carves/trial/task-contract.json", "trial/task-contract.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/agent-report.json", "trial/agent-report.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/diff-scope-summary.json", "trial/diff-scope-summary.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/test-evidence.json", "trial/test-evidence.json");
        CopyWorkspaceTrialArtifact(workspaceRoot, "artifacts/carves-agent-trial-result.json", "trial/carves-agent-trial-result.json");
        WriteProofSummary();
    }

    private void CopyWorkspaceTrialArtifact(string workspaceRoot, string sourceRelativePath, string destinationRelativePath)
    {
        var sourcePath = Path.Combine(workspaceRoot, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var destinationPath = Path.Combine(ArtifactRoot, destinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        File.Copy(sourcePath, destinationPath, overwrite: true);
    }
}
