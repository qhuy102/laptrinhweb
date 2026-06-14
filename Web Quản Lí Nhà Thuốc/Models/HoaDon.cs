using System.ComponentModel.DataAnnotations;
namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class HoaDon
    {
        [Key]
        public int MaHoaDon { get; set; }

        public string UserId { get; set; }

        public DateTime NgayDat { get; set; }

        public decimal TongTien { get; set; }

        public string TrangThai { get; set; }

        public ApplicationUser User { get; set; }

        public ICollection<ChiTietHoaDon>? ChiTietHoaDons { get; set; }

        public double? ShipperLat { get; set; }
        
        public double? ShipperLng { get; set; }

        public string? DiaChiGiaoHang { get; set; }

        public double? CustomerLat { get; set; }

        public double? CustomerLng { get; set; }

        public string? PaymentMethod { get; set; }
    }
}
