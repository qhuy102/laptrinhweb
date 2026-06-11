using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class LoThuoc
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MaThuoc { get; set; }

        [ForeignKey("MaThuoc")]
        public Thuoc? Thuoc { get; set; }

        [Required]
        [StringLength(50)]
        public string SoLo { get; set; }

        [Required]
        public int SoLuong { get; set; }

        [Required]
        public DateTime NgaySanXuat { get; set; }

        [Required]
        public DateTime HanSuDung { get; set; }
    }
}
