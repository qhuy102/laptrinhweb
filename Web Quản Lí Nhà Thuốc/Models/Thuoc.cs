using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class Thuoc
    {
        [Key]
        public int MaThuoc { get; set; }

        [Required(ErrorMessage = "Tên thuốc không được để trống")]
        public string TenThuoc { get; set; }

        [Required(ErrorMessage = "Đơn giá không được để trống")]
        public decimal DonGia { get; set; }

        [Required(ErrorMessage = "Số lượng không được để trống")]
        public int SoLuong { get; set; }

        public string? MoTa { get; set; }

        public string? HinhAnh { get; set; }

        [Required(ErrorMessage = "Hạn sử dụng không được để trống")]
        public DateTime HanSuDung { get; set; }

        [Required]
        public int LoaiThuocId { get; set; }

        public LoaiThuoc? LoaiThuoc { get; set; }

        [Required(ErrorMessage = "Đơn vị cơ bản không được để trống")]
        [StringLength(50)]
        public string DonViCoBan { get; set; } = "Viên";

        [StringLength(150)]
        public string? HoatChat { get; set; }

        [StringLength(100)]
        public string? ViTriKe { get; set; } // e.g. "Kệ A - Tầng 2"

        public string? CongDung { get; set; }

        public string? ChongChiDinh { get; set; }

        public string? LieuLuong { get; set; }

        [StringLength(100)]
        public string? NhomDieuTri { get; set; }

        public ICollection<LoThuoc>? LoThuocs { get; set; }

        public ICollection<DonViQuyDoi>? DonViQuyDois { get; set; }
    }
}