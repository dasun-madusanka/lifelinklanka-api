using LifeLinkLanka.Domain.Constants;
using LifeLinkLanka.Domain.Entities;
using LifeLinkLanka.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LifeLinkLanka.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAllAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("Starting LifeLink Lanka clean database initialization...");

        // 1. Roles
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        // 2. Wipe any legacy mock/predefined data to ensure 100% user-generated data
        var demoEmails = new[]
        {
            "hospital@lifelinklanka.lk",
            "bloodbank@lifelinklanka.lk",
            "donor@lifelinklanka.lk",
            "donor.anusha@lifelinklanka.lk",
            "donor.ravi@lifelinklanka.lk",
            "donor.tharushi@lifelinklanka.lk"
        };

        var legacyDemoUsers = await db.Users.Where(u => demoEmails.Contains(u.Email)).ToListAsync();
        if (legacyDemoUsers.Count > 0)
        {
            logger.LogInformation("Purging legacy mock demo records and users...");
            db.CampRegistrations.RemoveRange(db.CampRegistrations);
            db.DonationAppointments.RemoveRange(db.DonationAppointments);
            db.DonationRecords.RemoveRange(db.DonationRecords);
            db.DonorMatches.RemoveRange(db.DonorMatches);
            db.BloodRequests.RemoveRange(db.BloodRequests);
            db.BloodInventories.RemoveRange(db.BloodInventories);
            db.BloodCamps.RemoveRange(db.BloodCamps);
            db.Hospitals.RemoveRange(db.Hospitals);
            db.BloodBanks.RemoveRange(db.BloodBanks);
            db.DonorProfiles.RemoveRange(db.DonorProfiles.Where(p => legacyDemoUsers.Select(u => u.Id).Contains(p.UserId)));

            foreach (var user in legacyDemoUsers)
            {
                await userManager.DeleteAsync(user);
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Legacy mock data successfully purged.");
        }

        // 3. Ensure System Administrator only
        var admin = await EnsureUserAsync(userManager, "admin@lifelinklanka.lk", "System Administrator", "199000000001", "Colombo", new DateTime(1990, 1, 1), Roles.Admin);

        logger.LogInformation("Database initialized clean. System Administrator ready: {Email}", admin.Email);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string nic,
        string district,
        DateTime dob,
        string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                NicNumber = nic,
                District = district,
                DateOfBirth = dob,
                EmailConfirmed = true,
                IsActive = true,
                AccountStatus = Domain.Enums.VerificationStatus.Verified
            };
            var result = await userManager.CreateAsync(user, "Admin@12345!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
        else
        {
            if (user.AccountStatus != Domain.Enums.VerificationStatus.Verified || !user.IsActive)
            {
                user.AccountStatus = Domain.Enums.VerificationStatus.Verified;
                user.IsActive = true;
                user.EmailConfirmed = true;
                await userManager.UpdateAsync(user);
            }
        }
        return user;
    }
}
