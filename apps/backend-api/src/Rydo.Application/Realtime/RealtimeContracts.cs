using Rydo.Application.Admin;
using Rydo.Application.Disputes;
using Rydo.Application.Matching;
using Rydo.Application.Payments;
using Rydo.Application.Trips;
using Rydo.Domain.Drivers;

namespace Rydo.Application.Realtime;

public sealed record DriverReviewChangedResult(
    Guid DriverUserId,
    DriverOnboardingStatus Status,
    string? RejectionReason,
    DateTimeOffset UpdatedAt);

public sealed record AdminOperationsChangedResult(
    string Resource,
    Guid EntityId,
    string ChangeType,
    DateTimeOffset OccurredAt);

public interface IRealtimeEventPublisher
{
    Task PublishTripUpdatedAsync(
        TripResult trip,
        CancellationToken cancellationToken);

    Task PublishTripOfferUpdatedAsync(
        TripOfferResult offer,
        CancellationToken cancellationToken);

    Task PublishDriverAvailabilityUpdatedAsync(
        DriverAvailabilityResult availability,
        Guid? activePassengerUserId,
        CancellationToken cancellationToken);

    Task PublishPaymentUpdatedAsync(
        PaymentResult payment,
        Guid? driverUserId,
        CancellationToken cancellationToken);

    Task PublishDisputeUpdatedAsync(
        DisputeDetailsResult dispute,
        Guid passengerUserId,
        Guid? driverUserId,
        CancellationToken cancellationToken);

    Task PublishDriverReviewUpdatedAsync(
        DriverReviewChangedResult review,
        CancellationToken cancellationToken);

    Task PublishAdminOperationsChangedAsync(
        AdminOperationsChangedResult change,
        CancellationToken cancellationToken);
}
