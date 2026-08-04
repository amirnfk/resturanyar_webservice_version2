using System.Collections.Generic;

namespace resturanyar.Helpers
{
    /// <summary>
    /// Cross-platform order-status colors mirrored from Android values/colors.xml ost_* (light).
    /// Android is the source of truth — keep hex values identical.
    /// </summary>
    public static class OrderStatusColors
    {
        /// <summary>Solid header accent per status id (charts, accents).</summary>
        public static readonly IReadOnlyDictionary<int, string> HeaderHexById =
            new Dictionary<int, string>
            {
                { 1, "#6366F1" },
                { 2, "#2563EB" },
                { 3, "#D97706" },
                { 4, "#EA580C" },
                { 5, "#0891B2" },
                { 6, "#059669" },
                { 7, "#0D9488" },
                { 8, "#16A34A" },
                { 9, "#DC2626" },
                { 10, "#DC2626" },
                { 11, "#64748B" },
                { 12, "#7C3AED" },
                { 99, "#6D28D9" },
            };

        public static string HeaderHex(int statusId, string fallback = "#64748B")
            => HeaderHexById.TryGetValue(statusId, out var hex) ? hex : fallback;

        /// <summary>Soft chip CSS classes matching Android status chips.</summary>
        public static string ChipClass(int statusId)
            => HeaderHexById.ContainsKey(statusId)
                ? $"status-badge ost-badge ost-{statusId}"
                : "status-badge ost-badge ost-11";

        /// <summary>Copy of header hex map for view models / JSON serialization.</summary>
        public static Dictionary<int, string> HeaderHexDictionary()
            => new Dictionary<int, string>(HeaderHexById);
    }
}
