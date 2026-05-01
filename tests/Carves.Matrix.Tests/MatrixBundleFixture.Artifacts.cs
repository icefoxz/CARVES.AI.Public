namespace Carves.Matrix.Tests;

internal sealed partial class MatrixBundleFixture
{
    public void DeleteArtifact(string relativePath)
    {
        var path = Path.Combine(ArtifactRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void ReplaceMatrixSummaryTextAndRefreshProofSummary(string oldValue, string newValue)
    {
        ReplaceArtifactTextAndRefreshProofSummary(
            Path.Combine(ArtifactRoot, "project", "matrix-summary.json"),
            "Matrix summary fixture text",
            oldValue,
            newValue);
    }

    public void ReplaceShieldEvaluationTextAndRefreshProofSummary(string oldValue, string newValue)
    {
        ReplaceArtifactTextAndRefreshProofSummary(
            Path.Combine(ArtifactRoot, "project", "shield-evaluate.json"),
            "Shield evaluation fixture text",
            oldValue,
            newValue);
    }

    private void ReplaceArtifactTextAndRefreshProofSummary(
        string path,
        string description,
        string oldValue,
        string newValue)
    {
        var text = File.ReadAllText(path);
        if (!text.Contains(oldValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{description} did not contain expected value: {oldValue}");
        }

        File.WriteAllText(path, text.Replace(oldValue, newValue, StringComparison.Ordinal));
        WriteProofSummary();
    }

    private void WriteArtifact(string relativePath, string contents)
    {
        var path = Path.Combine(ArtifactRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }
}
