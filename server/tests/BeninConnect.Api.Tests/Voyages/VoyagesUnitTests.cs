using BeninConnect.Modules.Voyages.Domain;
using BeninConnect.Shared.Domain;

namespace BeninConnect.Api.Tests.Voyages;

public sealed class VoyagesUnitTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Traveler = Guid.NewGuid();

    private static TripDetails Details(decimal kg = 10, int inDays = 10) =>
        new("fr", " Paris ", "BJ", "Porto-Novo", DateOnly.FromDateTime(Now.UtcDateTime).AddDays(inDays), kg, 8, "eur",
            [ParcelCategory.Documents, ParcelCategory.Clothes, ParcelCategory.Clothes], null, null);

    private static bool Verify(string hash, string code) => hash == $"h:{code}";

    [Theory]
    [InlineData("Porto-Novo", "porto novo")]
    [InlineData("  Abomey-Calavi ", "abomey calavi")]
    [InlineData("Bohicon", "bohicon")]
    [InlineData("Ouidah", "ouidah")]
    [InlineData("Évry-Courcouronnes", "evry courcouronnes")]
    public void City_key_ignores_case_accents_and_dashes(string city, string key) => Assert.Equal(key, VoyageRules.CityKey(city));

    [Fact]
    public void Trip_is_normalized()
    {
        var trip = Trip.Create(Traveler, Details(), Now);
        Assert.Equal("FR", trip.FromCountry);
        Assert.Equal("Paris", trip.FromCity);
        Assert.Equal("EUR", trip.Currency);
        Assert.Equal(2, trip.AcceptedCategories.Count);
    }

    [Fact]
    public void Price_is_capped_by_currency()
    {
        Assert.Throws<DomainException>(() => Trip.Create(Traveler, Details() with { PricePerKg = 500, Currency = "EUR" }, Now));
        Trip.Create(Traveler, Details() with { PricePerKg = 5000, Currency = "XOF" }, Now);
        Assert.Throws<DomainException>(() => Trip.Create(Traveler, Details() with { Currency = "USD" }, Now));
    }

    [Fact]
    public void Delivery_code_allows_five_attempts_then_needs_a_new_code()
    {
        var trip = Trip.Create(Traveler, Details(), Now);
        var sender = Guid.NewGuid();
        var booking = trip.RequestBooking(sender, 2, ParcelCategory.Documents, "Dossier administratif", true, Now);
        trip.Accept(booking.Id, Traveler, Now);
        Assert.Throws<ForbiddenException>(() => booking.SetDeliveryCode(Traveler, "h:123456", Now));
        booking.SetDeliveryCode(sender, "h:123456", Now);
        Assert.Throws<ForbiddenException>(() => booking.TryDeliver(sender, "123456", Verify, Now));

        for (var i = 0; i < VoyageRules.DeliveryCodeAttempts; i++)
        {
            Assert.False(booking.TryDeliver(Traveler, "999999", Verify, Now));
        }

        Assert.True(booking.CodeLocked);
        Assert.Throws<DomainException>(() => booking.TryDeliver(Traveler, "123456", Verify, Now));
        booking.SetDeliveryCode(sender, "h:654321", Now);
        Assert.False(booking.TryDeliver(Traveler, "123456", Verify, Now)); // l'ancien code ne marche plus
        Assert.True(booking.TryDeliver(Traveler, "654321", Verify, Now));
        Assert.Equal(BookingStatus.Delivered, booking.Status);
        Assert.Null(booking.DeliveryCodeHash);
    }

    [Fact]
    public void Full_trip_reopens_when_a_booking_is_cancelled()
    {
        var trip = Trip.Create(Traveler, Details(kg: 4), Now);
        var sender = Guid.NewGuid();
        var booking = trip.RequestBooking(sender, 4, ParcelCategory.Clothes, "Vêtements pour la famille", true, Now);
        trip.Accept(booking.Id, Traveler, Now);
        Assert.Equal(TripStatus.Full, trip.Status);
        Assert.Throws<DomainException>(() => trip.RequestBooking(Guid.NewGuid(), 1, ParcelCategory.Clothes, "Encore un petit colis", true, Now));

        trip.CancelBooking(booking.Id, sender, Now);
        Assert.Equal(TripStatus.Open, trip.Status);
        Assert.Equal(4, trip.RemainingKg);
    }

    [Fact]
    public void Departed_trip_expires_pending_requests()
    {
        var trip = Trip.Create(Traveler, Details(inDays: 1), Now);
        var pending = trip.RequestBooking(Guid.NewGuid(), 1, ParcelCategory.Clothes, "Vêtements pour la famille", true, Now);
        Assert.Empty(trip.MarkDeparted(Now.AddDays(1))); // le jour du départ : encore ouvert
        var expired = trip.MarkDeparted(Now.AddDays(2));
        Assert.Equal(TripStatus.Departed, trip.Status);
        Assert.Contains(pending, expired);
        Assert.Equal(BookingStatus.Cancelled, pending.Status);
    }

    [Fact]
    public void Reviews_need_a_delivery_and_dispute_blocks_them()
    {
        var trip = Trip.Create(Traveler, Details(), Now);
        var sender = Guid.NewGuid();
        var booking = trip.RequestBooking(sender, 2, ParcelCategory.Documents, "Dossier administratif", true, Now);
        trip.Accept(booking.Id, Traveler, Now);
        Assert.Throws<DomainException>(() => TripReview.Create(booking, sender, 5, null, Now));

        booking.Dispute(sender, "Colis jamais remis", Now);
        Assert.Throws<DomainException>(() => TripReview.Create(booking, sender, 5, null, Now));
        booking.Resolve(DisputeOutcome.Delivered, "Remise confirmée par les deux parties", Now);
        var review = TripReview.Create(booking, sender, 5, "Parfait", Now);
        Assert.True(review.AboutTraveler);
        Assert.Equal(Traveler, review.TargetId);
        Assert.Throws<ForbiddenException>(() => TripReview.Create(booking, Guid.NewGuid(), 5, null, Now));
    }

    [Fact]
    public void Alert_matches_route_city_and_period()
    {
        var trip = Trip.Create(Traveler, Details(inDays: 10), Now);
        Assert.True(RouteAlert.Create(Guid.NewGuid(), "FR", null, "BJ", "porto novo", null, null, Now).Matches(trip));
        Assert.False(RouteAlert.Create(Guid.NewGuid(), "FR", null, "BJ", "Cotonou", null, null, Now).Matches(trip));
        Assert.False(RouteAlert.Create(Guid.NewGuid(), "FR", null, "BJ", null, null, DateOnly.FromDateTime(Now.UtcDateTime).AddDays(5), Now).Matches(trip));
        Assert.False(RouteAlert.Create(Guid.NewGuid(), "BJ", null, "FR", null, null, null, Now).Matches(trip));
    }
}
