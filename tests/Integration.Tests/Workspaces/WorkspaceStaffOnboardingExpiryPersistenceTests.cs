namespace Integration.Tests;

using BunkFy.Host.Worker;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Messaging;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Gma.Modules.Organizations.Contracts;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class WorkspaceStaffOnboardingExpiryPersistenceTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Pending_claim_survives_link_expiry_then_claim_expiry_is_persisted_and_redacted()
    {
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_workspace_expiry_tests")
            .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);
        using IHost worker = CreateWorker(postgreSql.GetConnectionString());
        Guid organizationId = Guid.NewGuid();
        string scopeId = organizationId.ToString("D");
        Guid linkId = Guid.NewGuid();
        Guid claimId = Guid.NewGuid();
        Guid applicationId = Guid.NewGuid();
        DateTimeOffset nowUtc = new(2026, 7, 28, 10, 0, 0, TimeSpan.Zero);

        using IServiceScope scope = worker.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(scopeId);
        WorkspacesDbContext dbContext = scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        WorkspaceStaffOnboarding application = WorkspaceStaffOnboarding.Create(
            applicationId,
            scopeId,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            linkId,
            Guid.NewGuid().ToString("D"),
            "applicant@example.test",
            "Expiry Applicant",
            "Expiry Applicant",
            "staff@example.test",
            "+1 555 0199",
            "EMP-EXPIRY",
            "Receptionist",
            "Front desk",
            nowUtc).Value;
        Assert.True(application.ObserveClaimRequested(claimId, 1, nowUtc.AddMinutes(1)).IsSuccess);
        WorkspaceStaffAccessPlan plan = WorkspaceStaffAccessPlan.Create(
            linkId,
            scopeId,
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            Guid.NewGuid(),
            "front-desk",
            [],
            Guid.NewGuid().ToString("D"),
            nowUtc).Value;
        Assert.True(plan.Activate(nowUtc.AddMinutes(1)).IsSuccess);
        dbContext.StaffOnboardingApplications.Add(application);
        dbContext.StaffAccessPlans.Add(plan);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        IIntegrationEventSubscriptionRegistry subscriptions =
            scope.ServiceProvider.GetRequiredService<IIntegrationEventSubscriptionRegistry>();
        var linkHandler = (IIntegrationEventHandler<OrganizationEnrollmentLinkExpiredIntegrationEvent>)
            scope.ServiceProvider.GetRequiredService(subscriptions.Subscriptions.Single(subscription =>
                subscription.ConsumerModule == WorkspacesModuleMetadata.Name &&
                subscription.HandlerName == WorkspacesModuleMetadata.EnrollmentLinkExpiredHandlerName).HandlerType);
        await linkHandler.HandleAsync(
            new OrganizationEnrollmentLinkExpiredIntegrationEvent(
                Guid.NewGuid(),
                nowUtc.AddMinutes(2),
                scopeId,
                organizationId,
                linkId,
                nowUtc.AddMinutes(2),
                2),
            CancellationToken.None).ConfigureAwait(false);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        WorkspaceStaffAccessPlan sourceExpiredPlan =
            await dbContext.StaffAccessPlans.SingleAsync().ConfigureAwait(false);
        Assert.Equal(WorkspaceStaffAccessPlanState.Active, sourceExpiredPlan.Status);
        Assert.Equal(nowUtc.AddMinutes(2), sourceExpiredPlan.SourceExpiredAtUtc);
        dbContext.ChangeTracker.Clear();

        var claimHandler = (IIntegrationEventHandler<OrganizationEnrollmentClaimExpiredIntegrationEvent>)
            scope.ServiceProvider.GetRequiredService(subscriptions.Subscriptions.Single(subscription =>
                subscription.ConsumerModule == WorkspacesModuleMetadata.Name &&
                subscription.HandlerName == WorkspacesModuleMetadata.EnrollmentClaimExpiredHandlerName).HandlerType);
        await claimHandler.HandleAsync(
            new OrganizationEnrollmentClaimExpiredIntegrationEvent(
                Guid.NewGuid(),
                nowUtc.AddMinutes(3),
                scopeId,
                organizationId,
                linkId,
                claimId,
                nowUtc.AddMinutes(3),
                2),
            CancellationToken.None).ConfigureAwait(false);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
        dbContext.ChangeTracker.Clear();

        WorkspaceStaffOnboarding persisted = await dbContext.StaffOnboardingApplications
            .SingleAsync().ConfigureAwait(false);
        WorkspaceStaffAccessPlan persistedPlan = await dbContext.StaffAccessPlans
            .SingleAsync().ConfigureAwait(false);
        IWorkspaceStaffOnboardingRepository repository =
            scope.ServiceProvider.GetRequiredService<IWorkspaceStaffOnboardingRepository>();
        IReadOnlyList<WorkspaceStaffOnboarding> active = await repository.ListActiveBySourceAsync(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            linkId,
            CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(WorkspaceStaffOnboardingState.Expired, persisted.Status);
        Assert.Equal(2, persisted.ClaimVersion);
        Assert.Null(persisted.VerifiedAccountEmail);
        Assert.Null(persisted.DisplayName);
        Assert.Null(persisted.WorkEmail);
        Assert.Equal(WorkspaceStaffAccessPlanState.Expired, persistedPlan.Status);
        Assert.Empty(active);
    }

    private static IHost CreateWorker(string connectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = "Integration" });
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] = connectionString;
        builder.Configuration["Caching:Enabled"] = "false";
        builder.Configuration["NatsJetStream:Enabled"] = "false";
        builder.Configuration["NatsConsumers:Enabled"] = "false";
        builder.Configuration["Tasks:Worker:Enabled"] = "false";
        builder.Configuration["Worker:Modules:AccessControl"] = "true";
        builder.Configuration["Worker:Modules:Auth"] = "true";
        builder.Configuration["Worker:Modules:Organizations"] = "true";
        builder.Configuration["Worker:Modules:Properties"] = "true";
        builder.Configuration["Worker:Modules:Staff"] = "true";
        AuthTestConfiguration.ConfigureTokenHashing(builder.Configuration);
        builder.Logging.ClearProviders();

        builder.AddWorkerHost();
        ModuleCompositionValidationResult result = builder.ValidateModuleComposition();
        Assert.True(result.IsValid, result.Report);
        return builder.Build();
    }
}
