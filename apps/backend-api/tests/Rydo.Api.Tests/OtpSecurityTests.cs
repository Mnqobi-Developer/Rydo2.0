using System.Net;
using System.Net.Http.Json;

namespace Rydo.Api.Tests;

public sealed class OtpSecurityTests
{
    [Fact]
    public async Task OtpLocksAfterMaximumFailedAttempts()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var request = await AuthenticationTestClient.RequestOtpAsync(
            client,
            "+27820000004",
            "Passenger");

        var incorrectCode = request.DevelopmentCode == "000000" ? "000001" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await client.PostAsJsonAsync(
                "/api/v1/auth/otp/verify",
                new { request.ChallengeId, code = incorrectCode });
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var locked = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { request.ChallengeId, code = request.DevelopmentCode });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    [Fact]
    public async Task ExpiredOtpCannotBeUsed()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var request = await AuthenticationTestClient.RequestOtpAsync(
            client,
            "+27820000005",
            "Driver");
        factory.Clock.Advance(TimeSpan.FromMinutes(6));

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { request.ChallengeId, code = request.DevelopmentCode });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPhoneAndAdminSelfRegistrationAreRejected()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var invalidPhone = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/request",
            new { phoneNumber = "0820000000", role = "Passenger" });
        var admin = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/request",
            new { phoneNumber = "+27820000006", role = "Admin" });

        Assert.Equal(HttpStatusCode.BadRequest, invalidPhone.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, admin.StatusCode);
    }

    [Fact]
    public async Task ExistingPhoneCannotSwitchRoles()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        const string phoneNumber = "+27820000008";
        await AuthenticationTestClient.SignInAsync(client, phoneNumber, "Passenger");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/request",
            new { phoneNumber, role = "Driver" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RepeatedRequestForSamePhoneIsRateLimited()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        const string phoneNumber = "+27820000010";
        await AuthenticationTestClient.RequestOtpAsync(client, phoneNumber, "Passenger");

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/request",
            new { phoneNumber, role = "Passenger" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
