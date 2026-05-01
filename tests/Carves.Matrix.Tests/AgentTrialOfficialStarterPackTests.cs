using Carves.Matrix.Core;
using System.Diagnostics;
using System.Text.Json;

namespace Carves.Matrix.Tests;

public sealed class AgentTrialOfficialStarterPackTests
{
    private static readonly string[] RequiredStarterPackFiles =
    [
        "AGENTS.md",
        "CLAUDE.md",
        "README.md",
        "prompts/official-v1-local-mvp/task-001-bounded-edit.prompt.md",
        "tasks/task-001-bounded-edit.md",
        "src/bounded-fixture.js",
        "package.json",
        "tests/bounded-fixture.test.js",
        ".carves/constraints/base.md",
        ".carves/trial/pack.json",
        ".carves/trial/challenge.json",
        ".carves/trial/instruction-pack.json",
        ".carves/trial/task-contract.json",
        "artifacts/.gitkeep",
        "artifacts/README.md",
        "artifacts/agent-report.template.json",
    ];

    private static readonly string[] FixturePinnedFiles =
    [
        "tasks/task-001-bounded-edit.md",
        "src/bounded-fixture.js",
        "package.json",
        "tests/bounded-fixture.test.js",
        ".carves/trial/task-contract.json",
        "artifacts/agent-report.template.json",
    ];

    [Fact]
    public void OfficialStarterPack_ContainsRequiredPublicFiles()
    {
        var packRoot = ResolvePublicPackRoot();

        foreach (var relativePath in RequiredStarterPackFiles)
        {
            Assert.True(
                File.Exists(ResolvePackPath(packRoot, relativePath)),
                $"Missing starter pack file: {relativePath}");
        }
    }

    [Fact]
    public void OfficialStarterPack_TaskContractHashMatchesPinnedChallengeAndFixture()
    {
        var packRoot = ResolvePublicPackRoot();
        var taskContractPath = ResolvePackPath(packRoot, ".carves/trial/task-contract.json");
        var challengePath = ResolvePackPath(packRoot, ".carves/trial/challenge.json");
        var fixtureTaskContractPath = ResolveFixturePath(".carves/trial/task-contract.json");

        var taskContractHash = "sha256:" + MatrixArtifactManifestWriter.ComputeFileSha256(taskContractPath);
        using var challenge = JsonDocument.Parse(File.ReadAllText(challengePath));

        Assert.Equal(
            challenge.RootElement.GetProperty("expected_task_contract_sha256").GetString(),
            taskContractHash);
        Assert.Equal(
            MatrixArtifactManifestWriter.ComputeFileSha256(fixtureTaskContractPath),
            MatrixArtifactManifestWriter.ComputeFileSha256(taskContractPath));
    }

    [Fact]
    public void OfficialStarterPack_PinsMachineFilesToLocalMvpFixture()
    {
        var packRoot = ResolvePublicPackRoot();

        foreach (var relativePath in FixturePinnedFiles)
        {
            var publicPath = ResolvePackPath(packRoot, relativePath);
            var fixturePath = ResolveFixturePath(relativePath);

            Assert.Equal(
                MatrixArtifactManifestWriter.ComputeFileSha256(fixturePath),
                MatrixArtifactManifestWriter.ComputeFileSha256(publicPath));
        }
    }

    [Fact]
    public void OfficialStarterPack_AgentReportTemplatesValidateAgainstAgentReportSchema()
    {
        var schema = MatrixStandardJsonSchemaTestSupport.LoadPublicSchema("agent-report.v0.schema.json");
        var packRoot = ResolvePublicPackRoot();
        var publicTemplatePath = ResolvePackPath(packRoot, "artifacts/agent-report.template.json");
        var fixtureTemplatePath = ResolveFixturePath("artifacts/agent-report.template.json");

        MatrixStandardJsonSchemaTestSupport.AssertValid(schema, publicTemplatePath);
        MatrixStandardJsonSchemaTestSupport.AssertValid(schema, fixtureTemplatePath);
        Assert.Equal(
            MatrixArtifactManifestWriter.ComputeFileSha256(fixtureTemplatePath),
            MatrixArtifactManifestWriter.ComputeFileSha256(publicTemplatePath));

        using var publicTemplate = JsonDocument.Parse(File.ReadAllText(publicTemplatePath));
        var profile = publicTemplate.RootElement.GetProperty("agent_profile_snapshot");
        Assert.Equal("FILL_ME_AGENT_NAME", profile.GetProperty("agent_label").GetString());
        Assert.Equal("FILL_ME_MODEL_NAME", profile.GetProperty("model_label").GetString());
    }

    [Fact]
    public void OfficialStarterPack_ArtifactReadmeTellsUsersToFillTemplateStrictly()
    {
        var artifactReadme = File.ReadAllText(ResolvePackPath(ResolvePublicPackRoot(), "artifacts/README.md"));

        Assert.Contains("Copy `agent-report.template.json` to `agent-report.json`", artifactReadme, StringComparison.Ordinal);
        Assert.Contains("Replace every `FILL_ME_*` value", artifactReadme, StringComparison.Ordinal);
        Assert.Contains("Do not add comments, extra fields", artifactReadme, StringComparison.Ordinal);
        Assert.Contains("Do not leave placeholder text", artifactReadme, StringComparison.Ordinal);
    }

    [Fact]
    public void OfficialStarterPack_InstructionPackPinsPromptAndInstructionFiles()
    {
        var packRoot = ResolvePublicPackRoot();
        var instructionPackPath = ResolvePackPath(packRoot, ".carves/trial/instruction-pack.json");
        var challengePath = ResolvePackPath(packRoot, ".carves/trial/challenge.json");
        var packMetadataPath = ResolvePackPath(packRoot, ".carves/trial/pack.json");
        var instructionPackHash = "sha256:" + MatrixArtifactManifestWriter.ComputeFileSha256(instructionPackPath);

        using var instructionPack = JsonDocument.Parse(File.ReadAllText(instructionPackPath));
        using var challenge = JsonDocument.Parse(File.ReadAllText(challengePath));
        using var packMetadata = JsonDocument.Parse(File.ReadAllText(packMetadataPath));

        Assert.Equal("official-v1-local-mvp-instructions", instructionPack.RootElement.GetProperty("instruction_pack_id").GetString());
        Assert.Equal("0.1.0-local", instructionPack.RootElement.GetProperty("instruction_pack_version").GetString());
        Assert.Equal(
            instructionPackHash,
            challenge.RootElement.GetProperty("expected_instruction_pack_sha256").GetString());
        Assert.Equal(
            instructionPackHash,
            packMetadata.RootElement.GetProperty("expected_instruction_pack_sha256").GetString());

        var prompt = FindEntry(
            instructionPack.RootElement.GetProperty("prompt_samples"),
            "prompt_id",
            "official-v1-local-mvp-bounded-edit");
        var promptPath = prompt.GetProperty("path").GetString() ?? throw new InvalidOperationException("Missing prompt path.");
        Assert.Equal("0.1.0-local", prompt.GetProperty("prompt_version").GetString());
        Assert.Equal(
            "sha256:" + MatrixArtifactManifestWriter.ComputeFileSha256(ResolvePackPath(packRoot, promptPath)),
            prompt.GetProperty("sha256").GetString());

        foreach (var instructionFile in instructionPack.RootElement.GetProperty("canonical_instruction_files").EnumerateArray())
        {
            var relativePath = instructionFile.GetProperty("path").GetString()
                ?? throw new InvalidOperationException("Missing instruction file path.");
            Assert.Equal(
                "sha256:" + MatrixArtifactManifestWriter.ComputeFileSha256(ResolvePackPath(packRoot, relativePath)),
                instructionFile.GetProperty("sha256").GetString());
        }
    }

    [Fact]
    public void OfficialStarterPack_CopiesToCleanWorkspaceAndBaselineCommandRuns()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "carves-agent-trial-starter-pack-" + Guid.NewGuid().ToString("N"));
        CopyDirectory(ResolvePublicPackRoot(), workspaceRoot);
        try
        {
            var result = RunNodeBaseline(workspaceRoot);

            Assert.True(result.ExitCode == 0, result.Stderr);
            Assert.Contains("bounded-fixture tests passed.", result.Stdout, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(workspaceRoot);
        }
    }

    [Fact]
    public void OfficialStarterPack_DoesNotContainPrivateLocalPaths()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot().Replace('\\', '/');
        var packRoot = ResolvePublicPackRoot();

        foreach (var path in Directory.EnumerateFiles(packRoot, "*", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(path).Equals(".gitkeep", StringComparison.Ordinal))
            {
                continue;
            }

            var text = File.ReadAllText(path).Replace('\\', '/');

            Assert.DoesNotContain(repoRoot, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/home/", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\\\\wsl.localhost", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ResolvePublicPackRoot()
    {
        return Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "docs",
            "matrix",
            "starter-packs",
            "official-agent-dev-safety-v1-local-mvp");
    }

    private static string ResolveFixturePath(string relativePath)
    {
        return Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "tests",
            "fixtures",
            "agent-trial-v1",
            "task-001-pack",
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ResolvePackPath(string packRoot, string relativePath)
    {
        return Path.Combine(packRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static JsonElement FindEntry(JsonElement array, string propertyName, string value)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (string.Equals(item.GetProperty(propertyName).GetString(), value, StringComparison.Ordinal))
            {
                return item;
            }
        }

        throw new InvalidOperationException($"Unable to find {propertyName}={value}.");
    }

    private static (int ExitCode, string Stdout, string Stderr) RunNodeBaseline(string workspaceRoot)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo("node")
        {
            WorkingDirectory = workspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        process.StartInfo.ArgumentList.Add("tests/bounded-fixture.test.js");

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Starter pack baseline command timed out.");
        }

        return (process.ExitCode, stdoutTask.GetAwaiter().GetResult(), stderrTask.GetAwaiter().GetResult());
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var sourcePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            var destinationPath = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
