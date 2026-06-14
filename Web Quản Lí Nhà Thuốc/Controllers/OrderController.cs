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

            var subtotal = cartItems.Sum(c => c.SoLuong * c.Thuoc.GiaHienTai);
            
            decimal discountPercent = 0;
            string tierName = "Thành viên";
            
            if (user.DiemTichLuy >= 2000)
            {
                discountPercent = 0.15m;
                tierName = "VIP Kim Cương";
            }
            else if (user.DiemTichLuy >= 1000)
            {
                discountPercent = 0.10m;
                tierName = "VIP Vàng";
            }
            else if (user.DiemTichLuy >= 500)
            {
                discountPercent = 0.05m;
                tierName = "Thành viên Bạc";
            }

            var discountAmount = subtotal * discountPercent;
            var tongTien = subtotal - discountAmount;

            ViewBag.Subtotal = subtotal;
            ViewBag.DiscountPercent = discountPercent;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.TierName = tierName;
            ViewBag.TongTien = tongTien;
            
            return View(cartItems);
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(string diaChi, double? customerLat, double? customerLng, string paymentMethod, decimal? shippingFee)
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _context.GioHangs
                .Include(g => g.Thuoc)
                .Where(g => g.UserId == user.Id)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return Json(new { success = false, message = "Giỏ hàng của bạn đang trống!" });
            }

            var subtotal = cartItems.Sum(c => c.SoLuong * c.Thuoc.GiaHienTai);
            
            decimal discountPercent = 0;
            if (user.DiemTichLuy >= 2000)
            {
                discountPercent = 0.15m;
            }
            else if (user.DiemTichLuy >= 1000)
            {
                discountPercent = 0.10m;
            }
            else if (user.DiemTichLuy >= 500)
            {
                discountPercent = 0.05m;
            }

            var discountAmount = subtotal * discountPercent;
            var finalTotal = subtotal - discountAmount;
            
            if (shippingFee.HasValue && shippingFee.Value > 0)
            {
                finalTotal += shippingFee.Value;
            }

            var hoaDon = new HoaDon
            {
                UserId = user.Id,
                NgayDat = DateTime.Now,
                TongTien = finalTotal,
                TrangThai = "Chờ Xử Lý",
                DiaChiGiaoHang = diaChi,
                CustomerLat = customerLat,
                CustomerLng = customerLng,
                PaymentMethod = paymentMethod
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

            return Json(new { success = true, orderId = hoaDon.MaHoaDon, message = "Đặt hàng thành công!" });
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
