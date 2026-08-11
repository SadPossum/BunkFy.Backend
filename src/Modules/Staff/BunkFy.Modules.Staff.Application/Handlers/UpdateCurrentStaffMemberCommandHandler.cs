namespace BunkFy.Modules.Staff.Application.Handlers;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class UpdateCurrentStaffMemberCommandHandler(
    IStaffMemberRepository members,
    StaffMemberMutationCoordinator mutations,
    StaffProfileUpdateCoordinator updates)
    : ICommandHandler<
        UpdateCurrentStaffMemberCommand,
        StaffMemberMutationReceiptDto>
{
    public async Task<Result<StaffMemberMutationReceiptDto>> HandleAsync(
        UpdateCurrentStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.ProfileUpdateOperationInvalid);
        }

        Result<StaffProfileUpdateValues> values =
            StaffProfileUpdateValues.Create(
                command.DisplayName,
                command.LegalName,
                command.WorkEmail,
                command.WorkPhone,
                employeeNumber: null,
                command.JobTitle,
                command.Department,
                command.ActorId);
        if (values.IsFailure)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                values.Error);
        }

        string authSubjectId = command.AuthSubjectId?.Trim() ?? string.Empty;
        if (authSubjectId.Length is 0 or
            > StaffContractLimits.AuthSubjectIdMaxLength)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        StaffMember? member = await members
            .GetByAuthSubjectAsync(authSubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        member = await mutations.AcquireOperationalAsync(
                member.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (member is null ||
            !string.Equals(
                member.AuthSubjectId,
                authSubjectId,
                StringComparison.Ordinal))
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        return await updates.ExecuteSelfServiceAsync(
                member,
                command.OperationId,
                command.ExpectedVersion,
                values.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
