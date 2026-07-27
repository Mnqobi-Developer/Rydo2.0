using Microsoft.EntityFrameworkCore;
using Rydo.Domain.Drivers;
using Rydo.Domain.Identity;
using Rydo.Domain.Passengers;

namespace Rydo.Infrastructure.Persistence;

public sealed class RydoDbContext(DbContextOptions<RydoDbContext> options)
    : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();

    public DbSet<AuthSession> AuthSessions => Set<AuthSession>();

    public DbSet<SessionRefreshToken> RefreshTokens => Set<SessionRefreshToken>();

    public DbSet<PassengerProfile> PassengerProfiles => Set<PassengerProfile>();

    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.PhoneNumber).HasMaxLength(16).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(user => user.PhoneNumber).IsUnique();
        });

        modelBuilder.Entity<OtpChallenge>(entity =>
        {
            entity.ToTable("otp_challenges");
            entity.HasKey(challenge => challenge.Id);
            entity.Property(challenge => challenge.PhoneNumber).HasMaxLength(16).IsRequired();
            entity.Property(challenge => challenge.RequestedRole).HasConversion<string>().HasMaxLength(16);
            entity.Property(challenge => challenge.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(challenge => new { challenge.PhoneNumber, challenge.CreatedAt });
        });

        modelBuilder.Entity<AuthSession>(entity =>
        {
            entity.ToTable("auth_sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.RevocationReason).HasMaxLength(64);
            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(session => session.RefreshTokens)
                .WithOne(token => token.Session)
                .HasForeignKey(token => token.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(session => new { session.UserId, session.RevokedAt });
        });

        modelBuilder.Entity<SessionRefreshToken>(entity =>
        {
            entity.ToTable("session_refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(token => token.TokenHash).IsUnique();
        });

        modelBuilder.Entity<PassengerProfile>(entity =>
        {
            entity.ToTable("passenger_profiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.LastName).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.Email).HasMaxLength(254);
            entity.HasOne<UserAccount>()
                .WithOne()
                .HasForeignKey<PassengerProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DriverProfile>(entity =>
        {
            entity.ToTable("driver_profiles");
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.LastName).HasMaxLength(100).IsRequired();
            entity.Property(profile => profile.Email).HasMaxLength(254);
            entity.Property(profile => profile.OnboardingStatus)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(profile => profile.RejectionReason).HasMaxLength(500);
            entity.HasOne<UserAccount>()
                .WithOne()
                .HasForeignKey<DriverProfile>(profile => profile.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
