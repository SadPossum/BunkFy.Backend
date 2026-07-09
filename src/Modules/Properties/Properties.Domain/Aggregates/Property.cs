namespace Properties.Domain.Aggregates;

using Properties.Domain.Errors;
using Properties.Domain.Events;
using Properties.Domain.ValueObjects;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class Property : TenantAggregateRoot<Guid>
{
    public const int PropertyNameMaxLength = 256;
    public const int PropertyCodeMaxLength = 64;
    public const int TimeZoneIdMaxLength = 128;

    private Property() { }

    private Property(Guid id, string tenantId)
        : base(id, tenantId)
    {
    }

    public PropertyName Name { get; private set; }
    public PropertyCode Code { get; private set; }
    public PropertyTimeZoneId TimeZoneId { get; private set; }
    public PropertyState Status { get; private set; } = PropertyState.Active;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }

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

        Property property = new(id, values.Value.TenantId)
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
            property.TenantId,
            property.Name.Value,
            property.Code.Value,
            property.TimeZoneId.Value,
            property.Status));

        return Result.Success(property);
    }

    public Result Update(
        string name,
        string code,
        string timeZoneId,
        Guid eventId,
        DateTimeOffset nowUtc)
    {
        Result statusResult = this.EnsureKnownStatus();
        if (statusResult.IsFailure)
        {
            return statusResult;
        }

        if (eventId == Guid.Empty)
        {
            return Result.Failure(PropertiesDomainErrors.DomainEventIdRequired);
        }

        Result<PropertyValues> values = PropertyValues.Create(this.TenantId, name, code, timeZoneId);
        if (values.IsFailure)
        {
            return Result.Failure(values.Error);
        }

        this.Name = values.Value.Name;
        this.Code = values.Value.Code;
        this.TimeZoneId = values.Value.TimeZoneId;
        this.UpdatedAtUtc = nowUtc;

        this.RaiseDomainEvent(new PropertyUpdatedDomainEvent(
            eventId,
            nowUtc,
            this.Id,
            this.TenantId,
            this.Name.Value,
            this.Code.Value,
            this.TimeZoneId.Value,
            this.Status));

        return Result.Success();
    }

    private Result EnsureKnownStatus() =>
        this.Status is PropertyState.Active
            ? Result.Success()
            : Result.Failure(PropertiesDomainErrors.PropertyStatusUnknown);

    private sealed record PropertyValues(
        string TenantId,
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
