using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class YeuThich
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }

        public int MaThuoc { get; set; }

        public ApplicationUser User { get; set; }

        public Thuoc Thuoc { get; set; }
    }
}
