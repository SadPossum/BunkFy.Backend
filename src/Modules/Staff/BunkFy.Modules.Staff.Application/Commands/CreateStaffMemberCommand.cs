namespace BunkFy.Modules.Staff.Application.Commands;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Staff.Contracts;

public sealed record CreateStaffMemberCommand(string DisplayName, string? LegalName, string? WorkEmail,
    string? WorkPhone, string? EmployeeNumber, string? JobTitle, string? Department,
    string? AuthSubjectId, string ActorId) : ITransactionalCommand<StaffMemberDto>;
