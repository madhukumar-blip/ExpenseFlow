using Microsoft.AspNetCore.Identity;

namespace ExpenseFlow.Web.Setup;

public static class DevelopmentRoleSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var users =
            services.GetRequiredService<UserManager<IdentityUser>>();

        var roles =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        var assignments = new[]
 {
    (Role: "Admin", Key: "Bootstrap:AdminEmail"),
    (Role: "Manager", Key: "Bootstrap:ManagerEmail"),
    (Role: "Finance", Key: "Bootstrap:FinanceEmail")
};

        foreach (var assignment in assignments)
        {
            var email = configuration[assignment.Key];

            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var user = await users.FindByEmailAsync(email.Trim());

            if (user is null)
            {
                throw new InvalidOperationException(
                    $"Register the account configured in "
                    + $"{assignment.Key} before enabling it.");
            }

            if (!await roles.RoleExistsAsync(assignment.Role))
            {
                EnsureSucceeded(await roles.CreateAsync(
                    new IdentityRole(assignment.Role)));
            }

            if (!await users.IsInRoleAsync(user, assignment.Role))
            {
                EnsureSucceeded(await users.AddToRoleAsync(
                    user,
                    assignment.Role));
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description)));
        }
    }
}