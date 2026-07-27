namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffDataRightsExportContributor(
    StaffDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectExportContributor
{
    public const int MaximumAssignmentRecords = 1_000;
    public const string AssignmentRecordType = "staff-property-assignment";

    public string OwnerKey => StaffDataRightsDiscoveryContributor.Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.StaffRights];

    public DataRightsExportDescriptor Descriptor => StaffDataRightsExportSchema.Descriptor;

    public async Task<DataRightsSubjectExportResult> ExportAsync(
        DataRightsSubjectExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        if (!this.IsValidScope(request.CaseType, request.TenantId, request.PropertyId))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        if (request.Coordinate is not { } coordinate ||
            !string.Equals(
                coordinate.OwnerKey,
                StaffDataRightsDiscoveryContributor.Owner,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                coordinate.RecordType,
                StaffDataRightsDiscoveryContributor.ProfileRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            coordinate.RecordId == Guid.Empty ||
            coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        StaffExportSnapshot? snapshot = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(member => member.Id == coordinate.RecordId)
            .Select(member => new StaffExportSnapshot(
                new StaffProfileDataRightsExport(
                    member.Id,
                    member.DisplayName,
                    member.LegalName,
                    member.WorkEmail,
                    member.WorkPhone,
                    member.EmployeeNumber,
                    member.JobTitle,
                    member.Department,
                    member.AuthSubjectId,
                    member.Status,
                    member.Version,
                    member.CreatedAtUtc,
                    member.LastChangedAtUtc,
                    member.SuspendedAtUtc,
                    member.DepartedAtUtc,
                    member.DepartureEffectiveOn),
                member.Assignments
                    .OrderBy(assignment => assignment.Id)
                    .Select(assignment => new StaffAssignmentDataRightsExport(
                        assignment.Id,
                        assignment.StaffMemberId,
                        assignment.PropertyId,
                        assignment.PropertyJobTitle,
                        assignment.IsPrimary,
                        assignment.IsCurrent,
                        assignment.EffectiveFrom,
                        assignment.EffectiveTo,
                        assignment.AssignedAtUtc,
                        assignment.AssignedAtVersion,
                        assignment.UnassignedAtUtc,
                        assignment.UnassignedAtVersion))
                    .Take(MaximumAssignmentRecords + 1)
                    .ToArray()))
            .AsSingleQuery()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (snapshot.Profile.Version != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        if (snapshot.Assignments.Length > MaximumAssignmentRecords)
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        await sink.WriteAsync(
            StaffDataRightsExportSchema.CreateProfileRecord(snapshot.Profile),
            cancellationToken).ConfigureAwait(false);
        int recordCount = 1;

        foreach (StaffAssignmentDataRightsExport assignment in snapshot.Assignments)
        {
            await sink.WriteAsync(
                StaffDataRightsExportSchema.CreateAssignmentRecord(assignment),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        return DataRightsSubjectExportResult.Success(recordCount);
    }

    private bool IsValidScope(
        DataRightsCaseType caseType,
        string tenantId,
        Guid? propertyId) =>
        caseType == DataRightsCaseType.StaffRights &&
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(scopeContext.ScopeId, tenantId?.Trim(), StringComparison.Ordinal) &&
        !propertyId.HasValue;

    private sealed record StaffExportSnapshot(
        StaffProfileDataRightsExport Profile,
        StaffAssignmentDataRightsExport[] Assignments);
}
