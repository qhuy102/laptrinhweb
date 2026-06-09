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

            var context = serviceProvider.GetRequiredService<PharmacyDbContext>();
            await SeedMedicines(context);
        }

        private static async Task SeedMedicines(PharmacyDbContext context)
        {
            if (!context.LoaiThuocs.Any())
            {
                var categories = new List<LoaiThuoc>
                {
                    new LoaiThuoc { TenLoai = "Thuốc kê đơn" },
                    new LoaiThuoc { TenLoai = "Thuốc không kê đơn" },
                    new LoaiThuoc { TenLoai = "Thực phẩm chức năng" },
                    new LoaiThuoc { TenLoai = "Dược mỹ phẩm" }
                };
                await context.LoaiThuocs.AddRangeAsync(categories);
                await context.SaveChangesAsync();
            }

            if (!context.Thuocs.Any())
            {
                var keDon = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thuốc kê đơn");
                var khongKeDon = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thuốc không kê đơn");
                var tpcn = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thực phẩm chức năng");
                var duocMyPham = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Dược mỹ phẩm");

                if (keDon != null && khongKeDon != null && tpcn != null && duocMyPham != null)
                {
                    var drugs = new List<Thuoc>
                    {
                        new Thuoc
                        {
                            TenThuoc = "Paracetamol 500mg",
                            DonGia = 15000,
                            SoLuong = 100,
                            MoTa = "Thuốc giảm đau, hạ sốt hiệu quả nhanh. Dùng điều trị các chứng đau đầu, đau răng, đau cơ, sốt do cảm cúm.",
                            HanSuDung = DateTime.Now.AddYears(2),
                            LoaiThuocId = khongKeDon.Id
                        },
                        new Thuoc
                        {
                            TenThuoc = "Panadol Extra",
                            DonGia = 24000,
                            SoLuong = 80,
                            MoTa = "Panadol Extra chứa Paracetamol và Caffeine giúp giảm đau đầu, đau nửa đầu, đau họng và sốt hiệu quả.",
                            HanSuDung = DateTime.Now.AddYears(2),
                            LoaiThuocId = khongKeDon.Id
                        },
                        new Thuoc
                        {
                            TenThuoc = "Amoxicillin 500mg",
                            DonGia = 45000,
                            SoLuong = 50,
                            MoTa = "Thuốc kháng sinh bán tổng hợp nhóm beta-lactam. Dùng điều trị các bệnh nhiễm trùng đường hô hấp, tiết niệu.",
                            HanSuDung = DateTime.Now.AddYears(1),
                            LoaiThuocId = keDon.Id
                        },
                        new Thuoc
                        {
                            TenThuoc = "Vitamin C 1000mg Enervon",
                            DonGia = 35000,
                            SoLuong = 120,
                            MoTa = "Bổ sung Vitamin C và các vitamin nhóm B. Hỗ trợ tăng cường sức đề kháng, phục hồi sức khỏe sau ốm.",
                            HanSuDung = DateTime.Now.AddYears(2),
                            LoaiThuocId = tpcn.Id
                        },
                        new Thuoc
                        {
                            TenThuoc = "Sữa rửa mặt Cetaphil Gentle 125ml",
                            DonGia = 145000,
                            SoLuong = 30,
                            MoTa = "Sữa rửa mặt dịu nhẹ cho mọi loại da, đặc biệt là da nhạy cảm. Không chứa xà phòng, cân bằng độ pH.",
                            HanSuDung = DateTime.Now.AddYears(3),
                            LoaiThuocId = duocMyPham.Id
                        },
                        new Thuoc
                        {
                            TenThuoc = "Decolgen Forte",
                            DonGia = 18000,
                            SoLuong = 150,
                            MoTa = "Điều trị các triệu chứng cảm cúm như nghẹt mũi, sổ mũi, hắt hơi, sốt và nhức đầu.",
                            HanSuDung = DateTime.Now.AddYears(2),
                            LoaiThuocId = khongKeDon.Id
                        }
                    };
                    await context.Thuocs.AddRangeAsync(drugs);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}