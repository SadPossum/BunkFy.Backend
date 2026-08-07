namespace BunkFy.Modules.Guests.Persistence;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Domain;

internal sealed class GuestManagementOperation : IScopedEntity
{
    private GuestManagementOperation() { }

    internal GuestManagementOperation(GuestManagementOperationRecord record)
    {
        this.Id = record.OperationId;
        this.ScopeId = record.ScopeId;
        this.PropertyId = record.PropertyId;
        this.GuestId = record.GuestId;
        this.Kind = record.Kind;
        this.ExpectedVersion = record.ExpectedVersion;
        this.RequestFingerprint = record.RequestFingerprint;
        this.ResultStatus = record.ResultStatus;
        this.ResultVersion = record.ResultVersion;
        this.CompletedAtUtc = record.CompletedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public Guid GuestId { get; private set; }
    public GuestManagementOperationKind Kind { get; private set; }
    public long ExpectedVersion { get; private set; }
    public string? RequestFingerprint { get; private set; }
    public GuestStatus ResultStatus { get; private set; }
    public long ResultVersion { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    internal GuestManagementOperationRecord ToRecord() => new(
        this.Id,
        this.ScopeId,
        this.PropertyId,
        this.GuestId,
        this.Kind,
        this.ExpectedVersion,
        this.RequestFingerprint,
        this.ResultStatus,
        this.ResultVersion,
        this.CompletedAtUtc);
}
