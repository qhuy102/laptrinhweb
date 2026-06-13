using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class Benh
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Mã bệnh không được để trống")]
        [Display(Name = "Mã bệnh (Slug)")]
        public string MaBenh { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên bệnh không được để trống")]
        [Display(Name = "Tên bệnh")]
        public string TenBenh { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nhóm bệnh không được để trống")]
        [Display(Name = "Nhóm bệnh")]
        public string NhomBenh { get; set; } = string.Empty; // e.g. Hô hấp, Tiêu hóa, Tim mạch, Xương khớp, Da liễu, Truyền nhiễm

        [Display(Name = "Mã triệu chứng (ngăn cách bởi dấu phẩy)")]
        public string? TrieuChungCodes { get; set; } = string.Empty; // e.g. "sot,ho,dau-hong"

        [Display(Name = "Triệu chứng lâm sàng")]
        public string? MoTaTrieuChung { get; set; } = string.Empty;

        [Display(Name = "Tổng quan bệnh lý")]
        public string? TongQuan { get; set; } = string.Empty;

        [Display(Name = "Nguyên nhân mắc bệnh")]
        public string? NguyenNhan { get; set; } = string.Empty;

        [Display(Name = "Phương pháp phòng ngừa")]
        public string? PhongNgua { get; set; } = string.Empty;

        [Display(Name = "Thuốc khuyên dùng (ngăn cách bởi dấu phẩy)")]
        public string? ThuocKhuyenDung { get; set; } = string.Empty; // e.g. "Paracetamol 500mg, Panadol Extra"
    }
}
