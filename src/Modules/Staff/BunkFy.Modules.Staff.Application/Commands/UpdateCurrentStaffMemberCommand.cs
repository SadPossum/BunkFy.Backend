namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Cqrs;

public sealed record UpdateCurrentStaffMemberCommand(
    string AuthSubjectId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<StaffMemberDto>;
