using Rydo.Domain.Disputes;
using Rydo.Domain.Identity;

namespace Rydo.Application.Disputes;

public sealed record DisputeSummaryResult(
    Guid Id,
    Guid TripId,
    Guid OpenedByUserId,
    DisputeCategory Category,
    string Subject,
    DisputeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record DisputeMessageResult(
    Guid Id,
    Guid AuthorUserId,
    string Body,
    DateTimeOffset CreatedAt);

public sealed record DisputeDetailsResult(
    Guid Id,
    Guid TripId,
    Guid OpenedByUserId,
    DisputeCategory Category,
    string Subject,
    string Description,
    DisputeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    string? Resolution,
    IReadOnlyList<DisputeMessageResult> Messages);

public interface IDisputeService
{
    Task<(DisputeDetailsResult Dispute, bool Created)> OpenAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        DisputeCategory category,
        string subject,
        string description,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DisputeSummaryResult>> ListAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken);

    Task<DisputeDetailsResult?> GetAsync(
        Guid disputeId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken);

    Task<DisputeMessageResult> AddMessageAsync(
        Guid disputeId,
        Guid userId,
        UserRole role,
        string body,
        CancellationToken cancellationToken);
}

public sealed class DisputeTripNotFoundException : Exception;

public sealed class DisputeAccessException(string message) : Exception(message);

public sealed class DisputeStateConflictException(string message) : Exception(message);

public sealed class DisputeValidationException(string message) : Exception(message);
