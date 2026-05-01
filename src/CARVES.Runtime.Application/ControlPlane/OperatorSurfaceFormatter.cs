using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult Status(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        AiProviderConfig aiProviderConfig,
        CarvesCodeStandard carvesCodeStandard,
        PlannerAutonomyPolicy plannerAutonomyPolicy,
        bool aiClientConfigured,
        SafetyRules safetyRules,
        OpportunitySnapshot opportunities,
        int moduleDependencyCount,
        DomainTaskGraph graph,
        DispatchProjection dispatch,
        RuntimeSessionState? session,
        PlatformStatusSummary platformStatus,
        InteractionSnapshot interaction,
        OperationalSummary operationalSummary,
        RuntimeAgentWorkingModesSurface agentWorkingModes,
        RuntimeFormalPlanningPostureSurface formalPlanningPosture,
        RuntimeVendorNativeAccelerationSurface vendorNativeAcceleration,
        RuntimeSessionGatewayGovernanceAssistSurface sessionGatewayGovernanceAssist,
        RuntimeAcceptanceContractIngressPolicySurface acceptanceContractIngressPolicy)
    {
        return OperatorRuntimeStatusFormatter.Status(
            repoRoot,
            paths,
            systemConfig,
            aiProviderConfig,
            carvesCodeStandard,
            plannerAutonomyPolicy,
            aiClientConfigured,
            safetyRules,
            opportunities,
            moduleDependencyCount,
            graph,
            dispatch,
            session,
            platformStatus,
            interaction,
            operationalSummary,
            agentWorkingModes,
            formalPlanningPosture,
            vendorNativeAcceleration,
            sessionGatewayGovernanceAssist,
            acceptanceContractIngressPolicy);
    }

}
