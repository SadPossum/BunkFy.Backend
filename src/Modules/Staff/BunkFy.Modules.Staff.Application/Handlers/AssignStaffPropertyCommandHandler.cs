namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class AssignStaffPropertyCommandHandler(
    IStaffPropertyProjectionRepository properties,
    StaffMemberMutationCoordinator mutations,
    StaffPropertyAssignmentChangeCoordinator changes)
    : ICommandHandler<AssignStaffPropertyCommand, StaffMemberMutationReceiptDto>
{
    public async Task<Result<StaffMemberMutationReceiptDto>> HandleAsync(
        AssignStaffPropertyCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.AssignmentOperationInvalid);
        }

        Result<StaffPropertyAssignmentChangeValues> values =
            StaffPropertyAssignmentChangeValues.Create(
                command.PropertyJobTitle,
                command.ActorId);
        if (values.IsFailure)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(values.Error);
        }

        if (!await properties.IsActiveAsync(command.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.PropertyUnavailable);
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

        if (!await properties.IsActiveAsync(command.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.PropertyUnavailable);
        }

        return await changes.AssignAsync(
                member,
                command.OperationId,
                command.PropertyId,
                command.IsPrimary,
                command.EffectiveFrom,
                command.ExpectedVersion,
                values.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
