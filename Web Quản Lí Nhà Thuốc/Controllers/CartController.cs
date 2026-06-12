using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly PharmacyDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CartController(
            PharmacyDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var cartItems = await _context.GioHangs
                .Include(g => g.Thuoc)
                .Where(g => g.UserId == user.Id)
                .ToListAsync();

            return View(cartItems);
        }

        public async Task<IActionResult> AddToCart(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var cartItem = await _context.GioHangs
                .FirstOrDefaultAsync(x =>
                    x.UserId == user.Id &&
                    x.MaThuoc == id);

            if (cartItem == null)
            {
                var thuoc = await _context.Thuocs.FindAsync(id);
                if (thuoc == null) return NotFound();

                cartItem = new GioHang
                {
                    UserId = user.Id,
                    MaThuoc = id,
                    SoLuong = 1,
                    Thuoc = thuoc
                };

                _context.GioHangs.Add(cartItem);
            }
            else
            {
                cartItem.SoLuong++;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Remove(int id)
        {
            var item = await _context.GioHangs.FindAsync(id);

            if (item != null)
            {
                _context.GioHangs.Remove(item);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}