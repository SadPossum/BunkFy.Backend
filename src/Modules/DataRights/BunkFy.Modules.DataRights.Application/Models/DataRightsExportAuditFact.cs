namespace BunkFy.Modules.DataRights.Application.Models;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Models;

public sealed record DataRightsExportAuditFact(
    string TenantId,
    Guid ArtifactId,
    Guid CaseId,
    DataRightsCaseType CaseType,
    Guid? PropertyId,
    DataRightsExportAuditAction Action,
    string ActorId,
    string OutcomeCode,
    DateTimeOffset OccurredAtUtc);
