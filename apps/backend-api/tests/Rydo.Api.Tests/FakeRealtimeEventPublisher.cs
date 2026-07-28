using Rydo.Application.Disputes;
using Rydo.Application.Matching;
using Rydo.Application.Payments;
using Rydo.Application.Realtime;
using Rydo.Application.Trips;

namespace Rydo.Api.Tests;

internal sealed class FakeRealtimeEventPublisher : IRealtimeEventPublisher
{
    public List<TripResult> Trips { get; } = [];
    public List<TripOfferResult> Offers { get; } = [];
    public List<(DriverAvailabilityResult Availability, Guid? PassengerUserId)> Availabilities { get; } = [];
    public List<PaymentResult> Payments { get; } = [];
    public List<DisputeDetailsResult> Disputes { get; } = [];
    public List<DriverReviewChangedResult> DriverReviews { get; } = [];
    public List<AdminOperationsChangedResult> AdminChanges { get; } = [];

    public Task PublishTripUpdatedAsync(TripResult trip, CancellationToken cancellationToken)
    {
        Trips.Add(trip);
        return Task.CompletedTask;
    }

    public Task PublishTripOfferUpdatedAsync(TripOfferResult offer, CancellationToken cancellationToken)
    {
        Offers.Add(offer);
        return Task.CompletedTask;
    }

    public Task PublishDriverAvailabilityUpdatedAsync(
        DriverAvailabilityResult availability,
        Guid? activePassengerUserId,
        CancellationToken cancellationToken)
    {
        Availabilities.Add((availability, activePassengerUserId));
        return Task.CompletedTask;
    }

    public Task PublishPaymentUpdatedAsync(
        PaymentResult payment,
        Guid? driverUserId,
        CancellationToken cancellationToken)
    {
        Payments.Add(payment);
        return Task.CompletedTask;
    }

    public Task PublishDisputeUpdatedAsync(
        DisputeDetailsResult dispute,
        Guid passengerUserId,
        Guid? driverUserId,
        CancellationToken cancellationToken)
    {
        Disputes.Add(dispute);
        return Task.CompletedTask;
    }

    public Task PublishDriverReviewUpdatedAsync(
        DriverReviewChangedResult review,
        CancellationToken cancellationToken)
    {
        DriverReviews.Add(review);
        return Task.CompletedTask;
    }

    public Task PublishAdminOperationsChangedAsync(
        AdminOperationsChangedResult change,
        CancellationToken cancellationToken)
    {
        AdminChanges.Add(change);
        return Task.CompletedTask;
    }
}
