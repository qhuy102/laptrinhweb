using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly PharmacyDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(PharmacyDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _context.GioHangs
                .Include(g => g.Thuoc)
                .Where(g => g.UserId == user.Id)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index", "Cart");
            }

            var tongTien = cartItems.Sum(c => c.SoLuong * c.Thuoc.GiaHienTai);
            ViewBag.TongTien = tongTien;
            
            return View(cartItems);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(string diaChi, double? customerLat, double? customerLng)
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _context.GioHangs
                .Include(g => g.Thuoc)
                .Where(g => g.UserId == user.Id)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống!";
                return RedirectToAction("Index", "Cart");
            }

            var tongTien = cartItems.Sum(c => c.SoLuong * c.Thuoc.GiaHienTai);

            var hoaDon = new HoaDon
            {
                UserId = user.Id,
                NgayDat = DateTime.Now,
                TongTien = tongTien,
                TrangThai = "Chờ Xử Lý",
                DiaChiGiaoHang = diaChi,
                CustomerLat = customerLat,
                CustomerLng = customerLng
            };

            _context.HoaDons.Add(hoaDon);
            await _context.SaveChangesAsync(); // To get MaHoaDon

            foreach (var item in cartItems)
            {
                var chiTiet = new ChiTietHoaDon
                {
                    MaHoaDon = hoaDon.MaHoaDon,
                    MaThuoc = item.MaThuoc,
                    SoLuong = item.SoLuong,
                    DonGia = item.Thuoc.GiaHienTai,
                    HoaDon = hoaDon,
                    Thuoc = item.Thuoc
                };
                _context.ChiTietHoaDons.Add(chiTiet);

                // Update stock
                var thuoc = await _context.Thuocs.FindAsync(item.MaThuoc);
                if (thuoc != null)
                {
                    thuoc.SoLuong -= item.SoLuong;
                    // Prevent negative stock
                    if (thuoc.SoLuong < 0) thuoc.SoLuong = 0;
                }
            }

            // Remove cart items
            _context.GioHangs.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đặt hàng thành công!";
            return RedirectToAction(nameof(MyOrders));
        }

        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            var orders = await _context.HoaDons
                .Include(h => h.ChiTietHoaDons)
                .ThenInclude(c => c.Thuoc)
                .Where(h => h.UserId == user.Id)
                .OrderByDescending(h => h.NgayDat)
                .ToListAsync();

            return View(orders);
        }
    }
}
