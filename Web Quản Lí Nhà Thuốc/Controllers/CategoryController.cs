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
    public class CategoryController : Controller
    {
        private readonly PharmacyDbContext _context;

        public CategoryController(PharmacyDbContext context)
        {
            _context = context;
        }

        // GET: Category
        public async Task<IActionResult> Index()
        {
            return View(await _context.LoaiThuocs.ToListAsync());
        }

        // GET: Category/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,TenLoai")] LoaiThuoc loaiThuoc)
        {
            if (ModelState.IsValid)
            {
                _context.Add(loaiThuoc);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(loaiThuoc);
        }

        // GET: Category/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var loaiThuoc = await _context.LoaiThuocs.FindAsync(id);
            if (loaiThuoc == null) return NotFound();
            
            return View(loaiThuoc);
        }

        // POST: Category/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,TenLoai")] LoaiThuoc loaiThuoc)
        {
            if (id != loaiThuoc.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(loaiThuoc);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LoaiThuocExists(loaiThuoc.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(loaiThuoc);
        }

        // GET: Category/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var loaiThuoc = await _context.LoaiThuocs
                .FirstOrDefaultAsync(m => m.Id == id);
            if (loaiThuoc == null) return NotFound();

            return View(loaiThuoc);
        }

        // POST: Category/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var loaiThuoc = await _context.LoaiThuocs.FindAsync(id);
            if (loaiThuoc != null)
            {
                _context.LoaiThuocs.Remove(loaiThuoc);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LoaiThuocExists(int id)
        {
            return _context.LoaiThuocs.Any(e => e.Id == id);
        }
    }
}
