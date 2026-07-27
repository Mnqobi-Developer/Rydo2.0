using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rydo.Domain.Disputes;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Api.Tests;

public sealed class DisputeTests
{
    [Fact]
    public async Task PassengerCanOpenAndDriverCanDiscussCompletedTripDispute()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "101", 4101);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);

        var openResponse = await DisputeTestClient.OpenAsync(
            client,
            scenario.Trip.Id,
            DisputeCategory.Route,
            "  Unexpected route  ",
            "  The trip took a much longer route.  ");
        Assert.Equal(HttpStatusCode.Created, openResponse.StatusCode);
        var dispute = await DisputeTestClient.ReadAsync(openResponse);
        Assert.Equal(scenario.Passenger.User.Id, dispute.OpenedByUserId);
        Assert.Equal("Unexpected route", dispute.Subject);
        Assert.Equal("The trip took a much longer route.", dispute.Description);
        Assert.Equal(DisputeStatus.Open, dispute.Status);
        Assert.Empty(dispute.Messages);

        AuthenticationTestClient.UseBearerToken(client, scenario.Driver.AccessToken);
        var listResponse = await client.GetAsync("/api/v1/disputes/me");
        listResponse.EnsureSuccessStatusCode();
        var listed = Assert.Single(await DisputeTestClient.ReadListAsync(listResponse));
        Assert.Equal(dispute.Id, listed.Id);

        factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var messageResponse = await DisputeTestClient.AddMessageAsync(
            client,
            dispute.Id,
            "  I followed the navigation diversion.  ");
        Assert.Equal(HttpStatusCode.Created, messageResponse.StatusCode);
        var message = await DisputeTestClient.ReadMessageAsync(messageResponse);
        Assert.Equal(scenario.Driver.User.Id, message.AuthorUserId);
        Assert.Equal("I followed the navigation diversion.", message.Body);

        var getResponse = await client.GetAsync($"/api/v1/disputes/{dispute.Id}");
        getResponse.EnsureSuccessStatusCode();
        var details = await DisputeTestClient.ReadAsync(getResponse);
        Assert.Equal(message, Assert.Single(details.Messages));
        Assert.Equal(message.CreatedAt, details.UpdatedAt);
    }

    [Fact]
    public async Task ActiveTripCannotBeDisputed()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27821102001");
        var trip = await TripTestClient.RequestAsync(client);
        var driver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27821102002",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            driver,
            4102);
        await DriverMatchingTestClient.MatchAsync(client, passenger.AccessToken, trip.Id);
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await TripTestClient.TransitionAsync(client, trip.Id, "accept");
        AuthenticationTestClient.UseBearerToken(client, passenger.AccessToken);

        var response = await DisputeTestClient.OpenAsync(client, trip.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task NonParticipantCannotOpenOrReadDispute()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "103", 4103);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var openResponse = await DisputeTestClient.OpenAsync(client, scenario.Trip.Id);
        var dispute = await DisputeTestClient.ReadAsync(openResponse);
        var outsider = await TripTestClient.CreatePassengerAsync(client, "+27821103003");
        AuthenticationTestClient.UseBearerToken(client, outsider.AccessToken);

        var outsiderOpenResponse = await DisputeTestClient.OpenAsync(client, scenario.Trip.Id);
        var getResponse = await client.GetAsync($"/api/v1/disputes/{dispute.Id}");
        var messageResponse = await DisputeTestClient.AddMessageAsync(
            client,
            dispute.Id,
            "I should not see this case.");
        var listResponse = await client.GetAsync("/api/v1/disputes/me");

        Assert.Equal(HttpStatusCode.Forbidden, outsiderOpenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, messageResponse.StatusCode);
        listResponse.EnsureSuccessStatusCode();
        Assert.Empty(await DisputeTestClient.ReadListAsync(listResponse));
    }

    [Fact]
    public async Task IdenticalOpenRetryIsIdempotentButSecondCaseConflicts()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "104", 4104);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);

        var firstResponse = await DisputeTestClient.OpenAsync(client, scenario.Trip.Id);
        var first = await DisputeTestClient.ReadAsync(firstResponse);
        var retryResponse = await DisputeTestClient.OpenAsync(
            client,
            scenario.Trip.Id,
            subject: "  Incorrect fare  ",
            description: "  The final fare does not match the quoted amount. ");
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        var retry = await DisputeTestClient.ReadAsync(retryResponse);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(first.TripId, retry.TripId);
        Assert.Equal(first.OpenedByUserId, retry.OpenedByUserId);
        Assert.Equal(first.Category, retry.Category);
        Assert.Equal(first.Subject, retry.Subject);
        Assert.Equal(first.Description, retry.Description);
        Assert.Empty(first.Messages);
        Assert.Empty(retry.Messages);

        AuthenticationTestClient.UseBearerToken(client, scenario.Driver.AccessToken);
        var secondResponse = await DisputeTestClient.OpenAsync(
            client,
            scenario.Trip.Id,
            DisputeCategory.PassengerConduct,
            "Passenger conduct",
            "A separate concern.");
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Theory]
    [InlineData(0, "Subject", "Description")]
    [InlineData(1, "   ", "Description")]
    [InlineData(1, "Subject", "   ")]
    public async Task InvalidOpeningDetailsAreRejected(
        int category,
        string subject,
        string description)
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(
            factory,
            client,
            $"11{category}",
            4110 + category);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);

        var response = await DisputeTestClient.OpenAsync(
            client,
            scenario.Trip.Id,
            (DisputeCategory)category,
            subject,
            description);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClosedDisputeCannotAcceptParticipantMessages()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "106", 4106);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var openResponse = await DisputeTestClient.OpenAsync(client, scenario.Trip.Id);
        var opened = await DisputeTestClient.ReadAsync(openResponse);

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<RydoDbContext>();
            var admin = UserAccount.Create(
                "+27821106003",
                UserRole.Admin,
                factory.Clock.GetUtcNow());
            database.Users.Add(admin);
            var dispute = await database.Disputes.SingleAsync(item => item.Id == opened.Id);
            dispute.Resolve(admin.Id, "Fare reviewed and corrected.", factory.Clock.GetUtcNow());
            await database.SaveChangesAsync();
        }

        var response = await DisputeTestClient.AddMessageAsync(
            client,
            opened.Id,
            "Trying to add another message.");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
