using System.Net;
using System.Net.Http.Json;
using Rydo.Application.Admin;
using Rydo.Application.Payments;
using Rydo.Application.Trips;
using Rydo.Domain.Disputes;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Domain.Payments;
using Rydo.Domain.Trips;

namespace Rydo.Api.Tests;

public sealed class AdminApiTests
{
    [Fact]
    public async Task ConfiguredAdminCanLoginWhileMobileUsersCannotAccessOperations()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();

        var badLogin = await client.PostAsJsonAsync(
            "/api/v1/admin/auth/login",
            new
            {
                email = "admin@rydo.test",
                password = "wrong-test-password-value",
            });
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);

        var passenger = await TripTestClient.CreatePassengerAsync(client, "+27821201001");
        var forbidden = await client.GetAsync("/api/v1/admin/overview");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var firstAdminSession = await AdminTestClient.LoginAsync(client);
        var admin = await AdminTestClient.LoginAsync(client);
        Assert.Equal(UserRole.Admin, admin.User.Role);
        AuthenticationTestClient.UseBearerToken(client, firstAdminSession.AccessToken);
        var revokedSessionResponse = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, revokedSessionResponse.StatusCode);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);
        var overviewResponse = await client.GetAsync("/api/v1/admin/overview");
        overviewResponse.EnsureSuccessStatusCode();
        var overview = await AdminTestClient.ReadAsync<AdminOverviewResult>(overviewResponse);
        Assert.Equal(1, overview.PassengerCount);

        var usersResponse = await client.GetAsync("/api/v1/admin/users?role=Passenger&page=1&pageSize=10");
        usersResponse.EnsureSuccessStatusCode();
        var users = await AdminTestClient.ReadAsync<PagedResult<AdminUserResult>>(usersResponse);
        var listedPassenger = Assert.Single(users.Items);
        Assert.Equal(passenger.User.Id, listedPassenger.Id);
        Assert.Equal("Nandi Mokoena", listedPassenger.DisplayName);
    }

    [Fact]
    public async Task AdminCanApproveCompleteDriverReviewPacketAndAuditDecision()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var driver = await CreatePendingDriverAsync(client, "+27821202001", 4201);
        var admin = await AdminTestClient.LoginAsync(client);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);

        var response = await AdminTestClient.ReviewDriverAsync(
            client,
            driver.User.Id,
            approve: true);
        response.EnsureSuccessStatusCode();
        var reviewed = await AdminTestClient.ReadAsync<AdminDriverResult>(response);
        Assert.Equal(DriverOnboardingStatus.Approved, reviewed.Profile.OnboardingStatus);
        Assert.All(reviewed.Documents, document =>
            Assert.Equal(DriverDocumentReviewStatus.Approved, document.ReviewStatus));
        Assert.Equal(DriverVehicleReviewStatus.Approved, reviewed.Vehicle!.ReviewStatus);

        var pendingResponse = await client.GetAsync(
            "/api/v1/admin/drivers?status=PendingReview");
        pendingResponse.EnsureSuccessStatusCode();
        var pending = await AdminTestClient.ReadAsync<PagedResult<AdminDriverResult>>(
            pendingResponse);
        Assert.Empty(pending.Items);

        var auditResponse = await client.GetAsync("/api/v1/admin/audit");
        auditResponse.EnsureSuccessStatusCode();
        var audit = await AdminTestClient.ReadAsync<PagedResult<AdminAuditResult>>(
            auditResponse);
        var entry = Assert.Single(audit.Items);
        Assert.Equal("driver.approved", entry.Action);
        Assert.Equal(driver.User.Id, entry.EntityId);
        Assert.Equal(admin.User.Id, entry.AdminUserId);
    }

    [Fact]
    public async Task DriverRejectionRequiresReasonAndRejectsPendingPacket()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var driver = await CreatePendingDriverAsync(client, "+27821203001", 4301);
        var admin = await AdminTestClient.LoginAsync(client);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);

        var invalid = await AdminTestClient.ReviewDriverAsync(
            client,
            driver.User.Id,
            approve: false);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var response = await AdminTestClient.ReviewDriverAsync(
            client,
            driver.User.Id,
            approve: false,
            reason: "Identity details could not be verified.");
        response.EnsureSuccessStatusCode();
        var reviewed = await AdminTestClient.ReadAsync<AdminDriverResult>(response);
        Assert.Equal(DriverOnboardingStatus.Rejected, reviewed.Profile.OnboardingStatus);
        Assert.All(reviewed.Documents, document =>
            Assert.Equal(DriverDocumentReviewStatus.Rejected, document.ReviewStatus));
        Assert.Equal(DriverVehicleReviewStatus.Rejected, reviewed.Vehicle!.ReviewStatus);
        Assert.Equal(
            "Identity details could not be verified.",
            reviewed.Profile.RejectionReason);
    }

    [Fact]
    public async Task AdminCanRejectOneDocumentWithActionableFeedback()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var driver = await CreatePendingDriverAsync(client, "+27821203002", 4302);
        var admin = await AdminTestClient.LoginAsync(client);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);
        var driverResponse = await client.GetAsync($"/api/v1/admin/drivers/{driver.User.Id}");
        driverResponse.EnsureSuccessStatusCode();
        var pending = await AdminTestClient.ReadAsync<AdminDriverResult>(driverResponse);
        var identityDocument = pending.Documents.Single(document =>
            document.DocumentType == DriverDocumentType.IdentityDocument);

        var invalid = await AdminTestClient.ReviewDriverDocumentAsync(
            client,
            driver.User.Id,
            identityDocument.Id,
            approve: false);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var response = await AdminTestClient.ReviewDriverDocumentAsync(
            client,
            driver.User.Id,
            identityDocument.Id,
            approve: false,
            reason: "The image is blurred. Upload a clear photo showing all four corners.");
        response.EnsureSuccessStatusCode();
        var reviewed = await AdminTestClient.ReadAsync<AdminDriverResult>(response);
        Assert.Equal(DriverOnboardingStatus.Rejected, reviewed.Profile.OnboardingStatus);
        Assert.Contains("blurred", reviewed.Profile.RejectionReason);
        Assert.Equal(
            DriverDocumentReviewStatus.Rejected,
            reviewed.Documents.Single(document => document.Id == identityDocument.Id).ReviewStatus);
        Assert.All(
            reviewed.Documents.Where(document => document.Id != identityDocument.Id),
            document => Assert.Equal(
                DriverDocumentReviewStatus.PendingReview,
                document.ReviewStatus));
        Assert.Equal(DriverVehicleReviewStatus.PendingReview, reviewed.Vehicle!.ReviewStatus);

        var auditResponse = await client.GetAsync("/api/v1/admin/audit");
        var audit = await AdminTestClient.ReadAsync<PagedResult<AdminAuditResult>>(auditResponse);
        var entry = Assert.Single(audit.Items);
        Assert.Equal("driver-document.rejected", entry.Action);
        Assert.Equal(identityDocument.Id, entry.EntityId);
    }

    [Fact]
    public async Task AdminCanApproveIndividualDocumentWithoutClosingApplication()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var driver = await CreatePendingDriverAsync(client, "+27821203003", 4303);
        var admin = await AdminTestClient.LoginAsync(client);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);
        var driverResponse = await client.GetAsync($"/api/v1/admin/drivers/{driver.User.Id}");
        var pending = await AdminTestClient.ReadAsync<AdminDriverResult>(driverResponse);
        var licence = pending.Documents.Single(document =>
            document.DocumentType == DriverDocumentType.DriversLicense);

        var contentResponse = await client.GetAsync(
            $"/api/v1/admin/drivers/{driver.User.Id}/documents/{licence.Id}/content");
        contentResponse.EnsureSuccessStatusCode();
        Assert.Equal(licence.ContentType, contentResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await contentResponse.Content.ReadAsByteArrayAsync());

        var response = await AdminTestClient.ReviewDriverDocumentAsync(
            client,
            driver.User.Id,
            licence.Id,
            approve: true);
        response.EnsureSuccessStatusCode();
        var reviewed = await AdminTestClient.ReadAsync<AdminDriverResult>(response);

        Assert.Equal(DriverOnboardingStatus.PendingReview, reviewed.Profile.OnboardingStatus);
        Assert.Equal(
            DriverDocumentReviewStatus.Approved,
            reviewed.Documents.Single(document => document.Id == licence.Id).ReviewStatus);
    }

    [Fact]
    public async Task AdminCanInspectTripsPaymentsAndLiveDriverLocations()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "204", 4204);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var payment = await PaymentTestClient.CreateAsync(client, scenario.Trip.Id, "Cash");
        var onlineDriver = await AuthenticationTestClient.SignInAsync(
            client,
            "+27821204003",
            "Driver");
        await DriverMatchingTestClient.MakeEligibleAndOnlineAsync(
            factory,
            client,
            onlineDriver,
            4205,
            latitude: -26.2041,
            longitude: 28.0473);
        var admin = await AdminTestClient.LoginAsync(client);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);

        var tripsResponse = await client.GetAsync("/api/v1/admin/trips?status=Completed");
        tripsResponse.EnsureSuccessStatusCode();
        var trips = await AdminTestClient.ReadAsync<PagedResult<TripResult>>(tripsResponse);
        Assert.Equal(scenario.Trip.Id, Assert.Single(trips.Items).Id);

        var paymentsResponse = await client.GetAsync(
            "/api/v1/admin/payments?status=AwaitingPayment");
        paymentsResponse.EnsureSuccessStatusCode();
        var payments = await AdminTestClient.ReadAsync<PagedResult<PaymentResult>>(
            paymentsResponse);
        Assert.Equal(payment.Payment.Id, Assert.Single(payments.Items).Id);
        Assert.Equal(PaymentStatus.AwaitingPayment, payments.Items[0].Status);

        var liveResponse = await client.GetAsync("/api/v1/admin/drivers/live");
        liveResponse.EnsureSuccessStatusCode();
        var live = await AdminTestClient.ReadAsync<IReadOnlyList<AdminLiveDriverResult>>(
            liveResponse);
        var location = Assert.Single(live);
        Assert.Equal(onlineDriver.User.Id, location.DriverUserId);
        Assert.Equal(-26.2041, location.Latitude);
        Assert.Equal(28.0473, location.Longitude);
    }

    [Fact]
    public async Task AdminCanReviewAndResolveDisputeWithAuditTrail()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var scenario = await RatingTestClient.CompleteTripAsync(factory, client, "205", 4206);
        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var openResponse = await DisputeTestClient.OpenAsync(client, scenario.Trip.Id);
        var opened = await DisputeTestClient.ReadAsync(openResponse);
        var admin = await AdminTestClient.LoginAsync(client);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);

        var reviewResponse = await AdminTestClient.ReviewDisputeAsync(
            client,
            opened.Id,
            DisputeStatus.UnderReview);
        reviewResponse.EnsureSuccessStatusCode();
        var reviewing = await AdminTestClient.ReadAsync<AdminDisputeResult>(reviewResponse);
        Assert.Equal(DisputeStatus.UnderReview, reviewing.Dispute.Status);

        factory.Clock.Advance(TimeSpan.FromMinutes(5));
        var resolveResponse = await AdminTestClient.ReviewDisputeAsync(
            client,
            opened.Id,
            DisputeStatus.Resolved,
            "Fare reviewed and adjusted.");
        resolveResponse.EnsureSuccessStatusCode();
        var resolved = await AdminTestClient.ReadAsync<AdminDisputeResult>(resolveResponse);
        Assert.Equal(DisputeStatus.Resolved, resolved.Dispute.Status);
        Assert.Equal("Fare reviewed and adjusted.", resolved.Dispute.Resolution);
        Assert.NotNull(resolved.Dispute.ResolvedAt);

        var disputesResponse = await client.GetAsync(
            "/api/v1/admin/disputes?status=Resolved");
        disputesResponse.EnsureSuccessStatusCode();
        var disputes = await AdminTestClient.ReadAsync<PagedResult<AdminDisputeResult>>(
            disputesResponse);
        Assert.Equal(opened.Id, Assert.Single(disputes.Items).Dispute.Id);

        var auditResponse = await client.GetAsync("/api/v1/admin/audit");
        var audit = await AdminTestClient.ReadAsync<PagedResult<AdminAuditResult>>(
            auditResponse);
        Assert.Equal(2, audit.TotalCount);
        Assert.Contains(audit.Items, entry => entry.Action == "dispute.underreview");
        Assert.Contains(audit.Items, entry => entry.Action == "dispute.resolved");

        AuthenticationTestClient.UseBearerToken(client, scenario.Passenger.AccessToken);
        var messageResponse = await DisputeTestClient.AddMessageAsync(
            client,
            opened.Id,
            "This should be closed.");
        Assert.Equal(HttpStatusCode.Conflict, messageResponse.StatusCode);
    }

    [Fact]
    public async Task AdminPaginationIsValidated()
    {
        await using var factory = new AuthenticationApiFactory();
        using var client = factory.CreateClient();
        var admin = await AdminTestClient.LoginAsync(client);
        AuthenticationTestClient.UseBearerToken(client, admin.AccessToken);

        var response = await client.GetAsync("/api/v1/admin/users?page=0&pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<Rydo.Application.Authentication.TokenPairResult>
        CreatePendingDriverAsync(HttpClient client, string phoneNumber, int identifier)
    {
        var driver = await AuthenticationTestClient.SignInAsync(client, phoneNumber, "Driver");
        AuthenticationTestClient.UseBearerToken(client, driver.AccessToken);
        await DriverDocumentTestClient.CreateProfileAsync(client);
        await DriverDocumentTestClient.RegisterRequiredDocumentsAsync(client);
        await DriverVehicleTestClient.UpsertAsync(
            client,
            $"CA A{identifier:D4}",
            $"1HGCM82633B00{identifier:D4}");
        var submit = await client.PostAsync("/api/v1/drivers/me/onboarding/submit", null);
        submit.EnsureSuccessStatusCode();
        return driver;
    }
}
