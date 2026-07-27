using System.Net;
using System.Net.Http.Json;

namespace Rydo.Api.Tests;

public sealed class AuthenticationRateLimitTests
{
    [Fact]
    public async Task OtpRequestsAreRateLimitedPerClient()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        for (var requestNumber = 1; requestNumber <= 5; requestNumber++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/otp/request",
                new
                {
                    phoneNumber = $"+278200001{requestNumber:D2}",
                    role = "Passenger",
                });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/request",
            new { phoneNumber = "+27820000199", role = "Passenger" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
