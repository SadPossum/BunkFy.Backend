namespace BunkFy.Modules.Workspaces.Application.Models;

using BunkFy.Modules.Workspaces.Domain;

public sealed record WorkspaceStaffOnboardingDataRightsCorrectionTarget(
    Guid ApplicationId,
    long Version,
    WorkspaceStaffOnboardingState Status,
    string? DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department);
