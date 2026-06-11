using System;
using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class LichUongThuoc
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Tên thuốc không được để trống")]
        [StringLength(150)]
        public string TenThuoc { get; set; }

        [Required(ErrorMessage = "Liều lượng không được để trống")]
        [StringLength(100)]
        public string LieuDung { get; set; } // e.g. "1 viên", "10ml"

        [Required(ErrorMessage = "Giờ uống không được để trống")]
        [StringLength(100)]
        public string GioUong { get; set; } // e.g. "08:00, 20:00"

        [StringLength(100)]
        public string? GhiChu { get; set; } // e.g. "Sau ăn", "Trước ăn 30 phút"

        public bool DaUongHomNay { get; set; } = false;

        public DateTime NgayCapNhat { get; set; } = DateTime.Today;
    }
}
