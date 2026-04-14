using LedgerFlow.Application.Common;
using LedgerFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LedgerFlow.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
        var context = provider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Created role {Role}", role);
            }
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(userManager, "admin@ledgerflow.local", "Admin123!", new[] { Roles.Admin });
        await EnsureUserAsync(userManager, "accountant@ledgerflow.local", "Accountant123!", new[] { Roles.Accountant });
        await EnsureUserAsync(userManager, "user@ledgerflow.local", "User123!", new[] { Roles.User });
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, string email, string password, string[] roles)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null)
            return;

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to create {email}: " + string.Join(", ", result.Errors.Select(e => e.Description)));

        await users.AddToRolesAsync(user, roles);
    }
}
