namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Results;

public interface IDataRightsAnonymisationApprovalPolicy
{
    Task<Result<DataRightsApprovalPolicyEvidence>> EvaluateAsync(
        string tenantId,
        DataRightsCaseScope scope,
        Guid caseId,
        IReadOnlyCollection<DataRightsSubjectCoordinate> subjects,
        CancellationToken cancellationToken);
}
