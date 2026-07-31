namespace BunkFy.Extensions.Operations.Notifications.Tests;

using System.Security.Cryptography;
using BunkFy.DataGovernance;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OperationsNotificationsProductionAdmissionTests
{
    [Fact]
    public void Embedded_catalogue_digest_matches_the_exact_resource()
    {
        byte[] content = File.ReadAllBytes(Path.Combine(
            AppContext.BaseDirectory,
            "DataGovernance",
            "personal-data-catalog.v1.json"));

        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(content)),
            OperationsNotificationsPersonalDataCatalog.Current
                .ContentSha256);
    }

    [Fact]
    public void Repository_catalogue_blocks_production_admission()
    {
        ValidateOptionsResult result = Validate(
            ValidOptions(
                OperationsNotificationsPersonalDataCatalog.Current),
            catalogOverride:
                OperationsNotificationsPersonalDataCatalog.Current);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "catalogue is not approved",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Non_production_keeps_the_pending_defaults_inert()
    {
        ValidateOptionsResult result = Validate(
            new OperationsNotificationsProductionAdmissionOptions(),
            isProduction: false);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Production_registration_fails_closed_with_repository_defaults()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Production
            });

        OptionsValidationException exception =
            Assert.Throws<OptionsValidationException>(() =>
                builder
                    .AddBunkFyOperationsNotificationsProductionAdmission(
                        OperationsNotificationsProductionHostRole.Worker));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(
                "ApprovalState",
                StringComparison.Ordinal));
        Assert.Contains(
            exception.Failures,
            failure => failure.Contains(
                "catalogue is not approved",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Development_registration_is_inert_and_cannot_be_duplicated()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development
            });

        builder.AddBunkFyOperationsNotificationsProductionAdmission(
            OperationsNotificationsProductionHostRole.Worker);

        Assert.DoesNotContain(
            builder.Services,
            descriptor =>
                descriptor.ImplementationType ==
                typeof(
                    OperationsNotificationsProductionAdmissionReporter));
        Assert.Throws<InvalidOperationException>(() =>
            builder.AddBunkFyOperationsNotificationsProductionAdmission(
                OperationsNotificationsProductionHostRole.Worker));
    }

    [Fact]
    public void Approved_worker_owner_accepts_exact_policy_and_runtime()
    {
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            SyntheticApprovedCatalog();
        ValidateOptionsResult result = Validate(
            ValidOptions(catalog),
            catalogOverride: catalog);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Evidence_must_match_the_exact_catalogue()
    {
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            SyntheticApprovedCatalog();
        OperationsNotificationsProductionAdmissionOptions options =
            ValidOptions(catalog);
        options.CatalogVersion++;
        options.CatalogSha256 = new string('b', 64);

        ValidateOptionsResult result = Validate(
            options,
            catalogOverride: catalog);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "CatalogVersion",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "CatalogSha256",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Approved_windows_must_match_the_effective_gma_runtime()
    {
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            SyntheticApprovedCatalog();
        OperationsNotificationsProductionAdmissionOptions options =
            ValidOptions(catalog);
        options.ReadHistoryDays = 91;
        options.UnreadHistoryDays = 90;

        ValidateOptionsResult result = Validate(
            options,
            catalogOverride: catalog);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "UnreadHistoryDays must be greater",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "Notifications:Retention:ReadHistoryDays",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "Notifications:Retention:UnreadHistoryDays",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Selected_worker_must_compose_notifications()
    {
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            SyntheticApprovedCatalog();

        ValidateOptionsResult result = Validate(
            ValidOptions(catalog),
            notificationsComposed: false,
            catalogOverride: catalog);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "must compose the Notifications module",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Public_api_owner_requires_single_replica_and_enabled_cleanup()
    {
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            SyntheticApprovedCatalog();
        OperationsNotificationsProductionAdmissionOptions options =
            ValidOptions(catalog);
        options.RetentionOwner =
            OperationsNotificationsRetentionOwner.PublicApi;

        ValidateOptionsResult result = Validate(
            options,
            hostRole:
                OperationsNotificationsProductionHostRole.PublicApi,
            retentionEnabled: false,
            publicApiSingleReplica: false,
            catalogOverride: catalog);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "ApiTopology=SingleReplica",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "must be true",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Non_owner_process_rejects_enabled_cleanup()
    {
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            SyntheticApprovedCatalog();
        OperationsNotificationsProductionAdmissionOptions options =
            ValidOptions(catalog);

        ValidateOptionsResult result = Validate(
            options,
            hostRole:
                OperationsNotificationsProductionHostRole.AdminApi,
            retentionEnabled: true,
            catalogOverride: catalog);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "non-owner AdminApi",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Approval_legacy_disposition_and_owner_count_are_explicit()
    {
        OperationsNotificationsPersonalDataCatalogEvidence catalog =
            SyntheticApprovedCatalog();
        OperationsNotificationsProductionAdmissionOptions options =
            ValidOptions(catalog);
        options.ApprovalReference = "contains whitespace";
        options.LegacyHistoryDisposition =
            OperationsNotificationsLegacyHistoryDisposition.Unspecified;
        options.RetentionOwnerInstanceCount = 2;

        ValidateOptionsResult result = Validate(
            options,
            catalogOverride: catalog);

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "ApprovalReference",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "LegacyHistoryDisposition",
                StringComparison.Ordinal));
        Assert.Contains(
            result.Failures,
            failure => failure.Contains(
                "RetentionOwnerInstanceCount",
                StringComparison.Ordinal));
    }

    private static ValidateOptionsResult Validate(
        OperationsNotificationsProductionAdmissionOptions options,
        bool isProduction = true,
        OperationsNotificationsProductionHostRole hostRole =
            OperationsNotificationsProductionHostRole.Worker,
        bool notificationsComposed = true,
        bool retentionEnabled = true,
        bool publicApiSingleReplica = false,
        OperationsNotificationsPersonalDataCatalogEvidence?
            catalogOverride = null) =>
        new OperationsNotificationsProductionAdmissionValidator(
                new OperationsNotificationsProductionAdmissionRegistration(
                    isProduction,
                    hostRole,
                    notificationsComposed,
                    publicApiSingleReplica),
                catalogOverride ?? SyntheticApprovedCatalog(),
                new OperationsNotificationsRetentionRuntimeOptions(
                    retentionEnabled,
                    ReadHistoryDays: 90,
                    UnreadHistoryDays: 365,
                    BroadcastDays: 365,
                    DeliveryAttemptDays: 90))
            .Validate(name: null, options);

    private static OperationsNotificationsProductionAdmissionOptions
        ValidOptions(
            OperationsNotificationsPersonalDataCatalogEvidence catalog) =>
        new()
        {
            ApprovalState =
                OperationsNotificationsApprovalState.Approved,
            ApprovalReference = "privacy-board:notification-policy-v1",
            CatalogVersion = catalog.Document.CatalogVersion,
            CatalogSha256 = catalog.ContentSha256,
            ReadHistoryDays = 90,
            UnreadHistoryDays = 365,
            BroadcastDays = 365,
            DeliveryAttemptDays = 90,
            LegacyHistoryDisposition =
                OperationsNotificationsLegacyHistoryDisposition
                    .ResetBeforeAdmission,
            RetentionOwner =
                OperationsNotificationsRetentionOwner.Worker,
            RetentionOwnerInstanceCount = 1
        };

    private static OperationsNotificationsPersonalDataCatalogEvidence
        SyntheticApprovedCatalog()
    {
        PersonalDataCatalogDocument source =
            OperationsNotificationsPersonalDataCatalog.Current.Document;
        PersonalDataCatalogDocument approved = source with
        {
            ApprovalState =
                PersonalDataPolicyApprovalState.Approved,
            RetentionPolicies =
            [
                .. source.RetentionPolicies.Select(policy =>
                    policy with
                    {
                        ApprovalState =
                            PersonalDataPolicyApprovalState.Approved
                    })
            ],
            Fields =
            [
                .. source.Fields.Select(field =>
                    field with
                    {
                        ApprovalState =
                            PersonalDataPolicyApprovalState.Approved
                    })
            ]
        };

        return new OperationsNotificationsPersonalDataCatalogEvidence(
            approved,
            new string('a', 64));
    }
}
