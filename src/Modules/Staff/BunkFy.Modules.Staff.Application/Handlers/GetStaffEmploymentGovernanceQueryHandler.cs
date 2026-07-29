namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Governance;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetStaffEmploymentGovernanceQueryHandler(
    IStaffEmploymentGovernanceRepository repository)
    : IQueryHandler<
        GetStaffEmploymentGovernanceQuery,
        StaffEmploymentGovernanceDto>
{
    public async Task<Result<StaffEmploymentGovernanceDto>> HandleAsync(
        GetStaffEmploymentGovernanceQuery query,
        CancellationToken cancellationToken)
    {
        if (query.StaffMemberId == Guid.Empty)
        {
            return Result.Failure<StaffEmploymentGovernanceDto>(
                StaffApplicationErrors
                    .EmploymentGovernanceRequestInvalid);
        }

        StaffEmploymentGovernance? governance =
            await repository.GetAsync(
                query.StaffMemberId,
                cancellationToken).ConfigureAwait(false);
        return governance is null
            ? Result.Failure<StaffEmploymentGovernanceDto>(
                StaffApplicationErrors
                    .EmploymentGovernanceNotConfigured)
            : Result.Success(governance.ToDto());
    }
}
