using System;
using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; }

        [StringLength(100)]
        public string? UserEmail { get; set; }

        [Required]
        [StringLength(50)]
        public string Action { get; set; }

        [Required]
        public string MoTa { get; set; }

        [Required]
        public DateTime ThoiGian { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [Required]
        public bool IsSuspicious { get; set; } = false;
    }
}
