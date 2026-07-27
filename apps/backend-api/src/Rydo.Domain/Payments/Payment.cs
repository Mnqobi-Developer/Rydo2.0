namespace Rydo.Domain.Payments;

public sealed class Payment
{
    private Payment()
    {
    }

    private Payment(
        Guid id,
        Guid tripId,
        Guid passengerUserId,
        PaymentMethod method,
        decimal amount,
        DateTimeOffset createdAt)
    {
        Id = id;
        TripId = tripId;
        PassengerUserId = passengerUserId;
        Method = method;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = "ZAR";
        Status = PaymentStatus.AwaitingPayment;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Version = 1;
    }

    public Guid Id { get; private set; }

    public Guid TripId { get; private set; }

    public Guid PassengerUserId { get; private set; }

    public PaymentMethod Method { get; private set; }

    public PaymentStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string? ProviderPaymentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset? FailedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public int Version { get; private set; }

    public static Payment Create(
        Guid tripId,
        Guid passengerUserId,
        PaymentMethod method,
        decimal amount,
        DateTimeOffset createdAt)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be positive.");
        }

        return new Payment(
            Guid.NewGuid(),
            tripId,
            passengerUserId,
            method,
            amount,
            createdAt);
    }

    public void ConfirmCash(DateTimeOffset paidAt)
    {
        if (Method != PaymentMethod.Cash)
        {
            throw new InvalidOperationException("Only a cash payment can be confirmed manually.");
        }

        MarkPaid(null, paidAt);
    }

    public void ConfirmPayFast(string providerPaymentId, DateTimeOffset paidAt)
    {
        if (Method != PaymentMethod.PayFast)
        {
            throw new InvalidOperationException("The payment was not created for PayFast.");
        }

        MarkPaid(providerPaymentId.Trim(), paidAt);
    }

    public void Cancel(string? reason, DateTimeOffset cancelledAt)
    {
        if (Status == PaymentStatus.Paid)
        {
            throw new InvalidOperationException("A paid payment cannot be cancelled.");
        }

        if (Status == PaymentStatus.Cancelled)
        {
            return;
        }

        Status = PaymentStatus.Cancelled;
        FailureReason = NormalizeReason(reason);
        Touch(cancelledAt);
    }

    public void Fail(string reason, DateTimeOffset failedAt)
    {
        if (Status == PaymentStatus.Paid)
        {
            throw new InvalidOperationException("A paid payment cannot be failed.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = NormalizeReason(reason);
        FailedAt = failedAt;
        Touch(failedAt);
    }

    private void MarkPaid(string? providerPaymentId, DateTimeOffset paidAt)
    {
        if (Status == PaymentStatus.Paid)
        {
            if (ProviderPaymentId != providerPaymentId)
            {
                throw new InvalidOperationException(
                    "The payment is already linked to another provider transaction.");
            }

            return;
        }

        if (Status is PaymentStatus.Cancelled or PaymentStatus.Failed)
        {
            throw new InvalidOperationException($"A {Status} payment cannot be marked paid.");
        }

        Status = PaymentStatus.Paid;
        ProviderPaymentId = providerPaymentId;
        PaidAt = paidAt;
        FailureReason = null;
        Touch(paidAt);
    }

    private void Touch(DateTimeOffset updatedAt)
    {
        UpdatedAt = updatedAt;
        Version++;
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var normalized = reason.Trim();
        return normalized.Length <= 500 ? normalized : normalized[..500];
    }
}
