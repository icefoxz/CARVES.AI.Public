namespace Carves.Matrix.Tests;

public sealed class AgentTrialAdversarialFixtureHarnessTests
{
    public static IEnumerable<object[]> InitialAdversarialCases()
    {
        return AgentTrialAdversarialFixtureHarness.InitialCases.Select(testCase => new object[] { testCase });
    }

    [Theory]
    [MemberData(nameof(InitialAdversarialCases))]
    public void InitialAdversarialCases_RunCollectorVerifyAndPostureProjection(object testCaseObject)
    {
        var testCase = Assert.IsType<AgentTrialAdversarialCase>(testCaseObject);
        using var fixture = testCase.CreateFixture();

        var result = AgentTrialAdversarialFixtureHarness.Run(fixture, testCase);

        Assert.Equal(testCase.ExpectedCollectorStatus, result.CollectorStatus);
        Assert.Equal(testCase.ExpectedMatrixStatus, result.MatrixStatus);
        Assert.Equal(testCase.ExpectedTrialArtifactsMode, result.TrialArtifactsMode);
        Assert.Equal(testCase.ExpectedPostureOverall, result.SafetyPosture.Overall);
        AssertExpectedCodes(testCase.ExpectedMatrixReasonCodes, result.MatrixReasonCodes);
        AssertExpectedCodes(testCase.ExpectedPostureReasonCodes, result.SafetyPosture.ReasonCodes);
        if (testCase.ExpectedWorkspacePath is not null)
        {
            Assert.True(
                File.Exists(Path.Combine(fixture.WorkspaceRoot, testCase.ExpectedWorkspacePath.Replace('/', Path.DirectorySeparatorChar))),
                $"Expected fixture path to exist: {testCase.ExpectedWorkspacePath}");
        }

        if (testCase.Name == "self-edited-task-contract")
        {
            Assert.Contains("task_contract_pin_mismatch", result.CollectorFailureReasons);
        }
    }

    private static void AssertExpectedCodes(IReadOnlyList<string> expectedCodes, IReadOnlyList<string> actualCodes)
    {
        foreach (var expectedCode in expectedCodes)
        {
            Assert.Contains(expectedCode, actualCodes);
        }

        if (expectedCodes.Count == 0)
        {
            Assert.Empty(actualCodes);
        }
    }
}
