namespace BunkFy.Modules.Staff.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;

internal sealed class DepartStaffMemberCommandHandler(
    StaffMemberMutationCoordinator mutations,
    StaffLifecycleChangeCoordinator changes)
    : ICommandHandler<DepartStaffMemberCommand, StaffMemberMutationReceiptDto>
{
    public async Task<Result<StaffMemberMutationReceiptDto>> HandleAsync(
        DepartStaffMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.LifecycleOperationInvalid);
        }

        Result<StaffLifecycleChangeValues> values =
            StaffLifecycleChangeValues.Create(
                command.Reason,
                command.ActorId);
        if (values.IsFailure)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                values.Error);
        }

        StaffMember? member = await mutations.AcquireSafetyTransitionAsync(
                command.StaffMemberId,
                cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return Result.Failure<StaffMemberMutationReceiptDto>(
                StaffApplicationErrors.StaffMemberNotFound);
        }

        return await changes.DepartAsync(
                member,
                command.OperationId,
                command.EffectiveOn,
                command.ExpectedVersion,
                values.Value,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
