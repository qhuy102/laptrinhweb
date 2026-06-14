using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    public class CatalogController : Controller
    {
        private readonly PharmacyDbContext _context;

        public CatalogController(PharmacyDbContext context)
        {
            _context = context;
        }

        // Public category listing - displays Thuoc from DB when appropriate
        [HttpGet]
        public async Task<IActionResult> Index(string cat)
        {
            var key = (cat ?? "thuoc").ToLower();

            string title;
            string desc;

            switch (key)
            {
                case "thuoc":
                    title = "Thuốc";
                    desc = "Danh mục các loại thuốc";
                    break;
                case "tracuu":
                    title = "Tra cứu bệnh";
                    desc = "Tài nguyên tra cứu bệnh";
                    var benhs = await _context.Benhs.ToListAsync();
                    ViewData["DiseasesList"] = benhs;
                    break;
                case "tpcn":
                    title = "Thực phẩm bảo vệ sức khỏe";
                    desc = "Sản phẩm hỗ trợ sức khỏe";
                    break;
                case "me-be":
                    title = "Mẹ & Bé";
                    desc = "Sản phẩm cho mẹ và bé";
                    break;
                case "thietbiyte":
                    title = "Thiết bị y tế";
                    desc = "Thiết bị, dụng cụ y tế";
                    break;
                case "sanpham-tien-loi":
                    title = "Sản phẩm tiện lợi";
                    desc = "Đồ dùng tiện lợi cho gia đình";
                    break;
                default:
                    title = "Danh mục";
                    desc = "Các sản phẩm trong danh mục.";
                    break;
            }

            ViewData["CategoryKey"] = key;
            ViewData["CategoryTitle"] = title;
            ViewData["CategoryDescription"] = desc;

            // Load categories for sidebar
            var categories = await _context.LoaiThuocs.OrderBy(l => l.TenLoai).ToListAsync();
            ViewData["AllCategories"] = categories;

            // If category is a supported product key, show matching items from DB; otherwise show empty list
            if (key == "thuoc" || key == "tpcn" || key == "me-be" || key == "thietbiyte" || key == "sanpham-tien-loi")
            {
                // check for LoaiThuoc filter id
                int? loaiId = null;
                if (int.TryParse(Request.Query["loaiId"].FirstOrDefault() ?? "", out var tmp)) loaiId = tmp;

                string searchTerm = Request.Query["query"].FirstOrDefault() ?? "";

                var query = _context.Thuocs.Include(t => t.LoaiThuoc).Where(t => t.SoLuong > 0).AsQueryable();
                
                if (key == "tpcn")
                {
                    query = query.Where(t => t.LoaiThuoc.TenLoai == "Thực phẩm chức năng");
                }
                else if (key == "me-be")
                {
                    query = query.Where(t => t.LoaiThuoc.TenLoai == "Mẹ và bé");
                }
                else if (key == "thietbiyte")
                {
                    query = query.Where(t => t.LoaiThuoc.TenLoai == "Thiết bị y tế");
                }
                else if (key == "sanpham-tien-loi")
                {
                    query = query.Where(t => t.LoaiThuoc.TenLoai == "Sản phẩm tiện lợi");
                }
                else if (loaiId.HasValue)
                {
                    query = query.Where(t => t.LoaiThuocId == loaiId.Value);
                }

                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    var normalizedSearch = searchTerm.Trim().ToLower();
                    query = query.Where(t => t.TenThuoc.ToLower().Contains(normalizedSearch) || 
                                             (t.HoatChat != null && t.HoatChat.ToLower().Contains(normalizedSearch)) ||
                                             (t.CongDung != null && t.CongDung.ToLower().Contains(normalizedSearch)) ||
                                             (t.NhomDieuTri != null && t.NhomDieuTri.ToLower().Contains(normalizedSearch)));
                }

                var items = await query.OrderBy(t => t.TenThuoc).ToListAsync();
                return View(items);
            }

            var empty = new List<Thuoc>();
            return View(empty);
        }
    }
}
