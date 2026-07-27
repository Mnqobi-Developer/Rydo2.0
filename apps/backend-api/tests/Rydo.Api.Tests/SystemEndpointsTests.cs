using System.Net;
using System.Net.Http.Json;
using Rydo.Application.System;

namespace Rydo.Api.Tests;

public sealed class SystemEndpointsTests(AuthenticationApiFactory factory)
    : IClassFixture<AuthenticationApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task LiveHealthReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SystemStatusReturnsFoundationMetadata()
    {
        var response = await _client.GetAsync("/api/v1/system");
        var payload = await response.Content.ReadFromJsonAsync<ServiceStatusResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("RYDO API", payload.Name);
        Assert.Equal("foundation", payload.Stage);
        Assert.Equal("operational", payload.Status);
    }
}
