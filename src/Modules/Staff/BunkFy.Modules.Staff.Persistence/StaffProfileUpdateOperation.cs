namespace BunkFy.Modules.Staff.Persistence;

using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Domain;

internal sealed class StaffProfileUpdateOperation : IScopedEntity
{
    private StaffProfileUpdateOperation() { }

    internal StaffProfileUpdateOperation(
        StaffProfileUpdateOperationRecord record)
    {
        this.Id = record.OperationId;
        this.ScopeId = record.ScopeId;
        this.StaffMemberId = record.StaffMemberId;
        this.ExpectedVersion = record.ExpectedVersion;
        this.RequestFingerprint = record.RequestFingerprint;
        this.ResultStatus = record.ResultStatus;
        this.ResultVersion = record.ResultVersion;
        this.CompletedAtUtc = record.CompletedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid StaffMemberId { get; private set; }
    public long ExpectedVersion { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public StaffStatus ResultStatus { get; private set; }
    public long ResultVersion { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    internal StaffProfileUpdateOperationRecord ToRecord() => new(
        this.Id,
        this.ScopeId,
        this.StaffMemberId,
        this.ExpectedVersion,
        this.RequestFingerprint,
        this.ResultStatus,
        this.ResultVersion,
        this.CompletedAtUtc);
}
