namespace Rydo.Domain.Payments;

public enum PaymentStatus
{
    AwaitingPayment,
    Paid,
    Cancelled,
    Failed,
}
