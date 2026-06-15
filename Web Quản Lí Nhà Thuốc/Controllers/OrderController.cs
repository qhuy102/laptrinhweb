using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;
using Web_Quản_Lí_Nhà_Thuốc.Services;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly PharmacyDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;

        public OrderController(PharmacyDbContext context, UserManager<ApplicationUser> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
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

            // Gửi email xác nhận đơn hàng
            if (!string.IsNullOrEmpty(user.Email))
            {
                try
                {
                    var subject = $"[Nhà Thuốc] Xác nhận đơn hàng #{hoaDon.MaHoaDon} thành công";
                    
                    var orderRows = "";
                    foreach (var item in cartItems)
                    {
                        var tenThuoc = item.Thuoc?.TenThuoc ?? "Thuốc";
                        var soLuong = item.SoLuong;
                        var donGia = item.Thuoc?.GiaHienTai ?? 0;
                        var thanhTien = soLuong * donGia;
                        orderRows += $@"
                            <tr>
                                <td style=""padding: 10px; border: 1px solid #ddd; text-align: left;"">{tenThuoc}</td>
                                <td style=""padding: 10px; border: 1px solid #ddd; text-align: center;"">{soLuong}</td>
                                <td style=""padding: 10px; border: 1px solid #ddd; text-align: right;"">{donGia:N0} đ</td>
                                <td style=""padding: 10px; border: 1px solid #ddd; text-align: right;"">{thanhTien:N0} đ</td>
                            </tr>";
                    }

                    var discountRow = "";
                    if (discountAmount > 0)
                    {
                        discountRow = $@"<p style=""margin: 5px 0;"">Giảm giá thành viên: <strong>-{discountAmount:N0} đ</strong></p>";
                    }

                    var shippingFeeRow = "";
                    if (shippingFee.HasValue && shippingFee.Value > 0)
                    {
                        shippingFeeRow = $@"<p style=""margin: 5px 0;"">Phí vận chuyển: <strong>+{shippingFee.Value:N0} đ</strong></p>";
                    }

                    var htmlMessage = $@"
                        <div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; padding: 25px; border: 1px solid #e0e0e0; border-radius: 12px; background-color: #ffffff; box-shadow: 0 4px 12px rgba(0,0,0,0.05);"">
                          <div style=""text-align: center; border-bottom: 2px solid #0056b3; padding-bottom: 20px; margin-bottom: 20px;"">
                            <h2 style=""color: #0056b3; margin: 0; font-size: 24px; font-weight: 600;"">XÁC NHẬN ĐƠN HÀNG THÀNH CÔNG</h2>
                            <p style=""color: #666; margin: 8px 0 0 0; font-size: 14px;"">Cảm ơn bạn đã tin dùng sản phẩm của chúng tôi!</p>
                          </div>
                          
                          <div style=""line-height: 1.6; color: #333; font-size: 15px;"">
                            <p>Xin chào <strong>{user.HoTen}</strong>,</p>
                            <p>Đơn hàng của bạn đã được tiếp nhận và đang trong quá trình xử lý. Dưới đây là thông tin chi tiết đơn hàng:</p>
                            
                            <div style=""margin: 15px 0; padding: 10px 15px; background-color: #f8f9fa; border-left: 4px solid #0056b3; border-radius: 4px;"">
                              <p style=""margin: 4px 0;""><strong>Mã đơn hàng:</strong> #{hoaDon.MaHoaDon}</p>
                              <p style=""margin: 4px 0;""><strong>Thời gian đặt:</strong> {hoaDon.NgayDat:dd/MM/yyyy HH:mm}</p>
                            </div>
                            
                            <table style=""width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 14px;"">
                              <thead>
                                <tr style=""background-color: #0056b3; color: white;"">
                                  <th style=""padding: 12px 10px; text-align: left; border: 1px solid #ddd;"">Tên thuốc</th>
                                  <th style=""padding: 12px 10px; text-align: center; border: 1px solid #ddd; width: 80px;"">SL</th>
                                  <th style=""padding: 12px 10px; text-align: right; border: 1px solid #ddd; width: 100px;"">Đơn giá</th>
                                  <th style=""padding: 12px 10px; text-align: right; border: 1px solid #ddd; width: 110px;"">Thành tiền</th>
                                </tr>
                              </thead>
                              <tbody>
                                {orderRows}
                              </tbody>
                            </table>
                            
                            <div style=""margin-top: 20px; text-align: right; font-size: 15px; border-top: 1px solid #eee; padding-top: 15px;"">
                              <p style=""margin: 5px 0;"">Tạm tính: <strong>{subtotal:N0} đ</strong></p>
                              {discountRow}
                              {shippingFeeRow}
                              <p style=""margin: 5px 0; font-size: 18px; color: #d9534f; font-weight: bold;"">Tổng thanh toán: {hoaDon.TongTien:N0} đ</p>
                            </div>
                            
                            <div style=""margin-top: 25px; padding: 15px; background-color: #f4f7f6; border-radius: 8px; border: 1px solid #e2ebd5;"">
                              <h4 style=""margin: 0 0 10px 0; color: #0056b3; font-size: 16px; border-bottom: 1px solid #e2ebd5; padding-bottom: 5px;"">Thông tin giao hàng</h4>
                              <p style=""margin: 5px 0; font-size: 14px;""><strong>Người nhận:</strong> {user.HoTen}</p>
                              <p style=""margin: 5px 0; font-size: 14px;""><strong>Số điện thoại:</strong> {user.PhoneNumber ?? user.UserName}</p>
                              <p style=""margin: 5px 0; font-size: 14px;""><strong>Địa chỉ giao:</strong> {hoaDon.DiaChiGiaoHang}</p>
                              <p style=""margin: 5px 0; font-size: 14px;""><strong>Phương thức thanh toán:</strong> {hoaDon.PaymentMethod}</p>
                            </div>
                          </div>
                          
                          <div style=""text-align: center; border-top: 1px solid #eee; padding-top: 20px; margin-top: 25px; font-size: 12px; color: #777;"">
                            <p style=""margin: 0;"">Nếu cần hỗ trợ gấp, vui lòng liên hệ hotline chăm sóc khách hàng của chúng tôi.</p>
                            <p style=""margin: 8px 0 0 0; font-weight: bold; color: #0056b3;"">HỆ THỐNG QUẢN LÝ NHÀ THUỐC ANTIGRAVITY</p>
                          </div>
                        </div>";

                    await _emailService.SendEmailAsync(user.Email, subject, htmlMessage);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending checkout confirmation email: {ex.Message}");
                }
            }

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
