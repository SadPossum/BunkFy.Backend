namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class UpdateStaffMemberCommandHandler(
    StaffMemberMutationCoordinator mutations,
    StaffProfileUpdateCoordinator updates)
    : ICommandHandler<
        UpdateStaffMemberCommand,
        StaffProfileMutationReceiptDto>
{
    public async Task<Result<StaffProfileMutationReceiptDto>> HandleAsync(
        UpdateStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
                StaffApplicationErrors.ProfileUpdateOperationInvalid);
        }

        Result<StaffProfileUpdateValues> values =
            StaffProfileUpdateValues.Create(
                command.DisplayName,
                command.LegalName,
                command.WorkEmail,
                command.WorkPhone,
                command.EmployeeNumber,
                command.JobTitle,
                command.Department,
                command.ActorId);
        if (values.IsFailure)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
                values.Error);
        }

        StaffMember? member = await mutations.AcquireOperationalAsync(
                command.StaffMemberId,
                cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        return await updates.ExecuteAsync(
                member,
                command.OperationId,
                command.ExpectedVersion,
                values.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
