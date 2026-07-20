namespace BunkFy.Modules.Reservations.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Permissions;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tasks;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationsProfileTests
{
    [Fact]
    public void Default_profile_requires_inventory_and_outbox()
    {
        Assert.Contains(
            ReservationsProfiles.Default.Requires,
            feature => feature.Id == InventoryCompositionFeatures.Availability);
        Assert.Contains(
            ReservationsProfiles.Default.Requires,
            feature => feature.Id == MessagingCompositionFeatures.Outbox);
        Assert.Contains(
            ReservationsProfiles.Default.RequiredModules,
            module => module.ModuleName == InventoryModuleMetadata.Name);
    }

    [Fact]
    public void Descriptor_exposes_scoped_permissions_and_saga_contracts()
    {
        IReadOnlyCollection<ModulePermissionDescriptor> permissions = ReservationsModuleMetadata.Descriptor.GetPermissions();

        Assert.Equal(8, permissions.Count);
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeRequirement.Scoped, permission.ScopeRequirement));
        Assert.All(permissions, permission => Assert.Equal(PermissionScopeGrantPolicy.Descendants, permission.ScopeGrantPolicy));
        Assert.Equal(15, ReservationsModuleMetadata.Descriptor.GetPublishedEvents().Count);
        Assert.Equal(19, ReservationsModuleMetadata.Descriptor.GetSubscriptions().Count);
        Assert.Contains(
            ReservationsModuleMetadata.Descriptor.GetPublishedEvents(),
            published => published.EventType == ExternalReservationOperationCompletedIntegrationEvent.EventType);
        Assert.Contains(
            ReservationsModuleMetadata.Descriptor.GetPublishedEvents(),
            published => published.EventType == ReservationCheckedInIntegrationEvent.EventType);
        Assert.Contains(
            ReservationsModuleMetadata.Descriptor.GetPublishedEvents(),
            published => published.EventType == ReservationNoShowIntegrationEvent.EventType);
        Assert.Contains(
            ReservationsModuleMetadata.Descriptor.GetPublishedEvents(),
            published => published.EventType == ReservationCheckedOutIntegrationEvent.EventType);
        Assert.Contains(
            ReservationsModuleMetadata.Descriptor.GetPublishedEvents(),
            published => published.EventType == ReservationArrivalReminderDueIntegrationEvent.EventType);
        Assert.Equal(
            4,
            ReservationsModuleMetadata.Descriptor.GetSubscriptions().Count(subscription =>
                subscription.ProducerModule == ReservationsModuleMetadata.ExternalOperationSourceModuleName));
        Assert.Equal(4, ReservationsModuleMetadata.Descriptor.GetTasks().Count);
        Assert.Single(ReservationsModuleMetadata.Descriptor.GetCompositionProfiles());
        Assert.Equal(6, (int)ReservationStatus.CheckedIn);
        Assert.Equal(7, (int)ReservationStatus.NoShowPending);
        Assert.Equal(8, (int)ReservationStatus.NoShow);
        Assert.Equal(9, (int)ReservationStatus.CheckoutPending);
        Assert.Equal(10, (int)ReservationStatus.CheckedOut);
        Assert.EndsWith(
            ".reservations.reservation-checked-in.v1",
            ReservationsIntegrationSubjects.CreateReservationCheckedIn(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            ".reservations.reservation-no-show.v1",
            ReservationsIntegrationSubjects.CreateReservationNoShow(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            ".reservations.reservation-checked-out.v1",
            ReservationsIntegrationSubjects.CreateReservationCheckedOut(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            ".reservations.reservation-arrival-reminder-due.v1",
            ReservationsIntegrationSubjects.CreateReservationArrivalReminderDueV1(),
            StringComparison.Ordinal);
        Assert.EndsWith(
            ".reservations.reservation-arrival-reminder-due.v2",
            ReservationsIntegrationSubjects.CreateReservationArrivalReminderDue(),
            StringComparison.Ordinal);
    }
}
