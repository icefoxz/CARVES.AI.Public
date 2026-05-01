using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface ICurrentModelQualificationRepository
{
    ModelQualificationMatrix? LoadMatrix();

    void SaveMatrix(ModelQualificationMatrix matrix);

    ModelQualificationRunLedger? LoadLatestRun();

    void SaveLatestRun(ModelQualificationRunLedger run);

    ModelQualificationCandidateProfile? LoadCandidate();

    void SaveCandidate(ModelQualificationCandidateProfile candidate);
}
