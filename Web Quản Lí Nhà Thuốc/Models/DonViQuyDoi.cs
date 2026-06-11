using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class DonViQuyDoi
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MaThuoc { get; set; }

        [ForeignKey("MaThuoc")]
        public Thuoc? Thuoc { get; set; }

        [Required]
        [StringLength(50)]
        public string TenDonVi { get; set; }

        [Required]
        public int TyLeQuyDoi { get; set; }

        [Required]
        public decimal HeSoGia { get; set; }
    }
}
