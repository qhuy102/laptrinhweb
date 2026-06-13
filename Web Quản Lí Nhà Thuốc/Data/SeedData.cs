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