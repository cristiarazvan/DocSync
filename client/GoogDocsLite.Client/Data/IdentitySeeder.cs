using GoogDocsLite.Shared;
using Microsoft.AspNetCore.Identity;

namespace GoogDocsLite.Client.Data;

public static class IdentitySeeder
{
    // Creeaza utilizatorii demo daca nu exista deja, pentru testarea rapida a functionalitatilor.
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureUserAsync(userManager, logger, DemoSeedDefaults.OwnerUserId, DemoSeedDefaults.OwnerEmail, "Demo Owner");
        await EnsureUserAsync(userManager, logger, DemoSeedDefaults.EditorUserId, DemoSeedDefaults.EditorEmail, "Demo Editor");
        await EnsureUserAsync(userManager, logger, DemoSeedDefaults.ViewerUserId, DemoSeedDefaults.ViewerEmail, "Demo Viewer");
        await EnsureUserAsync(userManager, logger, DemoSeedDefaults.OutsiderUserId, DemoSeedDefaults.OutsiderEmail, "Demo Outsider");
    }

    // Asigura existenta unui singur cont demo pe email; daca lipseste, il creeaza cu id fix.
    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        string fixedId,
        string email,
        string displayName)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            if (!existing.EmailConfirmed)
            {
                existing.EmailConfirmed = true;
                await userManager.UpdateAsync(existing);
            }

            return;
        }

        var user = new ApplicationUser
        {
            Id = fixedId,
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, DemoSeedDefaults.SeedPassword);
        if (createResult.Succeeded)
        {
            logger.LogInformation("Seeded demo user {DisplayName} ({Email})", displayName, email);
            return;
        }

        var errors = string.Join("; ", createResult.Errors.Select(x => x.Description));
        logger.LogWarning("Failed to seed demo user {Email}: {Errors}", email, errors);
    }
}
