namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;

public sealed record ReconcileStaffIdentityCommand(
    string AuthSubjectId,
    string DisplayName,
    string? WorkEmail,
    bool IsActive,
    string ActorId,
    string Reason) : ITransactionalCommand<Unit>;
