namespace BunkFy.Modules.Ingestion.Domain.Controls;

using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class AdapterIngressTenantControl : ScopedAggregateRoot<string>
{
    public const int ActorMaxLength = 200;
    public const int ReasonCodeMaxLength = 128;

    private AdapterIngressTenantControl() { }

    private AdapterIngressTenantControl(string scopeId)
        : base(ScopeIds.Normalize(scopeId), scopeId)
    {
    }

    public bool IsSuspended { get; private set; }
    public string LastReasonCode { get; private set; } = string.Empty;
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public DateTimeOffset? SuspendedAtUtc { get; private set; }
    public DateTimeOffset? ResumedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<AdapterIngressTenantControl> CreateSuspended(
        string scopeId,
        string reasonCode,
        string actor,
        DateTimeOffset nowUtc)
    {
        if (!TryNormalize(reasonCode, actor, out string reason, out string changedBy))
        {
            return Result.Failure<AdapterIngressTenantControl>(
                IngestionDomainErrors.AdapterIngressControlDecisionInvalid);
        }

        AdapterIngressTenantControl control;
        try
        {
            control = new AdapterIngressTenantControl(scopeId);
        }
        catch (ArgumentException)
        {
            return Result.Failure<AdapterIngressTenantControl>(IngestionDomainErrors.ScopeRequired);
        }

        control.IsSuspended = true;
        control.LastReasonCode = reason;
        control.LastChangedBy = changedBy;
        control.LastChangedAtUtc = nowUtc;
        control.SuspendedAtUtc = nowUtc;
        return Result.Success(control);
    }

    public Result Suspend(
        long expectedVersion,
        string reasonCode,
        string actor,
        DateTimeOffset nowUtc)
    {
        if (this.Version != expectedVersion)
        {
            return Result.Failure(IngestionDomainErrors.VersionConflict);
        }

        if (this.IsSuspended)
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressTenantAlreadySuspended);
        }

        if (!TryNormalize(reasonCode, actor, out string reason, out string changedBy))
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressControlDecisionInvalid);
        }

        this.IsSuspended = true;
        this.LastReasonCode = reason;
        this.LastChangedBy = changedBy;
        this.LastChangedAtUtc = nowUtc;
        this.SuspendedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    public Result Resume(
        long expectedVersion,
        string reasonCode,
        string actor,
        DateTimeOffset nowUtc)
    {
        if (this.Version != expectedVersion)
        {
            return Result.Failure(IngestionDomainErrors.VersionConflict);
        }

        if (!this.IsSuspended)
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressTenantAlreadyActive);
        }

        if (!TryNormalize(reasonCode, actor, out string reason, out string changedBy))
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressControlDecisionInvalid);
        }

        this.IsSuspended = false;
        this.LastReasonCode = reason;
        this.LastChangedBy = changedBy;
        this.LastChangedAtUtc = nowUtc;
        this.ResumedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }

    private static bool TryNormalize(
        string? reasonCode,
        string? actor,
        out string normalizedReason,
        out string normalizedActor)
    {
        normalizedReason = reasonCode?.Trim() ?? string.Empty;
        normalizedActor = actor?.Trim() ?? string.Empty;
        return normalizedReason.Length is > 0 and <= ReasonCodeMaxLength &&
               normalizedActor.Length is > 0 and <= ActorMaxLength &&
               normalizedReason.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
    }
}
