using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public int DiemTichLuy { get; set; }
        public int Tuoi { get; set; }
        public string NhomTuoi { get; set; }
        public bool IsAdmin { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Họ tên không được để trống")]
            [Display(Name = "Họ và tên")]
            public string HoTen { get; set; }

            [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; }

            [Required(ErrorMessage = "Ngày sinh không được để trống")]
            [DataType(DataType.Date)]
            [Display(Name = "Ngày sinh")]
            public DateTime NgaySinh { get; set; }

            [Display(Name = "Địa chỉ")]
            public string DiaChi { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;
            DiemTichLuy = user.DiemTichLuy;
            Tuoi = user.Tuoi;
            NhomTuoi = user.NhomTuoi;
            IsAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            Input = new InputModel
            {
                HoTen = user.HoTen,
                PhoneNumber = IsAdmin ? null : phoneNumber,
                NgaySinh = user.NgaySinh,
                DiaChi = IsAdmin ? null : user.DiaChi
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin)
            {
                ModelState.Remove("Input.PhoneNumber");
                ModelState.Remove("Input.DiaChi");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            if (!isAdmin)
            {
                var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
                if (Input.PhoneNumber != phoneNumber)
                {
                    var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                    if (!setPhoneResult.Succeeded)
                    {
                        StatusMessage = "Lỗi: Không thể cập nhật số điện thoại.";
                        await LoadAsync(user);
                        return Page();
                    }
                }

                if (Input.DiaChi != user.DiaChi)
                {
                    user.DiaChi = Input.DiaChi;
                }
            }
            else
            {
                user.PhoneNumber = null;
                user.DiaChi = null;
                user.DiemTichLuy = 0;
            }

            if (Input.HoTen != user.HoTen)
            {
                user.HoTen = Input.HoTen;
            }

            if (Input.NgaySinh != user.NgaySinh)
            {
                user.NgaySinh = Input.NgaySinh;
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                StatusMessage = "Lỗi: Không thể cập nhật thông tin cá nhân.";
                await LoadAsync(user);
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Hồ sơ của bạn đã được cập nhật thành công.";
            return RedirectToPage();
        }
    }
}
