namespace BunkFy.Modules.Ingestion.Persistence;

using BunkFy.Modules.Ingestion.Application.Ports;
using Gma.Framework.Domain;

internal sealed class IngestionConnectionManagementOperation : IScopedEntity
{
    private IngestionConnectionManagementOperation() { }

    internal IngestionConnectionManagementOperation(
        IngestionConnectionManagementOperationRecord record)
    {
        this.Id = record.OperationId;
        this.ScopeId = record.ScopeId;
        this.PropertyId = record.PropertyId;
        this.ConnectionId = record.ConnectionId;
        this.Kind = record.Kind;
        this.ExpectedVersion = record.ExpectedVersion;
        this.RequestFingerprint = record.RequestFingerprint;
        this.ResultVersion = record.ResultVersion;
        this.CompletedAtUtc = record.CompletedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public Guid ConnectionId { get; private set; }
    public IngestionConnectionManagementMutationKind Kind { get; private set; }
    public long ExpectedVersion { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public long ResultVersion { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    internal IngestionConnectionManagementOperationRecord ToRecord() => new(
        this.Id,
        this.ScopeId,
        this.PropertyId,
        this.ConnectionId,
        this.Kind,
        this.ExpectedVersion,
        this.RequestFingerprint,
        this.ResultVersion,
        this.CompletedAtUtc);
}
