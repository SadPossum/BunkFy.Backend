namespace BunkFy.Modules.Properties.Persistence;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Domain;

internal sealed class PropertyMutationOperation : IScopedEntity
{
    private PropertyMutationOperation() { }

    internal PropertyMutationOperation(
        PropertyMutationOperationRecord record)
    {
        this.Id = record.OperationId;
        this.ScopeId = record.ScopeId;
        this.PropertyId = record.PropertyId;
        this.Kind = record.Kind;
        this.ExpectedVersion = record.ExpectedVersion;
        this.RequestFingerprint = record.RequestFingerprint;
        this.ResultStatus = record.ResultStatus;
        this.ResultProcessingStatus = record.ResultProcessingStatus;
        this.ResultVersion = record.ResultVersion;
        this.CompletedAtUtc = record.CompletedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public PropertyMutationKind Kind { get; private set; }
    public long ExpectedVersion { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public PropertyStatus ResultStatus { get; private set; }
    public PropertyProcessingStatus ResultProcessingStatus { get; private set; }
    public long ResultVersion { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    internal PropertyMutationOperationRecord ToRecord() => new(
        this.Id,
        this.ScopeId,
        this.PropertyId,
        this.Kind,
        this.ExpectedVersion,
        this.RequestFingerprint,
        this.ResultStatus,
        this.ResultProcessingStatus,
        this.ResultVersion,
        this.CompletedAtUtc);
}
