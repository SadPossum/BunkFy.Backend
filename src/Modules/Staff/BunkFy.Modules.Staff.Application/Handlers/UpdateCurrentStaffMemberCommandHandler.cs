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
        StaffProfileMutationReceiptDto>
{
    public async Task<Result<StaffProfileMutationReceiptDto>> HandleAsync(
        UpdateCurrentStaffMemberCommand command,
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

        string authSubjectId = command.AuthSubjectId?.Trim() ?? string.Empty;
        if (authSubjectId.Length is 0 or
            > StaffContractLimits.AuthSubjectIdMaxLength)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        StaffMember? member = await members
            .GetByAuthSubjectAsync(authSubjectId, cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffProfileMutationReceiptDto>(
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
