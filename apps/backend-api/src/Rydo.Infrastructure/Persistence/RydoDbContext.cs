using Microsoft.EntityFrameworkCore;
using Rydo.Domain.Drivers;
using Rydo.Domain.Disputes;
using Rydo.Domain.Identity;
using Rydo.Domain.Matching;
using Rydo.Domain.Passengers;
using Rydo.Domain.Payments;
using Rydo.Domain.Ratings;
using Rydo.Domain.Trips;

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

    public DbSet<Trip> Trips => Set<Trip>();

    public DbSet<DriverAvailability> DriverAvailability => Set<DriverAvailability>();

    public DbSet<TripOffer> TripOffers => Set<TripOffer>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Dispute> Disputes => Set<Dispute>();

    public DbSet<DisputeMessage> DisputeMessages => Set<DisputeMessage>();

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

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.ToTable("trips", table =>
            {
                table.HasCheckConstraint(
                    "CK_trips_PickupLatitude",
                    "\"PickupLatitude\" BETWEEN -90 AND 90");
                table.HasCheckConstraint(
                    "CK_trips_PickupLongitude",
                    "\"PickupLongitude\" BETWEEN -180 AND 180");
                table.HasCheckConstraint(
                    "CK_trips_DestinationLatitude",
                    "\"DestinationLatitude\" BETWEEN -90 AND 90");
                table.HasCheckConstraint(
                    "CK_trips_DestinationLongitude",
                    "\"DestinationLongitude\" BETWEEN -180 AND 180");
                table.HasCheckConstraint(
                    "CK_trips_FinalFareAmount",
                    "\"FinalFareAmount\" IS NULL OR \"FinalFareAmount\" > 0");
            });
            entity.HasKey(trip => trip.Id);
            entity.Property(trip => trip.PickupAddress).HasMaxLength(300).IsRequired();
            entity.Property(trip => trip.DestinationAddress).HasMaxLength(300).IsRequired();
            entity.Property(trip => trip.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(trip => trip.CancellationReason).HasMaxLength(250);
            entity.Property(trip => trip.FinalFareAmount).HasPrecision(12, 2);
            entity.Property(trip => trip.Version).IsConcurrencyToken();
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(trip => trip.PassengerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(trip => trip.DriverUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(trip => trip.PassengerUserId)
                .IsUnique()
                .HasFilter(
                    "\"Status\" IN ('Requested', 'Accepted', 'DriverArrived', 'InProgress')");
            entity.HasIndex(trip => trip.DriverUserId)
                .IsUnique()
                .HasFilter(
                    "\"DriverUserId\" IS NOT NULL AND \"Status\" IN ('Accepted', 'DriverArrived', 'InProgress')");
            entity.HasIndex(trip => new { trip.Status, trip.RequestedAt });
        });

        modelBuilder.Entity<DriverAvailability>(entity =>
        {
            entity.ToTable("driver_availability", table =>
            {
                table.HasCheckConstraint(
                    "CK_driver_availability_Latitude",
                    "\"Latitude\" BETWEEN -90 AND 90");
                table.HasCheckConstraint(
                    "CK_driver_availability_Longitude",
                    "\"Longitude\" BETWEEN -180 AND 180");
            });
            entity.HasKey(availability => availability.DriverUserId);
            entity.Property(availability => availability.Version).IsConcurrencyToken();
            entity.HasOne<UserAccount>()
                .WithOne()
                .HasForeignKey<DriverAvailability>(availability => availability.DriverUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(availability => new
            {
                availability.IsOnline,
                availability.LocationUpdatedAt,
            });
        });

        modelBuilder.Entity<TripOffer>(entity =>
        {
            entity.ToTable("trip_offers", table =>
            {
                table.HasCheckConstraint(
                    "CK_trip_offers_PickupDistanceKilometres",
                    "\"PickupDistanceKilometres\" >= 0");
                table.HasCheckConstraint(
                    "CK_trip_offers_Expiry",
                    "\"ExpiresAt\" > \"OfferedAt\"");
            });
            entity.HasKey(offer => offer.Id);
            entity.Property(offer => offer.Status).HasConversion<string>().HasMaxLength(24);
            entity.Property(offer => offer.Version).IsConcurrencyToken();
            entity.HasOne<Trip>()
                .WithMany()
                .HasForeignKey(offer => offer.TripId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(offer => offer.DriverUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(offer => new { offer.TripId, offer.DriverUserId }).IsUnique();
            entity.HasIndex(offer => new
            {
                offer.DriverUserId,
                offer.Status,
                offer.ExpiresAt,
            });
            entity.HasIndex(offer => new { offer.TripId, offer.Status });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments", table =>
            {
                table.HasCheckConstraint("CK_payments_Amount", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_payments_Currency", "\"Currency\" = 'ZAR'");
            });
            entity.HasKey(payment => payment.Id);
            entity.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(24);
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(payment => payment.Amount).HasPrecision(12, 2);
            entity.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();
            entity.Property(payment => payment.ProviderPaymentId).HasMaxLength(100);
            entity.Property(payment => payment.FailureReason).HasMaxLength(500);
            entity.Property(payment => payment.Version).IsConcurrencyToken();
            entity.HasOne<Trip>()
                .WithOne()
                .HasForeignKey<Payment>(payment => payment.TripId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(payment => payment.PassengerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(payment => payment.TripId).IsUnique();
            entity.HasIndex(payment => payment.ProviderPaymentId)
                .IsUnique()
                .HasFilter("\"ProviderPaymentId\" IS NOT NULL");
            entity.HasIndex(payment => new { payment.Status, payment.CreatedAt });
        });

        modelBuilder.Entity<PaymentEvent>(entity =>
        {
            entity.ToTable("payment_events");
            entity.HasKey(paymentEvent => paymentEvent.Id);
            entity.Property(paymentEvent => paymentEvent.Provider).HasMaxLength(32).IsRequired();
            entity.Property(paymentEvent => paymentEvent.EventType).HasMaxLength(64).IsRequired();
            entity.Property(paymentEvent => paymentEvent.ProviderEventId).HasMaxLength(100);
            entity.Property(paymentEvent => paymentEvent.FailureReason).HasMaxLength(500);
            entity.Property(paymentEvent => paymentEvent.PayloadSha256).HasMaxLength(64).IsRequired();
            entity.Property(paymentEvent => paymentEvent.RemoteIpAddress).HasMaxLength(64);
            entity.HasOne<Payment>()
                .WithMany()
                .HasForeignKey(paymentEvent => paymentEvent.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(paymentEvent => new
            {
                paymentEvent.Provider,
                paymentEvent.ProviderEventId,
            });
            entity.HasIndex(paymentEvent => new
            {
                paymentEvent.PaymentId,
                paymentEvent.ReceivedAt,
            });
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.ToTable("ratings", table =>
                table.HasCheckConstraint("CK_ratings_Score", "\"Score\" BETWEEN 1 AND 5"));
            entity.HasKey(rating => rating.Id);
            entity.Property(rating => rating.Comment).HasMaxLength(500);
            entity.HasOne<Trip>().WithMany().HasForeignKey(rating => rating.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(rating => rating.RaterUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>().WithMany().HasForeignKey(rating => rating.RatedUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(rating => new { rating.TripId, rating.RaterUserId }).IsUnique();
            entity.HasIndex(rating => new { rating.RatedUserId, rating.CreatedAt });
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.ToTable("disputes", table =>
            {
                table.HasCheckConstraint(
                    "CK_disputes_ResolutionState",
                    "(\"Status\" IN ('Open', 'UnderReview') AND \"ResolvedAt\" IS NULL AND \"ResolvedByUserId\" IS NULL AND \"Resolution\" IS NULL) OR " +
                    "(\"Status\" IN ('Resolved', 'Rejected') AND \"ResolvedAt\" IS NOT NULL AND \"ResolvedByUserId\" IS NOT NULL AND \"Resolution\" IS NOT NULL)");
            });
            entity.HasKey(dispute => dispute.Id);
            entity.Property(dispute => dispute.Category).HasConversion<string>().HasMaxLength(32);
            entity.Property(dispute => dispute.Subject).HasMaxLength(120).IsRequired();
            entity.Property(dispute => dispute.Description).HasMaxLength(2000).IsRequired();
            entity.Property(dispute => dispute.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(dispute => dispute.Resolution).HasMaxLength(2000);
            entity.Property(dispute => dispute.Version).IsConcurrencyToken();
            entity.HasOne<Trip>()
                .WithOne()
                .HasForeignKey<Dispute>(dispute => dispute.TripId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(dispute => dispute.OpenedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(dispute => dispute.ResolvedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(dispute => dispute.TripId).IsUnique();
            entity.HasIndex(dispute => new { dispute.Status, dispute.UpdatedAt });
            entity.HasIndex(dispute => dispute.OpenedByUserId);
        });

        modelBuilder.Entity<DisputeMessage>(entity =>
        {
            entity.ToTable("dispute_messages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.Body).HasMaxLength(2000).IsRequired();
            entity.HasOne<Dispute>()
                .WithMany()
                .HasForeignKey(message => message.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(message => message.AuthorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(message => new { message.DisputeId, message.CreatedAt });
        });

        base.OnModelCreating(modelBuilder);
    }
}
