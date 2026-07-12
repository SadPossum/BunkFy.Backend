namespace BunkFy.Modules.Staff.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;
using Gma.Framework.Scoping;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[ScopeAware]
public sealed record StaffMemberCreatedIntegrationEvent : IntegrationEvent, IScopedIntegrationEvent
{
    public const string EventType = "member-created";
    public const int EventVersion = 1;

    public StaffMemberCreatedIntegrationEvent(Guid eventId, string scopeId, DateTimeOffset occurredAtUtc,
        Guid staffMemberId, StaffStatus status, string? authSubjectId, long staffVersion)
        : base(eventId, occurredAtUtc, EventType, EventVersion)
    {
        this.ScopeId = ScopeIds.Normalize(scopeId, nameof(scopeId));
        this.StaffMemberId = IntegrationEventContractGuards.RequireId(staffMemberId, nameof(staffMemberId));
        this.Status = RequireStatus(status);
        this.AuthSubjectId = NormalizeOptional(authSubjectId, StaffContractLimits.AuthSubjectIdMaxLength, nameof(authSubjectId));
        this.StaffVersion = staffVersion > 0 ? staffVersion : throw new ArgumentOutOfRangeException(nameof(staffVersion));
    }

    public string ScopeId { get; }
    public Guid StaffMemberId { get; }
    public StaffStatus Status { get; }
    public string? AuthSubjectId { get; }
    public long StaffVersion { get; }
    string IScopedIntegrationEvent.ScopeId => this.ScopeId;

    internal static StaffStatus RequireStatus(StaffStatus status) =>
        status is StaffStatus.Active or StaffStatus.Suspended or StaffStatus.Departed
            ? status
            : throw new ArgumentOutOfRangeException(nameof(status));

    internal static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return normalized.Length == 0 ? null : normalized;
    }
}
