using Microsoft.EntityFrameworkCore;
using Rydo.Application.Admin;
using Rydo.Application.Disputes;
using Rydo.Application.Drivers;
using Rydo.Application.Payments;
using Rydo.Application.Realtime;
using Rydo.Application.Trips;
using Rydo.Domain.Admin;
using Rydo.Domain.Disputes;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Domain.Payments;
using Rydo.Domain.Trips;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Admin;

public sealed class AdminOperationsService(
    RydoDbContext database,
    TimeProvider timeProvider,
    IRealtimeEventPublisher realtime,
    Rydo.Infrastructure.Drivers.IDriverDocumentObjectStorage documentStorage) : IAdminOperationsService
{
    private static readonly DriverDocumentType[] RequiredDocumentTypes =
    [
        DriverDocumentType.IdentityDocument,
        DriverDocumentType.DriversLicense,
        DriverDocumentType.ProfessionalDrivingPermit,
    ];

    public async Task<AdminOverviewResult> GetOverviewAsync(
        CancellationToken cancellationToken)
    {
        var passengerCount = await database.Users.CountAsync(
            user => user.Role == UserRole.Passenger,
            cancellationToken);
        var driverCount = await database.Users.CountAsync(
            user => user.Role == UserRole.Driver,
            cancellationToken);
        var pendingDriverCount = await database.DriverProfiles.CountAsync(
            profile => profile.OnboardingStatus == DriverOnboardingStatus.PendingReview,
            cancellationToken);
        var activeTripCount = await database.Trips.CountAsync(
            trip => trip.Status != TripStatus.Completed && trip.Status != TripStatus.Cancelled,
            cancellationToken);
        var awaitingPaymentCount = await database.Payments.CountAsync(
            payment => payment.Status == PaymentStatus.AwaitingPayment,
            cancellationToken);
        var openDisputeCount = await database.Disputes.CountAsync(
            dispute => dispute.Status == DisputeStatus.Open ||
                dispute.Status == DisputeStatus.UnderReview,
            cancellationToken);
        var onlineDriverCount = await database.DriverAvailability.CountAsync(
            availability => availability.IsOnline,
            cancellationToken);

        return new AdminOverviewResult(
            passengerCount,
            driverCount,
            pendingDriverCount,
            activeTripCount,
            awaitingPaymentCount,
            openDisputeCount,
            onlineDriverCount);
    }

    public async Task<PagedResult<AdminUserResult>> ListUsersAsync(
        UserRole? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = database.Users.AsNoTracking();

        if (role is not null)
        {
            query = query.Where(user => user.Role == role);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var userIds = users.Select(user => user.Id).ToArray();
        var passengerNames = await database.PassengerProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => profile.FirstName + " " + profile.LastName,
                cancellationToken);
        var driverNames = await database.DriverProfiles.AsNoTracking()
            .Where(profile => userIds.Contains(profile.UserId))
            .ToDictionaryAsync(
                profile => profile.UserId,
                profile => profile.FirstName + " " + profile.LastName,
                cancellationToken);
        var results = users.Select(user => new AdminUserResult(
            user.Id,
            user.PhoneNumber,
            user.Role,
            user.IsActive,
            passengerNames.GetValueOrDefault(user.Id) ?? driverNames.GetValueOrDefault(user.Id),
            user.CreatedAt)).ToList();

        return new PagedResult<AdminUserResult>(results, page, pageSize, totalCount);
    }

    public async Task<PagedResult<AdminDriverResult>> ListDriversAsync(
        DriverOnboardingStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = database.DriverProfiles.AsNoTracking();

        if (status is not null)
        {
            query = query.Where(profile => profile.OnboardingStatus == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var driverIds = await query
            .OrderByDescending(profile => profile.UpdatedAt)
            .ThenBy(profile => profile.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(profile => profile.UserId)
            .ToListAsync(cancellationToken);
        var results = new List<AdminDriverResult>(driverIds.Count);

        foreach (var driverId in driverIds)
        {
            var driver = await GetDriverAsync(driverId, cancellationToken);

            if (driver is not null)
            {
                results.Add(driver);
            }
        }

        return new PagedResult<AdminDriverResult>(results, page, pageSize, totalCount);
    }

    public async Task<AdminDriverResult?> GetDriverAsync(
        Guid driverUserId,
        CancellationToken cancellationToken)
    {
        var profile = await database.DriverProfiles.AsNoTracking()
            .Where(item => item.UserId == driverUserId)
            .Select(item => new DriverProfileResult(
                item.UserId,
                item.FirstName,
                item.LastName,
                item.Email,
                item.OnboardingStatus,
                item.OnboardingStatus == DriverOnboardingStatus.Draft ||
                    item.OnboardingStatus == DriverOnboardingStatus.Rejected,
                item.CreatedAt,
                item.UpdatedAt,
                item.SubmittedAt,
                item.ReviewedAt,
                item.RejectionReason))
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var documents = await database.DriverDocuments.AsNoTracking()
            .Where(document => document.DriverUserId == driverUserId &&
                document.SupersededAt == null)
            .OrderBy(document => document.DocumentType)
            .Select(document => new DriverDocumentResult(
                document.Id,
                document.DocumentType,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256,
                document.ReviewStatus,
                document.UploadedAt,
                document.ReviewedAt,
                document.RejectionReason))
            .ToListAsync(cancellationToken);
        var vehicle = await database.DriverVehicles.AsNoTracking()
            .Where(item => item.DriverUserId == driverUserId)
            .Select(item => new DriverVehicleResult(
                item.Id,
                item.DriverUserId,
                item.Make,
                item.Model,
                item.Year,
                item.Color,
                item.RegistrationNumber,
                item.VehicleIdentificationNumber,
                item.SeatCapacity,
                item.ReviewStatus,
                item.CreatedAt,
                item.UpdatedAt,
                item.ReviewedAt,
                item.RejectionReason))
            .SingleOrDefaultAsync(cancellationToken);

        return new AdminDriverResult(profile, documents, vehicle);
    }

    public async Task<DriverDocumentContentResult?> OpenDriverDocumentAsync(
        Guid driverUserId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await database.DriverDocuments.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.Id == documentId &&
                item.DriverUserId == driverUserId &&
                item.SupersededAt == null,
                cancellationToken);
        if (document is null) return null;

        try
        {
            var content = await documentStorage.OpenReadAsync(
                document.StorageObjectKey,
                cancellationToken);
            return new DriverDocumentContentResult(
                new DriverDocumentResult(
                    document.Id,
                    document.DocumentType,
                    document.OriginalFileName,
                    document.ContentType,
                    document.SizeBytes,
                    document.Sha256,
                    document.ReviewStatus,
                    document.UploadedAt,
                    document.ReviewedAt,
                    document.RejectionReason),
                content);
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException)
        {
            throw new DriverDocumentStorageException(
                "The protected document is temporarily unavailable.",
                exception);
        }
    }

    public async Task<AdminDriverResult> ReviewDriverDocumentAsync(
        Guid adminUserId,
        Guid driverUserId,
        Guid documentId,
        bool approve,
        string? reason,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        var profile = await database.DriverProfiles.SingleOrDefaultAsync(
            item => item.UserId == driverUserId,
            cancellationToken) ?? throw new AdminResourceNotFoundException();
        var document = await database.DriverDocuments.SingleOrDefaultAsync(
            item => item.Id == documentId &&
                item.DriverUserId == driverUserId &&
                item.SupersededAt == null,
            cancellationToken) ?? throw new AdminResourceNotFoundException();

        if (profile.OnboardingStatus != DriverOnboardingStatus.PendingReview)
        {
            throw new AdminOperationConflictException(
                "Only documents in a Driver application pending review can be reviewed.");
        }

        var now = timeProvider.GetUtcNow();
        try
        {
            if (approve)
            {
                document.Approve(now);
            }
            else
            {
                var normalizedReason = NormalizeRequired(
                    reason,
                    500,
                    "A document rejection reason is required.");
                document.Reject(normalizedReason, now);
                profile.Reject($"{document.DocumentType}: {normalizedReason}", now);
            }
        }
        catch (ArgumentException exception)
        {
            throw new AdminOperationValidationException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new AdminOperationConflictException(exception.Message);
        }

        database.AdminAuditLogs.Add(AdminAuditLog.Create(
            adminUserId,
            approve ? "driver-document.approved" : "driver-document.rejected",
            "driver-document",
            documentId,
            approve
                ? $"driver={driverUserId}; decision=Approved"
                : $"driver={driverUserId}; decision=Rejected; reason={reason!.Trim()}",
            now));
        await SaveMutationAsync(cancellationToken);
        var result = (await GetDriverAsync(driverUserId, cancellationToken))!;
        await realtime.PublishDriverReviewUpdatedAsync(
            new DriverReviewChangedResult(
                driverUserId,
                result.Profile.OnboardingStatus,
                result.Profile.RejectionReason,
                result.Profile.UpdatedAt),
            cancellationToken);
        await realtime.PublishAdminOperationsChangedAsync(
            new AdminOperationsChangedResult(
                "driver-document",
                documentId,
                approve ? "approved" : "rejected",
                now),
            cancellationToken);
        return result;
    }

    public async Task<AdminDriverResult> ReviewDriverAsync(
        Guid adminUserId,
        Guid driverUserId,
        bool approve,
        string? reason,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        var profile = await database.DriverProfiles.SingleOrDefaultAsync(
            item => item.UserId == driverUserId,
            cancellationToken) ?? throw new AdminResourceNotFoundException();

        if (profile.OnboardingStatus != DriverOnboardingStatus.PendingReview)
        {
            throw new AdminOperationConflictException(
                "Only a Driver pending review can be reviewed.");
        }

        var documents = await database.DriverDocuments
            .Where(document => document.DriverUserId == driverUserId &&
                document.SupersededAt == null)
            .ToListAsync(cancellationToken);
        var vehicle = await database.DriverVehicles.SingleOrDefaultAsync(
            item => item.DriverUserId == driverUserId,
            cancellationToken);
        var now = timeProvider.GetUtcNow();

        try
        {
            if (approve)
            {
                var missing = RequiredDocumentTypes.Except(
                    documents.Where(document =>
                            document.ReviewStatus != DriverDocumentReviewStatus.Rejected)
                        .Select(document => document.DocumentType)).ToArray();

                if (missing.Length > 0 || vehicle is null ||
                    vehicle.ReviewStatus == DriverVehicleReviewStatus.Rejected)
                {
                    throw new AdminOperationConflictException(
                        "The Driver review packet is incomplete or contains rejected items.");
                }

                foreach (var document in documents.Where(document =>
                    document.ReviewStatus == DriverDocumentReviewStatus.PendingReview))
                {
                    document.Approve(now);
                }

                if (vehicle.ReviewStatus == DriverVehicleReviewStatus.PendingReview)
                {
                    vehicle.Approve(now);
                }

                profile.Approve(now);
            }
            else
            {
                var normalizedReason = NormalizeRequired(
                    reason,
                    500,
                    "A rejection reason is required.");

                foreach (var document in documents.Where(document =>
                    document.ReviewStatus == DriverDocumentReviewStatus.PendingReview))
                {
                    document.Reject(normalizedReason, now);
                }

                if (vehicle?.ReviewStatus == DriverVehicleReviewStatus.PendingReview)
                {
                    vehicle.Reject(normalizedReason, now);
                }

                profile.Reject(normalizedReason, now);
            }
        }
        catch (ArgumentException exception)
        {
            throw new AdminOperationValidationException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new AdminOperationConflictException(exception.Message);
        }

        database.AdminAuditLogs.Add(AdminAuditLog.Create(
            adminUserId,
            approve ? "driver.approved" : "driver.rejected",
            "driver",
            driverUserId,
            approve ? "decision=Approved" : $"decision=Rejected; reason={reason!.Trim()}",
            now));
        await SaveMutationAsync(cancellationToken);
        var result = (await GetDriverAsync(driverUserId, cancellationToken))!;
        await realtime.PublishDriverReviewUpdatedAsync(
            new DriverReviewChangedResult(
                driverUserId,
                result.Profile.OnboardingStatus,
                result.Profile.RejectionReason,
                result.Profile.UpdatedAt),
            cancellationToken);
        await realtime.PublishAdminOperationsChangedAsync(
            new AdminOperationsChangedResult("driver", driverUserId, "reviewed", now),
            cancellationToken);
        return result;
    }

    public async Task<PagedResult<TripResult>> ListTripsAsync(
        TripStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = database.Trips.AsNoTracking();
        if (status is not null) query = query.Where(trip => trip.Status == status);
        var totalCount = await query.CountAsync(cancellationToken);
        var itemsQuery = query
            .OrderByDescending(trip => trip.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
        var items = await ProjectTrips(itemsQuery).ToListAsync(cancellationToken);
        return new PagedResult<TripResult>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<PaymentResult>> ListPaymentsAsync(
        PaymentStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = database.Payments.AsNoTracking();
        if (status is not null) query = query.Where(payment => payment.Status == status);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(payment => payment.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(payment => new PaymentResult(
                payment.Id,
                payment.TripId,
                payment.PassengerUserId,
                payment.Method,
                payment.Status,
                payment.Amount,
                payment.Currency,
                payment.ProviderPaymentId,
                payment.CreatedAt,
                payment.UpdatedAt,
                payment.PaidAt,
                payment.FailedAt,
                payment.FailureReason,
                payment.Version))
            .ToListAsync(cancellationToken);
        return new PagedResult<PaymentResult>(items, page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<AdminLiveDriverResult>> ListLiveDriversAsync(
        CancellationToken cancellationToken)
    {
        return await (
            from availability in database.DriverAvailability.AsNoTracking()
            join profile in database.DriverProfiles.AsNoTracking()
                on availability.DriverUserId equals profile.UserId
            where availability.IsOnline
            orderby availability.LocationUpdatedAt descending
            select new AdminLiveDriverResult(
                availability.DriverUserId,
                profile.FirstName + " " + profile.LastName,
                availability.Latitude,
                availability.Longitude,
                availability.LocationUpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AdminDisputeResult>> ListDisputesAsync(
        DisputeStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = database.Disputes.AsNoTracking();
        if (status is not null) query = query.Where(dispute => dispute.Status == status);
        var totalCount = await query.CountAsync(cancellationToken);
        var disputeIds = await query.OrderByDescending(dispute => dispute.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(dispute => dispute.Id)
            .ToListAsync(cancellationToken);
        var items = new List<AdminDisputeResult>(disputeIds.Count);

        foreach (var disputeId in disputeIds)
        {
            items.Add(await GetAdminDisputeAsync(disputeId, cancellationToken));
        }

        return new PagedResult<AdminDisputeResult>(items, page, pageSize, totalCount);
    }

    public async Task<AdminDisputeResult> ReviewDisputeAsync(
        Guid adminUserId,
        Guid disputeId,
        DisputeStatus status,
        string? resolution,
        CancellationToken cancellationToken)
    {
        await EnsureAdminAsync(adminUserId, cancellationToken);
        var dispute = await database.Disputes.SingleOrDefaultAsync(
            item => item.Id == disputeId,
            cancellationToken) ?? throw new AdminResourceNotFoundException();
        var now = timeProvider.GetUtcNow();

        try
        {
            switch (status)
            {
                case DisputeStatus.UnderReview:
                    if (!string.IsNullOrWhiteSpace(resolution))
                    {
                        throw new AdminOperationValidationException(
                            "An under-review dispute cannot have a resolution.");
                    }

                    dispute.MarkUnderReview(now);
                    break;
                case DisputeStatus.Resolved:
                    dispute.Resolve(
                        adminUserId,
                        NormalizeRequired(resolution, 2000, "A resolution is required."),
                        now);
                    break;
                case DisputeStatus.Rejected:
                    dispute.Reject(
                        adminUserId,
                        NormalizeRequired(resolution, 2000, "A resolution is required."),
                        now);
                    break;
                default:
                    throw new AdminOperationValidationException(
                        "Admin dispute status must be UnderReview, Resolved, or Rejected.");
            }
        }
        catch (ArgumentException exception)
        {
            throw new AdminOperationValidationException(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new AdminOperationConflictException(exception.Message);
        }

        database.AdminAuditLogs.Add(AdminAuditLog.Create(
            adminUserId,
            $"dispute.{status.ToString().ToLowerInvariant()}",
            "dispute",
            disputeId,
            $"status={status}",
            now));
        await SaveMutationAsync(cancellationToken);
        var result = await GetAdminDisputeAsync(disputeId, cancellationToken);
        await realtime.PublishDisputeUpdatedAsync(
            result.Dispute,
            result.PassengerUserId,
            result.DriverUserId,
            cancellationToken);
        await realtime.PublishAdminOperationsChangedAsync(
            new AdminOperationsChangedResult("dispute", disputeId, "reviewed", now),
            cancellationToken);
        return result;
    }

    public async Task<PagedResult<AdminAuditResult>> ListAuditAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var totalCount = await database.AdminAuditLogs.CountAsync(cancellationToken);
        var items = await database.AdminAuditLogs.AsNoTracking()
            .OrderByDescending(audit => audit.CreatedAt)
            .ThenByDescending(audit => audit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(audit => new AdminAuditResult(
                audit.Id,
                audit.AdminUserId,
                audit.Action,
                audit.EntityType,
                audit.EntityId,
                audit.Details,
                audit.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<AdminAuditResult>(items, page, pageSize, totalCount);
    }

    private async Task<AdminDisputeResult> GetAdminDisputeAsync(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        var data = await (
            from dispute in database.Disputes.AsNoTracking()
            join trip in database.Trips.AsNoTracking() on dispute.TripId equals trip.Id
            where dispute.Id == disputeId
            select new { Dispute = dispute, trip.PassengerUserId, trip.DriverUserId })
            .SingleAsync(cancellationToken);
        var messages = await database.DisputeMessages.AsNoTracking()
            .Where(message => message.DisputeId == disputeId)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(message => new DisputeMessageResult(
                message.Id,
                message.AuthorUserId,
                message.Body,
                message.CreatedAt))
            .ToListAsync(cancellationToken);
        var details = new DisputeDetailsResult(
            data.Dispute.Id,
            data.Dispute.TripId,
            data.Dispute.OpenedByUserId,
            data.Dispute.Category,
            data.Dispute.Subject,
            data.Dispute.Description,
            data.Dispute.Status,
            data.Dispute.CreatedAt,
            data.Dispute.UpdatedAt,
            data.Dispute.ResolvedAt,
            data.Dispute.Resolution,
            messages);
        return new AdminDisputeResult(details, data.PassengerUserId, data.DriverUserId);
    }

    private async Task EnsureAdminAsync(
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        if (!await database.Users.AnyAsync(user =>
            user.Id == adminUserId && user.Role == UserRole.Admin && user.IsActive,
            cancellationToken))
        {
            throw new AdminResourceNotFoundException();
        }
    }

    private async Task SaveMutationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AdminOperationConflictException(
                "The record changed during review. Refresh and try again.");
        }
    }

    private static string NormalizeRequired(
        string? value,
        int maximumLength,
        string message)
    {
        var normalized = value?.Trim();
        return normalized is { Length: > 0 } && normalized.Length <= maximumLength
            ? normalized
            : throw new AdminOperationValidationException(message);
    }

    private static IQueryable<TripResult> ProjectTrips(IQueryable<Trip> query)
    {
        return query.Select(trip => new TripResult(
            trip.Id,
            trip.PassengerUserId,
            trip.DriverUserId,
            trip.PickupAddress,
            trip.PickupLatitude,
            trip.PickupLongitude,
            trip.DestinationAddress,
            trip.DestinationLatitude,
            trip.DestinationLongitude,
            trip.FareQuoteId,
            trip.RideCategory,
            trip.EstimatedFareAmount,
            trip.FareCurrency,
            trip.PricingVersion,
            trip.Status,
            trip.RequestedAt,
            trip.UpdatedAt,
            trip.AcceptedAt,
            trip.DriverArrivedAt,
            trip.StartedAt,
            trip.CompletedAt,
            trip.CancelledAt,
            trip.CancelledByUserId,
            trip.CancellationReason,
            trip.FinalFareAmount,
            trip.Version));
    }
}
