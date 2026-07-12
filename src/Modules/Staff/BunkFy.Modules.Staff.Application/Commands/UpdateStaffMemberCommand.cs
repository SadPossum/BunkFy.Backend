namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record UpdateStaffMemberCommand(Guid StaffMemberId, string DisplayName, string? LegalName,
    string? WorkEmail, string? WorkPhone, string? EmployeeNumber, string? JobTitle, string? Department,
    long ExpectedVersion, string ActorId) : ITransactionalCommand<StaffMemberDto>;
