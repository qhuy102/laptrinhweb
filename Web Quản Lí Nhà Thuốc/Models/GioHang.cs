using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class GioHang
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }

        public int MaThuoc { get; set; }

        public int SoLuong { get; set; }

        public Thuoc Thuoc { get; set; }

        public ApplicationUser User { get; set; }
    }
}
