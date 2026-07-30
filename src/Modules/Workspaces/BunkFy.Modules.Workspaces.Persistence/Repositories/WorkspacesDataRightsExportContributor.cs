namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class WorkspacesDataRightsExportContributor(
    WorkspacesDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectExportContributor
{
    public const int MaximumChildRecords = 1_000;
    public const string StaffAccessProfileSnapshotRecordType =
        "staff-access-profile-snapshot";
    public const string StaffAccessPlanPropertyRecordType =
        "staff-access-plan-property";

    public string OwnerKey => WorkspacesDataRightsCoordinates.Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.StaffRights];

    public DataRightsExportDescriptor Descriptor =>
        WorkspacesDataRightsExportSchema.Descriptor;

    public Task<DataRightsSubjectExportResult> ExportAsync(
        DataRightsSubjectExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        if (!this.IsValidScope(
                request.CaseType,
                request.TenantId,
                request.PropertyId))
        {
            return Task.FromResult(
                DataRightsSubjectExportResult.ScopeUnavailable());
        }

        DataRightsSubjectCoordinate? coordinate = request.Coordinate;
        if (coordinate is null ||
            !string.Equals(
                coordinate.OwnerKey,
                WorkspacesDataRightsCoordinates.Owner,
                StringComparison.OrdinalIgnoreCase) ||
            coordinate.RecordId == Guid.Empty ||
            coordinate.RecordVersion <= 0)
        {
            return Task.FromResult(
                DataRightsSubjectExportResult.NotFound());
        }

        string tenantId = request.TenantId.Trim();
        return coordinate.RecordType?.Trim().ToLowerInvariant() switch
        {
            WorkspacesDataRightsCoordinates.StaffOnboardingRecordType =>
                this.ExportOnboardingAsync(
                    tenantId,
                    coordinate,
                    sink,
                    cancellationToken),
            WorkspacesDataRightsCoordinates.StaffAccessProcessRecordType =>
                this.ExportAccessProcessAsync(
                    tenantId,
                    coordinate,
                    sink,
                    cancellationToken),
            WorkspacesDataRightsCoordinates.StaffAccessPlanRecordType =>
                this.ExportAccessPlanAsync(
                    tenantId,
                    coordinate,
                    sink,
                    cancellationToken),
            WorkspacesDataRightsCoordinates
                .StaffRetentionCorrelationReceiptRecordType =>
                this.ExportRetentionReceiptAsync(
                    tenantId,
                    coordinate,
                    sink,
                    cancellationToken),
            _ => Task.FromResult(
                DataRightsSubjectExportResult.NotFound())
        };
    }

    private async Task<DataRightsSubjectExportResult> ExportOnboardingAsync(
        string tenantId,
        DataRightsSubjectCoordinate coordinate,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffOnboardingDataRightsExport? record =
            await dbContext.StaffOnboardingApplications
                .AsNoTracking()
                .Where(application =>
                    application.ScopeId == tenantId &&
                    application.Id == coordinate.RecordId)
                .Select(application =>
                    new WorkspaceStaffOnboardingDataRightsExport(
                        application.Id,
                        application.ScopeId,
                        application.SourceKind,
                        application.SourceId,
                        application.ClaimId,
                        application.ClaimVersion,
                        application.SubjectId,
                        application.VerifiedAccountEmail,
                        application.DisplayName,
                        application.LegalName,
                        application.WorkEmail,
                        application.WorkPhone,
                        application.EmployeeNumber,
                        application.JobTitle,
                        application.Department,
                        application.Status,
                        application.StaffMemberId,
                        application.FailureCode,
                        application.Version,
                        application.CreatedAtUtc,
                        application.LastChangedAtUtc))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (record is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (record.Version != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        await sink.WriteAsync(
            WorkspacesDataRightsExportSchema.CreateRecord(
                WorkspacesDataRightsCoordinates.StaffOnboardingRecordType,
                record.Id,
                record.Version,
                record),
            cancellationToken).ConfigureAwait(false);
        return DataRightsSubjectExportResult.Success(1);
    }

    private async Task<DataRightsSubjectExportResult> ExportAccessProcessAsync(
        string tenantId,
        DataRightsSubjectCoordinate coordinate,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessProcessDataRightsExport? process =
            await dbContext.StaffAccessProcesses
                .AsNoTracking()
                .Where(process =>
                    process.ScopeId == tenantId &&
                    process.Id == coordinate.RecordId)
                .Select(process =>
                    new WorkspaceStaffAccessProcessDataRightsExport(
                        process.Id,
                        process.ScopeId,
                        process.StaffMemberId,
                        process.SubjectId,
                        process.TargetState,
                        process.TargetStaffVersion,
                        process.EffectiveOn,
                        process.RequestedBy,
                        process.State,
                        process.FailureCode,
                        process.Version,
                        process.CreatedAtUtc,
                        process.LastChangedAtUtc,
                        process.CompletedAtUtc))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (process is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (process.Version != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        WorkspaceStaffAccessProfileDataRightsExport[] profiles =
            await dbContext.StaffAccessProcesses
                .AsNoTracking()
                .Where(candidate =>
                    candidate.ScopeId == tenantId &&
                    candidate.Id == coordinate.RecordId)
                .SelectMany(candidate => candidate.ProfileSnapshots)
                .OrderBy(profile => profile.AssignmentScope)
                .ThenBy(profile => profile.ProfileId)
                .Take(MaximumChildRecords + 1)
                .Select(profile =>
                    new WorkspaceStaffAccessProfileDataRightsExport(
                        process.Id,
                        profile.ProfileId,
                        profile.AssignmentScope))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        if (profiles.Length > MaximumChildRecords)
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        await sink.WriteAsync(
            WorkspacesDataRightsExportSchema.CreateRecord(
                WorkspacesDataRightsCoordinates
                    .StaffAccessProcessRecordType,
                process.Id,
                process.Version,
                process),
            cancellationToken).ConfigureAwait(false);
        int recordCount = 1;
        foreach (WorkspaceStaffAccessProfileDataRightsExport profile in
                 profiles)
        {
            await sink.WriteAsync(
                WorkspacesDataRightsExportSchema.CreateRecord(
                    StaffAccessProfileSnapshotRecordType,
                    CreateDeterministicChildId(
                        process.Id,
                        $"{profile.ProfileId:N}|{profile.AssignmentScope}"),
                    process.Version,
                    profile),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        return DataRightsSubjectExportResult.Success(recordCount);
    }

    private async Task<DataRightsSubjectExportResult> ExportAccessPlanAsync(
        string tenantId,
        DataRightsSubjectCoordinate coordinate,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        WorkspaceStaffAccessPlanDataRightsExport? plan =
            await dbContext.StaffAccessPlans
                .AsNoTracking()
                .Where(candidate =>
                    candidate.ScopeId == tenantId &&
                    candidate.Id == coordinate.RecordId)
                .Select(candidate =>
                    new WorkspaceStaffAccessPlanDataRightsExport(
                        candidate.Id,
                        candidate.ScopeId,
                        candidate.SourceKind,
                        candidate.ProfileId,
                        candidate.ProfileKey,
                        candidate.CreatedBySubjectId,
                        candidate.Status,
                        candidate.SourceExpiredAtUtc,
                        candidate.Version,
                        candidate.CreatedAtUtc,
                        candidate.LastChangedAtUtc))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (plan is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (plan.Version != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        WorkspaceStaffAccessPlanPropertyDataRightsExport[] properties =
            await dbContext.StaffAccessPlans
                .AsNoTracking()
                .Where(candidate =>
                    candidate.ScopeId == tenantId &&
                    candidate.Id == coordinate.RecordId)
                .SelectMany(candidate => candidate.Properties)
                .OrderBy(property => property.PropertyId)
                .Take(MaximumChildRecords + 1)
                .Select(property =>
                    new WorkspaceStaffAccessPlanPropertyDataRightsExport(
                        property.ScopeId,
                        property.PlanId,
                        property.PropertyId))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        if (properties.Length > MaximumChildRecords)
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        await sink.WriteAsync(
            WorkspacesDataRightsExportSchema.CreateRecord(
                WorkspacesDataRightsCoordinates.StaffAccessPlanRecordType,
                plan.Id,
                plan.Version,
                plan),
            cancellationToken).ConfigureAwait(false);
        int recordCount = 1;
        foreach (WorkspaceStaffAccessPlanPropertyDataRightsExport property in
                 properties)
        {
            await sink.WriteAsync(
                WorkspacesDataRightsExportSchema.CreateRecord(
                    StaffAccessPlanPropertyRecordType,
                    CreateDeterministicChildId(
                        plan.Id,
                        property.PropertyId.ToString("N")),
                    plan.Version,
                    property),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        return DataRightsSubjectExportResult.Success(recordCount);
    }

    private async Task<DataRightsSubjectExportResult>
        ExportRetentionReceiptAsync(
            string tenantId,
            DataRightsSubjectCoordinate coordinate,
            IDataRightsExportSink sink,
            CancellationToken cancellationToken)
    {
        WorkspaceStaffRetentionCorrelationDataRightsExport? record =
            await dbContext.StaffRetentionCorrelationReceipts
                .AsNoTracking()
                .Where(receipt =>
                    receipt.ScopeId == tenantId &&
                    receipt.Id == coordinate.RecordId)
                .Select(receipt =>
                    new WorkspaceStaffRetentionCorrelationDataRightsExport(
                        receipt.ScopeId,
                        receipt.StaffMemberId,
                        receipt.SelectedStaffVersion,
                        new WorkspaceStaffRetentionCorrelationProofDataRightsExport(
                            receipt.Id,
                            receipt.ContractVersion,
                            receipt.ExecutionId,
                            receipt.OnboardingRecordsScrubbed,
                            receipt.AccessProcessRecordsScrubbed,
                            receipt.AccessPlanRecordsScrubbed,
                            receipt.CompletedAtUtc,
                            receipt.CanonicalSha256)))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (record is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (record.Proof.ContractVersion != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        await sink.WriteAsync(
            WorkspacesDataRightsExportSchema.CreateRecord(
                WorkspacesDataRightsCoordinates
                    .StaffRetentionCorrelationReceiptRecordType,
                record.Proof.Id,
                record.Proof.ContractVersion,
                record),
            cancellationToken).ConfigureAwait(false);
        return DataRightsSubjectExportResult.Success(1);
    }

    private bool IsValidScope(
        DataRightsCaseType caseType,
        string tenantId,
        Guid? propertyId) =>
        caseType == DataRightsCaseType.StaffRights &&
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(
            scopeContext.ScopeId,
            tenantId?.Trim(),
            StringComparison.Ordinal) &&
        !propertyId.HasValue;

    private static Guid CreateDeterministicChildId(
        Guid namespaceId,
        string name)
    {
        byte[] namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);
        byte[] nameBytes = Encoding.UTF8.GetBytes(name);
        byte[] input = new byte[namespaceBytes.Length + nameBytes.Length];
        namespaceBytes.CopyTo(input, 0);
        nameBytes.CopyTo(input, namespaceBytes.Length);

        byte[] hash = SHA256.HashData(input);
        hash[6] = (byte)((hash[6] & 0x0f) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        Span<byte> guidBytes = hash.AsSpan(0, 16);
        SwapByteOrder(guidBytes);
        return new Guid(guidBytes);
    }

    private static void SwapByteOrder(Span<byte> bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }

}
