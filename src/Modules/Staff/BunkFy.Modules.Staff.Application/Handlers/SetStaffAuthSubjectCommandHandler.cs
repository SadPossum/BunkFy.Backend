namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class SetStaffAuthSubjectCommandHandler(
    StaffMemberMutationCoordinator mutations,
    StaffAuthSubjectChangeCoordinator changes)
    : ICommandHandler<SetStaffAuthSubjectCommand, StaffMemberMutationReceiptDto>
{
    public async Task<Result<StaffMemberMutationReceiptDto>> HandleAsync(
        SetStaffAuthSubjectCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.AuthSubjectOperationInvalid);
        }

        Result<StaffAuthSubjectChangeValues> values =
            StaffAuthSubjectChangeValues.Create(
                command.AuthSubjectId,
                command.ActorId);
        if (values.IsFailure)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                values.Error);
        }

        StaffMember? member = await mutations.AcquireOperationalAsync(
                command.StaffMemberId,
                cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        return await changes.ExecuteAsync(
                member,
                command.OperationId,
                command.ExpectedVersion,
                values.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
