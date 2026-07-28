namespace BunkFy.Modules.Ingestion.Domain.Controls;

using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Results;

public sealed class AdapterIngressGlobalControl : AggregateRoot<string>
{
    public const string SingletonId = "adapter-ingress";

    private AdapterIngressGlobalControl() { }

    private AdapterIngressGlobalControl(string id)
        : base(id)
    {
    }

    public bool IsStopped { get; private set; }
    public string LastReasonCode { get; private set; } = string.Empty;
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public DateTimeOffset? StoppedAtUtc { get; private set; }
    public DateTimeOffset? ResumedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<AdapterIngressGlobalControl> CreateStopped(
        string reasonCode,
        string actor,
        DateTimeOffset nowUtc)
    {
        if (!TryNormalize(reasonCode, actor, out string reason, out string changedBy))
        {
            return Result.Failure<AdapterIngressGlobalControl>(
                IngestionDomainErrors.AdapterIngressControlDecisionInvalid);
        }

        AdapterIngressGlobalControl control = new(SingletonId)
        {
            IsStopped = true,
            LastReasonCode = reason,
            LastChangedBy = changedBy,
            LastChangedAtUtc = nowUtc,
            StoppedAtUtc = nowUtc
        };
        return Result.Success(control);
    }

    public Result Stop(
        long expectedVersion,
        string reasonCode,
        string actor,
        DateTimeOffset nowUtc)
    {
        if (this.Version != expectedVersion)
        {
            return Result.Failure(IngestionDomainErrors.VersionConflict);
        }

        if (this.IsStopped)
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressGlobalAlreadyStopped);
        }

        if (!TryNormalize(reasonCode, actor, out string reason, out string changedBy))
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressControlDecisionInvalid);
        }

        this.IsStopped = true;
        this.LastReasonCode = reason;
        this.LastChangedBy = changedBy;
        this.LastChangedAtUtc = nowUtc;
        this.StoppedAtUtc = nowUtc;
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

        if (!this.IsStopped)
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressGlobalAlreadyActive);
        }

        if (!TryNormalize(reasonCode, actor, out string reason, out string changedBy))
        {
            return Result.Failure(IngestionDomainErrors.AdapterIngressControlDecisionInvalid);
        }

        this.IsStopped = false;
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
        return normalizedReason.Length is > 0 and <= AdapterIngressTenantControl.ReasonCodeMaxLength &&
               normalizedActor.Length is > 0 and <= AdapterIngressTenantControl.ActorMaxLength &&
               normalizedReason.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
    }
}
