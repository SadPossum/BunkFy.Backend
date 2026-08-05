namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;

public interface IDataRightsResponseDeadlinePolicy
{
    Task<Result<DataRightsResponseDeadlinePolicyEvidence>> ResolveGuestAsync(
        Guid propertyId,
        DataRightsCaseOperation requestedOperations,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset evaluatedAtUtc,
        CancellationToken cancellationToken);
}
