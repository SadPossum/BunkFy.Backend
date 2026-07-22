namespace BunkFy.Extensions.Operations.Notifications.Tests;

using System.Text.Json;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Notifications;
using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Gma.Modules.Organizations.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OperationalNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrganizationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly string ScopeId = OrganizationId.ToString("D");

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
        Assert.Equal(13, subscriptions.Length);
        Assert.All(subscriptions, subscription => Assert.True(subscription.IsTenantScoped()));
    }

    [Fact]
    public async Task Property_event_fans_out_to_active_staff_and_workspace_owners_with_stable_distinct_ids()
    {
        var audience = new TestAudienceReader(["user-a", "user-b"]);
        var workspaceOwners = new TestWorkspaceOwnerAudienceReader(["owner-a", "user-b"]);
        var access = new TestOrganizationAccessCandidateFilter();
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(audience, workspaceOwners, access, notifications);
        var handler = new ReservationCancelledNotificationHandler(projector);
        Guid sourceEventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var integrationEvent = new ReservationCancelledIntegrationEvent(
            sourceEventId,
            ScopeId,
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
        Assert.Equal([["owner-a", "user-a", "user-b"]], access.Requests);
    }

    [Fact]
    public async Task Successful_provider_operation_does_not_create_an_attention_notification()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
            new TestWorkspaceOwnerAudienceReader(["owner-a"]),
            new TestOrganizationAccessCandidateFilter(),
            notifications);
        var handler = new ExternalReservationOperationAttentionNotificationHandler(projector);
        var integrationEvent = new ExternalReservationOperationCompletedIntegrationEvent(
            Guid.NewGuid(),
            ScopeId,
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
    public async Task Arrival_reminder_omits_guest_identity_and_keeps_exact_reservation_navigation_payload()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
            new TestWorkspaceOwnerAudienceReader([]),
            new TestOrganizationAccessCandidateFilter(),
            notifications);
        var handler = new ReservationArrivalReminderV2NotificationHandler(projector);
        Guid reservationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        var integrationEvent = new ReservationArrivalReminderDueIntegrationEventV2(
            Guid.NewGuid(),
            ScopeId,
            Now,
            reservationId,
            propertyId,
            new DateOnly(2026, 7, 16),
            new TimeOnly(15, 30),
            "Europe/Moscow",
            3);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        UserNotificationRequestedIntegrationEventV2 notification = Assert.Single(notifications.Events);
        Assert.Equal("reservation-arrival-soon", notification.NotificationName);
        Assert.DoesNotContain("Maya Chen", notification.Body, StringComparison.Ordinal);
        Assert.Contains("A reservation", notification.Body, StringComparison.Ordinal);
        Assert.Contains("15:30", notification.Body, StringComparison.Ordinal);
        Assert.Equal(["PropertyId", "ReservationId"], JsonProperties(notification.PayloadJson));
        Assert.Contains(reservationId.ToString(), notification.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(propertyId.ToString(), notification.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Legacy_arrival_reminder_projects_the_same_minimized_notification()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
            new TestWorkspaceOwnerAudienceReader([]),
            new TestOrganizationAccessCandidateFilter(),
            notifications);
        var handler = new ReservationArrivalReminderNotificationHandler(projector);
        var integrationEvent = new ReservationArrivalReminderDueIntegrationEvent(
            Guid.NewGuid(),
            ScopeId,
            Now,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 7, 16),
            new TimeOnly(15, 30),
            "Europe/Moscow",
            3);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        UserNotificationRequestedIntegrationEventV2 notification = Assert.Single(notifications.Events);
        Assert.Equal("A reservation is expected at 15:30 on Jul 16.", notification.Body);
    }

    [Fact]
    public async Task Property_event_excludes_the_initiating_user_without_suppressing_other_recipients()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a", "user-b"]),
            new TestWorkspaceOwnerAudienceReader(["owner-a"]),
            new TestOrganizationAccessCandidateFilter(),
            notifications);
        var handler = new ReservationCancelledNotificationHandler(projector);
        var integrationEvent = new ReservationCancelledIntegrationEvent(
            Guid.NewGuid(),
            ScopeId,
            Now,
            Guid.NewGuid(),
            Guid.NewGuid(),
            3,
            "user:user-a");

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Equal(["owner-a", "user-b"], notifications.Events.Select(item => item.UserId).ToArray());
    }

    [Fact]
    public async Task Property_event_filters_every_candidate_through_authoritative_active_membership()
    {
        var access = new TestOrganizationAccessCandidateFilter(["user-a"]);
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a", "stale-staff"]),
            new TestWorkspaceOwnerAudienceReader(["stale-owner"]),
            access,
            notifications);
        var handler = new ReservationCancelledNotificationHandler(projector);

        await handler.HandleAsync(
            new ReservationCancelledIntegrationEvent(
                Guid.NewGuid(), ScopeId, Now, Guid.NewGuid(), Guid.NewGuid(), 3),
            CancellationToken.None);

        Assert.Equal(["user-a"], notifications.Events.Select(item => item.UserId).ToArray());
        Assert.Equal([["stale-owner", "stale-staff", "user-a"]], access.Requests);
    }

    [Fact]
    public async Task Membership_authority_queries_are_candidate_bounded()
    {
        string[] candidates = Enumerable.Range(0, 1001)
            .Select(index => $"user-{index:D4}")
            .ToArray();
        var access = new TestOrganizationAccessCandidateFilter([]);
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(candidates),
            new TestWorkspaceOwnerAudienceReader([]),
            access,
            new CapturingProjector());

        await new ReservationCancelledNotificationHandler(projector).HandleAsync(
            new ReservationCancelledIntegrationEvent(
                Guid.NewGuid(), ScopeId, Now, Guid.NewGuid(), Guid.NewGuid(), 3),
            CancellationToken.None);

        Assert.Equal([500, 500, 1], access.Requests.Select(request => request.Count).ToArray());
    }

    [Fact]
    public async Task Invalid_product_scope_fails_before_any_notification_is_projected()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
            new TestWorkspaceOwnerAudienceReader([]),
            new TestOrganizationAccessCandidateFilter(),
            notifications);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ReservationCancelledNotificationHandler(projector).HandleAsync(
                new ReservationCancelledIntegrationEvent(
                    Guid.NewGuid(), "not-an-organization", Now, Guid.NewGuid(), Guid.NewGuid(), 3),
                CancellationToken.None));
        Assert.Empty(notifications.Events);
    }

    [Fact]
    public async Task Free_text_and_technical_source_values_do_not_enter_notification_content()
    {
        const string sensitive = "SENSITIVE provider or operator text";
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
            new TestWorkspaceOwnerAudienceReader([]),
            new TestOrganizationAccessCandidateFilter(),
            notifications);
        Guid propertyId = Guid.NewGuid();

        await new ManualInventoryBlockCreatedNotificationHandler(projector).HandleAsync(
            new ManualInventoryBlockCreatedIntegrationEvent(
                Guid.NewGuid(), ScopeId, Now, Guid.NewGuid(), Guid.NewGuid(), propertyId,
                Guid.NewGuid(), new DateOnly(2026, 7, 14), new DateOnly(2026, 7, 16),
                sensitive, 2, "system:source-actor"),
            CancellationToken.None);
        await new ExternalReservationOperationAttentionNotificationHandler(projector).HandleAsync(
            new ExternalReservationOperationCompletedIntegrationEvent(
                Guid.NewGuid(), ScopeId, Now, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                propertyId, ExternalReservationOperationKind.Amend,
                ExternalReservationOperationOutcome.ValidationRejected, Guid.NewGuid(), 2, 3, sensitive),
            CancellationToken.None);

        Assert.Equal(2, notifications.Events.Count);
        Assert.All(notifications.Events, notification =>
        {
            Assert.DoesNotContain(sensitive, notification.Title, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitive, notification.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitive, notification.PayloadJson, StringComparison.Ordinal);
            Assert.DoesNotContain("source-actor", notification.PayloadJson, StringComparison.Ordinal);
        });
        Assert.Equal(
            ["Arrival", "BlockGroupId", "Departure", "PropertyId"],
            JsonProperties(notifications.Events[0].PayloadJson));
        Assert.Equal(
            ["ConnectionId", "PropertyId", "ReceiptId", "ReservationId"],
            JsonProperties(notifications.Events[1].PayloadJson));
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
            new TestOrganizationAccessCandidateFilter(),
            notifications);
        var handler = new StaffMemberLifecycleChangedNotificationHandler(projector);
        var integrationEvent = new StaffMemberLifecycleChangedIntegrationEvent(
            Guid.NewGuid(),
            ScopeId,
            Now,
            Guid.NewGuid(),
            StaffStatus.Suspended,
            new DateOnly(2026, 7, 13),
            2);

        await handler.HandleAsync(integrationEvent, CancellationToken.None);

        Assert.Empty(notifications.Events);
    }

    private static string[] JsonProperties(string payloadJson)
    {
        using JsonDocument document = JsonDocument.Parse(payloadJson);
        return document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
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

    private sealed class TestOrganizationAccessCandidateFilter(
        IReadOnlyCollection<string>? allowedSubjects = null) : IOrganizationAccessCandidateFilter
    {
        private readonly HashSet<string>? allowed = allowedSubjects?.ToHashSet(StringComparer.Ordinal);

        public List<IReadOnlyList<string>> Requests { get; } = [];

        public Task<IReadOnlyList<string>> FilterAllowedAsync(
            Guid organizationId,
            IReadOnlyCollection<string> candidateSubjectIds,
            CancellationToken cancellationToken)
        {
            Assert.Equal(OrganizationId, organizationId);
            string[] candidates = candidateSubjectIds.ToArray();
            this.Requests.Add(candidates);
            IReadOnlyList<string> result = this.allowed is null
                ? candidates
                : candidates.Where(this.allowed.Contains).ToArray();
            return Task.FromResult(result);
        }
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
