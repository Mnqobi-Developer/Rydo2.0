using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rydo.Application.Authentication;

namespace Rydo.Api.Tests;

internal static class AuthenticationTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task<OtpRequestResult> RequestOtpAsync(
        HttpClient client,
        string phoneNumber,
        string role)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/request",
            new { phoneNumber, role });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<OtpRequestResult>(JsonOptions))!;
    }

    public static async Task<TokenPairResult> SignInAsync(
        HttpClient client,
        string phoneNumber,
        string role)
    {
        var request = await RequestOtpAsync(client, phoneNumber, role);
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new { request.ChallengeId, code = request.DevelopmentCode });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<TokenPairResult>(JsonOptions))!;
    }

    public static void UseBearerToken(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public static async Task<TokenPairResult> ReadTokenPairAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<TokenPairResult>(JsonOptions))!;
    }

    public static async Task<AuthenticatedUser> ReadAuthenticatedUserAsync(
        HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<AuthenticatedUser>(JsonOptions))!;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
