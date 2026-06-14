using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize(Roles = "Admin,Pharmacist")]
    public class MedicineController : Controller
    {
        private readonly PharmacyDbContext _context;

        public MedicineController(PharmacyDbContext context)
        {
            _context = context;
        }

        // GET: Medicine
        public async Task<IActionResult> Index()
        {
            var pharmacyDbContext = _context.Thuocs.Include(t => t.LoaiThuoc);
            return View(await pharmacyDbContext.ToListAsync());
        }

        // GET: Medicine/Create
        public IActionResult Create()
        {
            ViewData["LoaiThuocId"] = new SelectList(_context.LoaiThuocs, "Id", "TenLoai");
            return View();
        }

        // POST: Medicine/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MaThuoc,TenThuoc,DonGia,SoLuong,MoTa,HinhAnh,HanSuDung,LoaiThuocId,DonViCoBan,HoatChat,ViTriKe,CongDung,ChongChiDinh,LieuLuong,NhomDieuTri,IsDeal,DiscountPercent")] Thuoc thuoc)
        {
            if (ModelState.IsValid)
            {
                _context.Add(thuoc);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["LoaiThuocId"] = new SelectList(_context.LoaiThuocs, "Id", "TenLoai", thuoc.LoaiThuocId);
            return View(thuoc);
        }

        // GET: Medicine/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var thuoc = await _context.Thuocs.FindAsync(id);
            if (thuoc == null) return NotFound();

            ViewData["LoaiThuocId"] = new SelectList(_context.LoaiThuocs, "Id", "TenLoai", thuoc.LoaiThuocId);
            return View(thuoc);
        }

        // POST: Medicine/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MaThuoc,TenThuoc,DonGia,SoLuong,MoTa,HinhAnh,HanSuDung,LoaiThuocId,DonViCoBan,HoatChat,ViTriKe,CongDung,ChongChiDinh,LieuLuong,NhomDieuTri,IsDeal,DiscountPercent")] Thuoc thuoc)
        {
            if (id != thuoc.MaThuoc) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(thuoc);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ThuocExists(thuoc.MaThuoc)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["LoaiThuocId"] = new SelectList(_context.LoaiThuocs, "Id", "TenLoai", thuoc.LoaiThuocId);
            return View(thuoc);
        }

        // GET: Medicine/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var thuoc = await _context.Thuocs
                .Include(t => t.LoaiThuoc)
                .FirstOrDefaultAsync(m => m.MaThuoc == id);
            if (thuoc == null) return NotFound();

            return View(thuoc);
        }

        // POST: Medicine/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var thuoc = await _context.Thuocs.FindAsync(id);
            if (thuoc != null)
            {
                _context.Thuocs.Remove(thuoc);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ThuocExists(int id)
        {
            return _context.Thuocs.Any(e => e.MaThuoc == id);
        }
    }
}
