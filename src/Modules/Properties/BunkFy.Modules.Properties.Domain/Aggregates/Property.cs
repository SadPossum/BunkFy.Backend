namespace BunkFy.Modules.Properties.Domain.Aggregates;

using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class Property : ScopedAggregateRoot<Guid>
{
    public const int ActorIdMaxLength = 200;
    public const int PropertyNameMaxLength = 256;
    public const int PropertyCodeMaxLength = 64;
    public const int TimeZoneIdMaxLength = 128;

    private Property() { }

    private Property(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public PropertyName Name { get; private set; }
    public PropertyCode Code { get; private set; }
    public PropertyTimeZoneId TimeZoneId { get; private set; }
    public PropertyState Status { get; private set; } = PropertyState.Active;
    public long Version { get; private set; } = 1;
    public long ProjectionOrdinal { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public DateTimeOffset? RetiredAtUtc { get; private set; }

    public static Result<Property> Create(
        Guid id,
        string tenantId,
        string name,
        string code,
        string timeZoneId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Property>(PropertiesDomainErrors.PropertyIdRequired);
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure<Property>(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Result<PropertyValues> values = PropertyValues.Create(tenantId, name, code, timeZoneId);
        if (values.IsFailure)
        {
            return Result.Failure<Property>(values.Error);
        }

        Property property = new(id, values.Value.ScopeId)
        {
            Name = values.Value.Name,
            Code = values.Value.Code,
            TimeZoneId = values.Value.TimeZoneId,
            CreatedAtUtc = nowUtc
        };

        property.RaiseDomainEvent(new PropertyCreatedDomainEvent(
            eventId,
            nowUtc,
            property.Id,
            property.ScopeId,
            property.Name.Value,
            property.Code.Value,
            property.TimeZoneId.Value,
            property.Status,
            property.Version));

        return Result.Success(property);
    }

    public Result Update(
        string name,
        string code,
        string timeZoneId,
        long expectedVersion,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Result<PropertyValues> values = PropertyValues.Create(this.ScopeId, name, code, timeZoneId);
        if (values.IsFailure)
        {
            return Result.Failure(values.Error);
        }

        this.Name = values.Value.Name;
        this.Code = values.Value.Code;
        this.TimeZoneId = values.Value.TimeZoneId;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;

        this.RaiseDomainEvent(new PropertyUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.Id,
            this.ScopeId,
            this.Name.Value,
            this.Code.Value,
            this.TimeZoneId.Value,
            this.Status,
            this.Version));

        return Result.Success();
    }

    public Result RegisterRoom(long expectedVersion)
    {
        Result statusResult = this.EnsureActive();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        this.Version++;
        return Result.Success();
    }

    public Result Retire(long expectedVersion, Guid eventId, DateTimeOffset nowUtc, string? actorId = null)
    {
        Result statusResult = this.EnsureCanRetire();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        Result versionResult = this.EnsureExpectedVersion(expectedVersion);
        if (versionResult.IsFailure)
        {
            return versionResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        if (!TryNormalizeOptionalActor(actorId, out string? normalizedActorId))
        {
            return Result.Failure(PropertiesDomainErrors.ActorIdInvalid);
        }

        this.Status = PropertyState.Retired;
        this.RetiredAtUtc = nowUtc;
        this.UpdatedAtUtc = nowUtc;
        this.Version++;
        this.RaiseDomainEvent(new PropertyRetiredDomainEvent(
            eventId,
            nowUtc,
            this.Id,
            this.ScopeId,
            this.Version,
            normalizedActorId));

        return Result.Success();
    }

    private static bool TryNormalizeOptionalActor(string? value, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null ||
               (normalized.Length <= ActorIdMaxLength && !normalized.Any(char.IsControl));
    }

    private Result EnsureActive() =>
        this.Status switch
        {
            PropertyState.Active => Result.Success(),
            PropertyState.Retired => Result.Failure(PropertiesDomainErrors.PropertyRetired),
            _ => Result.Failure(PropertiesDomainErrors.PropertyStatusUnknown)
        };

    private Result EnsureCanRetire() =>
        this.Status switch
        {
            PropertyState.Active => Result.Success(),
            PropertyState.Retired => Result.Failure(PropertiesDomainErrors.PropertyAlreadyRetired),
            _ => Result.Failure(PropertiesDomainErrors.PropertyStatusUnknown)
        };

    private Result EnsureExpectedVersion(long expectedVersion) =>
        expectedVersion == this.Version
            ? Result.Success()
            : Result.Failure(PropertiesDomainErrors.VersionConflict);

    private sealed record PropertyValues(
        string ScopeId,
        PropertyName Name,
        PropertyCode Code,
        PropertyTimeZoneId TimeZoneId)
    {
        public static Result<PropertyValues> Create(
            string tenantId,
            string? name,
            string? code,
            string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return Result.Failure<PropertyValues>(PropertiesDomainErrors.TenantRequired);
            }

            if (!TenantIds.TryNormalize(tenantId, out string? normalizedTenantId))
            {
                return Result.Failure<PropertyValues>(PropertiesDomainErrors.TenantInvalid);
            }

            Result<PropertyName> nameResult = PropertyName.Create(name);
            if (nameResult.IsFailure)
            {
                return Result.Failure<PropertyValues>(nameResult.Error);
            }

            Result<PropertyCode> codeResult = PropertyCode.Create(code);
            if (codeResult.IsFailure)
            {
                return Result.Failure<PropertyValues>(codeResult.Error);
            }

            Result<PropertyTimeZoneId> timeZoneResult = PropertyTimeZoneId.Create(timeZoneId);
            if (timeZoneResult.IsFailure)
            {
                return Result.Failure<PropertyValues>(timeZoneResult.Error);
            }

            return Result.Success(new PropertyValues(
                normalizedTenantId,
                nameResult.Value,
                codeResult.Value,
                timeZoneResult.Value));
        }
    }
}
