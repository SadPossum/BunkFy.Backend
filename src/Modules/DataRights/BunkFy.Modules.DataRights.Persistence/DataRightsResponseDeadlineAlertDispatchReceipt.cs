namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Domain;

public sealed class DataRightsResponseDeadlineAlertDispatchReceipt
    : IScopedEntity
{
    private DataRightsResponseDeadlineAlertDispatchReceipt() { }

    private DataRightsResponseDeadlineAlertDispatchReceipt(
        Guid id,
        string scopeId,
        Guid caseId,
        Guid propertyId,
        DataRightsResponseDeadlineAlertKind alertKind,
        DateTimeOffset dueAtUtc,
        DateTimeOffset dispatchedAtUtc)
    {
        this.Id = id;
        this.ScopeId = scopeId;
        this.CaseId = caseId;
        this.PropertyId = propertyId;
        this.AlertKind = alertKind;
        this.DueAtUtc = dueAtUtc;
        this.DispatchedAtUtc = dispatchedAtUtc;
    }

    public Guid Id { get; private set; }
    public string ScopeId { get; private set; } = string.Empty;
    public Guid CaseId { get; private set; }
    public Guid PropertyId { get; private set; }
    public DataRightsResponseDeadlineAlertKind AlertKind { get; private set; }
    public DateTimeOffset DueAtUtc { get; private set; }
    public DateTimeOffset DispatchedAtUtc { get; private set; }

    internal static DataRightsResponseDeadlineAlertDispatchReceipt Create(
        Guid id,
        string scopeId,
        Guid caseId,
        Guid propertyId,
        DataRightsResponseDeadlineAlertKind alertKind,
        DateTimeOffset dueAtUtc,
        DateTimeOffset dispatchedAtUtc) => new(
            id,
            scopeId,
            caseId,
            propertyId,
            alertKind,
            dueAtUtc,
            dispatchedAtUtc);
}
