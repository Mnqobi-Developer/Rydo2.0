using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rydo.Application.Payments;
using Rydo.Domain.Payments;
using Rydo.Infrastructure.Payments;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Api.Tests;

public sealed class PaymentTests
{
    [Fact]
    public async Task PaymentRequiresServerFinalizedFare()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001301");
        var trip = await TripTestClient.RequestAsync(client);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/payments",
            new { method = "Cash" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CashPaymentIsIdempotentAndVisibleToPassenger()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001302");
        var trip = await TripTestClient.RequestAsync(client);
        await PaymentTestClient.FinalizeFareAsync(factory, trip.Id);

        var first = await PaymentTestClient.CreateAsync(client, trip.Id, "Cash");
        var second = await PaymentTestClient.CreateAsync(client, trip.Id, "Cash");
        var getResponse = await client.GetAsync($"/api/v1/trips/{trip.Id}/payment");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await PaymentTestClient.ReadAsync(getResponse);

        Assert.Equal(first.Payment, second.Payment);
        Assert.Equal(first.Payment, fetched);
        Assert.Equal(125.50m, fetched.Amount);
        Assert.Equal("ZAR", fetched.Currency);
        Assert.Equal(PaymentMethod.Cash, fetched.Method);
        Assert.Equal(PaymentStatus.AwaitingPayment, fetched.Status);
        Assert.Null(first.PayFastCheckout);
    }

    [Fact]
    public async Task AssignedDriverCanConfirmCashAfterCompletingTrip()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27820001303");
        var trip = await TripTestClient.RequestAsync(client);
        await PaymentTestClient.FinalizeFareAsync(factory, trip.Id, 99.99m);
        var payment = await PaymentTestClient.CreateAsync(client, trip.Id, "Cash");
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27820001304",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            driver,
            1304);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        await TripTestClient.TransitionAsync(client, trip.Id, "arrive");
        await TripTestClient.TransitionAsync(client, trip.Id, "start");
        await TripTestClient.TransitionAsync(client, trip.Id, "complete");

        var response = await client.PostAsync(
            $"/api/v1/payments/{payment.Payment.Id}/cash/confirm",
            null);
        response.EnsureSuccessStatusCode();
        var paid = await PaymentTestClient.ReadAsync(response);

        Assert.Equal(PaymentStatus.Paid, paid.Status);
        Assert.NotNull(paid.PaidAt);
        Assert.Null(paid.ProviderPaymentId);
    }

    [Fact]
    public async Task PayFastCheckoutIsUnavailableWithoutDashboardConfiguration()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001305");
        var trip = await TripTestClient.RequestAsync(client);
        await PaymentTestClient.FinalizeFareAsync(factory, trip.Id);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/trips/{trip.Id}/payments",
            new { method = "PayFast" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
        Assert.False(await database.Payments.AnyAsync());
    }

    [Fact]
    public async Task ConfiguredPayFastReturnsHostedCheckoutFields()
    {
        var gateway = new FakePayFastGateway();
        await using var factory = PaymentTestClient.CreatePayFastFactory(gateway);
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001306");
        var trip = await TripTestClient.RequestAsync(client);
        await PaymentTestClient.FinalizeFareAsync(factory, trip.Id, 150m);

        var created = await PaymentTestClient.CreateAsync(client, trip.Id, "PayFast");

        Assert.NotNull(created.PayFastCheckout);
        Assert.Equal(
            "https://sandbox.payfast.co.za/eng/process",
            created.PayFastCheckout.ProcessUrl);
        Assert.Equal("150.00", created.PayFastCheckout.Fields["amount"]);
        Assert.Equal(created.Payment.Id.ToString(),
            created.PayFastCheckout.Fields["m_payment_id"]);
    }

    [Fact]
    public async Task ValidPayFastNotificationMarksPaymentPaidAndIsIdempotent()
    {
        var gateway = new FakePayFastGateway();
        await using var factory = PaymentTestClient.CreatePayFastFactory(gateway);
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001307");
        var trip = await TripTestClient.RequestAsync(client);
        await PaymentTestClient.FinalizeFareAsync(factory, trip.Id, 200m);
        var created = await PaymentTestClient.CreateAsync(client, trip.Id, "PayFast");
        var fields = NotificationFields(created.Payment, "PF-1001", 200m);

        var firstResponse = await client.PostAsync(
            "/api/v1/payments/payfast/notify",
            new FormUrlEncodedContent(fields));
        var secondResponse = await client.PostAsync(
            "/api/v1/payments/payfast/notify",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        AuthenticationTestClient.UseBearerToken(
            client,
            (await AuthenticationTestClient.SignInAsync(
                client,
                "+27820001307",
                "Passenger")).AccessToken);
        var paymentResponse = await client.GetAsync($"/api/v1/trips/{trip.Id}/payment");
        paymentResponse.EnsureSuccessStatusCode();
        var paid = await PaymentTestClient.ReadAsync(paymentResponse);
        Assert.Equal(PaymentStatus.Paid, paid.Status);
        Assert.Equal("PF-1001", paid.ProviderPaymentId);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
        Assert.Equal(2, await database.PaymentEvents.CountAsync(item => item.IsValid));
    }

    [Fact]
    public async Task AmountMismatchIsAuditedWithoutMarkingPaymentPaid()
    {
        var gateway = new FakePayFastGateway();
        await using var factory = PaymentTestClient.CreatePayFastFactory(gateway);
        using var client = factory.CreateClient();
        await TripTestClient.CreatePassengerAsync(client, "+27820001308");
        var trip = await TripTestClient.RequestAsync(client);
        await PaymentTestClient.FinalizeFareAsync(factory, trip.Id, 250m);
        var created = await PaymentTestClient.CreateAsync(client, trip.Id, "PayFast");

        var response = await client.PostAsync(
            "/api/v1/payments/payfast/notify",
            new FormUrlEncodedContent(NotificationFields(
                created.Payment,
                "PF-1002",
                249m)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
        var payment = await database.Payments.SingleAsync();
        var paymentEvent = await database.PaymentEvents.SingleAsync();
        Assert.Equal(PaymentStatus.AwaitingPayment, payment.Status);
        Assert.False(paymentEvent.IsValid);
        Assert.Contains("amount", paymentEvent.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("hello world", "hello+world")]
    [InlineData("https://rydo.co.za/a?b=1", "https%3A%2F%2Frydo.co.za%2Fa%3Fb%3D1")]
    [InlineData("value~test", "value%7Etest")]
    public void PayFastEncodingMatchesCustomIntegrationRules(string value, string expected)
    {
        Assert.Equal(expected, PayFastSignature.Encode(value));
    }

    [Fact]
    public void PayFastSignaturePreservesDocumentedFieldOrder()
    {
        var fields = new Dictionary<string, string>
        {
            ["merchant_id"] = "10000100",
            ["merchant_key"] = "46f0cd694581a",
            ["amount"] = "100.00",
            ["item_name"] = "Test Product",
        };

        var signature = PayFastSignature.Generate(fields, "jt7NOE43FZPn");

        Assert.Equal("065ea401401305adffa928319e0be82d", signature);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> NotificationFields(
        PaymentResult payment,
        string providerPaymentId,
        decimal amount)
    {
        return
        [
            new("m_payment_id", payment.Id.ToString()),
            new("pf_payment_id", providerPaymentId),
            new("payment_status", "COMPLETE"),
            new("amount_gross", amount.ToString("0.00", CultureInfo.InvariantCulture)),
            new("merchant_id", "test-merchant"),
            new("signature", "test-signature"),
        ];
    }
}
