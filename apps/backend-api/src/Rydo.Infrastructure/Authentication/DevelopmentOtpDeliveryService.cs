using Microsoft.Extensions.Logging;

namespace Rydo.Infrastructure.Authentication;

public sealed partial class DevelopmentOtpDeliveryService(
    ILogger<DevelopmentOtpDeliveryService> logger) : IOtpDeliveryService
{
    public Task<string?> DeliverAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken)
    {
        LogDevelopmentOtp(phoneNumber, code);

        return Task.FromResult<string?>(code);
    }

    [LoggerMessage(
        LogLevel.Information,
        "Development OTP generated for {PhoneNumber}: {OtpCode}")]
    private partial void LogDevelopmentOtp(string phoneNumber, string otpCode);
}
