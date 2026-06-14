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

                IF OBJECT_ID(N'Benhs', N'U') IS NULL
                BEGIN
                    CREATE TABLE Benhs (
                        Id int IDENTITY(1,1) PRIMARY KEY,
                        MaBenh nvarchar(150) NOT NULL,
                        TenBenh nvarchar(150) NOT NULL,
                        NhomBenh nvarchar(100) NOT NULL,
                        TrieuChungCodes nvarchar(max) NULL,
                        MoTaTrieuChung nvarchar(max) NULL,
                        TongQuan nvarchar(max) NULL,
                        NguyenNhan nvarchar(max) NULL,
                        PhongNgua nvarchar(max) NULL,
                        ThuocKhuyenDung nvarchar(max) NULL
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
            await SeedDiseases(context);
        }

        private static async Task SeedMedicines(PharmacyDbContext context)
        {
            // Ensure all 7 categories are seeded
            var catNames = new List<string> { "Thuốc kê đơn", "Thuốc không kê đơn", "Thực phẩm chức năng", "Dược mỹ phẩm", "Mẹ và bé", "Thiết bị y tế", "Sản phẩm tiện lợi" };
            foreach (var name in catNames)
            {
                if (!context.LoaiThuocs.Any(c => c.TenLoai == name))
                {
                    await context.LoaiThuocs.AddAsync(new LoaiThuoc { TenLoai = name });
                }
            }
            await context.SaveChangesAsync();

            var keDon = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thuốc kê đơn");
            var khongKeDon = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thuốc không kê đơn");
            var tpcn = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thực phẩm chức năng");
            var duocMyPham = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Dược mỹ phẩm");
            var meBe = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Mẹ và bé");
            var thietBiYTe = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Thiết bị y tế");
            var sanPhamTienLoi = context.LoaiThuocs.FirstOrDefault(c => c.TenLoai == "Sản phẩm tiện lợi");

            if (keDon == null || khongKeDon == null || tpcn == null || duocMyPham == null || meBe == null || thietBiYTe == null || sanPhamTienLoi == null) return;

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
                        NhomDieuTri = "Tăng đề kháng & Miễn dịch"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Ginkgo Biloba 120mg",
                        DonGia = 8500,
                        SoLuong = 180,
                        MoTa = "Hỗ trợ tăng cường tuần hoàn não, giảm các triệu chứng thiểu năng tuần hoàn não như đau đầu, chóng mặt, suy giảm trí nhớ.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = tpcn.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Ginkgo Biloba",
                        ViTriKe = "Kệ D - Tầng 2",
                        CongDung = "Cải thiện trí nhớ, giảm căng thẳng, tăng cường lưu thông máu não.",
                        ChongChiDinh = "Người chuẩn bị phẫu thuật, phụ nữ mang thai hoặc đang trong kỳ kinh nguyệt.",
                        LieuLuong = "Uống 1-2 viên/ngày sau bữa ăn.",
                        NhomDieuTri = "Bổ não & Giảm căng thẳng"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Viên uống Dầu Cá Omega-3",
                        DonGia = 5500,
                        SoLuong = 220,
                        MoTa = "Bổ sung axit béo Omega-3 tốt cho mắt, tim mạch và não bộ. Hỗ trợ giảm cholesterol máu, phòng ngừa xơ vữa động mạch.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = tpcn.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Fish Oil Omega-3",
                        ViTriKe = "Kệ D - Tầng 3",
                        CongDung = "Hỗ trợ tim mạch khỏe mạnh, giảm mỏi mắt, khô mắt, phát triển trí não.",
                        ChongChiDinh = "Mẫn cảm với dầu cá hoặc các thành phần của sản phẩm.",
                        LieuLuong = "Uống 1 viên/lần, ngày 2 lần sau ăn.",
                        NhomDieuTri = "Bổ mắt & Tim mạch"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Glucosamine Chondroitin 1500mg",
                        DonGia = 9500,
                        SoLuong = 140,
                        MoTa = "Giúp tái tạo sụn khớp, tăng tiết dịch khớp bôi trơn các khớp xương. Hỗ trợ giảm đau khớp do khô khớp, viêm khớp.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = tpcn.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Glucosamine",
                        ViTriKe = "Kệ D - Tầng 4",
                        CongDung = "Tăng độ dẻo dai của khớp, giảm đau khớp, tái tạo sụn.",
                        ChongChiDinh = "Người dưới 18 tuổi, phụ nữ có thai hoặc đang cho con bú.",
                        LieuLuong = "Uống 1 viên mỗi ngày sau bữa ăn.",
                        NhomDieuTri = "Xương khớp chắc khỏe"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Men vi sinh Optibac Probiotics",
                        DonGia = 12000,
                        SoLuong = 100,
                        MoTa = "Cung cấp hàng tỷ lợi khuẩn giúp cân bằng hệ vi sinh đường ruột. Hỗ trợ tiêu hóa khỏe mạnh, giảm đầy hơi, táo bón.",
                        HanSuDung = DateTime.Now.AddYears(1),
                        LoaiThuocId = tpcn.Id,
                        DonViCoBan = "Gói",
                        HoatChat = "Lợi khuẩn đường ruột",
                        ViTriKe = "Kệ E - Tầng 1",
                        CongDung = "Bổ sung lợi khuẩn, cải thiện các rối loạn tiêu hóa, tăng khả năng hấp thu.",
                        ChongChiDinh = "Không có chống chỉ định đặc biệt.",
                        LieuLuong = "Hòa 1 gói với nước nguội uống mỗi buổi sáng.",
                        NhomDieuTri = "Hỗ trợ tiêu hóa"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Viên uống Collagen Glow & White",
                        DonGia = 15000,
                        SoLuong = 80,
                        MoTa = "Bổ sung Collagen peptide, Vitamin E và Glutathione giúp dưỡng trắng da từ sâu bên trong, tăng độ đàn hồi, ngăn ngừa lão hóa.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = tpcn.Id,
                        DonViCoBan = "Viên",
                        HoatChat = "Collagen & Glutathione",
                        ViTriKe = "Kệ E - Tầng 2",
                        CongDung = "Làm chậm quá trình lão hóa da, dưỡng sáng da, giảm thâm nám, giúp tóc móng chắc khỏe.",
                        ChongChiDinh = "Mẫn cảm với các thành phần của sản phẩm.",
                        LieuLuong = "Uống 2 viên mỗi ngày trước khi đi ngủ.",
                        NhomDieuTri = "Đẹp da & Chống lão hóa"
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
                    },
                    new Thuoc
                    {
                        TenThuoc = "Sữa bột Similac Newborn 400g",
                        DonGia = 285000,
                        SoLuong = 40,
                        MoTa = "Dinh dưỡng công thức cho trẻ từ 0 - 6 tháng tuổi, bổ sung HMO và DHA giúp trẻ phát triển não bộ và tăng sức đề kháng tự nhiên.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = meBe.Id,
                        DonViCoBan = "Hộp",
                        HoatChat = "Sữa bột công thức",
                        ViTriKe = "Kệ Mẹ & Bé - Tầng 1",
                        CongDung = "Thay thế bữa ăn phụ hoặc bổ sung dinh dưỡng thiếu hụt cho bé sơ sinh.",
                        ChongChiDinh = "Trẻ bị dị ứng đạm sữa bò.",
                        LieuLuong = "Pha theo bảng hướng dẫn trên vỏ hộp sữa bột.",
                        NhomDieuTri = "Trẻ sơ sinh (0 - 6 tháng)"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Tã dán Bobby Size S 56 miếng",
                        DonGia = 165000,
                        SoLuong = 60,
                        MoTa = "Tã dán siêu thấm, thiết kế mỏng nhẹ, bề mặt 3D giúp mông bé luôn khô thoáng, ngăn ngừa hăm tã hiệu quả.",
                        HanSuDung = DateTime.Now.AddYears(3),
                        LoaiThuocId = meBe.Id,
                        DonViCoBan = "Gói",
                        HoatChat = "Tã giấy em bé",
                        ViTriKe = "Kệ Mẹ & Bé - Tầng 2",
                        CongDung = "Thấm hút chất thải, giữ vệ sinh cho em bé.",
                        ChongChiDinh = "Không dùng khi da trẻ đang bị viêm nhiễm nặng vùng tã.",
                        LieuLuong = "Thay tã sau mỗi 3-4 tiếng hoặc sau khi bé tiêu bẩn.",
                        NhomDieuTri = "Trẻ sơ sinh (0 - 6 tháng)"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Vitamin Prenatal DHA cho mẹ bầu",
                        DonGia = 350000,
                        SoLuong = 50,
                        MoTa = "Viên uống tổng hợp bổ sung 20 loại vitamin, khoáng chất và DHA chất lượng cao hỗ trợ sự phát triển thai nhi khỏe mạnh.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = meBe.Id,
                        DonViCoBan = "Hộp",
                        HoatChat = "DHA & Multivitamins",
                        ViTriKe = "Kệ Mẹ & Bé - Tầng 3",
                        CongDung = "Bổ sung vitamin tổng hợp và DHA cho phụ nữ chuẩn bị mang thai, đang mang thai và cho con bú.",
                        ChongChiDinh = "Người mẫn cảm với bất cứ thành phần nào của thuốc.",
                        LieuLuong = "Uống 1 viên mỗi ngày sau bữa ăn.",
                        NhomDieuTri = "Dành cho Mẹ bầu"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Máy đo huyết áp Omron HEM-7121",
                        DonGia = 980000,
                        SoLuong = 20,
                        MoTa = "Máy đo huyết áp bắp tay tự động, sử dụng công nghệ Intellisense tiên tiến cho kết quả nhanh và chính xác cao.",
                        HanSuDung = DateTime.Now.AddYears(5),
                        LoaiThuocId = thietBiYTe.Id,
                        DonViCoBan = "Bộ",
                        HoatChat = "Thiết bị đo dao động",
                        ViTriKe = "Kệ Thiết bị - Tầng 1",
                        CongDung = "Theo dõi huyết áp và nhịp tim tự động tại nhà.",
                        ChongChiDinh = "Không đo trên tay có vết thương hở hoặc đang truyền dịch.",
                        LieuLuong = "Đo ngày 1-2 lần vào buổi sáng trước khi ăn và tối trước khi đi ngủ.",
                        NhomDieuTri = "Máy đo huyết áp"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Nhiệt kế hồng ngoại Microlife FR1MF1",
                        DonGia = 650000,
                        SoLuong = 25,
                        MoTa = "Nhiệt kế đo trán không tiếp xúc, cho kết quả đo chính xác chỉ trong 1 giây, có cảnh báo sốt thông minh bằng đèn.",
                        HanSuDung = DateTime.Now.AddYears(5),
                        LoaiThuocId = thietBiYTe.Id,
                        DonViCoBan = "Cái",
                        HoatChat = "Cảm biến hồng ngoại",
                        ViTriKe = "Kệ Thiết bị - Tầng 2",
                        CongDung = "Đo thân nhiệt cơ thể, nhiệt độ nước tắm, sữa cho trẻ.",
                        ChongChiDinh = "Không có chong chỉ định.",
                        LieuLuong = "Để đầu dò cách trán 1-3cm, bấm nút đo trong 1 giây.",
                        NhomDieuTri = "Nhiệt kế"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Máy đo SpO2 cầm tay thông minh",
                        DonGia = 320000,
                        SoLuong = 35,
                        MoTa = "Thiết bị đo nồng độ oxy trong máu SpO2 và nhịp tim qua đầu ngón tay nhỏ gọn, màn hình hiển thị trực quan rõ nét.",
                        HanSuDung = DateTime.Now.AddYears(5),
                        LoaiThuocId = thietBiYTe.Id,
                        DonViCoBan = "Cái",
                        HoatChat = "Cảm biến quang học SpO2",
                        ViTriKe = "Kệ Thiết bị - Tầng 3",
                        CongDung = "Kiểm tra nhanh nhịp tim và độ bão hòa oxy SpO2 cơ thể tại nhà.",
                        ChongChiDinh = "Không dùng trên ngón tay có sơn móng tay quá dày hoặc bị thương nặng.",
                        LieuLuong = "Kẹp vào ngón tay, giữ yên tay trong 10-15 giây để đọc kết quả.",
                        NhomDieuTri = "Máy đo SpO2"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Khẩu trang y tế 4 lớp kháng khuẩn",
                        DonGia = 45000,
                        SoLuong = 120,
                        MoTa = "Khẩu trang cấu trúc 4 lớp lọc bụi mịn, kháng khuẩn vượt trội, dây đeo tai co giãn êm ái thoải mái.",
                        HanSuDung = DateTime.Now.AddYears(3),
                        LoaiThuocId = sanPhamTienLoi.Id,
                        DonViCoBan = "Hộp",
                        HoatChat = "Vải không dệt & màng lọc",
                        ViTriKe = "Kệ Tiện Lợi - Quầy chính",
                        CongDung = "Lọc khói bụi, kháng khuẩn, phòng tránh các bệnh lây lan qua đường hô hấp.",
                        ChongChiDinh = "Không dùng khẩu trang đã giặt đi giặt lại hoặc bị ướt rách.",
                        LieuLuong = "Sử dụng khẩu trang 1 lần khi đi ra ngoài hoặc làm việc môi trường khói bụi.",
                        NhomDieuTri = "Khẩu trang & Sát khuẩn"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Nước rửa tay Lifebuoy 500ml",
                        DonGia = 85000,
                        SoLuong = 70,
                        MoTa = "Sữa rửa tay diệt khuẩn vượt trội, bảo vệ khỏi 99.9% vi khuẩn gây hại, bổ sung tinh chất dưỡng da ẩm mịn.",
                        HanSuDung = DateTime.Now.AddYears(3),
                        LoaiThuocId = sanPhamTienLoi.Id,
                        DonViCoBan = "Chai",
                        HoatChat = "Công thức bảo vệ diệt khuẩn",
                        ViTriKe = "Kệ Tiện Lợi - Tầng 1",
                        CongDung = "Làm sạch tay, sát khuẩn bảo vệ sức khỏe gia đình.",
                        ChongChiDinh = "Tránh tiếp xúc trực tiếp với mắt. Nếu dính vào mắt phải rửa bằng nước sạch.",
                        LieuLuong = "Lấy một lượng vừa đủ xoa đều tay trong 20 giây rồi rửa lại bằng nước sạch.",
                        NhomDieuTri = "Khẩu trang & Sát khuẩn"
                    },
                    new Thuoc
                    {
                        TenThuoc = "Kẹo ngậm ho thảo dược Strepsils Cool",
                        DonGia = 35000,
                        SoLuong = 150,
                        MoTa = "Kẹo ngậm ho thảo dược vị bạc hà mát lạnh, giúp kháng khuẩn nhẹ, làm dịu nhanh rát họng và giảm ho tức thì.",
                        HanSuDung = DateTime.Now.AddYears(2),
                        LoaiThuocId = sanPhamTienLoi.Id,
                        DonViCoBan = "Hộp",
                        HoatChat = "Amylmetacresol & Dichlorobenzyl Alcohol",
                        ViTriKe = "Kệ Tiện Lợi - Tầng 2",
                        CongDung = "Giảm rát họng, giảm ho, thông mũi mát họng.",
                        ChongChiDinh = "Trẻ em dưới 6 tuổi.",
                        LieuLuong = "Ngậm 1 viên cách nhau 2-3 giờ. Tối đa 12 viên/ngày.",
                        NhomDieuTri = "Kẹo ngậm & Đồ uống"
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

        private static async Task SeedDiseases(PharmacyDbContext context)
        {
            if (!context.Benhs.Any())
            {
                var diseases = new List<Benh>
                {
                    new Benh
                    {
                        MaBenh = "cam-cum",
                        TenBenh = "Cảm cúm",
                        NhomBenh = "Hô hấp",
                        TrieuChungCodes = "sot,ho,dau-dau,dau-hong,so-mui,met-moi",
                        MoTaTrieuChung = "Sốt cao đột ngột, ho khan hoặc ho có đờm nhẹ, đau họng, nghẹt mũi hoặc chảy nước mũi, nhức đầu, đau mỏi toàn thân, mệt mỏi rã rời.",
                        TongQuan = "Cảm cúm là bệnh truyền nhiễm cấp tính đường hô hấp do các chủng virus cúm (thường gặp nhất là cúm A và cúm B) gây ra. Bệnh lây lan rất nhanh qua các giọt bắn li ti khi người bệnh ho, hắt hơi hoặc khi tiếp xúc với các bề mặt bị ô nhiễm.",
                        NguyenNhan = "Nhiễm các chủng virus cúm lây lan qua không khí. Các yếu tố thuận lợi gồm hệ miễn dịch suy yếu, thời tiết chuyển mùa, tiếp xúc gần với người mang mầm bệnh.",
                        PhongNgua = "Tiêm vắc xin ngừa cúm hàng năm. Thường xuyên rửa sạch tay bằng xà phòng hoặc dung dịch sát khuẩn. Đeo khẩu trang khi ra ngoài hoặc đến nơi tập trung đông người. Tránh chạm tay vào mắt, mũi, miệng.",
                        ThuocKhuyenDung = "Paracetamol 500mg, Decolgen Forte, Panadol Extra"
                    },
                    new Benh
                    {
                        MaBenh = "viem-hong",
                        TenBenh = "Viêm họng",
                        NhomBenh = "Hô hấp",
                        TrieuChungCodes = "sot,ho,dau-hong",
                        MoTaTrieuChung = "Đau họng dữ dội hoặc ngứa rát cổ họng, nuốt đau hoặc nuốt vướng, sốt nhẹ hoặc sốt vừa, ho khan hoặc ho có đờm, amidan sưng to đỏ có thể mủ.",
                        TongQuan = "Viêm họng là tình trạng viêm nhiễm lớp niêm mạc lót trong cổ họng, gây đau rát, khó chịu. Bệnh vô cùng phổ biến, đặc biệt khi thời tiết thay đổi đột ngột hoặc môi trường ô nhiễm khói bụi.",
                        NguyenNhan = "Phần lớn (khoảng 80%) do nhiễm các loại virus đường hô hấp thông thường; phần còn lại do vi khuẩn (như liên cầu khuẩn Streptococcus) hoặc do các tác nhân kích ứng bên ngoài như khói thuốc, khói bụi, rượu bia.",
                        PhongNgua = "Giữ ấm cơ thể, đặc biệt là vùng cổ họng khi trời lạnh. Súc họng hàng ngày bằng nước muối sinh lý ấm. Hạn chế tối đa việc uống nước quá đá lạnh hoặc sử dụng đồ uống có cồn. Tránh khói thuốc và bụi bặm.",
                        ThuocKhuyenDung = "Amoxicillin 500mg, Panadol Extra"
                    },
                    new Benh
                    {
                        MaBenh = "viem-xoang",
                        TenBenh = "Viêm xoang cấp & mãn tính",
                        NhomBenh = "Hô hấp",
                        TrieuChungCodes = "dau-dau,so-mui",
                        MoTaTrieuChung = "Nghẹt mũi, chảy nước mũi đặc màu xanh hoặc vàng, đau nhức vùng xoang (trán, má, giữa hai mắt), đau đầu nhiều, giảm hoặc mất khứu giác.",
                        TongQuan = "Viêm xoang là tình trạng viêm các lớp niêm mạc lót trong lòng các xoang cạnh mũi, gây tắc nghẽn lưu thông dịch nhầy, dẫn đến nhiễm trùng cấp hoặc mãn tính.",
                        NguyenNhan = "Do nhiễm vi khuẩn, virus, nấm hoặc phản ứng dị ứng thời tiết, vách ngăn mũi bị lệch, hoặc do môi trường ô nhiễm, khói bụi độc hại kéo dài.",
                        PhongNgua = "Đeo khẩu trang khi ra ngoài đường bụi bặm. Tránh tiếp xúc với khói thuốc lá, phấn hoa hoặc chất kích thích. Súc rửa mũi hàng ngày bằng nước muối sinh lý.",
                        ThuocKhuyenDung = "Paracetamol 500mg, Otrivin 0.1%, Clopheniramin 4mg"
                    },
                    new Benh
                    {
                        MaBenh = "hen-suyen",
                        TenBenh = "Hen suyễn (Suyễn)",
                        NhomBenh = "Hô hấp",
                        TrieuChungCodes = "ho,kho-tho",
                        MoTaTrieuChung = "Khó thở thành cơn, thở khò khè nghe thấy tiếng rít, ho nhiều (đặc biệt vào ban đêm hoặc sáng sớm), nặng ngực, tức ngực.",
                        TongQuan = "Hen suyễn là bệnh lý viêm mạn tính của đường hô hấp. Khi gặp tác nhân kích thích, phế quản sẽ co thắt, sưng phù, tiết dịch nhầy gây tắc nghẽn đường thở.",
                        NguyenNhan = "Do cơ địa dị ứng (di truyền), tiếp xúc với chất dị nguyên (phấn hoa, lông thú, mạt bụi, nấm mốc), thay đổi thời tiết đột ngột, gắng sức quá mức, hoặc stress tâm lý.",
                        PhongNgua = "Tránh các tác nhân gây dị ứng đã biết. Giữ nhà cửa luôn sạch sẽ, thông thoáng, không nuôi thú cưng trong nhà nếu bị dị ứng. Sử dụng thuốc dự phòng hen đều đặn theo chỉ định.",
                        ThuocKhuyenDung = "Ventolin Evohaler, Symbicort Turbuhaler"
                    },
                    new Benh
                    {
                        MaBenh = "dau-da-day",
                        TenBenh = "Đau dạ dày (Viêm dạ dày)",
                        NhomBenh = "Tiêu hóa",
                        TrieuChungCodes = "dau-bung",
                        MoTaTrieuChung = "Đau rát hoặc đau âm ỉ vùng thượng vị (trên rốn), đầy bụng, chướng hơi, ăn mau no, ợ chua, ợ hơi, buồn nôn hoặc nôn mửa, có thể đau tăng lên khi đói hoặc ngay sau ăn.",
                        TongQuan = "Đau dạ dày là từ gọi chung cho tình trạng niêm mạc dạ dày bị viêm, loét gây đau đớn khó chịu. Đây là căn bệnh tiêu hóa vô cùng thường gặp ở mọi lứa tuổi do nhịp sống hiện đại.",
                        NguyenNhan = "Nhiễm vi khuẩn Helicobacter pylori (vi khuẩn HP), thói quen ăn uống thất thường (bỏ bữa, ăn vội), lạm dụng chất kích thích (rượu bia, thuốc lá, đồ cay nóng), stress tâm lý kéo dài, hoặc lạm dụng các loại thuốc giảm đau kháng viêm NSAIDs.",
                        PhongNgua = "Ăn uống đúng giờ giấc, nhai kỹ trước khi nuốt. Hạn chế thức ăn quá cay, chua, nhiều dầu mỡ và đồ uống chứa cồn hoặc ga. Giữ tinh thần thoải mái vui vẻ, hạn chế thức khuya.",
                        ThuocKhuyenDung = "PhosphaLugel, Nexium 20mg, Maalox"
                    },
                    new Benh
                    {
                        MaBenh = "trao-nguoc-gerd",
                        TenBenh = "Trào ngược dạ dày thực quản (GERD)",
                        NhomBenh = "Tiêu hóa",
                        TrieuChungCodes = "dau-bung,dau-hong",
                        MoTaTrieuChung = "Ợ nóng (cảm giác nóng rát từ thượng vị lan lên cổ họng), ợ chua, đau ngực không do tim, nuốt vướng, ho khan kéo dài, khàn tiếng.",
                        TongQuan = "Trào ngược dạ dày thực quản (GERD) là tình trạng dịch dạ dày (axit, thức ăn, dịch mật) trào ngược lên thực quản thường xuyên, gây tổn thương niêm mạc thực quản và họng.",
                        NguyenNhan = "Suy yếu cơ vòng thực quản dưới, thoát vị hoành, tăng áp lực dạ dày (do béo phì, mang thai), ăn quá no, nằm ngay sau khi ăn, hoặc stress.",
                        PhongNgua = "Không nằm ngay sau khi ăn (chờ ít nhất 2-3 tiếng). Chia nhỏ bữa ăn trong ngày. Tránh ăn tối muộn hoặc sát giờ đi ngủ. Hạn chế bia rượu, sô-cô-la, đồ uống có ga.",
                        ThuocKhuyenDung = "Nexium 20mg, Gaviscon Duo, PhosphaLugel"
                    },
                    new Benh
                    {
                        MaBenh = "tieu-chay",
                        TenBenh = "Tiêu chảy cấp tính",
                        NhomBenh = "Tiêu hóa",
                        TrieuChungCodes = "sot,dau-bung,met-moi",
                        MoTaTrieuChung = "Đi ngoài phân lỏng hoặc nước trên 3 lần mỗi ngày, đau bụng âm ỉ hoặc quặn thắt, sốt nhẹ, mệt mỏi, khô miệng do mất nước.",
                        TongQuan = "Tiêu chảy cấp là tình trạng đi tiêu phân lỏng bất thường xảy ra đột ngột và kéo dài dưới 14 ngày. Nguy hiểm lớn nhất của bệnh là gây mất nước và chất điện giải nhanh chóng.",
                        NguyenNhan = "Nhiễm khuẩn đường ruột (vi khuẩn E.coli, Salmonella, virus Rotavirus từ thức ăn ôi thiu), ngộ độc thực phẩm, hoặc phản ứng phụ khi dùng thuốc kháng sinh.",
                        PhongNgua = "Thực hiện 'ăn chín uống sôi'. Rửa tay bằng xà phòng trước khi ăn và sau khi đi vệ sinh. Bảo quản thức ăn đúng cách, không ăn đồ ôi thiu hay thức ăn đường phố mất vệ sinh.",
                        ThuocKhuyenDung = "Oresol, Smecta, Loperamide"
                    },
                    new Benh
                    {
                        MaBenh = "tang-huyet-ap",
                        TenBenh = "Tăng huyết áp",
                        NhomBenh = "Tim mạch",
                        TrieuChungCodes = "dau-dau,met-moi",
                        MoTaTrieuChung = "Phần lớn không có triệu chứng rõ rệt ('kẻ giết người thầm lặng'). Khi huyết áp tăng cao đột ngột có thể gây nhức đầu vùng chẩm, chóng mặt, hoa mắt, ù tai, hồi hộp đánh trống ngực.",
                        TongQuan = "Tăng huyết áp là tình trạng áp lực máu tác động lên thành động mạch liên tục tăng cao. Nếu không kiểm soát, bệnh có thể dẫn đến nhiều biến chứng nguy hiểm như đột quỵ, nhồi máu cơ tim, suy tim và suy thận.",
                        NguyenNhan = "Yếu tố gia đình (di truyền), tuổi tác, chế độ ăn quá nhiều muối, béo phì, lười vận động cơ thể, hút thuốc lá và thường xuyên gặp stress căng thẳng trong cuộc sống.",
                        PhongNgua = "Thực hiện chế độ ăn nhạt (giảm lượng muối dưới 5g/ngày). Tăng cường rau xanh, hoa quả tươi. Thường xuyên tập thể dục (ít nhất 30 phút mỗi ngày). Giảm cân nếu bị thừa cân. Kiểm tra chỉ số huyết áp định kỳ.",
                        ThuocKhuyenDung = "Amlodipine, Losartan, Coveram"
                    },
                    new Benh
                    {
                        MaBenh = "thoai-hoa-khop",
                        TenBenh = "Thoái hóa khớp",
                        NhomBenh = "Xương khớp",
                        TrieuChungCodes = "dau-khop",
                        MoTaTrieuChung = "Đau âm ỉ khớp chịu lực (khớp gối, khớp háng, cột sống) tăng lên khi vận động và giảm đi khi nghỉ ngơi; cứng khớp vào buổi sáng sớm kéo dài dưới 30 phút, có tiếng lạo xạo khi cử động khớp.",
                        TongQuan = "Thoái hóa khớp là bệnh lý tổn thương sụn khớp và xương dưới sụn, đi kèm phản ứng viêm giảm dịch khớp. Bệnh gây đau đớn và có nguy cơ hạn chế khả năng đi lại của người cao tuổi.",
                        NguyenNhan = "Lão hóa tự nhiên do tuổi tác. Ngoài ra còn do thừa cân, béo phì (gây đè nén khớp), lao động nặng nhọc sai tư thế, chấn thương khớp trong quá khứ.",
                        PhongNgua = "Duy trì mức cân nặng hợp lý để giảm tải cho khớp gối. Luyện tập các môn thể thao nhẹ nhàng như bơi lội, đạp xe hoặc yoga. Bổ sung chế độ ăn giàu canxi, vitamin D.",
                        ThuocKhuyenDung = "Glucosamine, Paracetamol 500mg, Meloxicam"
                    },
                    new Benh
                    {
                        MaBenh = "benh-gout",
                        TenBenh = "Bệnh Gút (Gout)",
                        NhomBenh = "Xương khớp",
                        TrieuChungCodes = "sot,dau-khop",
                        MoTaTrieuChung = "Sưng nóng, đỏ và đau khớp đột ngột dữ dội (thường ở khớp ngón chân cái, cổ chân, đầu gối), có thể sốt nhẹ hoặc lạnh run khi cơn đau cấp xuất hiện.",
                        TongQuan = "Bệnh Gút là một dạng viêm khớp do rối loạn chuyển hóa purin làm tăng axit uric trong máu, dẫn đến tích tụ các tinh thể muối urat sắc nhọn tại các ổ khớp gây đau đớn dữ dội.",
                        NguyenNhan = "Chế độ ăn nhiều thực phẩm giàu purin (thịt đỏ, hải sản, nội tạng động vật), lạm dụng bia rượu, béo phì, suy giảm chức năng thận không đào thải được axit uric.",
                        PhongNgua = "Hạn chế rượu bia, đặc biệt là bia. Giảm ăn thịt đỏ, hải sản. Uống nhiều nước (2-3 lít/ngày) để tăng đào thải axit uric. Tập luyện thể thao đều đặn và kiểm soát cân nặng.",
                        ThuocKhuyenDung = "Colchicine, Meloxicam, Allopurinol"
                    },
                    new Benh
                    {
                        MaBenh = "di-ung-da",
                        TenBenh = "Dị ứng da (Mề đay / Viêm da)",
                        NhomBenh = "Da liễu",
                        TrieuChungCodes = "ngua",
                        MoTaTrieuChung = "Ngứa ngáy dữ dội tại vùng da bị tổn thương, xuất hiện mẩn đỏ, nốt sần, mề đay nổi thành từng mảng lớn, da khô ráp bong tróc, trường hợp nặng có thể rộp nước.",
                        TongQuan = "Dị ứng da là phản ứng quá mẫn của hệ thống miễn dịch cơ thể trước các tác nhân lạ khi tiếp xúc qua da hoặc qua đường ăn uống, biểu hiện trực tiếp trên bề mặt biểu bì da.",
                        NguyenNhan = "Tiếp xúc với dị ứng nguyên như mỹ phẩm lạ, hóa chất tẩy rửa, phấn hoa, lông động vật, thời tiết quá lạnh/nóng đột ngột, hoặc ăn các thực phẩm dễ kích ứng (hải sản, nhộng tằm).",
                        PhongNgua = "Ghi nhớ và tránh xa các tác nhân từng gây dị ứng. Dưỡng ẩm da đầy đủ bằng kem dịu nhẹ lành tính. Tắm nước ấm vừa phải, giữ vệ sinh cơ thể sạch sẽ.",
                        ThuocKhuyenDung = "Loratadine, Cetirizine, Fexofenadine"
                    },
                    new Benh
                    {
                        MaBenh = "sot-xuat-huyet",
                        TenBenh = "Sốt xuất huyết Dengue",
                        NhomBenh = "Truyền nhiễm",
                        TrieuChungCodes = "sot,dau-dau,ngua,met-moi",
                        MoTaTrieuChung = "Sốt cao đột ngột liên tục từ 2 đến 7 ngày không hạ; đau đầu dữ dội, đau sâu trong hốc mắt, đau mỏi cơ khớp; xuất hiện chấm xuất huyết dưới da, chảy máu cam hoặc chân răng, phát ban ngứa đỏ khi hạ sốt.",
                        TongQuan = "Sốt xuất huyết Dengue là bệnh truyền nhiễm cấp tính do virus Dengue gây ra, lây truyền chủ yếu qua trung gian muỗi vằn đốt. Bệnh có thể tiến triển nặng gây sốc mất máu nguy hiểm tính mạng.",
                        NguyenNhan = "Virus Dengue truyền qua vết muỗi vằn Aedes aegypti mang mầm bệnh đốt người lành.",
                        PhongNgua = "Diệt loăng quăng/bọ gậy bằng cách thả cá vào lu nước, đậy kín nắp lu bể. Dọn dẹp chai lọ đọng nước quanh nhà. Ngủ mùng kể cả ban ngày. Phun thuốc diệt muỗi định kỳ.",
                        ThuocKhuyenDung = "Paracetamol 500mg"
                    },
                    new Benh
                    {
                        MaBenh = "thuy-dau",
                        TenBenh = "Bệnh Thủy đậu (Trái rạ)",
                        NhomBenh = "Truyền nhiễm",
                        TrieuChungCodes = "sot,dau-dau,ngua,met-moi",
                        MoTaTrieuChung = "Sốt nhẹ, mệt mỏi, đau đầu, sau đó phát ban đỏ khắp cơ thể rồi tiến triển nhanh thành các mụn nước nhỏ lỏng bỏng ngứa ngáy dữ dội.",
                        TongQuan = "Thủy đậu là bệnh truyền nhiễm cấp tính do virus Varicella-Zoster gây ra. Bệnh lây qua đường hô hấp do tiếp xúc dịch tiết mũi họng hoặc chất dịch từ mụn nước của người bệnh.",
                        NguyenNhan = "Do nhiễm virus Varicella-Zoster lây truyền rất mạnh trong không khí hoặc tiếp xúc trực tiếp.",
                        PhongNgua = "Tiêm phòng vắc xin thủy đậu đầy đủ. Cách ly người bệnh để tránh lây lan cho gia đình và cộng đồng. Giữ vệ sinh thân thể sạch sẽ, không gãi vỡ mụn nước để tránh sẹo và nhiễm trùng da.",
                        ThuocKhuyenDung = "Acyclovir 400mg, Paracetamol 500mg"
                    }
                };

                await context.Benhs.AddRangeAsync(diseases);
                await context.SaveChangesAsync();
            }
        }
    }
}