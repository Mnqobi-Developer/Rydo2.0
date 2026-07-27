using System.Net;
using Rydo.Domain.Identity;
using Rydo.Domain.Payments;

namespace Rydo.Application.Payments;

public sealed record PaymentResult(
    Guid Id,
    Guid TripId,
    Guid PassengerUserId,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    string Currency,
    string? ProviderPaymentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FailedAt,
    string? FailureReason,
    int Version);

public sealed record PayFastCheckout(
    string ProcessUrl,
    IReadOnlyDictionary<string, string> Fields);

public sealed record CreatePaymentResult(
    PaymentResult Payment,
    PayFastCheckout? PayFastCheckout);

public sealed record PayFastValidationResult(bool IsValid, string? FailureReason)
{
    public static PayFastValidationResult Valid() => new(true, null);

    public static PayFastValidationResult Invalid(string reason) => new(false, reason);
}

public interface IPayFastGateway
{
    bool IsConfigured { get; }

    PayFastCheckout CreateCheckout(
        Payment payment,
        string firstName,
        string lastName,
        string? email,
        string phoneNumber);

    Task<PayFastValidationResult> ValidateNotificationAsync(
        IReadOnlyList<KeyValuePair<string, string>> fields,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken);
}

public interface IPaymentService
{
    Task<CreatePaymentResult> CreateAsync(
        Guid tripId,
        Guid passengerUserId,
        PaymentMethod method,
        CancellationToken cancellationToken);

    Task<PaymentResult?> GetForTripAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken);

    Task<PaymentResult> ConfirmCashAsync(
        Guid paymentId,
        Guid driverUserId,
        CancellationToken cancellationToken);

    Task ProcessPayFastNotificationAsync(
        IReadOnlyList<KeyValuePair<string, string>> fields,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken);
}

public sealed class PaymentNotFoundException : Exception;

public sealed class PaymentAccessException : Exception;

public sealed class PaymentConflictException(string message) : Exception(message);

public sealed class TripFareNotFinalizedException : Exception
{
    public TripFareNotFinalizedException()
        : base("The trip fare must be finalized before creating a payment.")
    {
    }
}

public sealed class PaymentProviderUnavailableException : Exception
{
    public PaymentProviderUnavailableException()
        : base("PayFast checkout is disabled until merchant credentials and public callback URLs are configured.")
    {
    }
}
