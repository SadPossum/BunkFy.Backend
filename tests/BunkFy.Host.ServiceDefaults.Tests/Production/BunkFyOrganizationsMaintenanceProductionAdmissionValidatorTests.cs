namespace BunkFy.Host.ServiceDefaults.Tests.Production;

using BunkFy.Host.ServiceDefaults.Production;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class BunkFyOrganizationsMaintenanceProductionAdmissionValidatorTests
{
    [Fact]
    public void Production_rejects_an_unapproved_ownerless_policy()
    {
        string[] failures = Validate(
            new BunkFyOrganizationsMaintenanceProductionAdmissionOptions(),
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
                organizationsComposed: true),
            CreateRuntime(enabled: true));

        Assert.Empty(failures);
    }

    [Fact]
    public void Owner_must_be_a_worker_that_composes_organizations()
    {
        string[] failures = Validate(
            CreateApprovedOptions(ownsMaintenance: true),
            CreateRegistration(BunkFyDeploymentSurface.AdminApi),
            CreateRuntime(enabled: true));

        Assert.Contains(failures, failure => failure.Contains("Only a BunkFy Worker", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("must compose Organizations persistence", StringComparison.Ordinal));
    }

    [Fact]
    public void Lifecycle_and_retention_must_match_current_process_ownership()
    {
        BunkFyOrganizationsMaintenanceRuntimeOptions partialRuntime =
            CreateRuntime(enabled: true) with
            {
                RetentionEnabled = false
            };
        string[] ownerFailures = Validate(
            CreateApprovedOptions(ownsMaintenance: true),
            CreateRegistration(
                BunkFyDeploymentSurface.Worker,
                organizationsComposed: true),
            partialRuntime);
        string[] nonOwnerFailures = Validate(
            CreateApprovedOptions(ownsMaintenance: false),
            CreateRegistration(
                BunkFyDeploymentSurface.Worker,
                organizationsComposed: true),
            CreateRuntime(enabled: true));

        Assert.Contains(ownerFailures, failure => failure.Contains("Organizations:Retention:Enabled must be true", StringComparison.Ordinal));
        Assert.Contains(nonOwnerFailures, failure => failure.Contains("Organizations:Lifecycle:Enabled must be false", StringComparison.Ordinal));
        Assert.Contains(nonOwnerFailures, failure => failure.Contains("Organizations:Retention:Enabled must be false", StringComparison.Ordinal));
    }

    [Fact]
    public void Approved_windows_must_be_bounded_and_match_runtime()
    {
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options =
            CreateApprovedOptions(ownsMaintenance: true);
        options.InvitationHistoryDays = 0;
        options.EnrollmentHistoryDays = 91;

        string[] failures = Validate(
            options,
            CreateRegistration(
                BunkFyDeploymentSurface.Worker,
                organizationsComposed: true),
            CreateRuntime(enabled: true));

        Assert.Contains(failures, failure => failure.Contains("InvitationHistoryDays must be between", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("InvitationHistoryDays must equal", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("EnrollmentHistoryDays must equal", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_unacknowledged_existing_history_cleanup()
    {
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options =
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
            new BunkFyOrganizationsMaintenanceProductionAdmissionOptions(),
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
                builder.AddBunkFyOrganizationsMaintenanceProductionAdmission(
                    BunkFyDeploymentSurface.PublicApi,
                    organizationsComposed: true));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("ApprovalState", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_composition_binds_an_approved_non_owner_policy()
    {
        HostApplicationBuilder builder = CreateProductionBuilder();
        ConfigureApprovedAdmission(builder, ownsMaintenance: false);

        builder.AddBunkFyOrganizationsMaintenanceProductionAdmission(
            BunkFyDeploymentSurface.PublicApi,
            organizationsComposed: true);

        using IHost host = builder.Build();
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options =
            host.Services
                .GetRequiredService<IOptions<
                    BunkFyOrganizationsMaintenanceProductionAdmissionOptions>>()
                .Value;
        Assert.Equal(
            BunkFyOrganizationsMaintenanceApprovalState.Approved,
            options.ApprovalState);
        Assert.False(options.CurrentProcessOwnsMaintenance);
    }

    private static string[] Validate(
        BunkFyOrganizationsMaintenanceProductionAdmissionOptions options,
        BunkFyOrganizationsMaintenanceProductionAdmissionRegistration registration,
        BunkFyOrganizationsMaintenanceRuntimeOptions runtime)
    {
        ValidateOptionsResult result =
            new BunkFyOrganizationsMaintenanceProductionAdmissionValidator(
                registration,
                runtime)
                .Validate(name: null, options);
        return result.Failed
            ? result.Failures.ToArray()
            : [];
    }

    private static BunkFyOrganizationsMaintenanceProductionAdmissionOptions
        CreateApprovedOptions(bool ownsMaintenance) =>
        new()
        {
            ApprovalState =
                BunkFyOrganizationsMaintenanceApprovalState.Approved,
            ApprovalReference =
                "ops/organizations-maintenance-approval-2026-08",
            MaintenanceOwner = BunkFyOrganizationsMaintenanceOwner.Worker,
            MaintenanceOwnerInstanceCount = 1,
            CurrentProcessOwnsMaintenance = ownsMaintenance,
            ExistingHistoryDisposition =
                BunkFyExistingHistoryDisposition.ApplyApprovedWindows
        };

    private static BunkFyOrganizationsMaintenanceProductionAdmissionRegistration
        CreateRegistration(
            BunkFyDeploymentSurface surface,
            bool isProduction = true,
            bool organizationsComposed = false) =>
        new(isProduction, surface, organizationsComposed);

    private static BunkFyOrganizationsMaintenanceRuntimeOptions CreateRuntime(
        bool enabled) =>
        new(
            LifecycleEnabled: enabled,
            RetentionEnabled: enabled,
            InvitationHistoryDays: 90,
            EnrollmentHistoryDays: 90);

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
            BunkFyOrganizationsMaintenanceProductionAdmissionOptions.SectionName;
        builder.Configuration[$"{section}:ApprovalState"] = "Approved";
        builder.Configuration[$"{section}:ApprovalReference"] =
            "ops/organizations-maintenance-approval-2026-08";
        builder.Configuration[$"{section}:MaintenanceOwner"] = "Worker";
        builder.Configuration[$"{section}:MaintenanceOwnerInstanceCount"] = "1";
        builder.Configuration[$"{section}:CurrentProcessOwnsMaintenance"] =
            ownsMaintenance.ToString();
        builder.Configuration[$"{section}:ExistingHistoryDisposition"] =
            "ApplyApprovedWindows";
    }
}
