using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Web_Quản_Lí_Nhà_Thuốc.Data;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Hubs
{
    public class ChatSession
    {
        public string State { get; set; } = "";
        public string Symptom { get; set; } = "";
        public List<int> SuggestedDrugIds { get; set; } = new List<int>();
    }

    public class ChatHub : Hub
    {
        private readonly PharmacyDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;
        private static readonly Dictionary<string, ChatSession> Sessions = new Dictionary<string, ChatSession>();

        public ChatHub(PharmacyDbContext context, IConfiguration configuration, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _configuration = configuration;
            _userManager = userManager;
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Sessions.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public async Task AskAI(string userMessage)
        {
            // Send user message back to caller (for UI sync)
            await Clients.Caller.SendAsync("ReceiveUserMessage", userMessage);

            // Trigger typing status
            await Clients.Caller.SendAsync("ReceiveTypingStatus", true);

            // Simulate AI thinking latency
            await Task.Delay(600);

            string replyMessage = "";
            string messageLower = userMessage.ToLower().Trim();

            try
            {
                // Retrieve user session or create a new one
                if (!Sessions.TryGetValue(Context.ConnectionId, out var session))
                {
                    session = new ChatSession();
                    Sessions[Context.ConnectionId] = session;
                }

                // Normalizing and parsing words
                string[] words = messageLower.Split(new[] { ' ', ',', '.', '?', '!', ';', ':', '-' }, StringSplitOptions.RemoveEmptyEntries);

                // Symptom detection
                bool hasCough = words.Contains("ho") || words.Contains("họng") || messageLower.Contains("ho khan") || messageLower.Contains("ho đờm") || messageLower.Contains("ho có đờm") || messageLower.Contains("ho nhiều") || messageLower.Contains("bị ho") || messageLower.Contains("thuốc ho") || messageLower.Contains("đau họng") || messageLower.Contains("rát họng");
                
                bool hasFever = words.Contains("sốt") || words.Contains("sot") || words.Contains("nóng") || messageLower.Contains("nóng sốt") || messageLower.Contains("sốt nhẹ") || messageLower.Contains("sốt cao") || messageLower.Contains("bị sốt") || messageLower.Contains("thuốc sốt") || messageLower.Contains("nhiệt độ") || messageLower.Contains("cảm cúm");
                
                bool hasHeadache = messageLower.Contains("đau đầu") || messageLower.Contains("dau dau") || messageLower.Contains("nhức đầu") || messageLower.Contains("nhuc dau") || messageLower.Contains("đau nửa đầu") || messageLower.Contains("nhức đầu");

                bool isPrescriptionQuery = messageLower.Contains("đơn thuốc") || messageLower.Contains("don thuoc") || messageLower.Contains("gợi ý đơn") || messageLower.Contains("goi y don") || messageLower.Contains("cho xin đơn") || messageLower.Contains("kê đơn") || messageLower.Contains("đơn thế nào") || messageLower.Contains("đơn cụ thể") || messageLower.Contains("cho đơn") || messageLower.Contains("đề xuất đơn");

                bool isUsageQuery = messageLower.Contains("uống như nào") || messageLower.Contains("uống ra sao") || messageLower.Contains("uống thế nào") || messageLower.Contains("uống như thế nào") || messageLower.Contains("ngày uống sao") || messageLower.Contains("cách uống") || messageLower.Contains("liều lượng") || messageLower.Contains("cách dùng") || messageLower.Contains("uống sao") || messageLower.Contains("uống ngày") || messageLower.Contains("uống thế nào");

                bool isPrecautionQuery = messageLower.Contains("kiêng cử") || messageLower.Contains("kiêng cữ") || messageLower.Contains("kiêng ăn") || messageLower.Contains("kiêng uống") || messageLower.Contains("không nên ăn") || messageLower.Contains("không nên uống") || messageLower.Contains("hạn chế thứ gì") || messageLower.Contains("kiêng gì") || messageLower.Contains("hạn chế gì") || messageLower.Contains("có kiêng") || messageLower.Contains("hạn chế thứ");

                // ==========================================
                // 1. INTENT: ORDER TRACKING (Theo dõi đơn hàng)
                // ==========================================
                if (messageLower.Contains("đơn hàng") || messageLower.Contains("don hang") || messageLower.Contains("theo dõi đơn"))
                {
                    var user = await _userManager.GetUserAsync(Context.User);
                    if (user == null)
                    {
                        replyMessage = "Bạn vui lòng <strong>đăng nhập</strong> tài khoản để có thể theo dõi danh sách đơn hàng của mình.";
                    }
                    else
                    {
                        var orders = await _context.HoaDons
                            .Where(h => h.UserId == user.Id)
                            .OrderByDescending(h => h.NgayDat)
                            .Take(3)
                            .ToListAsync();

                        if (orders.Any())
                        {
                            replyMessage = "📋 <strong>Trạng thái các đơn hàng gần đây của bạn:</strong><br/><br/>";
                            foreach (var order in orders)
                            {
                                string statusBadge = order.TrangThai switch
                                {
                                    "Chờ Xử Lý" => "<span class='badge bg-warning text-dark'>Chờ Xử Lý</span>",
                                    "Đang Giao" => "<span class='badge bg-info text-white'>Đang Giao</span>",
                                    "Đã Giao" => "<span class='badge bg-success text-white'>Đã Giao</span>",
                                    "Đã Hủy" => "<span class='badge bg-danger text-white'>Đã Hủy</span>",
                                    _ => $"<span class='badge bg-secondary text-white'>{order.TrangThai}</span>"
                                };

                                replyMessage += $@"
                                    <div class='p-3 bg-white border rounded-3 mb-2 shadow-sm' style='font-size: 0.85rem;'>
                                        <div class='d-flex justify-content-between mb-1'>
                                            <strong>Đơn hàng #{order.MaHoaDon}</strong>
                                            {statusBadge}
                                        </div>
                                        <div class='text-muted small'>Ngày đặt: {order.NgayDat.ToString("dd/MM/yyyy HH:mm")}</div>
                                        <div class='text-muted small mb-2'>Tổng tiền: <strong>{order.TongTien.ToString("N0")} đ</strong></div>
                                        <div class='d-flex gap-2 justify-content-end mt-1'>
                                            <a href='/OrderManagement/MapTracking/{order.MaHoaDon}' class='btn btn-xs btn-outline-primary py-1 px-2 rounded-pill' style='font-size: 0.72rem;' target='_blank'>
                                                <i class='bi bi-geo-alt-fill'></i> Theo dõi bản đồ
                                            </a>
                                        </div>
                                    </div>";
                            }
                        }
                        else
                        {
                            replyMessage = "Bạn hiện chưa có đơn hàng nào trên hệ thống.";
                        }
                    }
                }
                // ==========================================
                // 2. INTENT: SPECIFIC DOSAGE / USAGE QUERY ("uống như nào", "ngày uống sao")
                // ==========================================
                else if (isUsageQuery)
                {
                    string symptom = !string.IsNullOrEmpty(session.Symptom) ? session.Symptom : "";
                    if (hasCough || symptom == "ho")
                    {
                        replyMessage = @"
                            <div class='p-3 bg-white border-primary border-start border-4 rounded-3 shadow-sm'>
                                <h6 class='fw-bold text-primary mb-2'><i class='bi bi-clock-history me-1'></i> Liều dùng & Cách uống thuốc trị Ho:</h6>
                                <ul class='mb-0 ps-3 text-dark' style='font-size: 0.85rem;'>
                                    <li><strong>Kẹo ngậm ho Strepsils Cool:</strong> Ngậm tan chậm trong miệng khi cảm thấy ho hoặc đau họng. Cách 2-3 giờ ngậm 1 viên. Tối đa 12 viên/ngày.</li>
                                    <li><strong>Decolgen Forte:</strong> Uống 1 viên/lần, ngày 3 lần sau khi ăn (sáng - trưa - tối). Giúp thông mũi, giảm ngứa cổ.</li>
                                    <li><strong>Vitamin C Enervon:</strong> Uống 1 viên vào buổi sáng sau ăn no để tăng đề kháng, không uống tối gây khó ngủ.</li>
                                </ul>
                                <span class='text-muted small d-block mt-2' style='font-size: 0.72rem;'>⚠️ *Thông tin chỉ mang tính tham khảo, hãy uống theo đúng chỉ định.*</span>
                            </div>";
                    }
                    else if (hasFever || symptom == "sốt")
                    {
                        replyMessage = @"
                            <div class='p-3 bg-white border-danger border-start border-4 rounded-3 shadow-sm'>
                                <h6 class='fw-bold text-danger mb-2'><i class='bi bi-clock-history me-1'></i> Liều dùng & Cách uống thuốc hạ Sốt:</h6>
                                <ul class='mb-0 ps-3 text-dark' style='font-size: 0.85rem;'>
                                    <li><strong>Paracetamol 500mg:</strong> Uống 1 viên khi sốt từ 38.5°C trở lên. Cách nhau ít nhất 4-6 tiếng nếu còn sốt cao. <em>Không uống quá 4 viên (2g) mỗi ngày.</em> Uống sau ăn nhẹ hoặc khi sốt.</li>
                                    <li><strong>Vitamin C Enervon:</strong> Uống 1 viên/ngày vào buổi sáng sau ăn để tăng cường hệ miễn dịch.</li>
                                    <li><strong>Decolgen Forte:</strong> Uống 1 viên/lần, ngày 3 lần sau ăn nếu có sổ mũi kèm sốt nhẹ.</li>
                                </ul>
                                <span class='text-muted small d-block mt-2' style='font-size: 0.72rem;'>⚠️ *Thông tin chỉ mang tính tham khảo, hãy uống theo đúng chỉ định.*</span>
                            </div>";
                    }
                    else if (hasHeadache || symptom == "đau đầu")
                    {
                        replyMessage = @"
                            <div class='p-3 bg-white border-info border-start border-4 rounded-3 shadow-sm'>
                                <h6 class='fw-bold text-info-emphasis mb-2'><i class='bi bi-clock-history me-1'></i> Liều dùng & Cách uống thuốc trị Đau đầu:</h6>
                                <ul class='mb-0 ps-3 text-dark' style='font-size: 0.85rem;'>
                                    <li><strong>Panadol Extra (hoặc Paracetamol):</strong> Uống 1 viên/lần khi đau đầu, cách nhau ít nhất 4-6 tiếng. Uống sau ăn. <em>Tối đa uống 4-6 viên mỗi ngày.</em></li>
                                    <li><strong>Ginkgo Biloba 120mg:</strong> Uống 1 viên/ngày vào buổi sáng sau bữa ăn để tăng lưu thông máu não.</li>
                                </ul>
                                <span class='text-muted small d-block mt-2' style='font-size: 0.72rem;'>⚠️ *Thông tin chỉ mang tính tham khảo, hãy uống theo đúng chỉ định.*</span>
                            </div>";
                    }
                    else
                    {
                        // Check if specific drug is mentioned in the query
                        var allDrugs = await _context.Thuocs.ToListAsync();
                        Thuoc? target = allDrugs.FirstOrDefault(d => messageLower.Contains(d.TenThuoc.ToLower()) || (d.HoatChat != null && messageLower.Contains(d.HoatChat.ToLower())));
                        if (target != null)
                        {
                            replyMessage = $@"
                                <div class='p-3 bg-white border-primary border-start border-4 rounded-3 shadow-sm'>
                                    <h6 class='fw-bold text-primary mb-2'><i class='bi bi-clock-history me-1'></i> Hướng dẫn sử dụng cho {target.TenThuoc}:</h6>
                                    <p class='text-dark mb-1' style='font-size: 0.85rem;'><strong>Liều dùng tham khảo:</strong> {target.LieuLuong ?? "Sử dụng theo chỉ dẫn của dược sĩ/bác sĩ."}</p>
                                    <p class='text-dark mb-0' style='font-size: 0.85rem;'><strong>Công dụng chính:</strong> {target.CongDung ?? "Hỗ trợ điều trị bệnh lý."}</p>
                                    <span class='text-muted small d-block mt-2' style='font-size: 0.72rem;'>⚠️ *Thông tin chỉ mang tính chất tham khảo, vui lòng hỏi dược sĩ.*</span>
                                </div>";
                        }
                        else
                        {
                            replyMessage = "Bạn muốn biết cách uống của đơn thuốc nào? Vui lòng gõ cụ thể (ví dụ: 'Cách uống đơn thuốc ho', 'Liều dùng Paracetamol').";
                        }
                    }
                }
                // ==========================================
                // 3. INTENT: SPECIFIC DIETARY PRECAUTION QUERY ("kiêng cử gì không", "hạn chế gì")
                // ==========================================
                else if (isPrecautionQuery)
                {
                    string symptom = !string.IsNullOrEmpty(session.Symptom) ? session.Symptom : "";
                    if (hasCough || symptom == "ho")
                    {
                        replyMessage = @"
                            <div class='p-3 bg-white border-warning border-start border-4 rounded-3 shadow-sm'>
                                <h6 class='fw-bold text-warning-emphasis mb-2'><i class='bi bi-exclamation-triangle-fill me-1'></i> Chế độ kiêng cữ & hạn chế khi bị Ho:</h6>
                                <ul class='mb-0 ps-3 text-dark' style='font-size: 0.85rem;'>
                                    <li><strong>Kiêng ăn uống:</strong> Tuyệt đối kiêng nước đá lạnh, đồ uống có ga, bia rượu. Hạn chế đồ ăn cay nóng (ớt, tiêu), thức ăn chứa quá nhiều dầu mỡ (chiên xào) và các loại hải sản dễ kích ứng ngứa cổ họng (như tôm, cua).</li>
                                    <li><strong>Hạn chế sinh hoạt:</strong> Tránh gió lạnh thổi trực tiếp vào cổ họng, ngực hoặc mặt (từ quạt hoặc điều hòa lạnh). Không tắm nước lạnh vào buổi tối muộn. Hạn chế nói to, nói nhiều làm tổn thương thanh quản. Tránh khói thuốc lá và khói bụi.</li>
                                    <li><strong>Nên làm:</strong> Uống nước ấm thường xuyên (2 lít/ngày) để giữ ẩm họng và loãng đờm. Súc họng bằng nước muối sinh lý ấm 2-3 lần mỗi ngày.</li>
                                </ul>
                            </div>";
                    }
                    else if (hasFever || symptom == "sốt")
                    {
                        replyMessage = @"
                            <div class='p-3 bg-white border-warning border-start border-4 rounded-3 shadow-sm'>
                                <h6 class='fw-bold text-warning-emphasis mb-2'><i class='bi bi-exclamation-triangle-fill me-1'></i> Chế độ kiêng cữ & hạn chế khi bị Sốt:</h6>
                                <ul class='mb-0 ps-3 text-dark' style='font-size: 0.85rem;'>
                                    <li><strong>Kiêng ăn uống:</strong> <strong>Tuyệt đối không uống rượu bia</strong> trong thời gian dùng thuốc hạ sốt Paracetamol vì có thể gây phá hủy tế bào gan nặng. Hạn chế đồ ăn ngọt chứa nhiều đường, đồ ăn nhanh, chiên xào khó tiêu hóa.</li>
                                    <li><strong>Hạn chế sinh hoạt:</strong> Không mặc quần áo quá chật hoặc đắp chăn dày kín mít khi đang sốt (cản trở quá trình thoát nhiệt tự nhiên của da). Không tắm nước lạnh đột ngột dễ gây sốc nhiệt nguy hiểm. Không hoạt động thể thao nặng khi mệt mỏi.</li>
                                    <li><strong>Nên làm:</strong> Uống nhiều nước ấm, nước cam hoặc nước Oresol để bù nước và điện giải. Lau người bằng nước ấm ở vùng nách, bẹn, trán để hỗ trợ hạ sốt nhanh.</li>
                                </ul>
                            </div>";
                    }
                    else if (hasHeadache || symptom == "đau đầu")
                    {
                        replyMessage = @"
                            <div class='p-3 bg-white border-warning border-start border-4 rounded-3 shadow-sm'>
                                <h6 class='fw-bold text-warning-emphasis mb-2'><i class='bi bi-exclamation-triangle-fill me-1'></i> Chế độ kiêng cữ & hạn chế khi bị Đau đầu:</h6>
                                <ul class='mb-0 ps-3 text-dark' style='font-size: 0.85rem;'>
                                    <li><strong>Kiêng ăn uống:</strong> Hạn chế các chất kích thích mạnh như cà phê, trà đặc, nước tăng lực, rượu bia. Hạn chế các thực phẩm nhiều bột ngọt (mì chính), đồ ăn nhiều chất béo động vật.</li>
                                    <li><strong>Hạn chế sinh hoạt:</strong> Tránh nhìn màn hình điện thoại, máy tính liên tục trong thời gian dài (đặc biệt trong bóng tối). Tránh căng thẳng, thức khuya hoặc làm việc quá sức.</li>
                                    <li><strong>Nên làm:</strong> Nghỉ ngơi trong phòng yên tĩnh, tối và thoáng gió. Massage nhẹ nhàng thái dương, có thể đắp khăn ấm lên trán.</li>
                                </ul>
                            </div>";
                    }
                    else
                    {
                        replyMessage = "Bạn muốn biết chế độ kiêng cữ cho triệu chứng nào? Vui lòng gõ cụ thể như: 'Kiêng gì khi bị ho', 'Bị sốt kiêng gì'.";
                    }
                }
                // ==========================================
                // 4. INTENT: PRESCRIPTION & SYMPTOM AUTO-RESPONSE (Đơn thuốc ho, sốt, đau đầu)
                // ==========================================
                else if (hasCough || hasFever || hasHeadache || isPrescriptionQuery)
                {
                    // If a prescription is requested but no symptom is specified
                    if (isPrescriptionQuery && !hasCough && !hasFever && !hasHeadache)
                    {
                        replyMessage = @"
                            🤖 <strong>Chọn triệu chứng của bạn để tôi gợi ý đơn thuốc phù hợp:</strong><br/><br/>
                            <div class='d-flex flex-column gap-2'>
                                <button class='btn btn-outline-primary text-start rounded-pill py-2 px-3 shadow-sm' onclick=""sendQuickMessage('Đơn thuốc ho')"">
                                    😷 <strong>Đơn thuốc Ho</strong> (Ho khan, ho có đờm)
                                </button>
                                <button class='btn btn-outline-danger text-start rounded-pill py-2 px-3 shadow-sm' onclick=""sendQuickMessage('Đơn thuốc sốt')"">
                                    🤒 <strong>Đơn thuốc Sốt</strong> (Hạ sốt, cảm cúm)
                                </button>
                                <button class='btn btn-outline-info text-start rounded-pill py-2 px-3 shadow-sm text-dark' onclick=""sendQuickMessage('Đơn thuốc đau đầu')"">
                                    🧠 <strong>Đơn thuốc Đau đầu</strong> (Đau nhức, chóng mặt)
                                </button>
                            </div>";
                    }
                    else if (hasCough)
                    {
                        session.Symptom = "ho";
                        session.State = ""; // Reset state to prevent prompt loop

                        // Determine if they specified wet/dry
                        bool isDry = messageLower.Contains("khan") || messageLower.Contains("không đờm") || messageLower.Contains("ko đờm") || messageLower.Contains("ngứa họng");
                        bool isWet = messageLower.Contains("đờm") || messageLower.Contains("ướt") || messageLower.Contains("có đờm");

                        List<Thuoc> recommended = new List<Thuoc>();

                        if (isDry)
                        {
                            recommended = await _context.Thuocs
                                .Where(t => t.TenThuoc.Contains("Panadol") || t.TenThuoc.Contains("Strepsils") || t.TenThuoc.Contains("Vitamin C"))
                                .Take(3)
                                .ToListAsync();
                        }
                        else if (isWet)
                        {
                            recommended = await _context.Thuocs
                                .Where(t => t.TenThuoc.Contains("Decolgen") || t.TenThuoc.Contains("Strepsils") || t.TenThuoc.Contains("Vitamin C"))
                                .Take(3)
                                .ToListAsync();
                        }
                        else
                        {
                            recommended = await _context.Thuocs
                                .Where(t => t.TenThuoc.Contains("Strepsils") || t.TenThuoc.Contains("Decolgen") || t.TenThuoc.Contains("Vitamin C"))
                                .Take(3)
                                .ToListAsync();
                        }

                        string drugCards = "";
                        if (recommended.Any())
                        {
                            foreach (var drug in recommended)
                            {
                                drugCards += RenderDrugCard(drug);
                            }
                        }
                        else
                        {
                            drugCards = "<p class='text-muted small'>Không tìm thấy sản phẩm ho nào trong CSDL.</p>";
                        }

                        string coughTypeLabel = isDry ? " (HO KHAN)" : (isWet ? " (HO CÓ ĐỜM)" : "");

                        replyMessage = $@"
                            <div class='prescription-box border-primary border-start border-4 p-3 bg-white rounded-3 shadow-sm mb-2'>
                                <h6 class='text-primary fw-bold mb-1'><i class='bi bi-file-earmark-medical-fill me-1'></i> ĐƠN THUỐC GỢI Ý: TRỊ HO{coughTypeLabel}</h6>
                                <p class='text-muted mb-3' style='font-size: 0.75rem;'>Hỗ trợ giảm kích ứng họng, loãng đờm và tăng đề kháng.</p>
                                
                                <div class='mb-3'>
                                    <strong class='text-dark d-block mb-2' style='font-size: 0.8rem;'>💊 Thuốc khuyên dùng (lấy từ CSDL):</strong>
                                    {drugCards}
                                </div>

                                <div class='bg-light p-2 rounded mb-2' style='font-size: 0.82rem;'>
                                    <strong class='text-primary d-block mb-1'><i class='bi bi-clock-history me-1'></i> CÁCH UỐNG & LIỀU DÙNG:</strong>
                                    <ul class='mb-0 ps-3 text-dark' style='font-size: 0.78rem;'>
                                        <li><strong>Kẹo ngậm ho Strepsils Cool:</strong> Ngậm tan chậm 1 viên/lần, ngậm khi ho hoặc đau họng. Cách 2-3 tiếng ngậm lại.</li>
                                        <li><strong>Decolgen Forte:</strong> Uống 1 viên/lần x 3 lần/ngày sau bữa ăn (sáng/trưa/tối) nếu kèm nghẹt mũi, hắt hơi.</li>
                                        <li><strong>Vitamin C Enervon:</strong> Uống 1 viên vào buổi sáng sau ăn để tăng miễn dịch.</li>
                                    </ul>
                                </div>

                                <div class='p-2 rounded mb-1 border border-warning-subtle bg-warning-subtle text-warning-emphasis' style='font-size: 0.82rem;'>
                                    <strong class='d-block mb-1'><i class='bi bi-exclamation-triangle-fill me-1'></i> CHẾ ĐỘ KIÊNG CỮ:</strong>
                                    <ul class='mb-0 ps-3' style='font-size: 0.78rem;'>
                                        <li>Kiêng uống nước đá, đồ ăn lạnh. Hạn chế đồ ăn cay nóng, chiên xào nhiều mỡ, tôm cua ghẹ gây ngứa họng.</li>
                                        <li>Giữ ấm cổ họng, súc miệng nước muối ấm 2-3 lần/ngày. Tránh làm việc quá sức.</li>
                                    </ul>
                                </div>

                                <span class='text-muted small d-block mt-2' style='font-size: 0.7rem; line-height: 1.2;'>⚠️ *Lưu ý: Sản phẩm không thay thế đơn thuốc điều trị của bác sĩ chuyên khoa nếu bệnh trở nặng.*</span>
                            </div>";
                    }
                    else if (hasFever)
                    {
                        session.Symptom = "sốt";
                        session.State = "";

                        var recommended = await _context.Thuocs
                            .Where(t => t.TenThuoc.Contains("Paracetamol") || t.TenThuoc.Contains("Panadol") || t.TenThuoc.Contains("Vitamin C") || t.TenThuoc.Contains("Decolgen"))
                            .Take(3)
                            .ToListAsync();

                        string drugCards = "";
                        foreach (var drug in recommended)
                        {
                            drugCards += RenderDrugCard(drug);
                        }

                        replyMessage = $@"
                            <div class='prescription-box border-danger border-start border-4 p-3 bg-white rounded-3 shadow-sm mb-2'>
                                <h6 class='text-danger fw-bold mb-1'><i class='bi bi-file-earmark-medical-fill me-1'></i> ĐƠN THUỐC GỢI Ý: HẠ SỐT & CẢM CÚM</h6>
                                <p class='text-muted mb-3' style='font-size: 0.75rem;'>Giúp hạ thân nhiệt nhanh, bổ sung đề kháng đẩy lùi virus cúm.</p>
                                
                                <div class='mb-3'>
                                    <strong class='text-dark d-block mb-2' style='font-size: 0.8rem;'>💊 Thuốc khuyên dùng (lấy từ CSDL):</strong>
                                    {drugCards}
                                </div>

                                <div class='bg-light p-2 rounded mb-2' style='font-size: 0.82rem;'>
                                    <strong class='text-primary d-block mb-1'><i class='bi bi-clock-history me-1'></i> CÁCH UỐNG & LIỀU DÙNG:</strong>
                                    <ul class='mb-0 ps-3 text-dark' style='font-size: 0.78rem;'>
                                        <li><strong>Paracetamol 500mg:</strong> Uống 1 viên khi sốt từ 38.5°C trở lên. Cách nhau ít nhất 4-6 tiếng. Uống sau ăn nhẹ hoặc lúc sốt. Không uống quá 4 viên/ngày.</li>
                                        <li><strong>Vitamin C Enervon:</strong> Uống 1 viên vào buổi sáng sau ăn để tăng đề kháng.</li>
                                        <li><strong>Decolgen Forte:</strong> Uống 1 viên/lần, ngày 3 lần sau ăn nếu có sổ mũi đi kèm sốt nhẹ.</li>
                                    </ul>
                                </div>

                                <div class='p-2 rounded mb-1 border border-warning-subtle bg-warning-subtle text-warning-emphasis' style='font-size: 0.82rem;'>
                                    <strong class='d-block mb-1'><i class='bi bi-exclamation-triangle-fill me-1'></i> CHẾ ĐỘ KIÊNG CỮ:</strong>
                                    <ul class='mb-0 ps-3' style='font-size: 0.78rem;'>
                                        <li><strong>Tuyệt đối không uống rượu bia</strong> khi uống thuốc hạ sốt để tránh hủy hoại gan. Hạn chế đồ ăn ngọt, đồ nhiều dầu mỡ.</li>
                                        <li>Mặc quần áo thoáng mát, không đắp chăn dày. Lau người bằng nước ấm ở nách, bẹn. Nghỉ ngơi hoàn toàn, uống nhiều nước ấm.</li>
                                    </ul>
                                </div>

                                <span class='text-muted small d-block mt-2' style='font-size: 0.7rem; line-height: 1.2;'>⚠️ *Lưu ý: Nếu sốt cao kéo dài trên 3 ngày cần đi khám bác sĩ.*</span>
                            </div>";
                    }
                    else if (hasHeadache)
                    {
                        session.Symptom = "đau đầu";
                        session.State = "";

                        var recommended = await _context.Thuocs
                            .Where(t => t.TenThuoc.Contains("Panadol") || t.TenThuoc.Contains("Paracetamol") || t.TenThuoc.Contains("Ginkgo"))
                            .Take(2)
                            .ToListAsync();

                        string drugCards = "";
                        foreach (var drug in recommended)
                        {
                            drugCards += RenderDrugCard(drug);
                        }

                        replyMessage = $@"
                            <div class='prescription-box border-info border-start border-4 p-3 bg-white rounded-3 shadow-sm mb-2'>
                                <h6 class='text-info-emphasis fw-bold mb-1'><i class='bi bi-file-earmark-medical-fill me-1'></i> ĐƠN THUỐC GỢI Ý: GIẢM ĐAU ĐẦU & BỔ NÃO</h6>
                                <p class='text-muted mb-3' style='font-size: 0.75rem;'>Giảm đau nhức đầu và tăng tuần hoàn máu não.</p>
                                
                                <div class='mb-3'>
                                    <strong class='text-dark d-block mb-2' style='font-size: 0.8rem;'>💊 Thuốc khuyên dùng (lấy từ CSDL):</strong>
                                    {drugCards}
                                </div>

                                <div class='bg-light p-2 rounded mb-2' style='font-size: 0.82rem;'>
                                    <strong class='text-primary d-block mb-1'><i class='bi bi-clock-history me-1'></i> CÁCH UỐNG & LIỀU DÙNG:</strong>
                                    <ul class='mb-0 ps-3 text-dark' style='font-size: 0.78rem;'>
                                        <li><strong>Panadol Extra (hoặc Paracetamol):</strong> Uống 1 viên khi đau đầu, cách nhau ít nhất 4-6 tiếng. Uống sau ăn. Tối đa 4-6 viên/ngày.</li>
                                        <li><strong>Ginkgo Biloba 120mg:</strong> Uống 1 viên vào buổi sáng sau ăn để điều trị đau đầu lâu dài.</li>
                                    </ul>
                                </div>

                                <div class='p-2 rounded mb-1 border border-warning-subtle bg-warning-subtle text-warning-emphasis' style='font-size: 0.82rem;'>
                                    <strong class='d-block mb-1'><i class='bi bi-exclamation-triangle-fill me-1'></i> CHẾ ĐỘ KIÊNG CỮ:</strong>
                                    <ul class='mb-0 ps-3' style='font-size: 0.78rem;'>
                                        <li>Kiêng cà phê, nước chè đặc, rượu bia. Hạn chế bột ngọt và đồ ăn nhiều mỡ.</li>
                                        <li>Nằm nghỉ ở phòng tối, yên tĩnh. Tránh stress, không thức khuya, hạn chế nhìn điện thoại/máy tính.</li>
                                    </ul>
                                </div>

                                <span class='text-muted small d-block mt-2' style='font-size: 0.7rem; line-height: 1.2;'>⚠️ *Lưu ý: Không tự ý dùng Ginkgo cho người có hội chứng máu khó đông.*</span>
                            </div>";
                    }
                }
                // ==========================================
                // 5. INTENT: DRUG SAFETY CHECK (Tương tác thuốc)
                // ==========================================
                else if (messageLower.Contains("+") || messageLower.Contains(" và ") || messageLower.Contains(" chung với "))
                {
                    bool foundInteraction = false;
                    string warningBox = "";

                    if ((messageLower.Contains("paracetamol") || messageLower.Contains("panadol")) && messageLower.Contains("amoxicillin"))
                    {
                        foundInteraction = true;
                        warningBox = @"
                            <div class='alert alert-success border-0 mb-0 py-2 px-3' style='border-radius: 10px; font-size: 0.85rem;'>
                                <i class='bi bi-check-circle-fill text-success me-1'></i> <strong>Paracetamol + Amoxicillin:</strong><br/>
                                Không ghi nhận tương tác bất lợi. Có thể phối hợp khi điều trị sốt đi kèm nhiễm khuẩn theo đơn.
                            </div>";
                    }
                    else if ((messageLower.Contains("paracetamol") || messageLower.Contains("panadol")) && messageLower.Contains("ibuprofen"))
                    {
                        foundInteraction = true;
                        warningBox = @"
                            <div class='alert alert-warning border-0 mb-0 py-2 px-3' style='border-radius: 10px; font-size: 0.85rem;'>
                                <i class='bi bi-exclamation-triangle-fill text-warning me-1'></i> <strong>Paracetamol + Ibuprofen:</strong><br/>
                                Có thể kết hợp tăng tác dụng hạ sốt/giảm đau nhưng cần uống cách liều, tránh lạm dụng liều cao gây suy gan, viêm loét dạ dày.
                            </div>";
                    }
                    else if (messageLower.Contains("amoxicillin") && messageLower.Contains("tetracycline"))
                    {
                        foundInteraction = true;
                        warningBox = @"
                            <div class='alert alert-danger border-0 mb-0 py-2 px-3' style='border-radius: 10px; font-size: 0.85rem;'>
                                <i class='bi bi-x-circle-fill text-danger me-1'></i> <strong>Amoxicillin + Tetracycline:</strong><br/>
                                <strong>CẢNH BÁO NGUY HIỂM:</strong> Tetracycline (kìm khuẩn) ức chế hoạt động diệt khuẩn của Amoxicillin. <strong>Không được phối hợp!</strong>
                            </div>";
                    }
                    else if (messageLower.Contains("aspirin") && messageLower.Contains("ibuprofen"))
                    {
                        foundInteraction = true;
                        warningBox = @"
                            <div class='alert alert-danger border-0 mb-0 py-2 px-3' style='border-radius: 10px; font-size: 0.85rem;'>
                                <i class='bi bi-x-circle-fill text-danger me-1'></i> <strong>Aspirin + Ibuprofen:</strong><br/>
                                <strong>CẢNH BÁO:</strong> Làm tăng đáng kể nguy cơ loét đường tiêu hóa và xuất huyết dạ dày. <strong>Không tự ý kết hợp!</strong>
                            </div>";
                    }

                    if (foundInteraction)
                    {
                        replyMessage = $"Kết quả kiểm tra tương tác thuốc:<br/><br/>{warningBox}<br/><span class='text-muted' style='font-size: 0.75rem;'>⚠️ *Thông tin chỉ mang tính tham khảo, vui lòng hỏi dược sĩ.*</span>";
                    }
                    else
                    {
                        replyMessage = "Tôi chưa có dữ liệu tương tác cho sự kết hợp cụ thể này. Hãy hỏi trực tiếp dược sĩ để có chỉ dẫn an toàn.";
                    }
                }
                // ==========================================
                // 6. INTENT: SPECIFIC MEDICINE QUESTION (Hỏi đáp thuốc: Công dụng, giá, tồn kho)
                // ==========================================
                else
                {
                    var allDrugs = await _context.Thuocs.ToListAsync();
                    Thuoc? targetDrug = null;

                    foreach (var drug in allDrugs)
                    {
                        string drugNameLower = drug.TenThuoc.ToLower();
                        if (messageLower.Contains(drugNameLower) || (drug.HoatChat != null && messageLower.Contains(drug.HoatChat.ToLower())))
                        {
                            targetDrug = drug;
                            break;
                        }
                    }

                    if (targetDrug != null)
                    {
                        string stockStatus = targetDrug.SoLuong > 0 ? $"Còn hàng ({targetDrug.SoLuong} {targetDrug.DonViCoBan})" : "Hết hàng";
                        string stockClass = targetDrug.SoLuong > 0 ? "text-success" : "text-danger";

                        replyMessage = $@"
                            🔍 <strong>Thông tin thuốc từ CSDL Hệ thống:</strong><br/><br/>
                            📌 <strong>Tên thuốc:</strong> {targetDrug.TenThuoc}<br/>
                            🧪 <strong>Hoạt chất:</strong> {targetDrug.HoatChat ?? "Chưa rõ"}<br/>
                            📝 <strong>Mô tả/Chỉ định:</strong> {targetDrug.MoTa ?? "Chưa có mô tả."}<br/>
                            🩺 <strong>Công dụng:</strong> {targetDrug.CongDung ?? "Hỗ trợ giảm các triệu chứng bệnh lý."}<br/>
                            💊 <strong>Liều dùng tham khảo:</strong> {targetDrug.LieuLuong ?? "Sử dụng theo chỉ định."}<br/>
                            💵 <strong>Giá bán:</strong> {targetDrug.GiaHienTai.ToString("N0")} đ<br/>
                            📦 <strong>Tồn kho:</strong> <span class='{stockClass} fw-bold'>{stockStatus}</span><br/><br/>";

                        replyMessage += RenderDrugCard(targetDrug);
                        replyMessage += "<br/><span class='text-muted' style='font-size: 0.75rem;'>⚠️ *Thông tin chỉ mang tính tham khảo, vui lòng hỏi dược sĩ.*</span>";
                    }
                    else if (messageLower.StartsWith("tìm ") || messageLower.StartsWith("tìm kiếm "))
                    {
                        // Smart search fallback
                        string searchKeyword = messageLower.Replace("tìm kiếm ", "").Replace("tìm ", "").Trim();
                        var matchingDrugs = await _context.Thuocs
                            .Where(t => t.TenThuoc.Contains(searchKeyword) || (t.HoatChat != null && t.HoatChat.Contains(searchKeyword)) || (t.CongDung != null && t.CongDung.Contains(searchKeyword)))
                            .Take(3)
                            .ToListAsync();

                        if (matchingDrugs.Any())
                        {
                            replyMessage = $"Tôi tìm thấy {matchingDrugs.Count} sản phẩm khớp với từ khóa \"{searchKeyword}\":<br/><br/>";
                            foreach (var drug in matchingDrugs)
                            {
                                replyMessage += RenderDrugCard(drug);
                            }
                        }
                        else
                        {
                            replyMessage = $"Không tìm thấy thuốc nào phù hợp với \"{searchKeyword}\" trong CSDL.";
                        }
                    }
                    // OpenAI API Integration (RAG style if key is configured, else fallback)
                    else
                    {
                        string? apiKey = _configuration["OpenAI:ApiKey"];
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            try
                            {
                                replyMessage = await CallOpenAIApi(userMessage, apiKey);
                            }
                            catch
                            {
                                replyMessage = "Tôi chưa đủ dữ liệu để trả lời câu hỏi này.";
                            }
                        }
                        else
                        {
                            replyMessage = "Tôi chưa đủ dữ liệu để trả lời câu hỏi của bạn. Hãy thử hỏi về thuốc cụ thể, triệu chứng ho/sốt/đau đầu hoặc tra cứu đơn hàng.";
                        }
                    }
                }
            }
            catch (Exception)
            {
                replyMessage = "Tôi chưa đủ dữ liệu để trả lời.";
            }

            // End typing status
            await Clients.Caller.SendAsync("ReceiveTypingStatus", false);

            // Send reply to caller
            await Clients.Caller.SendAsync("ReceiveBotMessage", replyMessage);
        }

        public async Task ProcessPrescriptionText(string extractedText)
        {
            await Clients.Caller.SendAsync("ReceiveTypingStatus", true);
            await Task.Delay(1000);

            string replyMessage = "";
            try
            {
                if (string.IsNullOrEmpty(extractedText) || extractedText.Length < 3)
                {
                    replyMessage = "Không trích xuất được chữ nào từ toa thuốc của bạn. Vui lòng thử lại với ảnh rõ nét hơn.";
                }
                else
                {
                    string textCleaned = Regex.Replace(extractedText, @"[^a-zA-Z0-9\sàáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]", " ");
                    string[] words = textCleaned.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    var keywords = words
                        .Where(w => w.Length >= 4 && !int.TryParse(w, out _))
                        .Select(w => w.ToLower())
                        .Distinct()
                        .ToList();

                    var matchedDrugs = new List<Thuoc>();

                    if (keywords.Any())
                    {
                        var allDrugs = await _context.Thuocs.ToListAsync();
                        foreach (var drug in allDrugs)
                        {
                            string drugNameLower = drug.TenThuoc.ToLower();
                            if (keywords.Any(kw => drugNameLower.Contains(kw)))
                            {
                                matchedDrugs.Add(drug);
                            }
                        }
                    }

                    if (matchedDrugs.Any())
                    {
                        replyMessage = $"📝 <strong>Đã nhận diện các thuốc trong toa:</strong><br/><br/>";
                        
                        var matchedIds = new List<int>();
                        foreach (var drug in matchedDrugs)
                        {
                            matchedIds.Add(drug.MaThuoc);
                            string stockLabel = drug.SoLuong > 0 ? "Còn hàng" : "Hết hàng";
                            string stockClass = drug.SoLuong > 0 ? "text-success" : "text-danger";

                            string imgUrl = string.IsNullOrEmpty(drug.HinhAnh) ? "https://via.placeholder.com/100?text=Thuoc" : drug.HinhAnh;
                            replyMessage += $@"
                                <div class='card drug-result-card mb-2 p-2 shadow-sm border-0' style='border-radius: 10px; background: #fff;'>
                                    <div class='d-flex align-items-center gap-2'>
                                        <img src='{imgUrl}' alt='{drug.TenThuoc}' class='rounded' style='width: 40px; height: 40px; object-fit: contain;' />
                                        <div class='flex-grow-1 text-truncate'>
                                            <div class='fw-bold text-dark' style='font-size: 0.8rem;'>{drug.TenThuoc}</div>
                                            <span class='text-muted small' style='font-size: 0.72rem;'>Tình trạng: <strong class='{stockClass}'>{stockLabel}</strong></span>
                                        </div>
                                        <button class='btn btn-xs btn-outline-success py-1 px-2 rounded-pill fw-bold' style='font-size: 0.65rem;' onclick='addDrugToCart({drug.MaThuoc}, ""{drug.TenThuoc}"")' {(drug.SoLuong > 0 ? "" : "disabled")}>
                                            Thêm
                                        </button>
                                    </div>
                                </div>";
                        }

                        // Add action button to buy all
                        string jsonIds = string.Join(",", matchedIds);
                        replyMessage += $@"
                            <div class='mt-3 text-center'>
                                <button class='btn btn-sm btn-primary rounded-pill px-3 fw-bold w-100' onclick='addAllDrugsToCart([{jsonIds}])'>
                                    <i class='bi bi-bag-plus-fill me-1'></i> Thêm tất cả vào giỏ hàng
                                </button>
                            </div>";
                    }
                    else
                    {
                        replyMessage = "❌ <strong>Không nhận diện được thuốc:</strong> Không tìm thấy thuốc trong đơn phù hợp với tủ thuốc hiện có tại PharmaHub.";
                    }
                }
            }
            catch (Exception)
            {
                replyMessage = "Không thể phân tích đơn thuốc do sự cố kỹ thuật.";
            }

            await Clients.Caller.SendAsync("ReceiveTypingStatus", false);
            await Clients.Caller.SendAsync("ReceiveBotMessage", replyMessage);
        }

        private string RenderDrugCard(Thuoc drug)
        {
            string imgUrl = string.IsNullOrEmpty(drug.HinhAnh) ? "https://via.placeholder.com/100?text=Thuoc" : drug.HinhAnh;
            string stockStatus = drug.SoLuong > 0 ? $"Còn hàng ({drug.SoLuong})" : "Hết hàng";
            string stockClass = drug.SoLuong > 0 ? "text-success" : "text-danger";

            return $@"
                <div class='card drug-result-card mb-2 p-2 shadow-sm border-0' style='border-radius: 10px; background: #fff;'>
                    <div class='d-flex align-items-center gap-2'>
                        <img src='{imgUrl}' alt='{drug.TenThuoc}' class='rounded' style='width: 50px; height: 50px; object-fit: contain;' />
                        <div class='flex-grow-1 text-truncate'>
                            <div class='fw-bold text-dark' style='font-size: 0.85rem;'>{drug.TenThuoc}</div>
                            <div class='text-muted small' style='font-size: 0.72rem;'>Tồn kho: <span class='{stockClass} fw-bold'>{stockStatus}</span></div>
                            <span class='text-danger fw-bold' style='font-size: 0.8rem;'>{drug.GiaHienTai.ToString("N0")} đ</span>
                        </div>
                        <div class='d-flex flex-column gap-1'>
                            <button class='btn btn-sm btn-success py-1 px-2 rounded-pill fw-bold' style='font-size: 0.7rem;' onclick='addDrugToCart({drug.MaThuoc}, ""{drug.TenThuoc}"")' {(drug.SoLuong > 0 ? "" : "disabled")}>
                                <i class='bi bi-cart-plus'></i> Mua ngay
                            </button>
                            <button class='btn btn-sm btn-outline-primary py-1 px-2 rounded-pill' style='font-size: 0.7rem;' onclick='showDetailPopup({drug.MaThuoc}, ""{drug.TenThuoc}"", ""{imgUrl}"", ""{drug.GiaHienTai.ToString("N0")} đ"", ""{HtmlEncode(drug.MoTa ?? "Chưa có mô tả.")}"", ""{HtmlEncode(drug.CongDung ?? "Chưa có công dụng.")}"", ""{HtmlEncode(drug.LieuLuong ?? "Sử dụng theo chỉ định.")}"")'>
                                Chi tiết
                            </button>
                        </div>
                    </div>
                </div>";
        }

        private string HtmlEncode(string text)
        {
            return text.Replace("'", "\\'").Replace("\"", "&quot;").Replace("\n", " ").Replace("\r", " ");
        }

        // Call OpenAI Completion API with context
        private async Task<string> CallOpenAIApi(string userMessage, string apiKey)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            // RAG context: get some drugs
            var drugs = await _context.Thuocs.Take(10).ToListAsync();
            var drugContextList = drugs.Select(d => new { d.TenThuoc, d.HoatChat, d.MoTa, d.CongDung, d.GiaHienTai, d.SoLuong });
            string contextJson = JsonSerializer.Serialize(drugContextList);

            var systemPrompt = $@"
                Bạn là Trợ lý Nhà Thuốc AI của PharmaHub.
                Dưới đây là thông tin thực tế từ cơ sở dữ liệu của chúng tôi:
                {contextJson}
                
                Quy tắc bắt buộc:
                - Chỉ trả lời các câu hỏi về y tế, thuốc, triệu chứng, hoặc cách sử dụng thuốc.
                - KHÔNG được bịa đặt thuốc mới, KHÔNG tự kê đơn.
                - Luôn ưu tiên lấy dữ liệu từ cơ sở dữ liệu trên.
                - Đối với các triệu chứng ho, sốt, đau đầu, không kê đơn mà phải hỏi các câu hỏi làm rõ (ho khan/ho có đờm, kéo dài bao lâu,...) trước khi gợi ý nhóm thuốc.
                - Nếu không chắc chắn hoặc thiếu thông tin, hãy trả lời: 'Tôi chưa đủ dữ liệu để trả lời.'";

            var payload = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                temperature = 0.3
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", requestContent);
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("API Call Failed");
            }

            var responseBody = await response.Content.ReadAsStringStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "Tôi chưa đủ dữ liệu để trả lời.";
        }
    }

    // Extension helper for string reading in older framework versions
    public static class HttpContentExtensions
    {
        public static async Task<string> ReadAsStringStringAsync(this HttpContent content)
        {
            return await content.ReadAsStringAsync();
        }
    }
}
