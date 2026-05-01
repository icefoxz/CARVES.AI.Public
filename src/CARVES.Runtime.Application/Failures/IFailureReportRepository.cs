using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Application.Failures;

public interface IFailureReportRepository
{
    void Append(FailureReport report);

    IReadOnlyList<FailureReport> LoadAll();
}
