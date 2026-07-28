using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Rydo.Application.Payments;
using Rydo.Application.Realtime;
using Rydo.Domain.Identity;
using Rydo.Domain.Payments;
using Rydo.Domain.Trips;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Payments;

public sealed class PaymentService(
    RydoDbContext database,
    IPayFastGateway payFast,
    TimeProvider timeProvider,
    IRealtimeEventPublisher realtime) : IPaymentService
{
    public async Task<CreatePaymentResult> CreateAsync(
        Guid tripId,
        Guid passengerUserId,
        PaymentMethod method,
        CancellationToken cancellationToken)
    {
        var trip = await database.Trips.SingleOrDefaultAsync(
            item => item.Id == tripId,
            cancellationToken) ?? throw new PaymentNotFoundException();

        if (trip.PassengerUserId != passengerUserId)
        {
            throw new PaymentAccessException();
        }

        if (trip.Status == TripStatus.Cancelled)
        {
            throw new PaymentConflictException("A cancelled trip cannot be paid.");
        }

        if (trip.FinalFareAmount is null)
        {
            throw new TripFareNotFinalizedException();
        }

        var payment = await database.Payments.SingleOrDefaultAsync(
            item => item.TripId == tripId,
            cancellationToken);

        if (payment is not null && payment.Method != method)
        {
            throw new PaymentConflictException(
                "A payment with another method already exists for this trip.");
        }

        if (method == PaymentMethod.PayFast && trip.FinalFareAmount < 5m)
        {
            throw new PaymentConflictException(
                "PayFast requires a minimum once-off payment of R5.00.");
        }

        if (method == PaymentMethod.PayFast && !payFast.IsConfigured)
        {
            throw new PaymentProviderUnavailableException();
        }

        var created = false;

        if (payment is null)
        {
            payment = Payment.Create(
                tripId,
                passengerUserId,
                method,
                trip.FinalFareAmount.Value,
                timeProvider.GetUtcNow());
            database.Payments.Add(payment);
            await SaveChangesAsync(cancellationToken);
            created = true;
        }

        PayFastCheckout? checkout = null;

        if (method == PaymentMethod.PayFast && payment.Status == PaymentStatus.AwaitingPayment)
        {
            var profile = await database.PassengerProfiles.SingleAsync(
                item => item.UserId == passengerUserId,
                cancellationToken);
            var phoneNumber = await database.Users
                .Where(item => item.Id == passengerUserId)
                .Select(item => item.PhoneNumber)
                .SingleAsync(cancellationToken);
            checkout = payFast.CreateCheckout(
                payment,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                phoneNumber);
        }

        var result = ToResult(payment);

        if (created)
        {
            await realtime.PublishPaymentUpdatedAsync(
                result,
                trip.DriverUserId,
                cancellationToken);
        }

        return new CreatePaymentResult(result, checkout);
    }

    public async Task<PaymentResult?> GetForTripAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var query = from payment in database.Payments
                    join trip in database.Trips on payment.TripId equals trip.Id
                    where payment.TripId == tripId &&
                        (role == UserRole.Passenger && payment.PassengerUserId == userId ||
                            role == UserRole.Driver && trip.DriverUserId == userId)
                    select payment;

        return await Project(query).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PaymentResult> ConfirmCashAsync(
        Guid paymentId,
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var payment = await database.Payments.SingleOrDefaultAsync(
            item => item.Id == paymentId,
            cancellationToken) ?? throw new PaymentNotFoundException();
        var trip = await database.Trips.SingleAsync(
            item => item.Id == payment.TripId,
            cancellationToken);

        if (trip.DriverUserId != driverUserId)
        {
            throw new PaymentAccessException();
        }

        if (trip.Status != TripStatus.Completed)
        {
            throw new PaymentConflictException(
                "Cash can only be confirmed after the assigned driver completes the trip.");
        }

        try
        {
            payment.ConfirmCash(timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            throw new PaymentConflictException(exception.Message);
        }

        await SaveChangesAsync(cancellationToken);
        var result = ToResult(payment);
        await realtime.PublishPaymentUpdatedAsync(result, driverUserId, cancellationToken);
        return result;
    }

    public async Task ProcessPayFastNotificationAsync(
        IReadOnlyList<KeyValuePair<string, string>> fields,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var values = fields.ToDictionary(field => field.Key, field => field.Value);
        var paymentId = values.TryGetValue("m_payment_id", out var paymentIdValue) &&
            Guid.TryParse(paymentIdValue, out var parsedPaymentId)
                ? parsedPaymentId
                : (Guid?)null;
        var payment = paymentId is null
            ? null
            : await database.Payments.SingleOrDefaultAsync(
                item => item.Id == paymentId,
                cancellationToken);
        var eventType = values.GetValueOrDefault("payment_status") ?? "UNKNOWN";
        var providerEventId = values.GetValueOrDefault("pf_payment_id");
        var validation = await ValidateNotificationAsync(
            fields,
            remoteIpAddress,
            payment,
            cancellationToken);

        if (validation.IsValid && payment is not null)
        {
            try
            {
                if (eventType == "COMPLETE")
                {
                    payment.ConfirmPayFast(providerEventId!, now);
                }
                else if (eventType == "CANCELLED")
                {
                    payment.Cancel("PayFast reported the payment as cancelled.", now);
                }
                else
                {
                    validation = PayFastValidationResult.Invalid(
                        "Unsupported PayFast payment status.");
                }
            }
            catch (InvalidOperationException exception)
            {
                validation = PayFastValidationResult.Invalid(exception.Message);
            }
        }

        database.PaymentEvents.Add(PaymentEvent.Create(
            payment?.Id,
            eventType,
            providerEventId,
            validation.IsValid,
            validation.FailureReason,
            HashPayload(fields),
            remoteIpAddress?.ToString(),
            now));
        await SaveChangesAsync(cancellationToken);

        if (validation.IsValid && payment is not null)
        {
            var driverUserId = await database.Trips
                .Where(trip => trip.Id == payment.TripId)
                .Select(trip => trip.DriverUserId)
                .SingleAsync(cancellationToken);
            await realtime.PublishPaymentUpdatedAsync(
                ToResult(payment),
                driverUserId,
                cancellationToken);
        }
    }

    private async Task<PayFastValidationResult> ValidateNotificationAsync(
        IReadOnlyList<KeyValuePair<string, string>> fields,
        IPAddress? remoteIpAddress,
        Payment? payment,
        CancellationToken cancellationToken)
    {
        if (payment is null)
        {
            return PayFastValidationResult.Invalid("Unknown RYDO payment identifier.");
        }

        PayFastValidationResult providerValidation;

        try
        {
            providerValidation = await payFast.ValidateNotificationAsync(
                fields,
                remoteIpAddress,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return PayFastValidationResult.Invalid(
                "PayFast server confirmation was unavailable.");
        }

        if (!providerValidation.IsValid)
        {
            return providerValidation;
        }

        var values = fields.ToDictionary(field => field.Key, field => field.Value);

        if (!decimal.TryParse(
                values.GetValueOrDefault("amount_gross"),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var grossAmount) ||
            grossAmount != payment.Amount)
        {
            return PayFastValidationResult.Invalid("PayFast amount does not match the payment.");
        }

        if (string.IsNullOrWhiteSpace(values.GetValueOrDefault("pf_payment_id")))
        {
            return PayFastValidationResult.Invalid("PayFast transaction identifier is missing.");
        }

        return PayFastValidationResult.Valid();
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new PaymentConflictException(
                "Payment state changed before the operation completed. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            throw new PaymentConflictException(
                "Payment state conflicts with another operation.");
        }
    }

    private static string HashPayload(IEnumerable<KeyValuePair<string, string>> fields)
    {
        var payload = string.Join('&', fields.Select(
            field => $"{field.Key}={PayFastSignature.Encode(field.Value)}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static IQueryable<PaymentResult> Project(IQueryable<Payment> query)
    {
        return query.Select(payment => new PaymentResult(
            payment.Id,
            payment.TripId,
            payment.PassengerUserId,
            payment.Method,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.ProviderPaymentId,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.PaidAt,
            payment.FailedAt,
            payment.FailureReason,
            payment.Version));
    }

    private static PaymentResult ToResult(Payment payment)
    {
        return new PaymentResult(
            payment.Id,
            payment.TripId,
            payment.PassengerUserId,
            payment.Method,
            payment.Status,
            payment.Amount,
            payment.Currency,
            payment.ProviderPaymentId,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.PaidAt,
            payment.FailedAt,
            payment.FailureReason,
            payment.Version);
    }
}
