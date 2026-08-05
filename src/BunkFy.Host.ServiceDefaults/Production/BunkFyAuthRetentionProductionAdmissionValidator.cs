namespace BunkFy.Host.ServiceDefaults.Production;

using Microsoft.Extensions.Options;

internal sealed class BunkFyAuthRetentionProductionAdmissionValidator(
    BunkFyAuthRetentionProductionAdmissionRegistration registration,
    BunkFyAuthRetentionRuntimeOptions runtime)
    : IValidateOptions<BunkFyAuthRetentionProductionAdmissionOptions>
{
    public ValidateOptionsResult Validate(
        string? name,
        BunkFyAuthRetentionProductionAdmissionOptions options)
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
        BunkFyAuthRetentionProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.ApprovalState != BunkFyAuthRetentionApprovalState.Approved)
        {
            failures.Add(
                $"{BunkFyAuthRetentionProductionAdmissionOptions.SectionName}:ApprovalState must be Approved in Production.");
        }

        if (!BunkFyProductionAdmissionReference.IsValid(options.ApprovalReference))
        {
            failures.Add(
                $"{BunkFyAuthRetentionProductionAdmissionOptions.SectionName}:ApprovalReference must be a 3-128 character non-secret evidence identifier.");
        }

        if (options.ExistingHistoryDisposition !=
            BunkFyExistingHistoryDisposition.ApplyApprovedWindows)
        {
            failures.Add(
                $"{BunkFyAuthRetentionProductionAdmissionOptions.SectionName}:ExistingHistoryDisposition must be ApplyApprovedWindows in Production.");
        }

        ValidateHours(options.ExpiredExchangeHistoryHours, nameof(options.ExpiredExchangeHistoryHours), failures);
        ValidateHours(options.PasswordRecoveryHistoryHours, nameof(options.PasswordRecoveryHistoryHours), failures);
        ValidateDays(options.SessionHistoryDays, nameof(options.SessionHistoryDays), failures);
        ValidateHours(options.AuthenticationChallengeHistoryHours, nameof(options.AuthenticationChallengeHistoryHours), failures);
        ValidateHours(options.ExpiredTotpEnrollmentHistoryHours, nameof(options.ExpiredTotpEnrollmentHistoryHours), failures);
        ValidateDays(options.DisabledTotpAuthenticatorHistoryDays, nameof(options.DisabledTotpAuthenticatorHistoryDays), failures);
        ValidateHours(options.MultiFactorFailureHistoryHours, nameof(options.MultiFactorFailureHistoryHours), failures);
        ValidateHours(options.AuthenticationFailureHistoryHours, nameof(options.AuthenticationFailureHistoryHours), failures);
    }

    private static void ValidateOwner(
        BunkFyAuthRetentionProductionAdmissionOptions options,
        List<string> failures)
    {
        if (options.MaintenanceOwner != BunkFyAuthRetentionMaintenanceOwner.Worker)
        {
            failures.Add(
                $"{BunkFyAuthRetentionProductionAdmissionOptions.SectionName}:MaintenanceOwner must be Worker.");
        }

        if (options.MaintenanceOwnerInstanceCount != 1)
        {
            failures.Add(
                $"{BunkFyAuthRetentionProductionAdmissionOptions.SectionName}:MaintenanceOwnerInstanceCount must be exactly 1.");
        }
    }

    private void ValidateRuntime(
        BunkFyAuthRetentionProductionAdmissionOptions options,
        List<string> failures)
    {
        bool ownsMaintenance = options.CurrentProcessOwnsMaintenance;
        if (ownsMaintenance && registration.HostSurface != BunkFyDeploymentSurface.Worker)
        {
            failures.Add("Only a BunkFy Worker process may own Auth retention.");
        }

        if (ownsMaintenance && !registration.AuthComposed)
        {
            failures.Add("The Auth retention owner must compose Auth persistence.");
        }

        if (runtime.Enabled != ownsMaintenance)
        {
            failures.Add(
                $"Auth:Retention:Enabled must be {ownsMaintenance.ToString().ToLowerInvariant()} for this process role.");
        }

        Compare(options.ExpiredExchangeHistoryHours, runtime.ExpiredExchangeHistoryHours, "Auth:Retention:ExpiredExchangeHistoryHours", failures);
        Compare(options.PasswordRecoveryHistoryHours, runtime.PasswordRecoveryHistoryHours, "Auth:Retention:PasswordRecoveryHistoryHours", failures);
        Compare(options.SessionHistoryDays, runtime.SessionHistoryDays, "Auth:Retention:SessionHistoryDays", failures);
        Compare(options.AuthenticationChallengeHistoryHours, runtime.AuthenticationChallengeHistoryHours, "Auth:Retention:AuthenticationChallengeHistoryHours", failures);
        Compare(options.ExpiredTotpEnrollmentHistoryHours, runtime.ExpiredTotpEnrollmentHistoryHours, "Auth:Retention:ExpiredTotpEnrollmentHistoryHours", failures);
        Compare(options.DisabledTotpAuthenticatorHistoryDays, runtime.DisabledTotpAuthenticatorHistoryDays, "Auth:Retention:DisabledTotpAuthenticatorHistoryDays", failures);
        Compare(options.MultiFactorFailureHistoryHours, runtime.MultiFactorFailureHistoryHours, "Auth:Retention:MultiFactorFailureHistoryHours", failures);
        Compare(options.AuthenticationFailureHistoryHours, runtime.AuthenticationFailureHistoryHours, "Auth:Retention:AuthenticationFailureHistoryHours", failures);
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

    private static void ValidateHours(
        int value,
        string name,
        List<string> failures)
    {
        if (value is < 1 or > 8_760)
        {
            failures.Add(
                $"{BunkFyAuthRetentionProductionAdmissionOptions.SectionName}:{name} must be between 1 and 8760.");
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
                $"{BunkFyAuthRetentionProductionAdmissionOptions.SectionName}:{name} must be between 1 and 3650.");
        }
    }

}

internal sealed record BunkFyAuthRetentionProductionAdmissionRegistration(
    bool IsProduction,
    BunkFyDeploymentSurface HostSurface,
    bool AuthComposed);

internal sealed record BunkFyAuthRetentionRuntimeOptions(
    bool Enabled,
    int ExpiredExchangeHistoryHours,
    int PasswordRecoveryHistoryHours,
    int SessionHistoryDays,
    int AuthenticationChallengeHistoryHours,
    int ExpiredTotpEnrollmentHistoryHours,
    int DisabledTotpAuthenticatorHistoryDays,
    int MultiFactorFailureHistoryHours,
    int AuthenticationFailureHistoryHours);
