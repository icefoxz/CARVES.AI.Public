using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal static class LocalHostPaths
{
    private const string RuntimeRootDirectoryName = "carves-runtime-host";
    private const string DeploymentsDirectoryName = "deployments";
    private const string ColdCommandsDirectoryName = "cold-commands";
    private const string GenerationDescriptorsDirectoryName = "host-generations";

    public static string ResolveMachineId()
    {
        return Environment.MachineName;
    }

    public static string GetHostId(string repoRoot, string? machineId = null)
    {
        return PlatformIdentity.CreateHostId(repoRoot, ResolveMachineIdOrDefault(machineId));
    }

    public static string GetDescriptorDirectory(string repoRoot)
    {
        return Path.Combine(ControlPlanePaths.FromRepoRoot(repoRoot).PlatformRoot, "host");
    }

    public static string GetDescriptorPath(string repoRoot)
    {
        return Path.Combine(GetDescriptorDirectory(repoRoot), "descriptor.json");
    }

    public static string GetSnapshotPath(string repoRoot)
    {
        return ControlPlanePaths.FromRepoRoot(repoRoot).PlatformHostSnapshotLiveStateFile;
    }

    public static string GetAgentGatewayReportsPath(string repoRoot)
    {
        return ControlPlanePaths.FromRepoRoot(repoRoot).PlatformAgentGatewayReportsRuntimeFile;
    }

    public static string GetRuntimeDirectory(string repoRoot)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(repoRoot))))
            .ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), RuntimeRootDirectoryName, hash[..16]);
    }

    public static string GetGatewayActivityJournalPath(string repoRoot)
    {
        return GetGatewayActivityJournalPathFromRuntimeDirectory(GetRuntimeDirectory(repoRoot));
    }

    public static string GetGatewayActivityJournalPathFromRuntimeDirectory(string runtimeDirectory)
    {
        return Path.Combine(runtimeDirectory, "gateway-activity.jsonl");
    }

    public static string GetDeploymentsDirectory(string repoRoot)
    {
        return Path.Combine(GetRuntimeDirectory(repoRoot), DeploymentsDirectoryName);
    }

    public static string GetStartupLockPath(string repoRoot)
    {
        return Path.Combine(GetRuntimeDirectory(repoRoot), "startup.lock");
    }

    public static string GetColdCommandsDirectory(string repoRoot)
    {
        return Path.Combine(GetRuntimeDirectory(repoRoot), ColdCommandsDirectoryName);
    }

    public static string GetGenerationDescriptorsDirectory(string repoRoot)
    {
        return Path.Combine(GetRuntimeDirectory(repoRoot), GenerationDescriptorsDirectoryName);
    }

    public static string GetGenerationDescriptorPath(string repoRoot, DateTimeOffset startedAt, int processId, int port)
    {
        return Path.Combine(
            GetGenerationDescriptorsDirectory(repoRoot),
            $"{startedAt.UtcDateTime:yyyyMMddHHmmssfff}-{processId}-{port}.json");
    }

    public static string GetColdCommandBuildDirectory(string repoRoot, string buildGenerationId)
    {
        return Path.Combine(GetColdCommandsDirectory(repoRoot), buildGenerationId);
    }

    public static string GetDeploymentDirectory(string repoRoot, string deploymentGenerationId)
    {
        return Path.Combine(GetDeploymentsDirectory(repoRoot), deploymentGenerationId);
    }

    public static string GetDeploymentAssemblyPath(string repoRoot, string deploymentGenerationId)
    {
        return Path.Combine(GetDeploymentDirectory(repoRoot, deploymentGenerationId), "Carves.Runtime.Host.dll");
    }

    public static string CreateDeploymentGenerationId()
    {
        return $"deployment-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
    }

    public static string CreateColdCommandBuildGenerationId()
    {
        return $"cold-build-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
    }

    public static bool IsDeploymentDirectory(string repoRoot, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var deploymentsDirectory = Path.GetFullPath(GetDeploymentsDirectory(repoRoot));
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(
            deploymentsDirectory + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    public static string GetMachineDescriptorDirectory()
    {
        return Path.Combine(Path.GetTempPath(), RuntimeRootDirectoryName);
    }

    public static string GetMachineDescriptorPath()
    {
        return Path.Combine(GetMachineDescriptorDirectory(), "active-host.json");
    }

    private static string ResolveMachineIdOrDefault(string? machineId)
    {
        return string.IsNullOrWhiteSpace(machineId) ? ResolveMachineId() : machineId;
    }
}
