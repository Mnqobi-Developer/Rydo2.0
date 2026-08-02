using Rydo.Application.Authentication;
using Rydo.Application.Disputes;
using Rydo.Application.Drivers;
using Rydo.Application.Payments;
using Rydo.Application.Trips;
using Rydo.Domain.Disputes;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Domain.Payments;
using Rydo.Domain.Trips;

namespace Rydo.Application.Admin;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminOverviewResult(
    int PassengerCount,
    int DriverCount,
    int PendingDriverCount,
    int ActiveTripCount,
    int AwaitingPaymentCount,
    int OpenDisputeCount,
    int OnlineDriverCount);

public sealed record AdminUserResult(
    Guid Id,
    string PhoneNumber,
    UserRole Role,
    bool IsActive,
    string? DisplayName,
    DateTimeOffset CreatedAt);

public sealed record AdminDriverResult(
    DriverProfileResult Profile,
    IReadOnlyList<DriverDocumentResult> Documents,
    DriverVehicleResult? Vehicle);

public sealed record AdminLiveDriverResult(
    Guid DriverUserId,
    string DisplayName,
    double Latitude,
    double Longitude,
    DateTimeOffset? LocationUpdatedAt);

public sealed record AdminDisputeResult(
    DisputeDetailsResult Dispute,
    Guid PassengerUserId,
    Guid? DriverUserId);

public sealed record AdminAuditResult(
    Guid Id,
    Guid AdminUserId,
    string Action,
    string EntityType,
    Guid EntityId,
    string Details,
    DateTimeOffset CreatedAt);

public interface IAdminAuthenticationService
{
    Task<TokenPairResult?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}

public interface IAdminOperationsService
{
    Task<AdminOverviewResult> GetOverviewAsync(CancellationToken cancellationToken);
    Task<PagedResult<AdminUserResult>> ListUsersAsync(UserRole? role, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AdminDriverResult>> ListDriversAsync(DriverOnboardingStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminDriverResult?> GetDriverAsync(Guid driverUserId, CancellationToken cancellationToken);
    Task<DriverDocumentContentResult?> OpenDriverDocumentAsync(Guid driverUserId, Guid documentId, CancellationToken cancellationToken);
    Task<AdminDriverResult> ReviewDriverDocumentAsync(Guid adminUserId, Guid driverUserId, Guid documentId, bool approve, string? reason, CancellationToken cancellationToken);
    Task<AdminDriverResult> ReviewDriverAsync(Guid adminUserId, Guid driverUserId, bool approve, string? reason, CancellationToken cancellationToken);
    Task<PagedResult<TripResult>> ListTripsAsync(TripStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<PaymentResult>> ListPaymentsAsync(PaymentStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminLiveDriverResult>> ListLiveDriversAsync(CancellationToken cancellationToken);
    Task<PagedResult<AdminDisputeResult>> ListDisputesAsync(DisputeStatus? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<AdminDisputeResult> ReviewDisputeAsync(Guid adminUserId, Guid disputeId, DisputeStatus status, string? resolution, CancellationToken cancellationToken);
    Task<PagedResult<AdminAuditResult>> ListAuditAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class AdminAccessUnavailableException : Exception;
public sealed class AdminResourceNotFoundException : Exception;
public sealed class AdminOperationConflictException(string message) : Exception(message);
public sealed class AdminOperationValidationException(string message) : Exception(message);
