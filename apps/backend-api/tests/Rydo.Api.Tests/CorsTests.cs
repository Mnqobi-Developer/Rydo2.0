using System.Net;

namespace Rydo.Api.Tests;

public sealed class CorsTests(AuthenticationApiFactory factory)
    : IClassFixture<AuthenticationApiFactory>
{
    [Fact]
    public async Task PassengerWebDevelopmentOriginIsAllowed()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/v1/auth/otp/request");
        request.Headers.Add("Origin", "http://localhost:8081");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://localhost:8081",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
