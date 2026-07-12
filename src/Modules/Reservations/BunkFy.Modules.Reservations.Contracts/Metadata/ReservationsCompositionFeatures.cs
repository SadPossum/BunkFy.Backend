namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.ModuleComposition;

public static class ReservationsCompositionFeatures
{
    public static readonly CompositionFeatureId BookingLifecycle = new("reservations.booking-lifecycle");

    public static ProvidedCompositionFeature BookingLifecycleProvided(string provider) =>
        new(BookingLifecycle, provider, "Reservation booking lifecycle is selected.");
}
