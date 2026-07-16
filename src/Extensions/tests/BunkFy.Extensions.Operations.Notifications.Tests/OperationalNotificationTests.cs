namespace BunkFy.Extensions.Operations.Notifications.Tests;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Notifications;
using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OperationalNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registration_preserves_tenant_scope_metadata_for_every_operational_handler()
    {
        ServiceCollection services = [];

        services.AddBunkFyOperationsNotifications();

        IntegrationEventSubscription[] subscriptions = services
            .Where(descriptor => descriptor.ServiceType == typeof(IntegrationEventSubscription))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<IntegrationEventSubscription>()
            .ToArray();
        Assert.Equal(12, subscriptions.Length);
        Assert.All(subscriptions, subscription => Assert.True(subscription.IsTenantScoped()));
    }

    [Fact]
    public async Task Property_event_fans_out_to_active_staff_and_workspace_owners_with_stable_distinct_ids()
    {
        var audience = new TestAudienceReader(["user-a", "user-b"]);
        var workspaceOwners = new TestWorkspaceOwnerAudienceReader(["owner-a", "user-b"]);
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(audience, workspaceOwners, notifications);
        var handler = new ReservationCancelledNotificationHandler(projector);
        Guid sourceEventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var integrationEvent = new ReservationCancelledIntegrationEvent(
            sourceEventId,
            "tenant-a",
            Now,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            3);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Equal(3, notifications.Events.Count);
        Assert.Equal(["owner-a", "user-a", "user-b"], notifications.Events.Select(item => item.UserId).ToArray());
        Assert.Equal(3, notifications.Events.Select(item => item.EventId).Distinct().Count());
        Assert.All(notifications.Events, item =>
        {
            Assert.Equal(
                Gma.Modules.Notifications.Contracts.NotificationDeliveryPolicy.RespectPreferences,
                item.DeliveryPolicy);
            Assert.Contains(item.Tags, tag => tag.Key == NotificationTags.Web);
            Assert.Contains(item.Tags, tag => tag.Key == "domain:reservations");
        });
        Assert.Equal(
            notifications.Events[0].EventId,
            OperationalNotificationProjector.CreateNotificationId(
                sourceEventId,
                "owner-a",
                "reservation-cancelled"));
    }

    [Fact]
    public async Task Successful_provider_operation_does_not_create_an_attention_notification()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
            new TestWorkspaceOwnerAudienceReader(["owner-a"]),
            notifications);
        var handler = new ExternalReservationOperationAttentionNotificationHandler(projector);
        var integrationEvent = new ExternalReservationOperationCompletedIntegrationEvent(
            Guid.NewGuid(),
            "tenant-a",
            Now,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ExternalReservationOperationKind.Amend,
            ExternalReservationOperationOutcome.Applied,
            Guid.NewGuid(),
            2,
            3,
            null);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Empty(notifications.Events);
    }

    [Fact]
    public async Task Arrival_reminder_names_the_guest_and_keeps_exact_reservation_navigation_payload()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
            new TestWorkspaceOwnerAudienceReader([]),
            notifications);
        var handler = new ReservationArrivalReminderNotificationHandler(projector);
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        var integrationEvent = new ReservationArrivalReminderDueIntegrationEvent(
            Guid.NewGuid(),
            "tenant-a",
            Now,
            reservationId,
            propertyId,
            "Maya Chen",
            new DateOnly(2026, 7, 16),
            new TimeOnly(15, 30),
            "Europe/Moscow",
            3);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        UserNotificationRequestedIntegrationEventV2 notification = Assert.Single(notifications.Events);
        Assert.Equal("reservation-arrival-soon", notification.NotificationName);
        Assert.Contains("Maya Chen", notification.Body, StringComparison.Ordinal);
        Assert.Contains("15:30", notification.Body, StringComparison.Ordinal);
        Assert.Contains(reservationId.ToString(), notification.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(propertyId.ToString(), notification.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_event_excludes_the_initiating_user_without_suppressing_other_recipients()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a", "user-b"]),
            new TestWorkspaceOwnerAudienceReader(["owner-a"]),
            notifications);
        var handler = new ReservationCancelledNotificationHandler(projector);
        var integrationEvent = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(),
            "tenant-a",
            Now,
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            "user:user-a");

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Equal(["owner-a", "user-b"], notifications.Events.Select(item => item.UserId).ToArray());
    }

    [Theory]
    [InlineData("service:user-a")]
    [InlineData("system:user-a")]
    [InlineData("admin-actor:user-a")]
    [InlineData(null)]
    public void Non_user_actors_are_not_treated_as_inbox_recipients(string? actorId)
    {
        Assert.False(OperationalNotificationProjector.IsInitiatingUser("user-a", actorId));
    }

    [Fact]
    public async Task Staff_event_is_quiet_when_the_profile_has_no_auth_subject()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader([], staffAuthSubjectId: null),
            new TestWorkspaceOwnerAudienceReader(["owner-a"]),
            notifications);
        var handler = new StaffMemberLifecycleChangedNotificationHandler(projector);
        var integrationEvent = new StaffMemberLifecycleChangedIntegrationEvent(
            Guid.NewGuid(),
            "tenant-a",
            Now,
            Guid.NewGuid(),
            StaffStatus.Suspended,
            new DateOnly(2026, 7, 13),
            2);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Empty(notifications.Events);
    }

    private sealed class TestAudienceReader(
        IReadOnlyList<string> propertyRecipients,
        string? staffAuthSubjectId = "user-a") : IStaffPropertyAudienceReader
    {
        public Task<IReadOnlyList<string>> ListActiveAuthSubjectIdsAsync(
            string scopeId,
            Guid propertyId,
            CancellationToken cancellationToken) =>
            Task.FromResult(propertyRecipients);

        public Task<string?> GetAuthSubjectIdAsync(
            string scopeId,
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult(staffAuthSubjectId);
    }

    private sealed class TestWorkspaceOwnerAudienceReader(IReadOnlyList<string> recipients)
        : IWorkspaceOwnerNotificationAudienceReader
    {
        public Task<IReadOnlyList<string>> ListAuthSubjectIdsAsync(
            string scopeId,
            CancellationToken cancellationToken) =>
            Task.FromResult(recipients);
    }

    private sealed class CapturingProjector : IUserNotificationRequestProjector
    {
        public List<UserNotificationRequestedIntegrationEventV2> Events { get; } = [];

        public Task ProjectAsync(
            UserNotificationRequestedIntegrationEventV2 integrationEvent,
            CancellationToken cancellationToken)
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
