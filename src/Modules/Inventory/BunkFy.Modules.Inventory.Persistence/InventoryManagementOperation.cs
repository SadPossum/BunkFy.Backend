namespace BunkFy.Modules.Inventory.Persistence;

using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Domain;

internal sealed class InventoryManagementOperation : IScopedEntity
{
    private InventoryManagementOperation() { }

    internal InventoryManagementOperation(
        InventoryManagementOperationRecord record)
    {
        this.Id = record.OperationId;
        this.ScopeId = record.ScopeId;
        this.PropertyId = record.PropertyId;
        this.ResourceKind = record.ResourceKind;
        this.ResourceId = record.ResourceId;
        this.Kind = record.Kind;
        this.ExpectedVersion = record.ExpectedVersion;
        this.RequestFingerprint = record.RequestFingerprint;
        this.ResultSalesMode = record.ResultSalesMode;
        this.ResultVersion = record.ResultVersion;
        this.CompletedAtUtc = record.CompletedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid PropertyId { get; private set; }
    public InventoryManagementResourceKind ResourceKind { get; private set; }
    public Guid ResourceId { get; private set; }
    public InventoryManagementMutationKind Kind { get; private set; }
    public long ExpectedVersion { get; private set; }
    public string RequestFingerprint { get; private set; } = string.Empty;
    public InventorySalesMode ResultSalesMode { get; private set; }
    public long ResultVersion { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    internal InventoryManagementOperationRecord ToRecord() => new(
        this.Id,
        this.ScopeId,
        this.PropertyId,
        this.ResourceKind,
        this.ResourceId,
        this.Kind,
        this.ExpectedVersion,
        this.RequestFingerprint,
        this.ResultSalesMode,
        this.ResultVersion,
        this.CompletedAtUtc);
}
