namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;

public interface IDataRightsAnonymisationApprovalPolicy
{
    Task<Result<DataRightsApprovalPolicyEvidence>> EvaluateAsync(
        Guid propertyId,
        CancellationToken cancellationToken);
}
