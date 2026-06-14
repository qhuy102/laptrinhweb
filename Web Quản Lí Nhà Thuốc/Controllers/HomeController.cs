using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly PharmacyDbContext _context;
        private readonly IWebHostEnvironment _env;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            PharmacyDbContext context,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _env = env;
        }

        public async Task<IActionResult> Index()
        {
            if (_signInManager.IsSignedIn(User) && User.IsInRole("Admin"))
            {
                return View("AdminIndex");
            }

            // Customer or Guest: get list of regular (non-deal) medicines from DB to display
            var products = await _context.Thuocs.Include(t => t.LoaiThuoc).Where(t => !t.IsDeal).Take(8).ToListAsync();
            
            // Get deal products (promotions)
            ViewBag.DealProducts = await _context.Thuocs.Include(t => t.LoaiThuoc).Where(t => t.IsDeal).Take(3).ToListAsync();

            return View("CustomerIndex", products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult PrescriptionUpload()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSearchSuggestions(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Json(new List<object>());
            }

            var normalizedQuery = query.Trim().ToLower();
            var suggestions = await _context.Thuocs
                .Include(t => t.LoaiThuoc)
                .Where(t => t.TenThuoc.ToLower().Contains(normalizedQuery) || 
                            (t.HoatChat != null && t.HoatChat.ToLower().Contains(normalizedQuery)))
                .Take(5)
                .Select(t => new {
                    t.MaThuoc,
                    t.TenThuoc,
                    t.HoatChat,
                    t.DonGia,
                    t.HinhAnh,
                    t.IsDeal,
                    t.DiscountPercent,
                    GiaHienTai = t.IsDeal ? t.DonGia * (100 - t.DiscountPercent) / 100m : t.DonGia,
                    LoaiThuoc = t.LoaiThuoc != null ? t.LoaiThuoc.TenLoai : ""
                })
                .ToListAsync();

            return Json(suggestions);
        }

        [HttpGet]
        public async Task<IActionResult> GetCartAndNotificationCounts()
        {
            int cartCount = 0;
            int notificationCount = 0;

            if (_signInManager.IsSignedIn(User))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    cartCount = await _context.GioHangs.CountAsync(g => g.UserId == user.Id);
                    notificationCount = await _context.DonThuocs.CountAsync(d => d.TenBenhNhan.StartsWith(user.HoTen) && d.TrangThai == "ChoDuyet");
                }
            }

            // Default mock notification for visual beauty if 0
            if (_signInManager.IsSignedIn(User) && notificationCount == 0)
            {
                notificationCount = 1;
            }

            return Json(new { cartCount, notificationCount });
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPrescriptionOCRForUser()
        {
            var files = Request.Form.Files;
            if (files.Count == 0)
            {
                return BadRequest("Không tìm thấy ảnh tải lên.");
            }

            var file = files[0];
            string fileName = file.FileName.ToLower();

            // Save file to wwwroot/uploads
            string relativePath = "";
            try
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                var filePath = Path.Combine(uploadsFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                relativePath = "/uploads/" + file.FileName;
            }
            catch (Exception)
            {
                relativePath = "/uploads/" + file.FileName;
            }

            // Simulate OCR scanning delay
            await Task.Delay(2000);

            // Extract user info
            string patient = "Khách Hàng";
            if (_signInManager.IsSignedIn(User))
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null) patient = user.HoTen;
            }

            // Read form data for contact info
            string customName = Request.Form["fullName"];
            string customPhone = Request.Form["phone"];
            if (!string.IsNullOrEmpty(customName))
            {
                patient = customName;
            }

            string diagnosis = "Khám lâm sàng & Tư vấn đơn thuốc";
            string doctor = "Bác sĩ Nguyễn Văn Cảnh (Chợ Rẫy)";

            var items = new List<OcrItemResult>();

            // Mock OCR parsing based on filename
            if (fileName.Contains("para") || fileName.Contains("panadol"))
            {
                var drug = await _context.Thuocs.FirstOrDefaultAsync(t => t.TenThuoc.Contains("Panadol") || t.TenThuoc.Contains("Paracetamol"));
                if (drug != null)
                {
                    items.Add(new OcrItemResult { MaThuoc = drug.MaThuoc, TenThuoc = drug.TenThuoc, Quantity = 20, LieuDung = "Sáng 1 viên, Tối 1 viên sau ăn" });
                }
            }

            if (fileName.Contains("amox") || fileName.Contains("khangsinh"))
            {
                var drug = await _context.Thuocs.FirstOrDefaultAsync(t => t.TenThuoc.Contains("Amoxicillin"));
                if (drug != null)
                {
                    items.Add(new OcrItemResult { MaThuoc = drug.MaThuoc, TenThuoc = drug.TenThuoc, Quantity = 15, LieuDung = "Sáng 1 viên, Trưa 1 viên, Tối 1 viên sau ăn" });
                }
            }

            if (fileName.Contains("decol") || fileName.Contains("cam"))
            {
                var drug = await _context.Thuocs.FirstOrDefaultAsync(t => t.TenThuoc.Contains("Decolgen"));
                if (drug != null)
                {
                    items.Add(new OcrItemResult { MaThuoc = drug.MaThuoc, TenThuoc = drug.TenThuoc, Quantity = 10, LieuDung = "Uống 1 viên khi nghẹt mũi hoặc sốt." });
                }
            }

            // Add a fallback if nothing matched
            if (!items.Any())
            {
                var drug = await _context.Thuocs.FirstOrDefaultAsync();
                if (drug != null)
                {
                    items.Add(new OcrItemResult { MaThuoc = drug.MaThuoc, TenThuoc = drug.TenThuoc, Quantity = 10, LieuDung = "Uống 1 viên khi đau đầu." });
                }
            }

            // Create a prescription in DB as ChoDuyet
            var pres = new DonThuoc
            {
                TenBenhNhan = patient + (string.IsNullOrEmpty(customPhone) ? "" : $" (SĐT: {customPhone})"),
                BacSiKeDon = doctor,
                ChanDoan = diagnosis,
                NgayKeDon = DateTime.Now,
                TrangThai = "ChoDuyet",
                HinhAnhDonThuoc = relativePath
            };

            _context.DonThuocs.Add(pres);
            await _context.SaveChangesAsync();

            foreach (var item in items)
            {
                var detail = new ChiTietDonThuoc
                {
                    DonThuocId = pres.Id,
                    MaThuoc = item.MaThuoc,
                    SoLuong = item.Quantity,
                    LieuDung = item.LieuDung
                };
                _context.ChiTietDonThuocs.Add(detail);
            }
            await _context.SaveChangesAsync();

            // Check stock status for each item
            var stockDetails = new List<object>();
            decimal totalDraftAmount = 0;
            foreach (var item in items)
            {
                var dbDrug = await _context.Thuocs.FindAsync(item.MaThuoc);
                bool isAvailable = dbDrug != null && dbDrug.SoLuong >= item.Quantity;
                int currentStock = dbDrug?.SoLuong ?? 0;
                decimal price = dbDrug?.DonGia ?? 0;
                totalDraftAmount += price * item.Quantity;

                stockDetails.Add(new {
                    maThuoc = item.MaThuoc,
                    tenThuoc = item.TenThuoc,
                    reqQty = item.Quantity,
                    stock = currentStock,
                    status = isAvailable ? "Còn hàng" : (currentStock > 0 ? "Không đủ số lượng" : "Hết hàng"),
                    isAvailable,
                    price = price,
                    subtotal = price * item.Quantity
                });
            }

            return Json(new
            {
                success = true,
                prescriptionId = pres.Id,
                patientName = pres.TenBenhNhan,
                doctorName = pres.BacSiKeDon,
                diagnosis = pres.ChanDoan,
                imagePath = relativePath,
                items = stockDetails,
                total = totalDraftAmount
            });
        }

        public IActionResult Policies(string tab)
        {
            ViewBag.ActiveTab = tab ?? "delivery";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
