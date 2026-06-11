using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class ChiTietDonThuoc
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int DonThuocId { get; set; }

        [ForeignKey("DonThuocId")]
        public DonThuoc? DonThuoc { get; set; }

        [Required]
        public int MaThuoc { get; set; }

        [ForeignKey("MaThuoc")]
        public Thuoc? Thuoc { get; set; }

        [Required]
        public int SoLuong { get; set; }

        [Required]
        [StringLength(250)]
        public string LieuDung { get; set; }
    }
}
