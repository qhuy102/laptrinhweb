using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class Thuoc
    {
        [Key]
        public int MaThuoc { get; set; }

        public string TenThuoc { get; set; }

        public decimal DonGia { get; set; }

        public int SoLuong { get; set; }

        public string? MoTa { get; set; }

        public string? HinhAnh { get; set; }

        public DateTime HanSuDung { get; set; }

        public int LoaiThuocId { get; set; }

        public LoaiThuoc? LoaiThuoc { get; set; }
    }
}