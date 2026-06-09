using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Web_Quản_Lí_Nhà_Thuốc.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string HoTen { get; set; }

        [Required]
        public DateTime NgaySinh { get; set; }

        public string? DiaChi { get; set; }

        public int DiemTichLuy { get; set; } = 0;

        public int Tuoi
        {
            get
            {
                var tuoi = DateTime.Now.Year - NgaySinh.Year;

                if (NgaySinh.Date > DateTime.Now.AddYears(-tuoi))
                {
                    tuoi--;
                }

                return tuoi;
            }
        }

        public string NhomTuoi
        {
            get
            {
                if (Tuoi < 18)
                    return "Trẻ em";

                if (Tuoi < 40)
                    return "Thanh niên";

                if (Tuoi < 60)
                    return "Trung niên";

                return "Người cao tuổi";
            }
        }
    }
}