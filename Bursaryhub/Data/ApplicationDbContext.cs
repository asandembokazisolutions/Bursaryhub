using BursaryHub.Models;
using Microsoft.EntityFrameworkCore;

namespace BursaryHub.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Bursary> Bursaries => Set<Bursary>();
    public DbSet<BursaryApplication> BursaryApplications => Set<BursaryApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique email index
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Unique: user cannot apply twice for same bursary
        modelBuilder.Entity<BursaryApplication>()
            .HasIndex(a => new { a.UserId, a.BursaryId })
            .IsUnique();

        // User → Role (many-to-one)
        modelBuilder.Entity<User>()
            .HasOne(u => u.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Application → User (applicant)
        modelBuilder.Entity<BursaryApplication>()
            .HasOne(a => a.User)
            .WithMany(u => u.Applications)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Application → Bursary
        modelBuilder.Entity<BursaryApplication>()
            .HasOne(a => a.Bursary)
            .WithMany(b => b.Applications)
            .HasForeignKey(a => a.BursaryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Application → Reviewer (nullable self-ref)
        modelBuilder.Entity<BursaryApplication>()
            .HasOne(a => a.ReviewedByUser)
            .WithMany()
            .HasForeignKey(a => a.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Bursary → Creator (nullable)
        modelBuilder.Entity<Bursary>()
            .HasOne(b => b.CreatedByUser)
            .WithMany(u => u.CreatedBursaries)
            .HasForeignKey(b => b.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─── Seed Roles ──────────────────────────────────────────────────────
        modelBuilder.Entity<Role>().HasData(
            new Role { RoleId = 1, RoleName = "Admin",     Description = "Full system access – manages users, bursaries, and roles.", CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new Role { RoleId = 2, RoleName = "Moderator", Description = "Content management – adds/edits bursaries and reviews applications.", CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
            new Role { RoleId = 3, RoleName = "User",      Description = "Regular user – browses bursaries and submits applications.", CreatedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true }
        );
    }
}
