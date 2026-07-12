namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record UnassignStaffPropertyCommand(Guid StaffMemberId, Guid PropertyId,
    DateOnly EffectiveTo, string Reason, long ExpectedVersion,
    string ActorId) : ITransactionalCommand<StaffMemberDto>;
