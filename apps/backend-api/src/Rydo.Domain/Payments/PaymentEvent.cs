namespace Rydo.Domain.Payments;

public sealed class PaymentEvent
{
    private PaymentEvent()
    {
    }

    private PaymentEvent(
        Guid id,
        Guid? paymentId,
        string eventType,
        string? providerEventId,
        bool isValid,
        string? failureReason,
        string payloadSha256,
        string? remoteIpAddress,
        DateTimeOffset receivedAt)
    {
        Id = id;
        PaymentId = paymentId;
        Provider = "PayFast";
        EventType = eventType;
        ProviderEventId = providerEventId;
        IsValid = isValid;
        FailureReason = failureReason;
        PayloadSha256 = payloadSha256;
        RemoteIpAddress = remoteIpAddress;
        ReceivedAt = receivedAt;
    }

    public Guid Id { get; private set; }
    public Guid? PaymentId { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string? ProviderEventId { get; private set; }
    public bool IsValid { get; private set; }
    public string? FailureReason { get; private set; }
    public string PayloadSha256 { get; private set; } = string.Empty;
    public string? RemoteIpAddress { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static PaymentEvent Create(
        Guid? paymentId,
        string eventType,
        string? providerEventId,
        bool isValid,
        string? failureReason,
        string payloadSha256,
        string? remoteIpAddress,
        DateTimeOffset receivedAt)
    {
        return new PaymentEvent(
            Guid.NewGuid(),
            paymentId,
            eventType.Trim(),
            providerEventId?.Trim(),
            isValid,
            failureReason?.Trim(),
            payloadSha256,
            remoteIpAddress,
            receivedAt);
    }
}
