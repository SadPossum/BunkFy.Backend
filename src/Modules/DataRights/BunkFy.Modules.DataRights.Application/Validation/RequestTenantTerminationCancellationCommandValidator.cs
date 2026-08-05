namespace BunkFy.Modules.DataRights.Application.Validation;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;

internal sealed class RequestTenantTerminationCancellationCommandValidator
    : ICommandValidator<RequestTenantTerminationCancellationCommand>
{
    public IEnumerable<string> Validate(
        RequestTenantTerminationCancellationCommand command)
    {
        if (command.ProcessId == Guid.Empty)
        {
            yield return "ProcessId is required.";
        }

        if (command.ExpectedProcessVersion <= 0)
        {
            yield return "ExpectedProcessVersion must be positive.";
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > TenantTerminationProcess.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}
