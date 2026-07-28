using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Rydo.Domain.Identity;
using Rydo.Infrastructure.Persistence;

namespace Rydo.Infrastructure.Admin;

public sealed class AdminBootstrapService(
    RydoDbContext database,
    IOptions<AdminAccessOptions> options,
    TimeProvider timeProvider)
{
    private readonly AdminAccessOptions _options = options.Value;

    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var normalizedEmail = _options.BootstrapEmail.Trim().ToLowerInvariant();
        var credential = await database.AdminCredentials.SingleOrDefaultAsync(
            item => item.Email == normalizedEmail,
            cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (credential is not null)
        {
            if (!AdminPasswordHasher.Verify(_options.BootstrapPassword, credential.PasswordHash))
            {
                credential.RotatePassword(
                    AdminPasswordHasher.Hash(_options.BootstrapPassword),
                    now);
                await database.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var normalizedPhone = _options.BootstrapPhoneNumber.Trim();
        var user = await database.Users.SingleOrDefaultAsync(
            item => item.PhoneNumber == normalizedPhone,
            cancellationToken);

        if (user is not null && user.Role != UserRole.Admin)
        {
            throw new InvalidOperationException(
                "The configured AdminAccess bootstrap phone belongs to a non-admin user.");
        }

        user ??= UserAccount.Create(normalizedPhone, UserRole.Admin, now);

        if (database.Entry(user).State == EntityState.Detached)
        {
            database.Users.Add(user);
        }

        database.AdminCredentials.Add(AdminCredential.Create(
            user.Id,
            normalizedEmail,
            AdminPasswordHasher.Hash(_options.BootstrapPassword),
            now));
        await database.SaveChangesAsync(cancellationToken);
    }
}
