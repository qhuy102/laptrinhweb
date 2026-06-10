using System.ComponentModel.DataAnnotations;
namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class ChiTietHoaDon
    {
        [Key]
        public int Id { get; set; }

        public int MaHoaDon { get; set; }

        public int MaThuoc { get; set; }

        public int SoLuong { get; set; }

        public decimal DonGia { get; set; }

        public HoaDon HoaDon { get; set; }

        public Thuoc Thuoc { get; set; }
    }
}
