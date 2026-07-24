namespace BunkFy.Modules.Guests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Domain.Errors;
using Gma.Framework.Results;

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
    public static readonly Error DataRightsApprovalRequired = new(
        "Guests.DataRightsApprovalRequired",
        "An exact approved data-rights operation is required.");
    public static readonly Error CorrectionIdempotencyConflict = new(
        "Guests.CorrectionIdempotencyConflict",
        "The correction idempotency key was already used for a different request.");
    public static readonly Error CorrectionNoChanges = new(
        "Guests.CorrectionNoChanges",
        "The approved correction must change at least one guest-profile field.");
    public static readonly Error CorrectionRequestInvalid = new(
        "Guests.CorrectionRequestInvalid",
        "The correction request is invalid.");
    public static Error VersionConflict => GuestsDomainErrors.VersionConflict;
    public static Error GuestArchived => GuestsDomainErrors.GuestArchived;
    public static Error GuestAlreadyArchived => GuestsDomainErrors.GuestAlreadyArchived;
}
