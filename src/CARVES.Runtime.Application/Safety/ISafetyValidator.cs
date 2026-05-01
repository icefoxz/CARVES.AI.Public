using Carves.Runtime.Domain.Safety;

namespace Carves.Runtime.Application.Safety;

public interface ISafetyValidator
{
    SafetyValidatorResult Validate(SafetyContext context);
}
