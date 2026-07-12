namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record AssignStaffPropertyCommand(Guid StaffMemberId, Guid PropertyId,
    string? PropertyJobTitle, bool IsPrimary, DateOnly EffectiveFrom,
    long ExpectedVersion, string ActorId) : ITransactionalCommand<StaffMemberDto>;
