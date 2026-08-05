namespace BunkFy.Host.ServiceDefaults.Tests.Production;

using BunkFy.Host.ServiceDefaults.Production;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class BunkFyAuthRetentionProductionAdmissionValidatorTests
{
    [Fact]
    public void Production_rejects_an_unapproved_ownerless_policy()
    {
        string[] failures = Validate(
            new BunkFyAuthRetentionProductionAdmissionOptions(),
            CreateRegistration(BunkFyDeploymentSurface.PublicApi),
            CreateRuntime(enabled: false));

        Assert.Contains(failures, failure => failure.Contains("ApprovalState", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ApprovalReference", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ExistingHistoryDisposition", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("MaintenanceOwner", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("MaintenanceOwnerInstanceCount", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_accepts_an_approved_non_owner_api()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: false),
            CreateRegistration(BunkFyDeploymentSurface.PublicApi),
            CreateRuntime(enabled: false));

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_accepts_one_exact_worker_owner()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: true),
            CreateRegistration(
                BunkFyDeploymentSurface.Worker,
                authComposed: true),
            CreateRuntime(enabled: true));

        Assert.Empty(failures);
    }

    [Fact]
    public void Owner_must_be_a_worker_that_composes_auth()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: true),
            CreateRegistration(BunkFyDeploymentSurface.AdminApi),
            CreateRuntime(enabled: true));

        Assert.Contains(failures, failure => failure.Contains("Only a BunkFy Worker", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("must compose Auth persistence", StringComparison.Ordinal));
    }

    [Fact]
    public void Enabled_runtime_must_match_current_process_ownership()
    {
        string[] nonOwnerFailures = Validate(
            CreateApprovedOptions(ownsMaintenance: false),
            CreateRegistration(BunkFyDeploymentSurface.Worker, authComposed: true),
            CreateRuntime(enabled: true));
        string[] ownerFailures = Validate(
            CreateApprovedOptions(ownsMaintenance: true),
            CreateRegistration(BunkFyDeploymentSurface.Worker, authComposed: true),
            CreateRuntime(enabled: false));

        Assert.Contains(nonOwnerFailures, failure => failure.Contains("must be false", StringComparison.Ordinal));
        Assert.Contains(ownerFailures, failure => failure.Contains("must be true", StringComparison.Ordinal));
    }

    [Fact]
    public void Approved_windows_must_be_bounded_and_match_runtime()
    {
        BunkFyAuthRetentionProductionAdmissionOptions options =
            CreateApprovedOptions(ownsMaintenance: true);
        options.SessionHistoryDays = 0;
        options.AuthenticationFailureHistoryHours = 25;

        string[] failures = Validate(
            options,
            CreateRegistration(BunkFyDeploymentSurface.Worker, authComposed: true),
            CreateRuntime(enabled: true));

        Assert.Contains(failures, failure => failure.Contains("SessionHistoryDays must be between", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("SessionHistoryDays must equal", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("AuthenticationFailureHistoryHours must equal", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_unacknowledged_existing_history_cleanup()
    {
        BunkFyAuthRetentionProductionAdmissionOptions options =
            CreateApprovedOptions(ownsMaintenance: false);
        options.ExistingHistoryDisposition =
            BunkFyExistingHistoryDisposition.Unspecified;

        string[] failures = Validate(
            options,
            CreateRegistration(BunkFyDeploymentSurface.PublicApi),
            CreateRuntime(enabled: false));

        Assert.Contains(failures, failure => failure.Contains("ApplyApprovedWindows", StringComparison.Ordinal));
    }

    [Fact]
    public void Non_production_does_not_require_approval()
    {
        string[] failures = Validate(
            new BunkFyAuthRetentionProductionAdmissionOptions(),
            CreateRegistration(
                BunkFyDeploymentSurface.Worker,
                isProduction: false),
            CreateRuntime(enabled: true));

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_composition_rejects_pending_admission()
    {
        HostApplicationBuilder builder = CreateProductionBuilder();

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(() =>
                builder.AddBunkFyAuthRetentionProductionAdmission(
                    BunkFyDeploymentSurface.PublicApi,
                    authComposed: true));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("ApprovalState", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_composition_binds_an_approved_non_owner_policy()
    {
        HostApplicationBuilder builder = CreateProductionBuilder();
        ConfigureApprovedAdmission(builder, ownsMaintenance: false);

        builder.AddBunkFyAuthRetentionProductionAdmission(
            BunkFyDeploymentSurface.PublicApi,
            authComposed: true);

        using IHost host = builder.Build();
        BunkFyAuthRetentionProductionAdmissionOptions options = host.Services
            .GetRequiredService<IOptions<
                BunkFyAuthRetentionProductionAdmissionOptions>>()
            .Value;
        Assert.Equal(BunkFyAuthRetentionApprovalState.Approved, options.ApprovalState);
        Assert.False(options.CurrentProcessOwnsMaintenance);
    }

    private static string[] Validate(
        BunkFyAuthRetentionProductionAdmissionOptions options,
        BunkFyAuthRetentionProductionAdmissionRegistration registration,
        BunkFyAuthRetentionRuntimeOptions runtime)
    {
        ValidateOptionsResult result =
            new BunkFyAuthRetentionProductionAdmissionValidator(
                registration,
                runtime)
                .Validate(name: null, options);
        return result.Failed
            ? result.Failures.ToArray()
            : [];
    }

    private static BunkFyAuthRetentionProductionAdmissionOptions
        CreateApprovedOptions(bool ownsMaintenance) =>
        new()
        {
            ApprovalState = BunkFyAuthRetentionApprovalState.Approved,
            ApprovalReference = "ops/auth-retention-approval-2026-08",
            MaintenanceOwner = BunkFyAuthRetentionMaintenanceOwner.Worker,
            MaintenanceOwnerInstanceCount = 1,
            CurrentProcessOwnsMaintenance = ownsMaintenance,
            ExistingHistoryDisposition =
                BunkFyExistingHistoryDisposition.ApplyApprovedWindows
        };

    private static BunkFyAuthRetentionProductionAdmissionRegistration
        CreateRegistration(
            BunkFyDeploymentSurface surface,
            bool isProduction = true,
            bool authComposed = false) =>
        new(isProduction, surface, authComposed);

    private static BunkFyAuthRetentionRuntimeOptions CreateRuntime(bool enabled) =>
        new(
            enabled,
            ExpiredExchangeHistoryHours: 24,
            PasswordRecoveryHistoryHours: 24,
            SessionHistoryDays: 365,
            AuthenticationChallengeHistoryHours: 24,
            ExpiredTotpEnrollmentHistoryHours: 24,
            DisabledTotpAuthenticatorHistoryDays: 365,
            MultiFactorFailureHistoryHours: 24,
            AuthenticationFailureHistoryHours: 24);

    private static HostApplicationBuilder CreateProductionBuilder() =>
        Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Production
            });

    private static void ConfigureApprovedAdmission(
        HostApplicationBuilder builder,
        bool ownsMaintenance)
    {
        string section =
            BunkFyAuthRetentionProductionAdmissionOptions.SectionName;
        builder.Configuration[$"{section}:ApprovalState"] = "Approved";
        builder.Configuration[$"{section}:ApprovalReference"] =
            "ops/auth-retention-approval-2026-08";
        builder.Configuration[$"{section}:MaintenanceOwner"] = "Worker";
        builder.Configuration[$"{section}:MaintenanceOwnerInstanceCount"] = "1";
        builder.Configuration[$"{section}:CurrentProcessOwnsMaintenance"] =
            ownsMaintenance.ToString();
        builder.Configuration[$"{section}:ExistingHistoryDisposition"] =
            "ApplyApprovedWindows";
    }
}
