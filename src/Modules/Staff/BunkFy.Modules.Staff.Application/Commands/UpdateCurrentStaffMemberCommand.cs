namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Application.Ports;
using Gma.Framework.Cqrs;

public sealed record UpdateCurrentStaffMemberCommand(
    Guid OperationId,
    string AuthSubjectId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<StaffProfileMutationReceiptDto>,
    IStaffPersistenceRetryableCommand;
