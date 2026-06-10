using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Models;
namespace Web_Quản_Lí_Nhà_Thuốc.Data
{
    public class PharmacyDbContext : IdentityDbContext<ApplicationUser>
    {
        public PharmacyDbContext(
            DbContextOptions<PharmacyDbContext> options)
            : base(options)
        {
        }
        public DbSet<Thuoc> Thuocs { get; set; }

        public DbSet<LoaiThuoc> LoaiThuocs { get; set; }

        public DbSet<GioHang> GioHangs { get; set; }

        public DbSet<HoaDon> HoaDons { get; set; }

        public DbSet<ChiTietHoaDon> ChiTietHoaDons { get; set; }

        public DbSet<YeuThich> YeuThiches { get; set; }
    }
}
