using Microsoft.EntityFrameworkCore;
using Rydo.Application.Disputes;
using Rydo.Application.Realtime;
using Rydo.Domain.Disputes;
using Rydo.Domain.Identity;
using Rydo.Domain.Trips;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Disputes;

public sealed class DisputeService(
    RydoDbContext database,
    TimeProvider timeProvider,
    IRealtimeEventPublisher realtime) : IDisputeService
{
    public async Task<(DisputeDetailsResult Dispute, bool Created)> OpenAsync(
        Guid tripId,
        Guid userId,
        UserRole role,
        DisputeCategory category,
        string subject,
        string description,
        CancellationToken cancellationToken)
    {
        var trip = await database.Trips.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == tripId,
            cancellationToken) ?? throw new DisputeTripNotFoundException();
        EnsureParticipant(trip, userId, role);

        if (trip.Status is not TripStatus.Completed and not TripStatus.Cancelled)
        {
            throw new DisputeStateConflictException(
                "A dispute can only be opened after a trip is completed or cancelled.");
        }

        if (trip.DriverUserId is null)
        {
            throw new DisputeStateConflictException(
                "A trip must have an assigned Driver before it can be disputed.");
        }

        Dispute candidate;

        try
        {
            candidate = Dispute.Open(
                tripId,
                userId,
                category,
                subject,
                description,
                timeProvider.GetUtcNow());
        }
        catch (ArgumentException exception)
        {
            throw new DisputeValidationException(exception.Message);
        }

        var existing = await database.Disputes.AsNoTracking().SingleOrDefaultAsync(
            dispute => dispute.TripId == tripId,
            cancellationToken);

        if (existing is not null)
        {
            return (await ResolveExistingAsync(existing, candidate, cancellationToken), false);
        }

        database.Disputes.Add(candidate);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.Entry(candidate).State = EntityState.Detached;
            existing = await database.Disputes.AsNoTracking().SingleOrDefaultAsync(
                dispute => dispute.TripId == tripId,
                cancellationToken);

            if (existing is not null)
            {
                return (await ResolveExistingAsync(existing, candidate, cancellationToken), false);
            }

            throw;
        }

        var result = ToDetails(candidate, []);
        await realtime.PublishDisputeUpdatedAsync(
            result,
            trip.PassengerUserId,
            trip.DriverUserId,
            cancellationToken);
        return (result, true);
    }

    public async Task<IReadOnlyList<DisputeSummaryResult>> ListAsync(
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var query = ParticipantQuery(userId, role);
        return await query
            .OrderByDescending(dispute => dispute.UpdatedAt)
            .Select(dispute => new DisputeSummaryResult(
                dispute.Id,
                dispute.TripId,
                dispute.OpenedByUserId,
                dispute.Category,
                dispute.Subject,
                dispute.Status,
                dispute.CreatedAt,
                dispute.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<DisputeDetailsResult?> GetAsync(
        Guid disputeId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var dispute = await ParticipantQuery(userId, role)
            .SingleOrDefaultAsync(item => item.Id == disputeId, cancellationToken);

        if (dispute is null)
        {
            return null;
        }

        var messages = await GetMessagesAsync(dispute.Id, cancellationToken);
        return ToDetails(dispute, messages);
    }

    public async Task<DisputeMessageResult> AddMessageAsync(
        Guid disputeId,
        Guid userId,
        UserRole role,
        string body,
        CancellationToken cancellationToken)
    {
        var dispute = await ParticipantQuery(userId, role)
            .SingleOrDefaultAsync(item => item.Id == disputeId, cancellationToken)
            ?? throw new DisputeAccessException("The dispute is not visible to this user.");

        if (!dispute.AcceptsMessages)
        {
            throw new DisputeStateConflictException("A closed dispute cannot accept messages.");
        }

        DisputeMessage message;
        var createdAt = timeProvider.GetUtcNow();

        try
        {
            message = DisputeMessage.Create(
                disputeId,
                userId,
                body,
                createdAt);
        }
        catch (ArgumentException exception)
        {
            throw new DisputeValidationException(exception.Message);
        }

        database.Attach(dispute);
        dispute.RecordMessage(createdAt);
        database.DisputeMessages.Add(message);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DisputeStateConflictException(
                "The dispute changed before the message was saved. Refresh and try again.");
        }

        var result = ToMessageResult(message);
        var details = ToDetails(
            dispute,
            await GetMessagesAsync(dispute.Id, cancellationToken));
        var participants = await database.Trips.AsNoTracking()
            .Where(trip => trip.Id == dispute.TripId)
            .Select(trip => new { trip.PassengerUserId, trip.DriverUserId })
            .SingleAsync(cancellationToken);
        await realtime.PublishDisputeUpdatedAsync(
            details,
            participants.PassengerUserId,
            participants.DriverUserId,
            cancellationToken);
        return result;
    }

    private IQueryable<Dispute> ParticipantQuery(Guid userId, UserRole role)
    {
        return role switch
        {
            UserRole.Passenger =>
                from dispute in database.Disputes.AsNoTracking()
                join trip in database.Trips on dispute.TripId equals trip.Id
                where trip.PassengerUserId == userId
                select dispute,
            UserRole.Driver =>
                from dispute in database.Disputes.AsNoTracking()
                join trip in database.Trips on dispute.TripId equals trip.Id
                where trip.DriverUserId == userId
                select dispute,
            _ => database.Disputes.Where(_ => false),
        };
    }

    private async Task<DisputeDetailsResult> ResolveExistingAsync(
        Dispute existing,
        Dispute candidate,
        CancellationToken cancellationToken)
    {
        if (existing.OpenedByUserId != candidate.OpenedByUserId ||
            existing.Category != candidate.Category ||
            existing.Subject != candidate.Subject ||
            existing.Description != candidate.Description)
        {
            throw new DisputeStateConflictException("This trip already has a dispute.");
        }

        return ToDetails(existing, await GetMessagesAsync(existing.Id, cancellationToken));
    }

    private Task<List<DisputeMessageResult>> GetMessagesAsync(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        return database.DisputeMessages.AsNoTracking()
            .Where(message => message.DisputeId == disputeId)
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Select(message => new DisputeMessageResult(
                message.Id,
                message.AuthorUserId,
                message.Body,
                message.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static void EnsureParticipant(Trip trip, Guid userId, UserRole role)
    {
        var isPassenger = role == UserRole.Passenger && trip.PassengerUserId == userId;
        var isDriver = role == UserRole.Driver && trip.DriverUserId == userId;

        if (!isPassenger && !isDriver)
        {
            throw new DisputeAccessException("Only a trip participant can open a dispute.");
        }
    }

    private static DisputeDetailsResult ToDetails(
        Dispute dispute,
        IReadOnlyList<DisputeMessageResult> messages)
    {
        return new DisputeDetailsResult(
            dispute.Id,
            dispute.TripId,
            dispute.OpenedByUserId,
            dispute.Category,
            dispute.Subject,
            dispute.Description,
            dispute.Status,
            dispute.CreatedAt,
            dispute.UpdatedAt,
            dispute.ResolvedAt,
            dispute.Resolution,
            messages);
    }

    private static DisputeMessageResult ToMessageResult(DisputeMessage message)
    {
        return new DisputeMessageResult(
            message.Id,
            message.AuthorUserId,
            message.Body,
            message.CreatedAt);
    }
}
