using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class DonThuoc
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string TenBenhNhan { get; set; }

        [Required]
        [StringLength(100)]
        public string BacSiKeDon { get; set; }

        [Required]
        [StringLength(250)]
        public string ChanDoan { get; set; }

        [Required]
        public DateTime NgayKeDon { get; set; } = DateTime.Now;

        [Required]
        [StringLength(50)]
        public string TrangThai { get; set; } = "ChoDuyet"; // ChoDuyet, DaDuyet, DaBan, DaHuy

        public string? HinhAnhDonThuoc { get; set; }

        public ICollection<ChiTietDonThuoc>? ChiTietDonThuocs { get; set; }
    }
}
