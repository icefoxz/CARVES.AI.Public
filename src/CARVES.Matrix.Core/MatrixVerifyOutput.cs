namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void WriteVerifyText(MatrixVerifyResult result)
    {
        Console.WriteLine("CARVES Matrix verify");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Posture: {result.VerificationPosture}");
        Console.WriteLine($"Trust-chain gates satisfied: {result.TrustChainHardening.GatesSatisfied.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Artifact root: {result.ArtifactRoot}");
        Console.WriteLine($"Manifest: {result.Manifest.Path}");
        Console.WriteLine($"Manifest SHA-256: {result.Manifest.Sha256 ?? "(missing)"}");
        Console.WriteLine($"Issue count: {result.IssueCount}");
        Console.WriteLine($"Reason codes: {(result.ReasonCodes.Count == 0 ? "(none)" : string.Join(", ", result.ReasonCodes))}");
        foreach (var issue in result.Issues)
        {
            Console.WriteLine($"  - {issue.ReasonCode}/{issue.Code}: {issue.ArtifactKind} {issue.Path}");
        }
    }
}
