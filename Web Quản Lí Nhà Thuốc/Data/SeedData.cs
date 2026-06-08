using Microsoft.AspNetCore.Identity;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Data
{
    public static class SeedData
    {
        public static async Task SeedRolesAndAdmin(IServiceProvider serviceProvider)
        {
            var roleManager =
                serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roles = { "Admin", "User" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(
                        new IdentityRole(role));
                }
            }

            var adminEmail = "admin@gmail.com";

            var admin =
                await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    HoTen = "Administrator",
                    NgaySinh = new DateTime(1990, 1, 1),
                    EmailConfirmed = true
                };

                await userManager.CreateAsync(
                    admin,
                    "Admin@123");

                await userManager.AddToRoleAsync(
                    admin,
                    "Admin");
            }
        }
    }
}