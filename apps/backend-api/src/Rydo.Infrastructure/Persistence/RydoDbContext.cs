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

    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();

    public DbSet<DriverVehicle> DriverVehicles => Set<DriverVehicle>();

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

        modelBuilder.Entity<DriverDocument>(entity =>
        {
            entity.ToTable("driver_documents");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.DocumentType)
                .HasConversion<string>()
                .HasMaxLength(48);
            entity.Property(document => document.StorageObjectKey).HasMaxLength(200).IsRequired();
            entity.Property(document => document.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(64).IsRequired();
            entity.Property(document => document.Sha256).HasMaxLength(64).IsRequired();
            entity.Property(document => document.ReviewStatus)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(document => document.RejectionReason).HasMaxLength(500);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(document => document.DriverUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(document => document.StorageObjectKey).IsUnique();
            entity.HasIndex(document => new
            {
                document.DriverUserId,
                document.DocumentType,
            })
                .IsUnique()
                .HasFilter("\"SupersededAt\" IS NULL");
        });

        modelBuilder.Entity<DriverVehicle>(entity =>
        {
            entity.ToTable("driver_vehicles", table =>
            {
                table.HasCheckConstraint(
                    "CK_driver_vehicles_Year",
                    "\"Year\" BETWEEN 1980 AND 2100");
                table.HasCheckConstraint(
                    "CK_driver_vehicles_SeatCapacity",
                    "\"SeatCapacity\" BETWEEN 1 AND 16");
            });
            entity.HasKey(vehicle => vehicle.Id);
            entity.Property(vehicle => vehicle.Make).HasMaxLength(100).IsRequired();
            entity.Property(vehicle => vehicle.Model).HasMaxLength(100).IsRequired();
            entity.Property(vehicle => vehicle.Color).HasMaxLength(50).IsRequired();
            entity.Property(vehicle => vehicle.RegistrationNumber).HasMaxLength(16).IsRequired();
            entity.Property(vehicle => vehicle.VehicleIdentificationNumber)
                .HasMaxLength(17)
                .IsRequired();
            entity.Property(vehicle => vehicle.ReviewStatus)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(vehicle => vehicle.RejectionReason).HasMaxLength(500);
            entity.HasOne<UserAccount>()
                .WithOne()
                .HasForeignKey<DriverVehicle>(vehicle => vehicle.DriverUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(vehicle => vehicle.DriverUserId).IsUnique();
            entity.HasIndex(vehicle => vehicle.RegistrationNumber).IsUnique();
            entity.HasIndex(vehicle => vehicle.VehicleIdentificationNumber).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
