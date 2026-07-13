namespace BunkFy.Extensions.Operations.Notifications.Tests;

using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Notifications;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OperationalNotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Property_event_fans_out_to_active_staff_with_stable_distinct_ids()
    {
        var audience = new TestAudienceReader(["user-a", "user-b"]);
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(audience, notifications);
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

        Assert.Equal(2, notifications.Events.Count);
        Assert.Equal(["user-a", "user-b"], notifications.Events.Select(item => item.UserId).ToArray());
        Assert.Equal(2, notifications.Events.Select(item => item.EventId).Distinct().Count());
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
                "user-a",
                "reservation-cancelled"));
    }

    [Fact]
    public async Task Successful_provider_operation_does_not_create_an_attention_notification()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader(["user-a"]),
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
    public async Task Staff_event_is_quiet_when_the_profile_has_no_auth_subject()
    {
        var notifications = new CapturingProjector();
        var projector = new OperationalNotificationProjector(
            new TestAudienceReader([], staffAuthSubjectId: null),
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
