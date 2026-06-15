using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Helpers;
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

        // --- EXCEL/CSV IMPORT ACTIONS ---
        [HttpGet]
        public IActionResult DownloadTemplate()
        {
            var csvBytes = ExcelHelper.GenerateMedicineTemplateCsv();
            return File(csvBytes, "text/csv; charset=utf-8", "Mau_Nhap_Thuoc.csv");
        }

        [HttpPost]
        public async Task<IActionResult> PreviewImport(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file tải lên." });
            }

            try
            {
                var categories = await _context.LoaiThuocs.ToListAsync();
                using (var stream = file.OpenReadStream())
                {
                    var rawRows = ExcelHelper.ReadFileRows(stream, file.FileName);
                    var parsedRows = ExcelHelper.ParseMedicines(rawRows, categories);
                    
                    return Json(new { 
                        success = true, 
                        rows = parsedRows,
                        totalRows = parsedRows.Count,
                        validRows = parsedRows.Count(r => r.IsValid),
                        invalidRows = parsedRows.Count(r => !r.IsValid)
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi đọc file: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveImport([FromBody] List<MedicineImportRow> rows)
        {
            if (rows == null || !rows.Any())
            {
                return Json(new { success = false, message = "Danh sách thuốc rỗng." });
            }

            var invalidRows = rows.Where(r => !r.IsValid).ToList();
            if (invalidRows.Any())
            {
                return Json(new { success = false, message = "Vui lòng sửa toàn bộ lỗi trước khi lưu." });
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var categories = await _context.LoaiThuocs.ToListAsync();

                    foreach (var row in rows)
                    {
                        var matchingCat = categories.FirstOrDefault(l => l.TenLoai.Equals(row.TenLoaiThuoc, StringComparison.OrdinalIgnoreCase));
                        if (matchingCat == null)
                        {
                            return Json(new { success = false, message = $"Loại thuốc '{row.TenLoaiThuoc}' ở dòng {row.RowIndex} không tồn tại." });
                        }

                        var newThuoc = new Thuoc
                        {
                            TenThuoc = row.TenThuoc,
                            LoaiThuocId = matchingCat.Id,
                            DonViCoBan = row.DonViCoBan,
                            DonGia = row.DonGia,
                            SoLuong = row.SoLuong,
                            HanSuDung = row.HanSuDung,
                            ViTriKe = row.ViTriKe,
                            HoatChat = row.HoatChat,
                            NhomDieuTri = row.NhomDieuTri,
                            CongDung = row.CongDung,
                            ChongChiDinh = row.ChongChiDinh,
                            LieuLuong = row.LieuLuong,
                            IsDeal = false,
                            DiscountPercent = 0
                        };

                        _context.Thuocs.Add(newThuoc);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Json(new { success = true, count = rows.Count });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Lỗi lưu dữ liệu: " + ex.Message });
                }
            }
        }

        private bool ThuocExists(int id)
        {
            return _context.Thuocs.Any(e => e.MaThuoc == id);
        }
    }
}
