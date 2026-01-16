using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace EcommerceApp.Data
{
    public static class RoleSeeder
    {
        public static async Task SeedRolesAsync(IServiceProvider service)
        {
            var roleManager = service.GetRequiredService<RoleManager<IdentityRole>>();

            // ✅ Array de roles correcto en C#
            string[] roles = { "Admin", "Cliente" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
    }
}
