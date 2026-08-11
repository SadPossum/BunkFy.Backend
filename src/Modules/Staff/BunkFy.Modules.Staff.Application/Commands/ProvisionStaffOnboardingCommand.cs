namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Application.Ports;
using Gma.Framework.Cqrs;

public sealed record ProvisionStaffOnboardingCommand(
    Guid OperationId,
    string AuthSubjectId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    string ActorId,
    string Reason) : ITransactionalCommand<StaffMemberDto>,
    IStaffPersistenceRetryableCommand;
