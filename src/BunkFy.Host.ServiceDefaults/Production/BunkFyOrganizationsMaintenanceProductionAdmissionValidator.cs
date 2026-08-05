namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Options;

internal sealed class BunkFyOrganizationsMaintenanceProductionAdmissionValidator(
    BunkFyOrganizationsMaintenanceProductionAdmissionRegistration registration,
    BunkFyOrganizationsMaintenanceRuntimeOptions runtime)
    : IValidateOptions<BunkFyOrganizationsMaintenanceProductionAdmissionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!registration.IsProduction)
        {
            return ValidateOptionsResult.Success;
        }

        List<string> failures = [];
        ValidateApproval(options, failures);
        ValidateOwner(options, failures);
        this.ValidateRuntime(options, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateApproval(
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.ApprovalState !=
            BunkFyOrganizationsMaintenanceApprovalState.Approved)
        {
            failures.Add(
                $"{BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName}:ApprovalState must be Approved in Production.");
        }

        if (!BunkFyProductionAdmissionReference.IsValid(options.ApprovalReference))
        {
            failures.Add(
                $"{BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName}:ApprovalReference must be a 3-128 character non-secret evidence identifier.");
        }

        if (options.ExistingHistoryDisposition !=
            BunkFyExistingHistoryDisposition.ApplyApprovedWindows)
        {
            failures.Add(
                $"{BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName}:ExistingHistoryDisposition must be ApplyApprovedWindows in Production.");
        }

        ValidateDays(options.InvitationHistoryDays, nameof(options.InvitationHistoryDays), failures);
        ValidateDays(options.EnrollmentHistoryDays, nameof(options.EnrollmentHistoryDays), failures);
    }

    private static void ValidateOwner(
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.MaintenanceOwner != BunkFyOrganizationsMaintenanceOwner.Worker)
        {
            failures.Add(
                $"{BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName}:MaintenanceOwner must be Worker.");
        }

        if (options.MaintenanceOwnerInstanceCount != 1)
        {
            failures.Add(
                $"{BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName}:MaintenanceOwnerInstanceCount must be exactly 1.");
        }
    }

    private void ValidateRuntime(
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options,
        List<string> failures)
    {
        bool ownsMaintenance = options.CurrentProcessOwnsMaintenance;
        if (ownsMaintenance && registration.HostSurface != BunkFyDeploymentSurface.Worker)
        {
            failures.Add(
                "Only a BunkFy Worker process may own Organizations maintenance.");
        }

        if (ownsMaintenance && !registration.OrganizationsComposed)
        {
            failures.Add(
                "The Organizations maintenance owner must compose Organizations persistence.");
        }

        CompareEnabled(
            ownsMaintenance,
            runtime.LifecycleEnabled,
            "Organizations:Lifecycle:Enabled",
            failures);
        CompareEnabled(
            ownsMaintenance,
            runtime.RetentionEnabled,
            "Organizations:Retention:Enabled",
            failures);
        Compare(
            options.InvitationHistoryDays,
            runtime.InvitationHistoryDays,
            "Organizations:Retention:InvitationHistoryDays",
            failures);
        Compare(
            options.EnrollmentHistoryDays,
            runtime.EnrollmentHistoryDays,
            "Organizations:Retention:EnrollmentHistoryDays",
            failures);
    }

    private static void CompareEnabled(
        bool expected,
        bool actual,
        string configurationKey,
        List<string> failures)
    {
        if (expected != actual)
        {
            failures.Add(
                $"{configurationKey} must be {expected.ToString().ToLowerInvariant()} for this process role.");
        }
    }

    private static void Compare(
        int approved,
        int configured,
        string configurationKey,
        List<string> failures)
    {
        if (approved != configured)
        {
            failures.Add($"{configurationKey} must equal the approved value {approved}.");
        }
    }

    private static void ValidateDays(
        int value,
        string name,
        List<string> failures)
    {
        if (value is < 1 or > 3_650)
        {
            failures.Add(
                $"{BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName}:{name} must be between 1 and 3650.");
        }
    }

}

internal sealed record BunkFyOrganizationsMaintenanceProductionAdmissionRegistration(
    bool IsProduction,
    BunkFyDeploymentSurface HostSurface,
    bool OrganizationsComposed);

internal sealed record BunkFyOrganizationsMaintenanceRuntimeOptions(
    bool LifecycleEnabled,
    bool RetentionEnabled,
    int InvitationHistoryDays,
    int EnrollmentHistoryDays);
