using Microsoft.EntityFrameworkCore;
using Rydo.Application.Passengers;
using Rydo.Domain.Identity;
using Rydo.Domain.Passengers;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Passengers;

public sealed class PassengerProfileService(
    RydoDbContext database,
    TimeProvider timeProvider) : IPassengerProfileService
{
    public async Task<PassengerProfileResult?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsActivePassengerAsync(userId, cancellationToken))
        {
            return null;
        }

        return await database.PassengerProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => new PassengerProfileResult(
                profile.UserId,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                profile.CreatedAt,
                profile.UpdatedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PassengerProfileResult?> UpsertAsync(
        Guid userId,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken)
    {
        if (!await IsActivePassengerAsync(userId, cancellationToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var profile = await database.PassengerProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = PassengerProfile.Create(userId, firstName, lastName, email, now);
            database.PassengerProfiles.Add(profile);
        }
        else
        {
            profile.Update(firstName, lastName, email, now);
        }

        await database.SaveChangesAsync(cancellationToken);
        return ToResult(profile);
    }

    private Task<bool> IsActivePassengerAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return database.Users.AnyAsync(
            user => user.Id == userId &&
                user.IsActive &&
                user.Role == UserRole.Passenger,
            cancellationToken);
    }

    private static PassengerProfileResult ToResult(PassengerProfile profile)
    {
        return new PassengerProfileResult(
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            profile.Email,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
