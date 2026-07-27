namespace Rydo.Domain.Matching;

public sealed class DriverAvailability
{
    private DriverAvailability()
    {
    }

    private DriverAvailability(
        Guid driverUserId,
        double latitude,
        double longitude,
        DateTimeOffset updatedAt)
    {
        DriverUserId = driverUserId;
        Latitude = latitude;
        Longitude = longitude;
        IsOnline = true;
        LocationUpdatedAt = updatedAt;
        UpdatedAt = updatedAt;
        Version = 1;
    }

    public Guid DriverUserId { get; private set; }

    public bool IsOnline { get; private set; }

    public double Latitude { get; private set; }

    public double Longitude { get; private set; }

    public DateTimeOffset? LocationUpdatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int Version { get; private set; }

    public static DriverAvailability GoOnline(
        Guid driverUserId,
        double latitude,
        double longitude,
        DateTimeOffset updatedAt)
    {
        ValidateCoordinates(latitude, longitude);
        return new DriverAvailability(driverUserId, latitude, longitude, updatedAt);
    }

    public void SetOnline(
        double latitude,
        double longitude,
        DateTimeOffset updatedAt)
    {
        ValidateCoordinates(latitude, longitude);
        Latitude = latitude;
        Longitude = longitude;
        IsOnline = true;
        LocationUpdatedAt = updatedAt;
        Touch(updatedAt);
    }

    public void UpdateLocation(
        double latitude,
        double longitude,
        DateTimeOffset updatedAt)
    {
        if (!IsOnline)
        {
            throw new InvalidOperationException(
                "A driver must be online before updating location.");
        }

        ValidateCoordinates(latitude, longitude);
        Latitude = latitude;
        Longitude = longitude;
        LocationUpdatedAt = updatedAt;
        Touch(updatedAt);
    }

    public void SetOffline(DateTimeOffset updatedAt)
    {
        if (!IsOnline)
        {
            return;
        }

        IsOnline = false;
        Touch(updatedAt);
    }

    private void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
        Version++;
    }

    private static void ValidateCoordinates(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90 ||
            !double.IsFinite(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Coordinates are outside valid bounds.");
        }
    }
}
