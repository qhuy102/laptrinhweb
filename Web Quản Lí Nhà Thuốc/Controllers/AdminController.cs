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
using Web_Quản_Lí_Nhà_Thuốc.Data;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly PharmacyDbContext _context;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            PharmacyDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
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

        // Create Pharmacist (GET)
        public IActionResult CreatePharmacist()
        {
            return View();
        }

        // Create Pharmacist (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePharmacist(CreatePharmacistViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Email", "Email này đã được đăng ký.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                HoTen = model.HoTen,
                NgaySinh = model.NgaySinh,
                DiaChi = model.DiaChi,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError("", err.Description);
                }
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Pharmacist");

            // Log the action
            var audit = new AuditLog
            {
                UserId = _userManager.GetUserId(User),
                UserEmail = User.Identity?.Name,
                Action = "CREATE_USER",
                MoTa = $"Admin đã tạo tài khoản dược sĩ mới: {model.Email} (Tên: {model.HoTen})",
                ThoiGian = DateTime.Now,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuspicious = false
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã tạo thành công tài khoản dược sĩ {model.Email}.";
            return RedirectToAction(nameof(Users));
        }

        // Audit Logs (GET)
        public async Task<IActionResult> AuditLogs()
        {
            var logs = await _context.AuditLogs.OrderByDescending(l => l.ThoiGian).Take(100).ToListAsync();
            return View(logs);
        }

        // Deals Management Dashboard (GET)
        public async Task<IActionResult> Deals()
        {
            var medicines = await _context.Thuocs.Include(t => t.LoaiThuoc).ToListAsync();
            return View(medicines);
        }

        // Update Deal status & discount percent (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDeal(int id, bool isDeal, int discountPercent)
        {
            var thuoc = await _context.Thuocs.FindAsync(id);
            if (thuoc == null) return NotFound();

            thuoc.IsDeal = isDeal;
            thuoc.DiscountPercent = isDeal ? discountPercent : 0;

            _context.Thuocs.Update(thuoc);
            await _context.SaveChangesAsync();

            // Log this action
            var audit = new AuditLog
            {
                UserId = _userManager.GetUserId(User),
                UserEmail = User.Identity?.Name,
                Action = "UPDATE_DEAL",
                MoTa = $"Admin đã cập nhật ưu đãi cho thuốc {thuoc.TenThuoc}: IsDeal={isDeal}, Giảm={discountPercent}%",
                ThoiGian = DateTime.Now,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuspicious = false
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái ưu đãi cho thuốc: {thuoc.TenThuoc}";
            return RedirectToAction(nameof(Deals));
        }
    }

    public class CreatePharmacistViewModel
    {
        [Required(ErrorMessage = "Họ tên không được để trống")]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mật khẩu không được để trống")]
        [MinLength(6, ErrorMessage = "Mật khẩu phải từ 6 ký tự")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Ngày sinh không được để trống")]
        [DataType(DataType.Date)]
        public DateTime NgaySinh { get; set; }

        [Required(ErrorMessage = "Địa chỉ không được để trống")]
        public string DiaChi { get; set; }
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
