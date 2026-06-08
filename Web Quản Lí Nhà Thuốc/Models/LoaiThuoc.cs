using System.ComponentModel.DataAnnotations;
namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class LoaiThuoc
    {
        public int Id { get; set; }

        [Required]
        public string TenLoai { get; set; }
    }
}
