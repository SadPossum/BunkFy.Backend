namespace BunkFy.Modules.Guests.Application;

using BunkFy.DataGovernance;
using Gma.Framework.Results;
using BunkFy.Modules.Guests.Domain.Errors;

public static class GuestsApplicationErrors
{
    public static Error CountryPolicyDenied(CountryPolicyDecisionReason reason) => new(
        $"Guests.CountryPolicyDenied.{reason}",
        "The property is not enabled for this data-processing operation.");
    public static IReadOnlyList<Error> CountryPolicyDenials { get; } =
        Enum.GetValues<CountryPolicyDecisionReason>()
            .Where(reason => reason is not CountryPolicyDecisionReason.Unknown and not CountryPolicyDecisionReason.Allowed)
            .Select(CountryPolicyDenied)
            .ToArray();
    public static readonly Error GuestNotFound = new("Guests.GuestNotFound", "The guest profile was not found.");
    public static readonly Error TenantRequired = new("Guests.TenantRequired", "A tenant context is required.");
    public static Error VersionConflict => GuestsDomainErrors.VersionConflict;
    public static Error GuestArchived => GuestsDomainErrors.GuestArchived;
    public static Error GuestAlreadyArchived => GuestsDomainErrors.GuestAlreadyArchived;
}
