using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rydo.Application.Authentication;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Api.Tests;

public sealed class AuthenticationFlowTests
{
    [Theory]
    [InlineData("+27820000001", "Passenger")]
    [InlineData("+27820000002", "Driver")]
    public async Task PhoneSignInReturnsIdentityAndRevocableSession(
        string phoneNumber,
        string role)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var tokens = await AuthenticationTestClient.SignInAsync(client, phoneNumber, role);
        AuthenticationTestClient.UseBearerToken(client, tokens.AccessToken);

        var meResponse = await client.GetAsync("/api/v1/auth/me");
        var me = await AuthenticationTestClient.ReadAuthenticatedUserAsync(meResponse);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.NotNull(me);
        Assert.Equal(phoneNumber, me.PhoneNumber);
        Assert.Equal(role, me.Role.ToString());

        var revokeResponse = await client.PostAsync("/api/v1/auth/sessions/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var rejectedResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task OtpAndRefreshTokenAreStoredOnlyAsHashes()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        const string phoneNumber = "+27820000003";

        var request = await AuthenticationTestClient.RequestOtpAsync(
            client,
            phoneNumber,
            "Passenger");
        Assert.NotNull(request.DevelopmentCode);

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
            var challenge = await database.OtpChallenges.SingleAsync();

            Assert.NotEqual(request.DevelopmentCode, challenge.CodeHash);
            Assert.DoesNotContain(request.DevelopmentCode, challenge.CodeHash, StringComparison.Ordinal);
        }

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { request.ChallengeId, code = request.DevelopmentCode });
        verifyResponse.EnsureSuccessStatusCode();
        var tokens = await AuthenticationTestClient.ReadTokenPairAsync(verifyResponse);

        using var verificationScope = factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<RydoDbContext>();
        var storedRefreshToken = await verificationDatabase.RefreshTokens.SingleAsync();

        Assert.NotEqual(tokens.RefreshToken, storedRefreshToken.TokenHash);
        Assert.DoesNotContain(tokens.RefreshToken, storedRefreshToken.TokenHash, StringComparison.Ordinal);
    }
}
