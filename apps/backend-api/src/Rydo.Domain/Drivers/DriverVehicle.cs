namespace Rydo.Domain.Drivers;

public sealed class DriverVehicle
{
    private DriverVehicle()
    {
    }

    private DriverVehicle(
        Guid id,
        Guid driverUserId,
        string make,
        string model,
        int year,
        string color,
        string registrationNumber,
        string vehicleIdentificationNumber,
        int seatCapacity,
        DateTimeOffset createdAt)
    {
        Id = id;
        DriverUserId = driverUserId;
        Make = make;
        Model = model;
        Year = year;
        Color = color;
        RegistrationNumber = registrationNumber;
        VehicleIdentificationNumber = vehicleIdentificationNumber;
        SeatCapacity = seatCapacity;
        ReviewStatus = DriverVehicleReviewStatus.PendingReview;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid DriverUserId { get; private set; }

    public string Make { get; private set; } = string.Empty;

    public string Model { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public string Color { get; private set; } = string.Empty;

    public string RegistrationNumber { get; private set; } = string.Empty;

    public string VehicleIdentificationNumber { get; private set; } = string.Empty;

    public int SeatCapacity { get; private set; }

    public DriverVehicleReviewStatus ReviewStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? RejectionReason { get; private set; }

    public static DriverVehicle Create(
        Guid driverUserId,
        string make,
        string model,
        int year,
        string color,
        string registrationNumber,
        string vehicleIdentificationNumber,
        int seatCapacity,
        DateTimeOffset createdAt)
    {
        return new DriverVehicle(
            Guid.NewGuid(),
            driverUserId,
            NormalizeText(make),
            NormalizeText(model),
            year,
            NormalizeText(color),
            NormalizeIdentifier(registrationNumber),
            NormalizeIdentifier(vehicleIdentificationNumber),
            seatCapacity,
            createdAt);
    }

    public void Update(
        string make,
        string model,
        int year,
        string color,
        string registrationNumber,
        string vehicleIdentificationNumber,
        int seatCapacity,
        DateTimeOffset updatedAt)
    {
        Make = NormalizeText(make);
        Model = NormalizeText(model);
        Year = year;
        Color = NormalizeText(color);
        RegistrationNumber = NormalizeIdentifier(registrationNumber);
        VehicleIdentificationNumber = NormalizeIdentifier(vehicleIdentificationNumber);
        SeatCapacity = seatCapacity;
        ReviewStatus = DriverVehicleReviewStatus.PendingReview;
        ReviewedAt = null;
        RejectionReason = null;
        UpdatedAt = updatedAt;
    }

    private static string NormalizeText(string value)
    {
        return value.Trim();
    }

    private static string NormalizeIdentifier(string value)
    {
        return value.Trim().ToUpperInvariant();
    }
}
