using System;
using System.Collections.Generic;
using System.Linq;

namespace Web_Quản_Lí_Nhà_Thuốc.Helpers
{
    public static class SearchHelper
    {
        // Compute Levenshtein distance to find fuzzy matches
        public static int GetLevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
                return string.IsNullOrEmpty(target) ? 0 : target.Length;

            if (string.IsNullOrEmpty(target))
                return source.Length;

            source = Normalize(source);
            target = Normalize(target);

            int n = source.Length;
            int m = target.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.ToLowerInvariant().Trim();
        }

        // Mock Database of active ingredient interactions
        private static readonly Dictionary<string, string> Interactions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Paracetamol+Ibuprofen", "Cảnh báo (Mức độ Nhẹ): Dùng chung có thể gây tăng gánh nặng cho gan và dạ dày nếu dùng liều cao kéo dài." },
            { "Aspirin+Warfarin", "Cảnh báo (Nguy hiểm - Đỏ): Tăng cực mạnh nguy cơ xuất huyết dạ dày và nội tạng. Không tự ý phối hợp." },
            { "Sildenafil+Nitroglycerin", "Cảnh báo (Chết người - Đỏ): Gây hạ huyết áp nghiêm trọng đột ngột, có thể dẫn đến đột quỵ hoặc tử vong." },
            { "Amoxicillin+Methotrexate", "Cảnh báo (Nghiêm trọng - Vàng): Amoxicillin làm tăng nồng độ Methotrexate trong máu, dễ gây độc tế bào." },
            { "Clopidogrel+Esomeprazole", "Cảnh báo (Nghiêm trọng - Vàng): Esomeprazole làm giảm hoạt tính chống đông của Clopidogrel, tăng nguy cơ huyết khối." }
        };

        public static string? CheckInteraction(string hoatChat1, string hoatChat2)
        {
            if (string.IsNullOrWhiteSpace(hoatChat1) || string.IsNullOrWhiteSpace(hoatChat2))
                return null;

            string key1 = $"{hoatChat1.Trim()}+{hoatChat2.Trim()}";
            string key2 = $"{hoatChat2.Trim()}+{hoatChat1.Trim()}";

            if (Interactions.TryGetValue(key1, out var desc1)) return desc1;
            if (Interactions.TryGetValue(key2, out var desc2)) return desc2;

            return null;
        }
    }
}
