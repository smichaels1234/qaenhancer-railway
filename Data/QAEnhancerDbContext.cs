using System;
using backend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace backend.Data
{
    public class QAEnhancerDbContext : IdentityDbContext<ApplicationUser>
    {
        public QAEnhancerDbContext(DbContextOptions<QAEnhancerDbContext> options)
            : base(options)
        {
        }

        public DbSet<TestTable> TestTables { get; set; }
        public DbSet<Bug> Bugs { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<CustomPlanRequest> CustomPlanRequests { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("qaenhancer");

            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.OrganizationId).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.OrganizationId);
            });

            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RefreshTokenHash).IsRequired().HasMaxLength(64);
                entity.Property(e => e.IpAddress).HasMaxLength(100);
                entity.Property(e => e.UserAgent).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.LastSeenAt).IsRequired();
                entity.Property(e => e.ExpiresAt).IsRequired();

                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.RefreshTokenHash).IsUnique();
                entity.HasIndex(e => new { e.RevokedAt, e.ExpiresAt });

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Bug entity
            modelBuilder.Entity<Bug>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Description).IsRequired().HasMaxLength(2000);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Severity).HasMaxLength(20);
                entity.Property(e => e.Location).HasMaxLength(500);
                entity.Property(e => e.AnalyzedUrl).HasMaxLength(1000);
                entity.Property(e => e.Source).HasMaxLength(50);
                entity.Property(e => e.OrganizationId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.AssignedUserName).HasMaxLength(200);
                entity.Property(e => e.AssignedUserEmail).HasMaxLength(256);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.IsActive).IsRequired();

                // Create index on frequently queried fields
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.AnalyzedUrl);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.OrganizationId);
                entity.HasIndex(e => e.AssignedUserId);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.AssignedUser)
                    .WithMany()
                    .HasForeignKey(e => e.AssignedUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure Subscription entity
            modelBuilder.Entity<Subscription>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.PlanType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Status).HasMaxLength(20);
                entity.Property(e => e.CreatedAt).IsRequired();
                
                // Create indexes
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.PlanType);
                entity.HasIndex(e => e.Status);
                
                // Foreign key relationship
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure CustomPlanRequest entity
            modelBuilder.Entity<CustomPlanRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired();
                entity.Property(e => e.CompanyName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TeamSize).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CreatedAt).IsRequired();
                
                // Create indexes
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
                
                // Foreign key relationship
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}