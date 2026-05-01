using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Planning;

public sealed partial class PlannerEmergenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService taskGraphService;
    private readonly ExecutionRunService executionRunService;
    private readonly ExecutionRunReportService executionRunReportService;
    private readonly ExecutionPatternService executionPatternService;
    private readonly RuntimeEvidenceStoreService evidenceStoreService;

    public PlannerEmergenceService(
        ControlPlanePaths paths,
        TaskGraphService taskGraphService,
        ExecutionRunService executionRunService)
    {
        this.paths = paths;
        this.taskGraphService = taskGraphService;
        this.executionRunService = executionRunService;
        executionRunReportService = new ExecutionRunReportService(paths);
        executionPatternService = new ExecutionPatternService();
        evidenceStoreService = new RuntimeEvidenceStoreService(paths);
    }
}
