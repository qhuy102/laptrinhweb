using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Web_Quản_Lí_Nhà_Thuốc.Models;

namespace Web_Quản_Lí_Nhà_Thuốc.Helpers
{
    public class MedicineImportRow
    {
        public int RowIndex { get; set; }
        public string TenThuoc { get; set; } = "";
        public string TenLoaiThuoc { get; set; } = "";
        public string DonViCoBan { get; set; } = "";
        public decimal DonGia { get; set; }
        public int SoLuong { get; set; }
        public DateTime HanSuDung { get; set; }
        public string? ViTriKe { get; set; }
        public string? HoatChat { get; set; }
        public string? NhomDieuTri { get; set; }
        public string? CongDung { get; set; }
        public string? ChongChiDinh { get; set; }
        public string? LieuLuong { get; set; }
        
        public bool IsValid { get; set; } = true;
        public List<string> ErrorMessages { get; set; } = new List<string>();
    }

    public class PrescriptionImportItem
    {
        public int RowIndex { get; set; }
        public string TenThuoc { get; set; } = "";
        public int SoLuong { get; set; }
        public string LieuDung { get; set; } = "";
        
        public int? MaThuoc { get; set; } // Resolved from db
        public bool IsValid { get; set; } = true;
        public List<string> ErrorMessages { get; set; } = new List<string>();
    }

    public class PrescriptionImportGroup
    {
        public string TenBenhNhan { get; set; } = "";
        public string BacSiKeDon { get; set; } = "";
        public string ChanDoan { get; set; } = "";
        public DateTime NgayKeDon { get; set; }
        public string TrangThai { get; set; } = "ChoDuyet";
        
        public List<PrescriptionImportItem> Items { get; set; } = new List<PrescriptionImportItem>();
        public bool IsValid => Items.All(i => i.IsValid);
        public List<string> ErrorMessages => Items.SelectMany(i => i.ErrorMessages).Distinct().ToList();
    }

    public static class ExcelHelper
    {
        // --- CSV TEMPLATE GENERATION ---
        public static byte[] GenerateMedicineTemplateCsv()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms, new System.Text.UTF8Encoding(true)))
                {
                    writer.WriteLine("Tên thuốc (*),Loại thuốc (*),Đơn vị cơ bản (*),Đơn giá bán (VNĐ) (*),Số lượng ban đầu (*),Hạn sử dụng (dd/MM/yyyy) (*),Vị trí kệ,Hoạt chất chính,Nhóm điều trị,Công dụng / Chỉ định,Chống chỉ định,Liều lượng");
                    writer.WriteLine("\"Panadol Extra\",\"Thuốc không kê đơn\",\"Viên\",5000,100,\"31/12/2027\",\"Kệ A - Tầng 1\",\"Paracetamol 500mg, Caffeine 65mg\",\"Hạ sốt - Giảm đau\",\"Hạ sốt, giảm đau đầu\",\"Mẫn cảm với paracetamol\",\"Uống 1-2 viên mỗi 4-6 giờ\"");
                    writer.WriteLine("\"Amoxicillin 500mg\",\"Thuốc kê đơn\",\"Viên\",8000,50,\"30/06/2027\",\"Kệ B - Tầng 2\",\"Amoxicillin 500mg\",\"Kháng sinh - Kháng viêm\",\"Điều trị nhiễm khuẩn\",\"Mẫn cảm với penicillin\",\"Uống theo chỉ định bác sĩ\"");
                }
                return ms.ToArray();
            }
        }

        public static byte[] GeneratePrescriptionTemplateCsv()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new StreamWriter(ms, new System.Text.UTF8Encoding(true)))
                {
                    writer.WriteLine("Tên bệnh nhân (*),Bác sĩ kê đơn (*),Chẩn đoán (*),Ngày kê đơn (dd/MM/yyyy) (*),Trạng thái (ChoDuyet/DaDuyet/DaBan/DaHuy) (*),Tên thuốc (*),Số lượng (*),Liều dùng (*)");
                    writer.WriteLine("\"Nguyễn Văn A\",\"Bác sĩ Nguyễn Văn Cảnh\",\"Cảm cúm sốt nhẹ\",\"15/06/2026\",\"ChoDuyet\",\"Panadol Extra\",10,\"Sáng 1 viên, tối 1 viên sau ăn\"");
                    writer.WriteLine("\"Nguyễn Văn A\",\"Bác sĩ Nguyễn Văn Cảnh\",\"Cảm cúm sốt nhẹ\",\"15/06/2026\",\"ChoDuyet\",\"Amoxicillin 500mg\",10,\"Uống 1 viên sau ăn trưa\"");
                    writer.WriteLine("\"Trần Thị B\",\"Bác sĩ Lê Hoàng\",\"Sốt siêu vi\",\"15/06/2026\",\"DaDuyet\",\"Panadol Extra\",15,\"Sáng 1 viên, trưa 1 viên, tối 1 viên\"");
                }
                return ms.ToArray();
            }
        }

        // --- CORE PARSING ENTRY POINTS ---
        public static List<List<string>> ReadFileRows(Stream stream, string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            if (extension == ".csv")
            {
                return ReadCsvSheet(stream);
            }
            else if (extension == ".xlsx")
            {
                return ReadXlsxSheet(stream);
            }
            throw new NotSupportedException("Chỉ hỗ trợ file .csv hoặc .xlsx");
        }

        // --- CSV READER ---
        private static List<List<string>> ReadCsvSheet(Stream stream)
        {
            var rows = new List<List<string>>();
            using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = ParseCsvLine(line);
                    rows.Add(row);
                }
            }
            return rows;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();
            char delimiter = line.Contains(";") ? ';' : ',';

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            result.Add(currentField.ToString().Trim());
            return result;
        }

        // --- XLSX ZIP READER ---
        private static List<List<string>> ReadXlsxSheet(Stream stream)
        {
            var rows = new List<List<string>>();
            var sharedStrings = new List<string>();

            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                // 1. Read shared strings
                var sstEntry = archive.GetEntry("xl/sharedStrings.xml");
                if (sstEntry != null)
                {
                    using (var sstStream = sstEntry.Open())
                    {
                        var doc = XDocument.Load(sstStream);
                        var tElements = doc.Descendants().Where(x => x.Name.LocalName == "t");
                        foreach (var t in tElements)
                        {
                            sharedStrings.Add(t.Value);
                        }
                    }
                }

                // 2. Read sheet1
                var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry != null)
                {
                    using (var sheetStream = sheetEntry.Open())
                    {
                        var doc = XDocument.Load(sheetStream);
                        var rowsElements = doc.Descendants().Where(x => x.Name.LocalName == "row").OrderBy(r => {
                            int.TryParse(r.Attribute("r")?.Value, out int idx);
                            return idx;
                        });

                        foreach (var rowEl in rowsElements)
                        {
                            var rowData = new List<string>();
                            var cellElements = rowEl.Elements().Where(x => x.Name.LocalName == "c").ToList();
                            if (!cellElements.Any()) continue;

                            var colValues = new Dictionary<int, string>();
                            int maxColIdx = 0;

                            foreach (var cellEl in cellElements)
                            {
                                var rAttr = cellEl.Attribute("r")?.Value ?? "";
                                int colIdx = GetColumnIndex(rAttr);
                                if (colIdx > maxColIdx) maxColIdx = colIdx;

                                var valEl = cellEl.Element(cellEl.Name.Namespace + "v");
                                var val = valEl?.Value ?? "";
                                var tAttr = cellEl.Attribute("t")?.Value;

                                if (tAttr == "s" && int.TryParse(val, out int sstIdx) && sstIdx >= 0 && sstIdx < sharedStrings.Count)
                                {
                                    val = sharedStrings[sstIdx];
                                }
                                colValues[colIdx] = val;
                            }

                            for (int c = 1; c <= maxColIdx; c++)
                            {
                                colValues.TryGetValue(c, out string? cellVal);
                                rowData.Add(cellVal ?? "");
                            }
                            rows.Add(rowData);
                        }
                    }
                }
            }
            return rows;
        }

        private static int GetColumnIndex(string cellReference)
        {
            string colLetter = new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpper();
            int colIndex = 0;
            foreach (char c in colLetter)
            {
                colIndex = colIndex * 26 + (c - 'A' + 1);
            }
            return colIndex;
        }

        // --- HIGH LEVEL MEDICINE PARSING ---
        public static List<MedicineImportRow> ParseMedicines(List<List<string>> rawRows, List<LoaiThuoc> loaiThuocs)
        {
            var result = new List<MedicineImportRow>();
            if (rawRows.Count <= 1) return result; // Headers only or empty

            for (int r = 1; r < rawRows.Count; r++) // Skip header row
            {
                var rowData = rawRows[r];
                if (rowData.Count == 0) continue;

                // Ensure row has enough elements
                while (rowData.Count < 12) rowData.Add("");

                var nameVal = rowData[0]?.Trim();
                var categoryVal = rowData[1]?.Trim();
                var unitVal = rowData[2]?.Trim();
                var priceVal = rowData[3]?.Trim();
                var qtyVal = rowData[4]?.Trim();
                var expiryVal = rowData[5]?.Trim();
                var shelfVal = rowData[6]?.Trim();
                var activeVal = rowData[7]?.Trim();
                var treatVal = rowData[8]?.Trim();
                var indicVal = rowData[9]?.Trim();
                var contraVal = rowData[10]?.Trim();
                var doseVal = rowData[11]?.Trim();

                if (string.IsNullOrEmpty(nameVal) && string.IsNullOrEmpty(categoryVal) && string.IsNullOrEmpty(unitVal))
                    continue;

                var importRow = new MedicineImportRow
                {
                    RowIndex = r + 1,
                    TenThuoc = nameVal ?? "",
                    TenLoaiThuoc = categoryVal ?? "",
                    DonViCoBan = unitVal ?? "",
                    ViTriKe = string.IsNullOrEmpty(shelfVal) ? null : shelfVal,
                    HoatChat = string.IsNullOrEmpty(activeVal) ? null : activeVal,
                    NhomDieuTri = string.IsNullOrEmpty(treatVal) ? null : treatVal,
                    CongDung = string.IsNullOrEmpty(indicVal) ? null : indicVal,
                    ChongChiDinh = string.IsNullOrEmpty(contraVal) ? null : contraVal,
                    LieuLuong = string.IsNullOrEmpty(doseVal) ? null : doseVal
                };

                // Validate
                if (string.IsNullOrEmpty(importRow.TenThuoc))
                {
                    importRow.IsValid = false;
                    importRow.ErrorMessages.Add("Tên thuốc không được để trống.");
                }

                if (string.IsNullOrEmpty(importRow.TenLoaiThuoc))
                {
                    importRow.IsValid = false;
                    importRow.ErrorMessages.Add("Loại thuốc không được để trống.");
                }
                else
                {
                    var matchingCat = loaiThuocs.FirstOrDefault(l => l.TenLoai.Equals(importRow.TenLoaiThuoc, StringComparison.OrdinalIgnoreCase));
                    if (matchingCat == null)
                    {
                        importRow.IsValid = false;
                        importRow.ErrorMessages.Add($"Loại thuốc '{importRow.TenLoaiThuoc}' không tồn tại.");
                    }
                }

                if (string.IsNullOrEmpty(importRow.DonViCoBan))
                {
                    importRow.IsValid = false;
                    importRow.ErrorMessages.Add("Đơn vị không được để trống.");
                }

                if (string.IsNullOrEmpty(priceVal) || !decimal.TryParse(priceVal, out decimal price) || price < 0)
                {
                    importRow.IsValid = false;
                    importRow.ErrorMessages.Add("Đơn giá bán phải là số dương hợp lệ.");
                }
                else
                {
                    importRow.DonGia = price;
                }

                if (string.IsNullOrEmpty(qtyVal) || !int.TryParse(qtyVal, out int qty) || qty < 0)
                {
                    importRow.IsValid = false;
                    importRow.ErrorMessages.Add("Số lượng phải là số nguyên dương hợp lệ.");
                }
                else
                {
                    importRow.SoLuong = qty;
                }

                if (string.IsNullOrEmpty(expiryVal) || !DateTime.TryParseExact(expiryVal, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "M/d/yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime expDate))
                {
                    importRow.IsValid = false;
                    importRow.ErrorMessages.Add("Hạn dùng không đúng định dạng (dd/MM/yyyy).");
                }
                else
                {
                    importRow.HanSuDung = expDate;
                }

                result.Add(importRow);
            }
            return result;
        }

        // --- HIGH LEVEL PRESCRIPTION PARSING ---
        public static List<PrescriptionImportGroup> ParsePrescriptions(List<List<string>> rawRows, List<Thuoc> existingDrugs)
        {
            var rawItems = new List<PrescriptionImportRow>();
            if (rawRows.Count <= 1) return new List<PrescriptionImportGroup>();

            for (int r = 1; r < rawRows.Count; r++) // Skip header
            {
                var rowData = rawRows[r];
                if (rowData.Count == 0) continue;

                while (rowData.Count < 8) rowData.Add("");

                var patientVal = rowData[0]?.Trim();
                var doctorVal = rowData[1]?.Trim();
                var diagVal = rowData[2]?.Trim();
                var dateVal = rowData[3]?.Trim();
                var statusVal = rowData[4]?.Trim();
                var drugNameVal = rowData[5]?.Trim();
                var qtyVal = rowData[6]?.Trim();
                var dosageVal = rowData[7]?.Trim();

                if (string.IsNullOrEmpty(patientVal) && string.IsNullOrEmpty(doctorVal) && string.IsNullOrEmpty(drugNameVal))
                    continue;

                var rawItem = new PrescriptionImportRow
                {
                    RowIndex = r + 1,
                    TenBenhNhan = patientVal ?? "",
                    BacSiKeDon = doctorVal ?? "",
                    ChanDoan = diagVal ?? "",
                    TenThuoc = drugNameVal ?? "",
                    LieuDung = dosageVal ?? "",
                    TrangThai = string.IsNullOrEmpty(statusVal) ? "ChoDuyet" : statusVal
                };

                // Normalize Status
                if (rawItem.TrangThai.ToLower() == "chờ tư vấn" || rawItem.TrangThai.ToLower() == "choduyet") rawItem.TrangThai = "ChoDuyet";
                else if (rawItem.TrangThai.ToLower() == "đã tư vấn" || rawItem.TrangThai.ToLower() == "daduyet") rawItem.TrangThai = "DaDuyet";
                else if (rawItem.TrangThai.ToLower() == "đã bán" || rawItem.TrangThai.ToLower() == "daban") rawItem.TrangThai = "DaBan";
                else if (rawItem.TrangThai.ToLower() == "đã hủy" || rawItem.TrangThai.ToLower() == "dahuy") rawItem.TrangThai = "DaHuy";

                // Validations
                if (string.IsNullOrEmpty(rawItem.TenBenhNhan))
                {
                    rawItem.IsValid = false;
                    rawItem.ErrorMessages.Add("Tên bệnh nhân không được để trống.");
                }

                if (string.IsNullOrEmpty(rawItem.BacSiKeDon))
                {
                    rawItem.IsValid = false;
                    rawItem.ErrorMessages.Add("Bác sĩ không được để trống.");
                }

                if (string.IsNullOrEmpty(rawItem.ChanDoan))
                {
                    rawItem.IsValid = false;
                    rawItem.ErrorMessages.Add("Chẩn đoán không được để trống.");
                }

                if (string.IsNullOrEmpty(dateVal) || !DateTime.TryParseExact(dateVal, new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "M/d/yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime pDate))
                {
                    rawItem.IsValid = false;
                    rawItem.ErrorMessages.Add("Ngày kê đơn sai định dạng (dd/MM/yyyy).");
                }
                else
                {
                    rawItem.NgayKeDon = pDate;
                }

                if (string.IsNullOrEmpty(rawItem.TenThuoc))
                {
                    rawItem.IsValid = false;
                    rawItem.ErrorMessages.Add("Tên thuốc không được để trống.");
                }
                else
                {
                    var matchingDrug = existingDrugs.FirstOrDefault(d => d.TenThuoc.Equals(rawItem.TenThuoc, StringComparison.OrdinalIgnoreCase));
                    if (matchingDrug == null)
                    {
                        rawItem.IsValid = false;
                        rawItem.ErrorMessages.Add($"Thuốc '{rawItem.TenThuoc}' không tồn tại trong hệ thống.");
                    }
                    else
                    {
                        rawItem.MaThuoc = matchingDrug.MaThuoc;
                    }
                }

                if (string.IsNullOrEmpty(qtyVal) || !int.TryParse(qtyVal, out int qty) || qty <= 0)
                {
                    rawItem.IsValid = false;
                    rawItem.ErrorMessages.Add("Số lượng phải là số nguyên lớn hơn 0.");
                }
                else
                {
                    rawItem.SoLuong = qty;
                }

                rawItems.Add(rawItem);
            }

            // Group into Prescriptions
            var groups = rawItems
                .GroupBy(x => new { 
                    x.TenBenhNhan, 
                    x.BacSiKeDon, 
                    x.ChanDoan, 
                    x.NgayKeDon,
                    x.TrangThai
                })
                .Select(g => new PrescriptionImportGroup
                {
                    TenBenhNhan = g.Key.TenBenhNhan,
                    BacSiKeDon = g.Key.BacSiKeDon,
                    ChanDoan = g.Key.ChanDoan,
                    NgayKeDon = g.Key.NgayKeDon,
                    TrangThai = g.Key.TrangThai,
                    Items = g.Select(i => new PrescriptionImportItem
                    {
                        RowIndex = i.RowIndex,
                        TenThuoc = i.TenThuoc,
                        SoLuong = i.SoLuong,
                        LieuDung = i.LieuDung,
                        MaThuoc = i.MaThuoc,
                        IsValid = i.IsValid,
                        ErrorMessages = i.ErrorMessages
                    }).ToList()
                })
                .ToList();

            return groups;
        }

        private class PrescriptionImportRow
        {
            public int RowIndex { get; set; }
            public string TenBenhNhan { get; set; } = "";
            public string BacSiKeDon { get; set; } = "";
            public string ChanDoan { get; set; } = "";
            public DateTime NgayKeDon { get; set; }
            public string TrangThai { get; set; } = "ChoDuyet";
            public string TenThuoc { get; set; } = "";
            public int SoLuong { get; set; }
            public string LieuDung { get; set; } = "";
            public int? MaThuoc { get; set; }
            public bool IsValid { get; set; } = true;
            public List<string> ErrorMessages { get; set; } = new List<string>();
        }
    }
}
