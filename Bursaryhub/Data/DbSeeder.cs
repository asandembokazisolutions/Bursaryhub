using BursaryHub.Models;
using BursaryHub.Services;
using Microsoft.EntityFrameworkCore;
 
namespace BursaryHub.Data;
 
public static class DbSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        // Migration is handled in Program.cs before this method is called.
        var db = services.GetRequiredService<ApplicationDbContext>();
        var hasher  = services.GetRequiredService<IPasswordHasher>();
        var logger  = services.GetRequiredService<ILogger<ApplicationDbContext>>();
 
        var adminEmail    = Environment.GetEnvironmentVariable("ADMIN_EMAIL")    ?? "admin@bursaryhub.com";
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") ?? "Admin@12345!";
 
        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var admin = new User
            {
                FirstName       = "Donda",
                LastName        = "Administrator",
                Email           = adminEmail,
                PasswordHash    = hasher.Hash(adminPassword),
                PhoneNumber     = "0612345678",
                RoleId          = 1,
                IsActive        = true,
                IsEmailVerified = false,
                CreatedDate     = DateTime.UtcNow,
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            logger.LogInformation("✅ Seeded default admin account: {Email}", adminEmail);
        }
 
        if (!await db.Bursaries.AnyAsync())
        {
            db.Bursaries.AddRange(
                new Bursary
                {
                    Name                = "National Merit Scholarship",
                    Description         = "Awarded annually to top-performing students across all disciplines. Covers full tuition for undergraduate study.",
                    Provider            = "National Merit Corporation",
                    Amount              = 50000m,
                    ApplicationDeadline = DateTime.UtcNow.AddDays(45),
                    AwardDate           = DateTime.UtcNow.AddDays(90),
                    EligibilityCriteria = "GPA 3.5+, SA or citizens only",
                    ApplicationUrl      = "https://www.nationalmerit.org/apply",
                    Status              = "Active",
                    IsActive            = true,
                    IsScraped           = false,
                    CreatedDate         = DateTime.UtcNow,
                },
                new Bursary
                {
                    Name                = "STEM Excellence Bursary",
                    Description         = "Supporting students pursuing degrees in Science, Technology, Engineering, or Mathematics. Preference given to first-generation university students.",
                    Provider            = "TechFutures Foundation",
                    Amount              = 25000m,
                    ApplicationDeadline = DateTime.UtcNow.AddDays(20),
                    AwardDate           = DateTime.UtcNow.AddDays(60),
                    EligibilityCriteria = "STEM major, financial need demonstrated",
                    ApplicationUrl      = "https://techfutures.org/bursary",
                    Status              = "Active",
                    IsActive            = true,
                    IsScraped           = false,
                    CreatedDate         = DateTime.UtcNow,
                },
                new Bursary
                {
                    Name                = "Community Leaders Award",
                    Description         = "Recognises students who have demonstrated outstanding community service and leadership potential.",
                    Provider            = "Ubuntu Community Trust",
                    Amount              = 15000m,
                    ApplicationDeadline = DateTime.UtcNow.AddDays(5),
                    AwardDate           = DateTime.UtcNow.AddDays(35),
                    EligibilityCriteria = "50+ hours community service, any major",
                    Status              = "Active",
                    IsActive            = true,
                    IsScraped           = false,
                    CreatedDate         = DateTime.UtcNow,
                }
            );
            await db.SaveChangesAsync();
            logger.LogInformation("✅ Seeded 3 sample bursaries");
        }
    }
}
