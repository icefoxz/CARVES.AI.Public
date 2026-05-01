using System.Text.Json;

namespace Carves.Matrix.Tests;

internal static class MatrixVerifyJsonAssertions
{
    public static JsonElement RunFailedVerifyJson(string artifactRoot)
    {
        return RunVerifyJson(artifactRoot, expectedExitCode: 1);
    }

    public static JsonElement RunVerifyJson(string artifactRoot, int expectedExitCode = 1, params string[] extraArguments)
    {
        var result = MatrixCliTestRunner.RunMatrixCli(["verify", artifactRoot, .. extraArguments, "--json"]);
        Assert.Equal(expectedExitCode, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        return document.RootElement.Clone();
    }

    public static void AssertContainsReasonCode(JsonElement root, string reasonCode)
    {
        Assert.Contains(
            root.GetProperty("reason_codes").EnumerateArray(),
            code => code.GetString() == reasonCode);
    }

    public static void AssertContainsIssue(JsonElement root, string artifactKind, string code, string reasonCode)
    {
        Assert.Contains(
            root.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("artifact_kind").GetString() == artifactKind
                     && issue.GetProperty("code").GetString() == code
                     && issue.GetProperty("reason_code").GetString() == reasonCode);
    }

    public static void AssertDoesNotContainIssue(JsonElement root, string artifactKind, string code)
    {
        Assert.DoesNotContain(
            root.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("artifact_kind").GetString() == artifactKind
                     && issue.GetProperty("code").GetString() == code);
    }
}
