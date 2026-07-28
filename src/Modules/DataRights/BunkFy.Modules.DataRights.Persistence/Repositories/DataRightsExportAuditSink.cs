namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Persistence.Security;
using Gma.Framework.Observability;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Results;

internal sealed class DataRightsExportAuditSink(
    DataRightsDbContext dbContext,
    IIdGenerator ids,
    ISecuritySignalRecorder securitySignals) : IDataRightsExportAuditSink
{
    public async Task RecordAsync(
        DataRightsExportAuditFact fact,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        Result<DataRightsExportAuditEntry> created =
            DataRightsExportAuditEntry.Create(
                ids.NewId(),
                fact.TenantId,
                fact.ArtifactId,
                fact.CaseId,
                fact.PropertyId,
                (DataRightsCaseKind)fact.CaseType,
                fact.Action,
                fact.ActorId,
                fact.OutcomeCode,
                fact.OccurredAtUtc);
        if (created.IsFailure)
        {
            throw new InvalidOperationException(created.Error.Code);
        }

        dbContext.ExportAuditEntries.Add(created.Value);
        await dbContext.SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);

        SecuritySignalDefinition? signal =
            DataRightsExportSecuritySignalDefinitions.ForAudit(
                fact.Action,
                fact.OutcomeCode);
        if (signal is not null)
        {
            securitySignals.Record(signal);
        }
    }
}
