using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Rydo.Application.Payments;
using Rydo.Domain.Payments;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Api.Tests;

internal static class PaymentTestClient
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static async Task FinalizeFareAsync(
        AuthenticationApiFactory factory,
        Guid tripId,
        decimal amount = 125.50m)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
        var trip = await database.Trips.SingleAsync(item => item.Id == tripId);
        trip.FinalizeFare(amount, factory.Clock.GetUtcNow());
        await database.SaveChangesAsync();
    }

    public static async Task<CreatePaymentResult> CreateAsync(
        HttpClient client,
        Guid tripId,
        string method)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/trips/{tripId}/payments",
            new { method });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatePaymentResult>(JsonOptions))!;
    }

    public static async Task<PaymentResult> ReadAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<PaymentResult>(JsonOptions))!;
    }

    public static AuthenticationApiFactory CreatePayFastFactory(
        FakePayFastGateway gateway)
    {
        return new AuthenticationApiFactory(services =>
        {
            services.RemoveAll<IPayFastGateway>();
            services.AddSingleton<IPayFastGateway>(gateway);
        });
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal sealed class FakePayFastGateway : IPayFastGateway
{
    public bool IsConfigured { get; init; } = true;

    public PayFastValidationResult ValidationResult { get; set; } =
        PayFastValidationResult.Valid();

    public PayFastCheckout CreateCheckout(
        Payment payment,
        string firstName,
        string lastName,
        string? email,
        string phoneNumber)
    {
        return new PayFastCheckout(
            "https://sandbox.payfast.co.za/eng/process",
            new Dictionary<string, string>
            {
                ["m_payment_id"] = payment.Id.ToString(),
                ["amount"] = payment.Amount.ToString("0.00", CultureInfo.InvariantCulture),
                ["signature"] = "test-signature",
            });
    }

    public Task<PayFastValidationResult> ValidateNotificationAsync(
        IReadOnlyList<KeyValuePair<string, string>> fields,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ValidationResult);
    }
}
