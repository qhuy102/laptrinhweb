using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Helpers;
using Web_Quản_Lí_Nhà_Thuốc.Hubs;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Controllers
{
    [Authorize(Roles = "Pharmacist,Admin")]
    public class PharmacistController : Controller
    {
        private readonly PharmacyDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<InventoryHub> _hubContext;

        public PharmacistController(
            PharmacyDbContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<InventoryHub> hubContext)
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
        }

        // 1. Pharmacist Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var now = DateTime.Now;
            var thirtyDaysFromNow = now.AddDays(30);

            // Calculate stats
            ViewBag.PendingPrescriptions = await _context.DonThuocs.CountAsync(d => d.TrangThai == "ChoDuyet");
            ViewBag.LowStockDrugs = await _context.Thuocs.CountAsync(t => t.SoLuong < 20);
            ViewBag.NearExpiryBatches = await _context.LoThuocs.CountAsync(l => l.HanSuDung <= thirtyDaysFromNow && l.HanSuDung > now);
            ViewBag.ExpiredBatches = await _context.LoThuocs.CountAsync(l => l.HanSuDung <= now);

            // Recent sales
            var todaySales = await _context.HoaDons
                .Where(h => h.NgayDat.Date == now.Date)
                .SumAsync(h => h.TongTien);
            ViewBag.TodaySales = todaySales;

            // Load low stock medicines
            var lowStockList = await _context.Thuocs
                .Where(t => t.SoLuong < 20)
                .Take(5)
                .ToListAsync();

            // Load near expiry batches
            var nearExpiryList = await _context.LoThuocs
                .Include(l => l.Thuoc)
                .Where(l => l.HanSuDung <= thirtyDaysFromNow)
                .OrderBy(l => l.HanSuDung)
                .Take(5)
                .ToListAsync();

            return View(new PharmacistDashboardViewModel
            {
                LowStockDrugsList = lowStockList,
                NearExpiryBatchesList = nearExpiryList
            });
        }

        // 2. POS Sell Medicine Page
        public async Task<IActionResult> Sell()
        {
            var medicines = await _context.Thuocs
                .Include(t => t.DonViQuyDois)
                .Include(t => t.LoThuocs)
                .ToListAsync();

            // Load active prescriptions
            ViewBag.Prescriptions = await _context.DonThuocs
                .Where(d => d.TrangThai == "ChoDuyet" || d.TrangThai == "DaDuyet")
                .ToListAsync();

            return View(medicines);
        }

        // API: Fuzzy Search using Levenshtein distance
        [HttpGet]
        public async Task<IActionResult> SearchDrugs(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                var all = await _context.Thuocs
                    .Include(t => t.DonViQuyDois)
                    .Select(t => new { t.MaThuoc, t.TenThuoc, t.HoatChat, t.DonGia, t.IsDeal, t.DiscountPercent, GiaHienTai = t.IsDeal ? t.DonGia * (100 - t.DiscountPercent) / 100m : t.DonGia, t.SoLuong, t.DonViCoBan, t.ViTriKe })
                    .ToListAsync();
                return Json(all);
            }

            var drugs = await _context.Thuocs
                .Include(t => t.DonViQuyDois)
                .ToListAsync();

            var results = drugs.Select(d => new
            {
                Drug = d,
                Distance = Math.Min(
                    SearchHelper.GetLevenshteinDistance(query, d.TenThuoc),
                    string.IsNullOrEmpty(d.HoatChat) ? 99 : SearchHelper.GetLevenshteinDistance(query, d.HoatChat)
                )
            })
            // Filter where distance is reasonably close, or name contains query
            .Where(x => x.Distance <= 4 || x.Drug.TenThuoc.Contains(query, StringComparison.OrdinalIgnoreCase) || (x.Drug.HoatChat != null && x.Drug.HoatChat.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Distance)
            .Select(x => new
            {
                x.Drug.MaThuoc,
                x.Drug.TenThuoc,
                x.Drug.HoatChat,
                x.Drug.DonGia,
                IsDeal = x.Drug.IsDeal,
                DiscountPercent = x.Drug.DiscountPercent,
                GiaHienTai = x.Drug.GiaHienTai,
                x.Drug.SoLuong,
                x.Drug.DonViCoBan,
                x.Drug.ViTriKe,
                conversions = x.Drug.DonViQuyDois.Select(c => new { c.TenDonVi, c.TyLeQuyDoi, c.HeSoGia })
            })
            .ToList();

            return Json(results);
        }

        // API: Check active ingredient interaction warnings
        [HttpPost]
        public async Task<IActionResult> CheckDrugInteractions([FromBody] List<int> drugIds)
        {
            if (drugIds == null || drugIds.Count < 2)
            {
                return Json(new { hasWarnings = false });
            }

            var drugs = await _context.Thuocs
                .Where(t => drugIds.Contains(t.MaThuoc))
                .Select(t => new { t.MaThuoc, t.TenThuoc, t.HoatChat })
                .ToListAsync();

            var warnings = new List<string>();

            for (int i = 0; i < drugs.Count; i++)
            {
                for (int j = i + 1; j < drugs.Count; j++)
                {
                    if (drugs[i].HoatChat != null && drugs[j].HoatChat != null)
                    {
                        var warning = SearchHelper.CheckInteraction(drugs[i].HoatChat!, drugs[j].HoatChat!);
                        if (warning != null)
                        {
                            warnings.Add($"**{drugs[i].TenThuoc} ({drugs[i].HoatChat})** và **{drugs[j].TenThuoc} ({drugs[j].HoatChat})**: {warning}");
                        }
                    }
                }
            }

            return Json(new
            {
                hasWarnings = warnings.Any(),
                warnings = warnings
            });
        }

        // API: Get customer VIP status by phone number
        [HttpGet]
        public async Task<IActionResult> GetCustomerInfo(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return NotFound();

            var customer = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone);
            if (customer == null) return NotFound();

            // Determine membership tier
            string tier = "Thành viên";
            decimal discount = 0;
            int pointsToNext = 500;

            if (customer.DiemTichLuy >= 2000)
            {
                tier = "VIP Kim Cương";
                discount = 0.15m; // 15% off
                pointsToNext = 0;
            }
            else if (customer.DiemTichLuy >= 1000)
            {
                tier = "VIP Vàng";
                discount = 0.10m; // 10% off
                pointsToNext = 2000 - customer.DiemTichLuy;
            }
            else if (customer.DiemTichLuy >= 500)
            {
                tier = "Thành viên Bạc";
                discount = 0.05m; // 5% off
                pointsToNext = 1000 - customer.DiemTichLuy;
            }
            else
            {
                pointsToNext = 500 - customer.DiemTichLuy;
            }

            return Json(new
            {
                id = customer.Id,
                name = customer.HoTen,
                phone = customer.PhoneNumber,
                points = customer.DiemTichLuy,
                tier = tier,
                discount = discount,
                pointsToNext = pointsToNext,
                percentToNext = tier == "VIP Kim Cương" ? 100 : (int)((customer.DiemTichLuy % 500) / 5.0)
            });
        }

        // API: Quick Register a Customer
        [HttpPost]
        public async Task<IActionResult> RegisterCustomer([FromBody] QuickRegisterRequestModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Phone) || string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest("Thông tin đăng ký không hợp lệ.");
            }

            var cleanPhone = model.Phone.Trim();
            var existing = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone);
            if (existing != null)
            {
                return BadRequest("Số điện thoại này đã được đăng ký.");
            }

            // Generate a unique fallback email
            var email = $"{cleanPhone}@pharmacy.com";
            
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                PhoneNumber = cleanPhone,
                HoTen = model.Name.Trim(),
                NgaySinh = DateTime.Today.AddYears(-20), // Default age 20
                EmailConfirmed = true,
                DiemTichLuy = 0
            };

            var result = await _userManager.CreateAsync(user, "User@123");
            if (!result.Succeeded)
            {
                return BadRequest(string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            await _userManager.AddToRoleAsync(user, "User");

            // Log Audit Trail
            var audit = new AuditLog
            {
                UserId = _userManager.GetUserId(User),
                UserEmail = User.Identity?.Name,
                Action = "CREATE_USER",
                MoTa = $"Dược sĩ đăng ký nhanh khách hàng mới: {model.Name} (SĐT: {cleanPhone})",
                ThoiGian = DateTime.Now,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuspicious = false
            };
            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                id = user.Id,
                name = user.HoTen,
                phone = user.PhoneNumber,
                points = user.DiemTichLuy,
                tier = "Thành viên",
                discount = 0m,
                pointsToNext = 500,
                percentToNext = 0
            });
        }

        // POS Checkout (POST)
        [HttpPost]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequestModel model)
        {
            if (model == null || model.Items == null || !model.Items.Any())
            {
                return BadRequest("Hóa đơn trống.");
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var customer = !string.IsNullOrEmpty(model.CustomerId)
                        ? await _userManager.FindByIdAsync(model.CustomerId)
                        : null;

                    decimal totalAmount = 0;
                    var details = new List<ChiTietHoaDon>();
                    var dosageSchedule = new List<DosageScheduleItem>();

                    foreach (var item in model.Items)
                    {
                        var drug = await _context.Thuocs
                            .Include(t => t.LoThuocs)
                            .Include(t => t.DonViQuyDois)
                            .FirstOrDefaultAsync(t => t.MaThuoc == item.MaThuoc);

                        if (drug == null)
                        {
                            return BadRequest($"Thuốc mã {item.MaThuoc} không tồn tại.");
                        }

                        // Determine conversion factor
                        int conversionFactor = 1;
                        decimal priceFactor = 1.0m;
                        if (item.Unit != drug.DonViCoBan)
                        {
                            var conv = drug.DonViQuyDois.FirstOrDefault(c => c.TenDonVi == item.Unit);
                            if (conv != null)
                            {
                                conversionFactor = conv.TyLeQuyDoi;
                                priceFactor = conv.HeSoGia;
                            }
                        }

                        // Required total base units (pills/vials)
                        int totalBaseUnitsRequired = item.Quantity * conversionFactor;

                        // Check stock
                        if (drug.SoLuong < totalBaseUnitsRequired)
                        {
                            return BadRequest($"Thuốc {drug.TenThuoc} không đủ hàng trong kho. Cần: {totalBaseUnitsRequired} {drug.DonViCoBan}, Có: {drug.SoLuong}");
                        }

                        // FEFO: Deduct stock from batches (LoThuoc) sorted by expiry date ascending
                        var activeBatches = drug.LoThuocs
                            .Where(l => l.HanSuDung > DateTime.Now && l.SoLuong > 0)
                            .OrderBy(l => l.HanSuDung)
                            .ToList();

                        int remainingToDeduct = totalBaseUnitsRequired;
                        foreach (var batch in activeBatches)
                        {
                            if (batch.SoLuong >= remainingToDeduct)
                            {
                                batch.SoLuong -= remainingToDeduct;
                                remainingToDeduct = 0;
                                break;
                            }
                            else
                            {
                                remainingToDeduct -= batch.SoLuong;
                                batch.SoLuong = 0;
                            }
                        }

                        if (remainingToDeduct > 0)
                        {
                            // If active unexpired batches are not enough, take from whatever is available
                            var allBatches = drug.LoThuocs.Where(l => l.SoLuong > 0).OrderBy(l => l.HanSuDung).ToList();
                            foreach (var batch in allBatches)
                            {
                                if (batch.SoLuong >= remainingToDeduct)
                                {
                                    batch.SoLuong -= remainingToDeduct;
                                    remainingToDeduct = 0;
                                    break;
                                }
                                else
                                {
                                    remainingToDeduct -= batch.SoLuong;
                                    batch.SoLuong = 0;
                                }
                            }
                        }

                        // Update global stock
                        drug.SoLuong -= totalBaseUnitsRequired;
                        _context.Entry(drug).State = EntityState.Modified;

                        // Calculate item total price
                        decimal itemPrice = (item.Unit == drug.DonViCoBan) 
                            ? drug.DonGia 
                            : (drug.DonGia * priceFactor);

                        decimal itemTotal = itemPrice * item.Quantity;
                        totalAmount += itemTotal;

                        details.Add(new ChiTietHoaDon
                        {
                            MaThuoc = drug.MaThuoc,
                            SoLuong = totalBaseUnitsRequired, // Stored in base units
                            DonGia = drug.DonGia
                        });

                        // Broadcast stock change via SignalR
                        await _hubContext.Clients.All.SendAsync("ReceiveStockUpdate", drug.MaThuoc, drug.SoLuong);

                        // Parse Dosage instructions for visual scheduler
                        dosageSchedule.Add(new DosageScheduleItem
                        {
                            TenThuoc = drug.TenThuoc,
                            DonVi = item.Unit,
                            SoLuong = item.Quantity,
                            LieuDung = item.LieuDung ?? drug.LieuLuong ?? "Uống theo hướng dẫn."
                        });
                    }

                    // Apply customer discount
                    decimal finalAmount = totalAmount;
                    if (customer != null)
                    {
                        decimal discountPercent = 0;
                        if (customer.DiemTichLuy >= 2000) discountPercent = 0.15m;
                        else if (customer.DiemTichLuy >= 1000) discountPercent = 0.10m;
                        else if (customer.DiemTichLuy >= 500) discountPercent = 0.05m;

                        finalAmount = totalAmount * (1.0m - discountPercent);

                        // Earn loyalty points (1 point per 10k VND spent)
                        int pointsEarned = (int)(finalAmount / 10000);
                        customer.DiemTichLuy += pointsEarned;
                        await _userManager.UpdateAsync(customer);
                    }

                    // Create Invoice (HoaDon)
                    var invoice = new HoaDon
                    {
                        UserId = customer?.Id ?? _userManager.GetUserId(User) ?? "Guest",
                        NgayDat = DateTime.Now,
                        TongTien = finalAmount,
                        TrangThai = "Hoàn thành",
                        ChiTietHoaDons = details
                    };

                    _context.HoaDons.Add(invoice);

                    // Update prescription status if linked
                    if (model.PrescriptionId.HasValue)
                    {
                        var pres = await _context.DonThuocs.FindAsync(model.PrescriptionId.Value);
                        if (pres != null)
                        {
                            pres.TrangThai = "DaBan";
                            _context.Entry(pres).State = EntityState.Modified;
                        }
                    }

                    await _context.SaveChangesAsync();

                    // Log Audit Trail
                    var audit = new AuditLog
                    {
                        UserId = _userManager.GetUserId(User),
                        UserEmail = User.Identity?.Name,
                        Action = "SELL",
                        MoTa = $"Dược sĩ bán đơn hàng #{invoice.MaHoaDon}. Tổng tiền: {finalAmount:N0}₫. Khách: {customer?.HoTen ?? "Khách vãng lai"}",
                        ThoiGian = DateTime.Now,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        IsSuspicious = false
                    };
                    _context.AuditLogs.Add(audit);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        invoiceId = invoice.MaHoaDon,
                        total = finalAmount,
                        saved = totalAmount - finalAmount,
                        customerName = customer?.HoTen ?? "Khách vãng lai",
                        pointsEarned = customer != null ? (int)(finalAmount / 10000) : 0,
                        schedule = dosageSchedule
                    });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return BadRequest($"Lỗi thanh toán: {ex.Message}");
                }
            }
        }

        // 3. Prescriptions Page
        public async Task<IActionResult> Prescriptions()
        {
            var prescriptions = await _context.DonThuocs
                .Include(d => d.ChiTietDonThuocs)
                .ThenInclude(c => c.Thuoc)
                .OrderByDescending(d => d.NgayKeDon)
                .ToListAsync();

            return View(prescriptions);
        }

        // API: Update prescription status
        [HttpPost]
        public async Task<IActionResult> UpdatePrescriptionStatus(int id, string status)
        {
            var pres = await _context.DonThuocs.FindAsync(id);
            if (pres == null) return NotFound("Đơn thuốc không tồn tại.");

            pres.TrangThai = status;
            _context.Entry(pres).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // API: Simulate OCR Recognition on uploaded file
        [HttpPost]
        public async Task<IActionResult> ProcessPrescriptionOCR()
        {
            var files = Request.Form.Files;
            if (files.Count == 0)
            {
                return BadRequest("Không tìm thấy ảnh tải lên.");
            }

            var file = files[0];
            string fileName = file.FileName.ToLower();

            // Simulate OCR scanning delay
            await Task.Delay(2000);

            // Mock recognition results depending on filename keywords
            string diagnosis = "Khám lâm sàng cảm cúm";
            string doctor = "Bác sĩ Nguyễn Văn Cảnh (Chợ Rẫy)";
            string patient = "Khách Hàng Quét Đơn";

            var items = new List<OcrItemResult>();

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

            // Fallback: Add Paracetamol if nothing matched
            if (!items.Any())
            {
                var drug = await _context.Thuocs.FirstOrDefaultAsync();
                if (drug != null)
                {
                    items.Add(new OcrItemResult { MaThuoc = drug.MaThuoc, TenThuoc = drug.TenThuoc, Quantity = 10, LieuDung = "Uống 1 viên khi đau đầu." });
                }
            }

            // Create a prescription in DB as Approved
            var pres = new DonThuoc
            {
                TenBenhNhan = patient,
                BacSiKeDon = doctor,
                ChanDoan = diagnosis,
                NgayKeDon = DateTime.Now,
                TrangThai = "DaDuyet",
                HinhAnhDonThuoc = "/uploads/" + file.FileName
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

            return Json(new
            {
                success = true,
                prescriptionId = pres.Id,
                patientName = pres.TenBenhNhan,
                doctorName = pres.BacSiKeDon,
                diagnosis = pres.ChanDoan,
                items = items
            });
        }

        // 4. Consult Drug Information Page
        public async Task<IActionResult> Consult()
        {
            var drugs = await _context.Thuocs
                .Include(t => t.LoaiThuoc)
                .Include(t => t.DonViQuyDois)
                .ToListAsync();

            return View(drugs);
        }

        // API: Manual Inventory Adjustment (Fraud Detection check)
        [HttpPost]
        public async Task<IActionResult> ManualAdjustStock(int drugId, int newQuantity)
        {
            var drug = await _context.Thuocs.FindAsync(drugId);
            if (drug == null) return NotFound("Thuốc không tồn tại.");

            int oldQuantity = drug.SoLuong;
            drug.SoLuong = newQuantity;
            _context.Entry(drug).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            // Check for potential fraud:
            // - Decrease of stock manually (not via sales checkout) by more than 50 units
            // - Adjustments made outside business hours (10:00 PM to 6:00 AM)
            int difference = oldQuantity - newQuantity;
            bool isSuspicious = false;
            string reason = "";

            if (difference > 50)
            {
                isSuspicious = true;
                reason += "Giảm số lượng tồn kho thủ công lớn (>50 đơn vị). ";
            }

            var currentHour = DateTime.Now.Hour;
            if (currentHour >= 22 || currentHour < 6)
            {
                isSuspicious = true;
                reason += "Thay đổi kho hàng ngoài giờ hành chính (22h - 6h). ";
            }

            // Register in AuditLog
            var audit = new AuditLog
            {
                UserId = _userManager.GetUserId(User),
                UserEmail = User.Identity?.Name,
                Action = "MANUAL_ADJUST",
                MoTa = $"Điều chỉnh tồn kho thủ công thuốc {drug.TenThuoc}. Từ {oldQuantity} về {newQuantity} (Hao hụt: {difference}). {(isSuspicious ? "CẢNH BÁO GIAN LẬN: " + reason : "")}",
                ThoiGian = DateTime.Now,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsSuspicious = isSuspicious
            };

            _context.AuditLogs.Add(audit);
            await _context.SaveChangesAsync();

            // Broadcast real-time change to all clients
            await _hubContext.Clients.All.SendAsync("ReceiveStockUpdate", drug.MaThuoc, drug.SoLuong);

            return Json(new { success = true, isSuspicious = isSuspicious, message = isSuspicious ? $"Cảnh báo: Thay đổi bất thường đã bị ghi nhật ký kiểm toán! {reason}" : "Cập nhật thành công." });
        }

        // ============================================================
        // API: Proxy Image to bypass CDN Hotlink Protection / CORS
        // ============================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ProxyImage([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url)) return NotFound();
            try
            {
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                http.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                http.DefaultRequestHeaders.Add("Accept-Language", "vi-VN,vi;q=0.9,en-US;q=0.8,en;q=0.7");
                
                // Add a valid referer to bypass hotlink protection
                if (url.Contains("tgdd.vn")) http.DefaultRequestHeaders.Add("Referer", "https://www.thegioididong.com/");
                else if (url.Contains("nhathuoclongchau")) http.DefaultRequestHeaders.Add("Referer", "https://nhathuoclongchau.com.vn/");
                else http.DefaultRequestHeaders.Add("Referer", "https://www.google.com/");

                var response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                    return File(bytes, contentType);
                }
            }
            catch { }
            return NotFound();
        }

        // ============================================================
        // API: Multi-source drug image lookup with 3-tier cascade:
        //   Tier 1 → NLM RxImage (by brand name)
        //   Tier 2 → RxNorm (get rxcui) → RxImage (by rxcui/related NDC)
        //   Tier 3 → OpenFDA (SPL set-id → DailyMed image URL)
        // ============================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetDrugImage(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { imageUrl = (string?)null, source = (string?)null });

            // ─── TIER 0: Local Database ───────────────────────────────────
            try
            {
                var localDrug = await _context.Thuocs
                    .FirstOrDefaultAsync(t => EF.Functions.Like(t.TenThuoc, $"%{name}%") || 
                                              EF.Functions.Like(t.HoatChat, $"%{name}%") ||
                                              EF.Functions.Like(name, $"%{t.TenThuoc}%") ||
                                              EF.Functions.Like(name, $"%{t.HoatChat}%"));
                if (localDrug != null && !string.IsNullOrEmpty(localDrug.HinhAnh))
                {
                    return Json(new { imageUrl = localDrug.HinhAnh, source = "Cơ sở dữ liệu nhà thuốc" });
                }
            }
            catch { }

            // Strip dosage info from name for better API matching
            // e.g. "Amoxicillin 500mg" → "Amoxicillin"
            var cleanName = System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+\d+\s*(mg|ml|g|mcg|iu|%|UI)\b.*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            
            // Translate Vietnamese local brands to US FDA names that are GUARANTEED to have images in RxImage
            var lowerName = cleanName.ToLowerInvariant();
            string usBrandName = cleanName;
            
            if (lowerName.Contains("panadol") || lowerName.Contains("paracetamol") || lowerName.Contains("decolgen") || lowerName.Contains("hapacol") || lowerName.Contains("efferalgan"))
            {
                usBrandName = "tylenol"; // US equivalent for Acetaminophen
            }
            else if (lowerName.Contains("vitamin c") || lowerName.Contains("enervon"))
            {
                usBrandName = "ascorbic acid";
            }
            else if (lowerName.Contains("cetaphil"))
            {
                usBrandName = "cetaphil";
            }
            else if (lowerName.Contains("amoxicillin") || lowerName.Contains("amoxicilin"))
            {
                usBrandName = "amoxil"; // US brand for Amoxicillin
            }

            var encodedUsName = Uri.EscapeDataString(usBrandName);
            var encodedFull = Uri.EscapeDataString(name.Trim());

            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(8);
            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36");
            
            // ─── TIER 1.5: RxImage Direct Name Search (Guarantees Image) ────────
            try
            {
                var rxDirectUrl = $"https://rximage.nlm.nih.gov/api/rximage/1/rxnav?name={encodedUsName}&resolution=thumbnail";
                var rxResp = await http.GetAsync(rxDirectUrl);
                if (rxResp.IsSuccessStatusCode)
                {
                    var rxJson = await rxResp.Content.ReadAsStringAsync();
                    using var rxDoc = System.Text.Json.JsonDocument.Parse(rxJson);
                    if (rxDoc.RootElement.TryGetProperty("nlmRxImages", out var rxImgs) && rxImgs.GetArrayLength() > 0)
                    {
                        var firstImg = rxImgs[0];
                        if (firstImg.TryGetProperty("imageUrl", out var imgUrl) && !string.IsNullOrEmpty(imgUrl.GetString()))
                        {
                            return Json(new { imageUrl = imgUrl.GetString(), source = "RxImage Direct" });
                        }
                    }
                }
            }
            catch { }

            // ─── TIER 2: OpenFDA → get SPL set_id → DailyMed image ────────
            try
            {
                // Search by brand name first using the US mapped name (Use %22 for quotes to avoid URI parsing errors)
                var fdaSearchUrl = $"https://api.fda.gov/drug/label.json?search=openfda.brand_name:%22{encodedUsName}%22&limit=1";
                var fdaResp = await http.GetAsync(fdaSearchUrl);

                // If not found by brand, try generic name
                if (!fdaResp.IsSuccessStatusCode)
                {
                    fdaSearchUrl = $"https://api.fda.gov/drug/label.json?search=openfda.generic_name:%22{encodedUsName}%22&limit=1";
                    fdaResp = await http.GetAsync(fdaSearchUrl);
                }

                if (fdaResp.IsSuccessStatusCode)
                {
                    var fdaJson = await fdaResp.Content.ReadAsStringAsync();
                    using var fdaDoc = System.Text.Json.JsonDocument.Parse(fdaJson);
                    var fdaRoot = fdaDoc.RootElement;

                    if (fdaRoot.TryGetProperty("results", out var fdaResults) && fdaResults.GetArrayLength() > 0)
                    {
                        var fdaFirst = fdaResults[0];
                        if (fdaFirst.TryGetProperty("set_id", out var setIdEl))
                        {
                            var setId = setIdEl.GetString();
                            if (!string.IsNullOrEmpty(setId))
                            {
                                // Try DailyMed API directly to get the actual image URLs (not the HTML wrapper)
                                var dailyMedApiUrl = $"https://dailymed.nlm.nih.gov/dailymed/services/v2/spls/{setId}/media.json";
                                var dmApiResp = await http.GetAsync(dailyMedApiUrl);
                                if (dmApiResp.IsSuccessStatusCode)
                                {
                                    var dmJson = await dmApiResp.Content.ReadAsStringAsync();
                                    using var dmDoc = System.Text.Json.JsonDocument.Parse(dmJson);
                                    var dmRoot = dmDoc.RootElement;
                                    if (dmRoot.TryGetProperty("data", out var dmData) && dmData.GetArrayLength() > 0)
                                    {
                                        var dmFirst = dmData[0];
                                        if (dmFirst.TryGetProperty("url", out var dmUrl) && !string.IsNullOrEmpty(dmUrl.GetString()))
                                            return Json(new { imageUrl = dmUrl.GetString(), source = "OpenFDA → DailyMed Media", setId });
                                    }
                                }
                            }
                        }

                        // Fallback: Get NDC from OpenFDA and build RxImage URL
                        if (fdaFirst.TryGetProperty("openfda", out var openFda) &&
                            openFda.TryGetProperty("package_ndc", out var ndcArr) &&
                            ndcArr.GetArrayLength() > 0)
                        {
                            var ndc = ndcArr[0].GetString();
                            if (!string.IsNullOrEmpty(ndc))
                            {
                                var ndcEncoded = Uri.EscapeDataString(ndc.Replace("-", ""));
                                var rxByNdc = $"https://rximage.nlm.nih.gov/api/rximage/1/rxbase?ndc={ndcEncoded}&resolution=thumbnail";
                                var ndcImgResp = await http.GetAsync(rxByNdc);
                                if (ndcImgResp.IsSuccessStatusCode)
                                {
                                    var ndcImgJson = await ndcImgResp.Content.ReadAsStringAsync();
                                    using var ndcImgDoc = System.Text.Json.JsonDocument.Parse(ndcImgJson);
                                    var ndcImgRoot = ndcImgDoc.RootElement;
                                    if (ndcImgRoot.TryGetProperty("nlmRxImages", out var ndcImgs) && ndcImgs.GetArrayLength() > 0)
                                    {
                                        if (ndcImgs[0].TryGetProperty("imageUrl", out var ndcImgUrl) && !string.IsNullOrEmpty(ndcImgUrl.GetString()))
                                            return Json(new { imageUrl = ndcImgUrl.GetString(), source = "OpenFDA NDC → RxImage", ndc });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // ─── TIER 3: Wikipedia API Image Search (Ultimate Fallback) ────────
            try
            {
                var wikiUrl = $"https://en.wikipedia.org/w/api.php?action=query&format=json&prop=pageimages&generator=search&gsrsearch={encodedUsName}&pithumbsize=500";
                var wikiResp = await http.GetAsync(wikiUrl);
                if (wikiResp.IsSuccessStatusCode)
                {
                    var wikiJson = await wikiResp.Content.ReadAsStringAsync();
                    using var wikiDoc = System.Text.Json.JsonDocument.Parse(wikiJson);
                    if (wikiDoc.RootElement.TryGetProperty("query", out var query) && query.TryGetProperty("pages", out var pages))
                    {
                        foreach (var page in pages.EnumerateObject())
                        {
                            if (page.Value.TryGetProperty("thumbnail", out var thumb) && thumb.TryGetProperty("source", out var src))
                            {
                                var imgUrl = src.GetString();
                                if (!string.IsNullOrEmpty(imgUrl))
                                {
                                    return Json(new { imageUrl = imgUrl, source = "Wikipedia API" });
                                }
                            }
                        }
                    }
                }
            }
            catch { /* all tiers exhausted */ }

            // All 3 tiers failed — return null so client shows placeholder
            return Json(new { imageUrl = (string?)null, source = (string?)null });
        }

        // API: Get current logged-in user's membership info (for POS auto-detect)
        [HttpGet]
        public async Task<IActionResult> GetCurrentUserMembership()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return NotFound();

            // Only return for non-admin, non-pharmacist roles
            var roles = await _userManager.GetRolesAsync(currentUser);
            if (roles.Contains("Admin") || roles.Contains("Pharmacist"))
                return NotFound();

            string tier = "Thành viên";
            decimal discount = 0;
            int pointsToNext = 500;

            if (currentUser.DiemTichLuy >= 2000)
            {
                tier = "VIP Kim Cương"; discount = 0.15m; pointsToNext = 0;
            }
            else if (currentUser.DiemTichLuy >= 1000)
            {
                tier = "VIP Vàng"; discount = 0.10m; pointsToNext = 2000 - currentUser.DiemTichLuy;
            }
            else if (currentUser.DiemTichLuy >= 500)
            {
                tier = "Thành viên Bạc"; discount = 0.05m; pointsToNext = 1000 - currentUser.DiemTichLuy;
            }
            else
            {
                pointsToNext = 500 - currentUser.DiemTichLuy;
            }

            return Json(new
            {
                id = currentUser.Id,
                name = currentUser.HoTen,
                phone = currentUser.PhoneNumber,
                points = currentUser.DiemTichLuy,
                tier,
                discount,
                pointsToNext,
                percentToNext = tier == "VIP Kim Cương" ? 100 : (int)((currentUser.DiemTichLuy % 500) / 5.0)
            });
        }
    }

    // ViewModels & Request models
    public class PharmacistDashboardViewModel
    {
        public List<Thuoc> LowStockDrugsList { get; set; } = new List<Thuoc>();
        public List<LoThuoc> NearExpiryBatchesList { get; set; } = new List<LoThuoc>();
    }

    public class CheckoutRequestModel
    {
        public string? CustomerId { get; set; }
        public int? PrescriptionId { get; set; }
        public List<CheckoutItemModel> Items { get; set; } = new List<CheckoutItemModel>();
    }

    public class CheckoutItemModel
    {
        public int MaThuoc { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public string? LieuDung { get; set; }
    }

    public class DosageScheduleItem
    {
        public string TenThuoc { get; set; }
        public string DonVi { get; set; }
        public int SoLuong { get; set; }
        public string LieuDung { get; set; }
    }

    public class OcrItemResult
    {
        public int MaThuoc { get; set; }
        public string TenThuoc { get; set; }
        public int Quantity { get; set; }
        public string LieuDung { get; set; }
    }

    public class QuickRegisterRequestModel
    {
        public string Name { get; set; }
        public string Phone { get; set; }
    }
}
