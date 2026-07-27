namespace Rydo.Infrastructure.Authentication;

public sealed class UnavailableOtpDeliveryService : IOtpDeliveryService
{
    public Task<string?> DeliverAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(
            "A production OTP delivery provider has not been configured.");
    }
}
