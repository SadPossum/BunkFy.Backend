namespace BunkFy.Extensions.Operations.Notifications;

using BunkFy.DataGovernance;
using Microsoft.Extensions.Options;

internal sealed class OperationsNotificationsProductionAdmissionValidator(
    OperationsNotificationsProductionAdmissionRegistration registration,
    OperationsNotificationsPersonalDataCatalogEvidence catalog,
    OperationsNotificationsRetentionRuntimeOptions runtime)
    : IValidateOptions<OperationsNotificationsProductionAdmissionOptions>
{
    private const int MaximumRetentionDays = 3650;

    public ValidateOptionsResult Validate(
        string? name,
        OperationsNotificationsProductionAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!registration.IsProduction)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        ValidateApproval(options, failures);
        this.ValidateCatalog(options, failures);
        ValidateWindows(options, failures);
        ValidateLegacyHistory(options, failures);
        this.ValidateOwner(options, failures);
        this.ValidateRuntime(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateApproval(
        OperationsNotificationsProductionAdmissionOptions options,
        List<string> failures)
    {
        if (!Enum.IsDefined(options.ApprovalState) ||
            options.ApprovalState !=
            OperationsNotificationsApprovalState.Approved)
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:ApprovalState must be Approved in Production.");
        }

        if (!IsApprovalReference(options.ApprovalReference))
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:ApprovalReference must be a 3-128 character non-secret evidence identifier.");
        }
    }

    private void ValidateCatalog(
        OperationsNotificationsProductionAdmissionOptions options,
        List<string> failures)
    {
        IReadOnlyList<string> catalogFailures =
            PersonalDataCatalogValidator.Validate(
                catalog.Document,
                PersonalDataCatalogValidationMode.Production);
        if (catalogFailures.Count > 0)
        {
            failures.Add(
                "The embedded Operations Notifications personal-data catalogue is not approved for Production.");
        }

        if (options.CatalogVersion != catalog.Document.CatalogVersion)
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:CatalogVersion must match embedded catalogue version {catalog.Document.CatalogVersion}.");
        }

        if (!IsSha256(options.CatalogSha256) ||
            !string.Equals(
                options.CatalogSha256,
                catalog.ContentSha256,
                StringComparison.Ordinal))
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:CatalogSha256 must match the exact embedded catalogue.");
        }
    }

    private static void ValidateWindows(
        OperationsNotificationsProductionAdmissionOptions options,
        List<string> failures)
    {
        ValidateWindow(
            options.ReadHistoryDays,
            nameof(options.ReadHistoryDays),
            failures);
        ValidateWindow(
            options.UnreadHistoryDays,
            nameof(options.UnreadHistoryDays),
            failures);
        ValidateWindow(
            options.BroadcastDays,
            nameof(options.BroadcastDays),
            failures);
        ValidateWindow(
            options.DeliveryAttemptDays,
            nameof(options.DeliveryAttemptDays),
            failures);

        if (options.UnreadHistoryDays < options.ReadHistoryDays)
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:UnreadHistoryDays must be greater than or equal to ReadHistoryDays.");
        }
    }

    private static void ValidateLegacyHistory(
        OperationsNotificationsProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.LegacyHistoryDisposition is not (
                OperationsNotificationsLegacyHistoryDisposition
                    .ResetBeforeAdmission or
                OperationsNotificationsLegacyHistoryDisposition
                    .VerifiedReferenceComplete))
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:LegacyHistoryDisposition must be ResetBeforeAdmission or VerifiedReferenceComplete.");
        }
    }

    private void ValidateOwner(
        OperationsNotificationsProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.RetentionOwner is not (
                OperationsNotificationsRetentionOwner.PublicApi or
                OperationsNotificationsRetentionOwner.Worker))
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:RetentionOwner must be PublicApi or Worker.");
        }

        if (options.RetentionOwnerInstanceCount != 1)
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:RetentionOwnerInstanceCount must be exactly 1.");
        }

        bool currentRoleOwnsRetention =
            RoleOwnsRetention(registration.HostRole, options.RetentionOwner);
        if (currentRoleOwnsRetention && !registration.NotificationsComposed)
        {
            failures.Add(
                $"The selected {registration.HostRole} retention owner must compose the Notifications module.");
        }

        if (registration.HostRole ==
                OperationsNotificationsProductionHostRole.PublicApi &&
            options.RetentionOwner ==
                OperationsNotificationsRetentionOwner.PublicApi &&
            !registration.PublicApiSingleReplica)
        {
            failures.Add(
                "A PublicApi notification-retention owner requires BunkFy:Deployment:ApiTopology=SingleReplica.");
        }
    }

    private void ValidateRuntime(
        OperationsNotificationsProductionAdmissionOptions options,
        List<string> failures)
    {
        bool currentRoleOwnsRetention =
            RoleOwnsRetention(registration.HostRole, options.RetentionOwner);
        if (runtime.RetentionEnabled != currentRoleOwnsRetention)
        {
            failures.Add(
                currentRoleOwnsRetention
                    ? $"Notifications:Retention:Enabled must be true on the selected {registration.HostRole} owner."
                    : $"Notifications:Retention:Enabled must be false on the non-owner {registration.HostRole} process.");
        }

        CompareWindow(
            options.ReadHistoryDays,
            runtime.ReadHistoryDays,
            "Notifications:Retention:ReadHistoryDays",
            failures);
        CompareWindow(
            options.UnreadHistoryDays,
            runtime.UnreadHistoryDays,
            "Notifications:Retention:UnreadHistoryDays",
            failures);
        CompareWindow(
            options.BroadcastDays,
            runtime.BroadcastDays,
            "Notifications:Retention:BroadcastDays",
            failures);
        CompareWindow(
            options.DeliveryAttemptDays,
            runtime.DeliveryAttemptDays,
            "Notifications:Delivery:AttemptRetentionDays",
            failures);
    }

    private static bool RoleOwnsRetention(
        OperationsNotificationsProductionHostRole role,
        OperationsNotificationsRetentionOwner owner) =>
        (role, owner) switch
        {
            (
                OperationsNotificationsProductionHostRole.PublicApi,
                OperationsNotificationsRetentionOwner.PublicApi) => true,
            (
                OperationsNotificationsProductionHostRole.Worker,
                OperationsNotificationsRetentionOwner.Worker) => true,
            _ => false
        };

    private static void ValidateWindow(
        int value,
        string name,
        List<string> failures)
    {
        if (value is < 1 or > MaximumRetentionDays)
        {
            failures.Add(
                $"{OperationsNotificationsProductionAdmissionOptions.SectionName}:{name} must be between 1 and {MaximumRetentionDays}.");
        }
    }

    private static void CompareWindow(
        int approved,
        int configured,
        string configurationKey,
        List<string> failures)
    {
        if (approved != configured)
        {
            failures.Add(
                $"{configurationKey} must equal the approved value {approved}.");
        }
    }

    private static bool IsApprovalReference(string? value)
    {
        if (value is not { Length: >= 3 and <= 128 })
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) &&
                character is not ('.' or '_' or ':' or '/' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSha256(string? value)
    {
        if (value is not { Length: 64 })
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!char.IsAsciiDigit(character) &&
                character is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed record OperationsNotificationsProductionAdmissionRegistration(
    bool IsProduction,
    OperationsNotificationsProductionHostRole HostRole,
    bool NotificationsComposed,
    bool PublicApiSingleReplica);

internal sealed record OperationsNotificationsRetentionRuntimeOptions(
    bool RetentionEnabled,
    int ReadHistoryDays,
    int UnreadHistoryDays,
    int BroadcastDays,
    int DeliveryAttemptDays);
