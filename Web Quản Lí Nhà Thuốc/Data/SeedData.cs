using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Data
{
    public static class SeedData
    {
        public static async Task SeedRolesAndAdmin(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<PharmacyDbContext>();

            // Ensure database schema matches new features (Pill reminders and Treatment groups)
            await context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'NhomDieuTri' AND Object_ID = Object_ID(N'Thuocs'))
                BEGIN
                    ALTER TABLE Thuocs ADD NhomDieuTri nvarchar(100) NULL;
                END

                IF OBJECT_ID(N'LichUongThuocs', N'U') IS NULL
                BEGIN
                    CREATE TABLE LichUongThuocs (
                        Id int IDENTITY(1,1) PRIMARY KEY,
                        UserId nvarchar(max) NOT NULL,
                        TenThuoc nvarchar(150) NOT NULL,
                        LieuDung nvarchar(100) NOT NULL,
                        GioUong nvarchar(100) NOT NULL,
                        GhiChu nvarchar(100) NULL,
                        DaUongHomNay bit NOT NULL DEFAULT 0,
                        NgayCapNhat datetime2 NOT NULL DEFAULT GETDATE()
                    );
                END
            ");

            string[] roles = { "Admin", "User", "Pharmacist" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Seed Admin
            var adminEmail = "admin@gmail.com";
            var admin = await userManager.FindByEmailAsync(adminEmail);
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

                await userManager.CreateAsync(admin, "Admin@123");
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Seed Pharmacist
            var pharEmail = "duocsi@gmail.com";
            var phar = await userManager.FindByEmailAsync(pharEmail);
            if (phar == null)
            {
                phar = new ApplicationUser
                {
                    UserName = pharEmail,
                    Email = pharEmail,
                    HoTen = "Dược sĩ Nguyễn Văn B",
                    NgaySinh = new DateTime(1992, 10, 10),
                    EmailConfirmed = true,
                    DiaChi = "Tòa nhà Pharmacy, Hà Nội",
                    DiemTichLuy = 0
                };

                await userManager.CreateAsync(phar, "Pharmacist@123");
                await userManager.AddToRoleAsync(phar, "Pharmacist");
            }

            // Seed some customers for VIP/loyalty testing
            var customerEmail = "khachhang@gmail.com";
            var customer = await userManager.FindByEmailAsync(customerEmail);
            if (customer == null)
            {
                customer = new ApplicationUser
                {
                    UserName = customerEmail,
                    Email = customerEmail,
                    HoTen = "Khách Hàng Vàng",
                    NgaySinh = new DateTime(1988, 8, 8),
                    EmailConfirmed = true,
                    DiaChi = "456 Phố Huế, Hai Bà Trưng, Hà Nội",
                    DiemTichLuy = 1500, // VIP Gold!
                    PhoneNumber = "0987654321"
                };

                await userManager.CreateAsync(customer, "Customer@123");
                await userManager.AddToRoleAsync(customer, "User");
            }
            else if (string.IsNullOrEmpty(customer.PhoneNumber))
            {
                customer.PhoneNumber = "0987654321";
                await userManager.UpdateAsync(customer);
            }

            await SeedMedicines(context);
        }

        private static async Task SeedMedicines(PharmacyDbContext context)
        {
            // First check if categories exist
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

            var keDon = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thuốc kê đơn");
            var khongKeDon = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thuốc không kê đơn");
            var tpcn = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thực phẩm chức năng");
            var duocMyPham = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Dược mỹ phẩm");

            if (keDon == null || khongKeDon == null || tpcn == null || duocMyPham == null) return;

            // Remove existing medicines to recreate with complete schema
            var existingDrugs = await context.Thuocs.ToListAsync();
            if (existingDrugs.Any())
            {
                // To prevent foreign key errors, clean up old references or skip clean up
                // We'll update the records if they don't have new columns filled, or clear them out.
                // Cleanest way is clearing and re-seeding since it's a dev database
                try
                {
                    context.ChiTietHoaDons.RemoveRange(context.ChiTietHoaDons);
                    context.GioHangs.RemoveRange(context.GioHangs);
                    context.YeuThiches.RemoveRange(context.YeuThiches);
                    context.Thuocs.RemoveRange(existingDrugs);
                    await context.SaveChangesAsync();
                }
                catch (Exception)
                {
                    // Ignore if some tables don't exist yet or foreign keys conflict
                }
            }

            if (!context.Thuocs.Any())
            {
                var drugs = new List<Thuoc>
                {
                    new Thuoc
                    {
                        TenThuoc = "Paracetamol 500mg",
                        DonGia = 1500,
                        SoLuong = 500,
                        MoTa = "Thuốc giảm đau, hạ sốt hiệu quả nhanh. Dùng điều trị các chứng đau đầu, đau răng, đau cơ, sốt do cảm cúm.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = khongKeDon.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Paracetamol",
                        ViTriKe = "Kệ A - Tầng 1",
                        CongDung = "Giảm đau đầu, đau răng, đau cơ, hạ sốt do cảm lạnh hoặc cảm cúm.",
                        ChongChiDinh = "Người có tiền sử mẫn cảm với paracetamol, bệnh nhân suy gan nặng.",
                        LieuLuong = "Uống 1-2 viên/lần, cách nhau 4-6 giờ. Không quá 4g/ngày.",
                        NhomDieuTri = "Hạ sốt - Giảm đau"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Panadol Extra",
                        DonGia = 2000,
                        SoLuong = 600,
                        MoTa = "Panadol Extra chứa Paracetamol và Caffeine giúp giảm đau đầu, đau nửa đầu, đau họng và sốt hiệu quả.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = khongKeDon.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Paracetamol",
                        ViTriKe = "Kệ A - Tầng 2",
                        CongDung = "Giảm đau đầu, đau răng, đau họng, đau cơ xương, hạ sốt nhanh.",
                        ChongChiDinh = "Mẫn cảm với thành phần của thuốc, trẻ em dưới 12 tuổi.",
                        LieuLuong = "Uống 1-2 viên/lần, cách nhau 4-6 giờ. Tối đa 8 viên/ngày.",
                        NhomDieuTri = "Hạ sốt - Giảm đau"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Decolgen Forte",
                        DonGia = 1800,
                        SoLuong = 400,
                        MoTa = "Điều trị các triệu chứng cảm cúm như nghẹt mũi, sổ mũi, hắt hơi, sốt và nhức đầu.",
                        HanSuDung = DateTime.Now.AddDays(15), // EXPIRED SOON FOR FEFO TEST
                        LoaiThuocId = khongKeDon.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Ibuprofen", // Mocked to trigger conflict check with Paracetamol
                        ViTriKe = "Kệ B - Tầng 1",
                        CongDung = "Điều trị các triệu chứng cảm cúm, sổ mũi, nghẹt mũi, viêm mũi dị ứng, sốt.",
                        ChongChiDinh = "Suy gan, suy thận nặng, người có bệnh mạch vành hoặc huyết áp cao.",
                        LieuLuong = "Uống 1 viên/lần, ngày 3-4 lần sau bữa ăn.",
                        NhomDieuTri = "Hô hấp - Cảm cúm"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Amoxicillin 500mg",
                        DonGia = 3000,
                        SoLuong = 300,
                        MoTa = "Thuốc kháng sinh bán tổng hợp nhóm beta-lactam. Dùng điều trị các bệnh nhiễm trùng đường hô hấp, tiết niệu.",
                        HanSuDung = DateTime.Now.AddYears(1),
                        LoaiThuocId = keDon.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Amoxicillin",
                        ViTriKe = "Kệ C - Tầng 2",
                        CongDung = "Điều trị nhiễm khuẩn tai mũi họng, nhiễm khuẩn đường hô hấp dưới, nhiễm khuẩn tiết niệu.",
                        ChongChiDinh = "Người có tiền sử dị ứng với kháng sinh nhóm Penicillin hoặc Cephalosporin.",
                        LieuLuong = "Uống 1 viên/lần, cách nhau 8 giờ. Dùng theo chỉ định của bác sĩ.",
                        NhomDieuTri = "Kháng sinh - Kháng viêm"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Vitamin C 1000mg Enervon",
                        DonGia = 4000,
                        SoLuong = 250,
                        MoTa = "Bổ sung Vitamin C và các vitamin nhóm B. Hỗ trợ tăng cường sức kháng, phục hồi sức khỏe sau ốm.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = tpcn.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Vitamin C",
                        ViTriKe = "Kệ D - Tầng 1",
                        CongDung = "Bổ sung vitamin C, hỗ trợ tăng đề kháng cơ thể, giảm mệt mỏi.",
                        ChongChiDinh = "Người bị sỏi thận, tăng oxalate niệu.",
                        LieuLuong = "Uống 1 viên mỗi ngày sau bữa ăn sáng.",
                        NhomDieuTri = "Bổ sung đề kháng - Vitamin"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Sữa rửa mặt Cetaphil Gentle 125ml",
                        DonGia = 125000,
                        SoLuong = 30,
                        MoTa = "Sữa rửa mặt dịu nhẹ cho mọi loại da, đặc biệt là da nhạy cảm. Không chứa xà phòng, cân bằng độ pH.",
                        HanSuDung = DateTime.Now.AddYears(3),
                        LoaiThuocId = duocMyPham.Id,
                        DonViCoBan = "Chai",
                        HoatChat = "Cetaphil Formula",
                        ViTriKe = "Kệ Mỹ phẩm - Quầy trước",
                        CongDung = "Làm sạch dịu nhẹ da mặt, giữ ẩm, ngăn ngừa mụn.",
                        ChongChiDinh = "Không có chong chỉ định đặc biệt, dùng ngoài da.",
                        LieuLuong = "Dùng rửa mặt ngày 2 lần Sáng và Tối.",
                        NhomDieuTri = "Chăm sóc da - Dược mỹ phẩm"
                    }
                };

                await context.Thuocs.AddRangeAsync(drugs);
                await context.SaveChangesAsync();
            }

            // Seed LoThuoc (Batches) for FEFO & Expiry alerts
            if (!context.LoThuocs.Any())
            {
                var paracetamol = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Paracetamol 500mg");
                var panadol = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Panadol Extra");
                var decolgen = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Decolgen Forte");
                var amox = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Amoxicillin 500mg");

                if (paracetamol != null && panadol != null && decolgen != null && amox != null)
                {
                    var batches = new List<LoThuoc>
                    {
                        // Paracetamol Batches
                        new LoThuoc { MaThuoc = paracetamol.MaThuoc, SoLo = "LOT-PARA-01", SoLuong = 150, NgaySanXuat = DateTime.Now.AddMonths(-6), HanSuDung = DateTime.Now.AddDays(10) }, // Near expiry!
                        new LoThuoc { MaThuoc = paracetamol.MaThuoc, SoLo = "LOT-PARA-02", SoLuong = 350, NgaySanXuat = DateTime.Now.AddMonths(-1), HanSuDung = DateTime.Now.AddYears(2) },
                        
                        // Panadol Batches
                        new LoThuoc { MaThuoc = panadol.MaThuoc, SoLo = "LOT-PANA-01", SoLuong = 200, NgaySanXuat = DateTime.Now.AddMonths(-8), HanSuDung = DateTime.Now.AddDays(25) }, // Near expiry!
                        new LoThuoc { MaThuoc = panadol.MaThuoc, SoLo = "LOT-PANA-02", SoLuong = 400, NgaySanXuat = DateTime.Now.AddMonths(-2), HanSuDung = DateTime.Now.AddYears(2) },

                        // Decolgen Batches
                        new LoThuoc { MaThuoc = decolgen.MaThuoc, SoLo = "LOT-DECOL-01", SoLuong = 100, NgaySanXuat = DateTime.Now.AddMonths(-11), HanSuDung = DateTime.Now.AddDays(15) }, // Near expiry!
                        new LoThuoc { MaThuoc = decolgen.MaThuoc, SoLo = "LOT-DECOL-02", SoLuong = 300, NgaySanXuat = DateTime.Now.AddMonths(-2), HanSuDung = DateTime.Now.AddYears(1) },

                        // Amox Batches
                        new LoThuoc { MaThuoc = amox.MaThuoc, SoLo = "LOT-AMOX-01", SoLuong = 300, NgaySanXuat = DateTime.Now.AddMonths(-3), HanSuDung = DateTime.Now.AddYears(1) }
                    };
                    await context.LoThuocs.AddRangeAsync(batches);
                    await context.SaveChangesAsync();
                }
            }

            // Seed DonViQuyDoi (Unit Conversion Matrix)
            if (!context.DonViQuyDois.Any())
            {
                var paracetamol = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Paracetamol 500mg");
                var panadol = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Panadol Extra");

                if (paracetamol != null && panadol != null)
                {
                    var conversions = new List<DonViQuyDoi>
                    {
                        // Paracetamol: 1 Vỉ = 10 Viên, 1 Hộp = 10 Vỉ = 100 Viên
                        new DonViQuyDoi { MaThuoc = paracetamol.MaThuoc, TenDonVi = "Vỉ", TyLeQuyDoi = 10, HeSoGia = 9.8m }, // 9.8x price of 1 base unit
                        new DonViQuyDoi { MaThuoc = paracetamol.MaThuoc, TenDonVi = "Hộp", TyLeQuyDoi = 100, HeSoGia = 95.0m }, // 95x price of 1 base unit (5% discount)

                        // Panadol Extra
                        new DonViQuyDoi { MaThuoc = panadol.MaThuoc, TenDonVi = "Vỉ", TyLeQuyDoi = 12, HeSoGia = 11.8m },
                        new DonViQuyDoi { MaThuoc = panadol.MaThuoc, TenDonVi = "Hộp", TyLeQuyDoi = 120, HeSoGia = 114.0m }
                    };
                    await context.DonViQuyDois.AddRangeAsync(conversions);
                    await context.SaveChangesAsync();
                }
            }

            // Seed DonThuoc (Sample Prescriptions)
            if (!context.DonThuocs.Any())
            {
                var amox = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Amoxicillin 500mg");
                var para = context.Thuocs.FirstOrDefault(t => t.TenThuoc == "Paracetamol 500mg");

                if (amox != null && para != null)
                {
                    var prescriptions = new List<DonThuoc>
                    {
                        new DonThuoc
                        {
                            TenBenhNhan = "Trần Văn Khỏe",
                            BacSiKeDon = "Bác sĩ Lê Hoàng",
                            ChanDoan = "Viêm họng cấp & sốt nhẹ",
                            NgayKeDon = DateTime.Now.AddDays(-1),
                            TrangThai = "ChoDuyet",
                            HinhAnhDonThuoc = "/images/sample_pres.png"
                        },
                        new DonThuoc
                        {
                            TenBenhNhan = "Nguyễn Thị Yếu",
                            BacSiKeDon = "Bác sĩ Nguyễn Thị Minh",
                            ChanDoan = "Cảm cúm và suy nhược",
                            NgayKeDon = DateTime.Now.AddDays(-3),
                            TrangThai = "DaBan",
                            HinhAnhDonThuoc = null
                        }
                    };

                    await context.DonThuocs.AddRangeAsync(prescriptions);
                    await context.SaveChangesAsync();

                    // Seed Details
                    var pres1 = prescriptions[0];
                    var pres2 = prescriptions[1];

                    var details = new List<ChiTietDonThuoc>
                    {
                        new ChiTietDonThuoc { DonThuocId = pres1.Id, MaThuoc = amox.MaThuoc, SoLuong = 15, LieuDung = "Uống 1 viên mỗi lần, ngày 3 lần sau ăn, liên tục trong 5 ngày." },
                        new ChiTietDonThuoc { DonThuocId = pres1.Id, MaThuoc = para.MaThuoc, SoLuong = 10, LieuDung = "Uống 1 viên khi sốt > 38.5 độ C, cách nhau ít nhất 4-6 giờ." },

                        new ChiTietDonThuoc { DonThuocId = pres2.Id, MaThuoc = para.MaThuoc, SoLuong = 10, LieuDung = "Uống 1 viên khi sốt." }
                    };
                    await context.ChiTietDonThuocs.AddRangeAsync(details);
                    await context.SaveChangesAsync();
                }
            }
        }
    }
}