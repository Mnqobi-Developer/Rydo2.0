using System.Net;
using System.Net.Http.Json;

namespace Rydo.Api.Tests;

public sealed class RefreshTokenSecurityTests
{
    [Fact]
    public async Task RefreshTokenReplayRevokesTheSessionFamily()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var initial = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000007",
            "Passenger");

        var rotationResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { initial.RefreshToken });
        rotationResponse.EnsureSuccessStatusCode();
        var rotated = await AuthenticationTestClient.ReadTokenPairAsync(rotationResponse);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);

        var replayResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { initial.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        AuthenticationTestClient.UseBearerToken(client, rotated.AccessToken);
        var meResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);

        var rotatedRefreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, rotatedRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedIdentityRejectsMissingToken()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProtectedIdentityRejectsTamperedToken()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var tokens = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820000009",
            "Driver");
        var replacement = tokens.AccessToken.EndsWith('A') ? 'B' : 'A';
        var tamperedToken = tokens.AccessToken[..^1] + replacement;
        AuthenticationTestClient.UseBearerToken(client, tamperedToken);

        var response = await client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
