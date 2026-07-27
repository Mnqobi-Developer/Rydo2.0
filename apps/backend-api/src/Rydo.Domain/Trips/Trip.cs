using Rydo.Domain.Identity;

namespace Rydo.Domain.Trips;

public sealed class Trip
{
    private Trip()
    {
    }

    private Trip(
        Guid id,
        Guid passengerUserId,
        string pickupAddress,
        double pickupLatitude,
        double pickupLongitude,
        string destinationAddress,
        double destinationLatitude,
        double destinationLongitude,
        DateTimeOffset requestedAt)
    {
        Id = id;
        PassengerUserId = passengerUserId;
        PickupAddress = NormalizeAddress(pickupAddress);
        PickupLatitude = pickupLatitude;
        PickupLongitude = pickupLongitude;
        DestinationAddress = NormalizeAddress(destinationAddress);
        DestinationLatitude = destinationLatitude;
        DestinationLongitude = destinationLongitude;
        Status = TripStatus.Requested;
        RequestedAt = requestedAt;
        UpdatedAt = requestedAt;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid PassengerUserId { get; private set; }

    public Guid? DriverUserId { get; private set; }

    public string PickupAddress { get; private set; } = string.Empty;

    public double PickupLatitude { get; private set; }

    public double PickupLongitude { get; private set; }

    public string DestinationAddress { get; private set; } = string.Empty;

    public double DestinationLatitude { get; private set; }

    public double DestinationLongitude { get; private set; }

    public TripStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? DriverArrivedAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public Guid? CancelledByUserId { get; private set; }

    public string? CancellationReason { get; private set; }

    public int Version { get; private set; }

    public bool IsActive => Status is not TripStatus.Completed and not TripStatus.Cancelled;

    public static Trip Request(
        Guid passengerUserId,
        string pickupAddress,
        double pickupLatitude,
        double pickupLongitude,
        string destinationAddress,
        double destinationLatitude,
        double destinationLongitude,
        DateTimeOffset requestedAt)
    {
        ValidateCoordinates(pickupLatitude, pickupLongitude, nameof(pickupLatitude));
        ValidateCoordinates(destinationLatitude, destinationLongitude, nameof(destinationLatitude));

        if (pickupLatitude == destinationLatitude && pickupLongitude == destinationLongitude)
        {
            throw new ArgumentException("Pickup and destination must be different locations.");
        }

        return new Trip(
            Guid.NewGuid(),
            passengerUserId,
            pickupAddress,
            pickupLatitude,
            pickupLongitude,
            destinationAddress,
            destinationLatitude,
            destinationLongitude,
            requestedAt);
    }

    public void Accept(Guid driverUserId, DateTimeOffset acceptedAt)
    {
        RequireStatus(TripStatus.Requested, "accept");
        DriverUserId = driverUserId;
        AcceptedAt = acceptedAt;
        TransitionTo(TripStatus.Accepted, acceptedAt);
    }

    public void MarkDriverArrived(Guid driverUserId, DateTimeOffset arrivedAt)
    {
        RequireDriver(driverUserId);
        RequireStatus(TripStatus.Accepted, "mark the driver as arrived");
        DriverArrivedAt = arrivedAt;
        TransitionTo(TripStatus.DriverArrived, arrivedAt);
    }

    public void Start(Guid driverUserId, DateTimeOffset startedAt)
    {
        RequireDriver(driverUserId);
        RequireStatus(TripStatus.DriverArrived, "start");
        StartedAt = startedAt;
        TransitionTo(TripStatus.InProgress, startedAt);
    }

    public void Complete(Guid driverUserId, DateTimeOffset completedAt)
    {
        RequireDriver(driverUserId);
        RequireStatus(TripStatus.InProgress, "complete");
        CompletedAt = completedAt;
        TransitionTo(TripStatus.Completed, completedAt);
    }

    public void Cancel(
        Guid userId,
        UserRole role,
        string? reason,
        DateTimeOffset cancelledAt)
    {
        if (Status is TripStatus.InProgress or TripStatus.Completed or TripStatus.Cancelled)
        {
            throw new InvalidOperationException(
                $"A trip in the {Status} state cannot be cancelled.");
        }

        var isPassenger = role == UserRole.Passenger && userId == PassengerUserId;
        var isAssignedDriver = role == UserRole.Driver && userId == DriverUserId;

        if (!isPassenger && !isAssignedDriver)
        {
            throw new UnauthorizedAccessException("Only a trip participant can cancel this trip.");
        }

        CancelledByUserId = userId;
        CancellationReason = NormalizeCancellationReason(reason);
        CancelledAt = cancelledAt;
        TransitionTo(TripStatus.Cancelled, cancelledAt);
    }

    private void RequireDriver(Guid driverUserId)
    {
        if (DriverUserId != driverUserId)
        {
            throw new UnauthorizedAccessException(
                "Only the assigned driver can change this trip state.");
        }
    }

    private void RequireStatus(TripStatus requiredStatus, string action)
    {
        if (Status != requiredStatus)
        {
            throw new InvalidOperationException(
                $"A trip in the {Status} state cannot {action}.");
        }
    }

    private void TransitionTo(TripStatus status, DateTimeOffset changedAt)
    {
        Status = status;
        UpdatedAt = changedAt;
        Version++;
    }

    private static void ValidateCoordinates(double latitude, double longitude, string name)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(name, "Coordinates are outside valid bounds.");
        }
    }

    private static string NormalizeAddress(string address)
    {
        var normalized = address.Trim();

        if (normalized.Length is 0 or > 300)
        {
            throw new ArgumentException("Trip addresses must contain between 1 and 300 characters.");
        }

        return normalized;
    }

    private static string? NormalizeCancellationReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var normalized = reason.Trim();

        if (normalized.Length > 250)
        {
            throw new ArgumentException("Cancellation reasons cannot exceed 250 characters.");
        }

        return normalized;
    }
}
