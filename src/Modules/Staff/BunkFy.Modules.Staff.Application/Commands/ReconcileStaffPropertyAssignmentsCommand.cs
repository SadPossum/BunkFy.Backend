namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record ReconcileStaffPropertyAssignmentsCommand(
    Guid StaffMemberId,
    IReadOnlyCollection<Guid> PropertyIds,
    string ActorId,
    string Reason) : ITransactionalCommand<IReadOnlyCollection<Guid>>;
