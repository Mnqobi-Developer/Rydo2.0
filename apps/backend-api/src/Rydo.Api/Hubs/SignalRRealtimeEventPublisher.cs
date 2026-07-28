using Microsoft.AspNetCore.SignalR;
using Rydo.Application.Disputes;
using Rydo.Application.Matching;
using Rydo.Application.Payments;
using Rydo.Application.Realtime;
using Rydo.Application.Trips;
using Rydo.Domain.Identity;

namespace Rydo.Api.Hubs;

public sealed class SignalRRealtimeEventPublisher(
    IHubContext<OperationsHub, IOperationsClient> hub,
    ILogger<SignalRRealtimeEventPublisher> logger) : IRealtimeEventPublisher
{
    private static readonly Action<ILogger, string, Guid, Exception?> LogPublishFailure =
        LoggerMessage.Define<string, Guid>(
            LogLevel.Error,
            new EventId(1, "SignalRPublishFailure"),
            "SignalR event {EventName} for {EntityId} could not be published.");

    public Task PublishTripUpdatedAsync(
        TripResult trip,
        CancellationToken cancellationToken)
    {
        var groups = ParticipantGroups(trip.PassengerUserId, trip.DriverUserId);
        return PublishSafelyAsync(
            () => hub.Clients.Groups(groups).TripUpdated(trip),
            "trip.updated",
            trip.Id);
    }

    public Task PublishTripOfferUpdatedAsync(
        TripOfferResult offer,
        CancellationToken cancellationToken)
    {
        return PublishSafelyAsync(
            () => hub.Clients.Groups(
                RealtimeGroups.User(offer.DriverUserId),
                RealtimeGroups.Role(UserRole.Admin))
                .TripOfferUpdated(offer),
            "trip-offer.updated",
            offer.Id);
    }

    public Task PublishDriverAvailabilityUpdatedAsync(
        DriverAvailabilityResult availability,
        Guid? activePassengerUserId,
        CancellationToken cancellationToken)
    {
        var groups = new HashSet<string>(StringComparer.Ordinal)
        {
            RealtimeGroups.User(availability.DriverUserId),
            RealtimeGroups.Role(UserRole.Admin),
        };

        if (activePassengerUserId is Guid passengerUserId)
        {
            groups.Add(RealtimeGroups.User(passengerUserId));
        }

        return PublishSafelyAsync(
            () => hub.Clients.Groups(groups).DriverAvailabilityUpdated(availability),
            "driver-availability.updated",
            availability.DriverUserId);
    }

    public Task PublishPaymentUpdatedAsync(
        PaymentResult payment,
        Guid? driverUserId,
        CancellationToken cancellationToken)
    {
        var groups = ParticipantGroups(payment.PassengerUserId, driverUserId);
        return PublishSafelyAsync(
            () => hub.Clients.Groups(groups).PaymentUpdated(payment),
            "payment.updated",
            payment.Id);
    }

    public Task PublishDisputeUpdatedAsync(
        DisputeDetailsResult dispute,
        Guid passengerUserId,
        Guid? driverUserId,
        CancellationToken cancellationToken)
    {
        var groups = ParticipantGroups(passengerUserId, driverUserId);
        return PublishSafelyAsync(
            () => hub.Clients.Groups(groups).DisputeUpdated(dispute),
            "dispute.updated",
            dispute.Id);
    }

    public Task PublishDriverReviewUpdatedAsync(
        DriverReviewChangedResult review,
        CancellationToken cancellationToken)
    {
        return PublishSafelyAsync(
            () => hub.Clients.Groups(
                RealtimeGroups.User(review.DriverUserId),
                RealtimeGroups.Role(UserRole.Admin))
                .DriverReviewUpdated(review),
            "driver-review.updated",
            review.DriverUserId);
    }

    public Task PublishAdminOperationsChangedAsync(
        AdminOperationsChangedResult change,
        CancellationToken cancellationToken)
    {
        return PublishSafelyAsync(
            () => hub.Clients.Group(RealtimeGroups.Role(UserRole.Admin))
                .AdminOperationsChanged(change),
            "admin-operations.changed",
            change.EntityId);
    }

    private async Task PublishSafelyAsync(
        Func<Task> publish,
        string eventName,
        Guid entityId)
    {
        try
        {
            await publish();
        }
        catch (Exception exception)
        {
            LogPublishFailure(logger, eventName, entityId, exception);
        }
    }

    private static List<string> ParticipantGroups(
        Guid passengerUserId,
        Guid? driverUserId)
    {
        var groups = new List<string>
        {
            RealtimeGroups.User(passengerUserId),
            RealtimeGroups.Role(UserRole.Admin),
        };

        if (driverUserId is Guid assignedDriverUserId)
        {
            groups.Add(RealtimeGroups.User(assignedDriverUserId));
        }

        return groups;
    }
}
