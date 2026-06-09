using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // List all users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserViewModel
                {
                    Id = user.Id,
                    HoTen = user.HoTen,
                    Email = user.Email ?? "N/A",
                    PhoneNumber = user.PhoneNumber ?? "N/A",
                    Role = roles.FirstOrDefault() ?? "Chưa phân quyền",
                    DiemTichLuy = user.DiemTichLuy,
                    Tuoi = user.Tuoi,
                    DiaChi = user.DiaChi ?? "Chưa có"
                });
            }

            return View(userList);
        }

        // Edit User (GET)
        public async Task<IActionResult> EditUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            var currentUser = await _userManager.GetUserAsync(User);
            var isSelf = currentUser != null && currentUser.Id == user.Id;

            var model = new EditUserViewModel
            {
                Id = user.Id,
                HoTen = user.HoTen,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                DiaChi = user.DiaChi,
                DiemTichLuy = user.DiemTichLuy,
                NgaySinh = user.NgaySinh,
                Role = roles.FirstOrDefault() ?? "User",
                AvailableRoles = allRoles,
                IsSelfEditing = isSelf
            };

            return View(model);
        }

        // Edit User (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var isSelf = currentUser != null && currentUser.Id == user.Id;

            if (isSelf && model.Role != "Admin")
            {
                ModelState.AddModelError("Role", "Bạn không thể tự hạ quyền (thay đổi vai trò Admin) của chính mình.");
            }

            // Remove validation errors on phone number and address if selected role is Admin
            if (model.Role == "Admin")
            {
                ModelState.Remove("PhoneNumber");
                ModelState.Remove("DiaChi");
            }

            if (!ModelState.IsValid)
            {
                var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                model.AvailableRoles = allRoles;
                model.IsSelfEditing = isSelf;
                return View(model);
            }

            user.HoTen = model.HoTen;
            user.NgaySinh = model.NgaySinh;

            if (model.Role == "Admin")
            {
                user.PhoneNumber = null;
                user.DiaChi = null;
                user.DiemTichLuy = 0;
            }
            else
            {
                user.PhoneNumber = model.PhoneNumber;
                user.DiaChi = model.DiaChi;
                user.DiemTichLuy = model.DiemTichLuy;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Không thể cập nhật thông tin người dùng.");
                var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                model.AvailableRoles = allRoles;
                model.IsSelfEditing = isSelf;
                return View(model);
            }

            // Role Management
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(model.Role))
            {
                // Remove from old roles
                await _userManager.RemoveFromRolesAsync(user, userRoles);
                // Add to new role
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật thành công tài khoản {user.Email}.";
            return RedirectToAction(nameof(Users));
        }
    }

    // ViewModels for User Management
    public class UserViewModel
    {
        public string Id { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public int DiemTichLuy { get; set; }
        public int Tuoi { get; set; }
        public string DiaChi { get; set; }
    }

    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string HoTen { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public string DiaChi { get; set; }

        [Required]
        [Range(0, 100000, ErrorMessage = "Điểm tích lũy phải lớn hơn hoặc bằng 0")]
        public int DiemTichLuy { get; set; }

        [Required(ErrorMessage = "Ngày sinh không được để trống")]
        public DateTime NgaySinh { get; set; }

        [Required]
        public string Role { get; set; }

        public bool IsSelfEditing { get; set; }

        public List<string> AvailableRoles { get; set; } = new List<string>();
    }
}
