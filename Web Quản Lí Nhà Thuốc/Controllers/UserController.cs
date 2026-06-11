using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly PharmacyDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(PharmacyDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            // Reset reminders if they were updated on a previous day
            var today = DateTime.Today;
            var reminders = await _context.LichUongThuocs
                .Where(r => r.UserId == userId)
                .ToListAsync();

            bool changed = false;
            foreach (var reminder in reminders)
            {
                if (reminder.NgayCapNhat.Date != today)
                {
                    reminder.DaUongHomNay = false;
                    reminder.NgayCapNhat = today;
                    _context.Entry(reminder).State = EntityState.Modified;
                    changed = true;
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }

            return View(reminders);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReminder(string tenThuoc, string lieuDung, string gioUong, string? ghiChu)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (string.IsNullOrWhiteSpace(tenThuoc) || string.IsNullOrWhiteSpace(lieuDung) || string.IsNullOrWhiteSpace(gioUong))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ thông tin bắt buộc.";
                return RedirectToAction(nameof(Dashboard));
            }

            var reminder = new LichUongThuoc
            {
                UserId = userId,
                TenThuoc = tenThuoc.Trim(),
                LieuDung = lieuDung.Trim(),
                GioUong = gioUong.Trim(),
                GhiChu = ghiChu?.Trim(),
                DaUongHomNay = false,
                NgayCapNhat = DateTime.Today
            };

            _context.LichUongThuocs.Add(reminder);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã thêm lịch nhắc uống thuốc thành công!";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleTaken(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var reminder = await _context.LichUongThuocs.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (reminder == null) return NotFound();

            reminder.DaUongHomNay = !reminder.DaUongHomNay;
            reminder.NgayCapNhat = DateTime.Today;
            _context.Entry(reminder).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Json(new { success = true, daUong = reminder.DaUongHomNay });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReminder(int id)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var reminder = await _context.LichUongThuocs.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (reminder == null) return NotFound();

            _context.LichUongThuocs.Remove(reminder);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa lịch nhắc thành công.";
            return RedirectToAction(nameof(Dashboard));
        }

        public IActionResult Orders()
        {
            return View();
        }

        public IActionResult Favorites()
        {
            return View();
        }

        public IActionResult Cart()
        {
            return View();
        }
    }
}
