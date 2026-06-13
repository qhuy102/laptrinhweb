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
    public class BenhController : Controller
    {
        private readonly PharmacyDbContext _context;

        public BenhController(PharmacyDbContext context)
        {
            _context = context;
        }

        // GET: Benh
        public async Task<IActionResult> Index()
        {
            var items = await _context.Benhs.OrderBy(b => b.NhomBenh).ThenBy(b => b.TenBenh).ToListAsync();
            return View(items);
        }

        // GET: Benh/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Benh/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,MaBenh,TenBenh,NhomBenh,TrieuChungCodes,MoTaTrieuChung,TongQuan,NguyenNhan,PhongNgua,ThuocKhuyenDung")] Benh benh)
        {
            if (ModelState.IsValid)
            {
                _context.Add(benh);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(benh);
        }

        // GET: Benh/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var benh = await _context.Benhs.FindAsync(id);
            if (benh == null) return NotFound();

            return View(benh);
        }

        // POST: Benh/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,MaBenh,TenBenh,NhomBenh,TrieuChungCodes,MoTaTrieuChung,TongQuan,NguyenNhan,PhongNgua,ThuocKhuyenDung")] Benh benh)
        {
            if (id != benh.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(benh);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BenhExists(benh.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(benh);
        }

        // GET: Benh/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var benh = await _context.Benhs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (benh == null) return NotFound();

            return View(benh);
        }

        // POST: Benh/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var benh = await _context.Benhs.FindAsync(id);
            if (benh != null)
            {
                _context.Benhs.Remove(benh);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BenhExists(int id)
        {
            return _context.Benhs.Any(e => e.Id == id);
        }
    }
}
