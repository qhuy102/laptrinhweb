using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize(Roles = "Admin,Pharmacist")]
    public class OrderManagementController : Controller
    {
        private readonly PharmacyDbContext _context;

        public OrderManagementController(PharmacyDbContext context)
        {
            _context = context;
        }

        // GET: OrderManagement
        public async Task<IActionResult> Index()
        {
            var orders = await _context.HoaDons
                .Include(h => h.User)
                .OrderByDescending(h => h.NgayDat)
                .ToListAsync();
            return View(orders);
        }

        // GET: OrderManagement/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var hoaDon = await _context.HoaDons
                .Include(h => h.User)
                .Include(h => h.ChiTietHoaDons)
                .ThenInclude(c => c.Thuoc)
                .FirstOrDefaultAsync(m => m.MaHoaDon == id);
                
            if (hoaDon == null) return NotFound();

            return View(hoaDon);
        }

        // POST: OrderManagement/UpdateStatus
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var hoaDon = await _context.HoaDons.FindAsync(id);
            if (hoaDon == null) return NotFound();

            hoaDon.TrangThai = status;
            await _context.SaveChangesAsync();
            
            TempData["SuccessMessage"] = $"Đã cập nhật đơn hàng #{id} thành {status}";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // GET: OrderManagement/ShipperTracking/5
        // Dành riêng cho Shipper truy cập trên điện thoại
        [AllowAnonymous]
        public async Task<IActionResult> ShipperTracking(int? id)
        {
            if (id == null) return NotFound();
            var hoaDon = await _context.HoaDons.FindAsync(id);
            if (hoaDon == null) return NotFound();
            
            return View(hoaDon);
        }

        // GET: OrderManagement/MapTracking/5
        // Khách hàng và dược sĩ theo dõi đơn hàng
        [AllowAnonymous]
        public async Task<IActionResult> MapTracking(int? id)
        {
            if (id == null) return NotFound();
            var hoaDon = await _context.HoaDons.FindAsync(id);
            if (hoaDon == null) return NotFound();
            
            return View(hoaDon);
        }
    }
}
