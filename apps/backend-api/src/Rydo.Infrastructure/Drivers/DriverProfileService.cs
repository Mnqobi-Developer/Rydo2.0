using Microsoft.EntityFrameworkCore;
using Rydo.Application.Drivers;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Drivers;

public sealed class DriverProfileService(
    RydoDbContext database,
    TimeProvider timeProvider) : IDriverProfileService
{
    private static readonly DriverDocumentType[] RequiredDocumentTypes =
    [
        DriverDocumentType.IdentityDocument,
        DriverDocumentType.DriversLicense,
        DriverDocumentType.ProfessionalDrivingPermit,
    ];

    public async Task<DriverProfileResult?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        return await database.DriverProfiles
            .Where(profile => profile.UserId == userId)
            .Select(profile => new DriverProfileResult(
                profile.UserId,
                profile.FirstName,
                profile.LastName,
                profile.Email,
                profile.OnboardingStatus,
                profile.OnboardingStatus == DriverOnboardingStatus.Draft ||
                    profile.OnboardingStatus == DriverOnboardingStatus.Rejected,
                profile.CreatedAt,
                profile.UpdatedAt,
                profile.SubmittedAt,
                profile.ReviewedAt,
                profile.RejectionReason))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<DriverProfileResult?> UpsertAsync(
        Guid userId,
        string firstName,
        string lastName,
        string? email,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var profile = await database.DriverProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = DriverProfile.Create(userId, firstName, lastName, email, now);
            database.DriverProfiles.Add(profile);
        }
        else
        {
            try
            {
                profile.Update(firstName, lastName, email, now);
            }
            catch (InvalidOperationException exception)
            {
                throw new DriverOnboardingStateException(exception.Message);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        return ToResult(profile);
    }

    public async Task<DriverProfileResult?> SubmitAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveDriverAsync(userId, cancellationToken))
        {
            return null;
        }

        var profile = await database.DriverProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new DriverProfileNotFoundException();

        var availableDocumentTypes = await database.DriverDocuments
            .Where(document => document.DriverUserId == userId &&
                document.SupersededAt == null &&
                document.ReviewStatus != DriverDocumentReviewStatus.Rejected)
            .Select(document => document.DocumentType)
            .Distinct()
            .ToListAsync(cancellationToken);
        var missingDocumentTypes = RequiredDocumentTypes
            .Except(availableDocumentTypes)
            .ToArray();

        if (missingDocumentTypes.Length > 0)
        {
            throw new DriverOnboardingDocumentsMissingException(missingDocumentTypes);
        }

        try
        {
            profile.Submit(timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            throw new DriverOnboardingStateException(exception.Message);
        }

        await database.SaveChangesAsync(cancellationToken);
        return ToResult(profile);
    }

    private Task<bool> IsActiveDriverAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return database.Users.AnyAsync(
            user => user.Id == userId &&
                user.IsActive &&
                user.Role == UserRole.Driver,
            cancellationToken);
    }

    private static DriverProfileResult ToResult(DriverProfile profile)
    {
        return new DriverProfileResult(
            profile.UserId,
            profile.FirstName,
            profile.LastName,
            profile.Email,
            profile.OnboardingStatus,
            profile.CanEdit,
            profile.CreatedAt,
            profile.UpdatedAt,
            profile.SubmittedAt,
            profile.ReviewedAt,
            profile.RejectionReason);
    }
}
