namespace Rydo.Infrastructure.Authentication;

public interface IOtpDeliveryService
{
    Task<string?> DeliverAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken);
}
