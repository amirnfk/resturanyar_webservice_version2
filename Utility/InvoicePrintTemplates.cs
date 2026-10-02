namespace resturanyar.Utility
{
    public record InvoicePrintTemplate(
        string Id,
        string Label,
        string Description,
        string PreviewKind);

    public static class InvoicePrintTemplates
    {
        public const string ClassicId = "classic";
        public const string ReadableId = "readable";
        public const string ThermalId = "thermal";
        public const string FormalId = "formal";
        public const string ElegantId = "elegant";

        private static readonly Dictionary<string, InvoicePrintTemplate> ById = new(StringComparer.OrdinalIgnoreCase)
        {
            [ClassicId] = new(
                ClassicId,
                "مدرن",
                "کارت رنگی با برند نارنجی؛ مناسب چاپ A4 و نمایش روی صفحه.",
                "modern"),
            [ReadableId] = new(
                ReadableId,
                "خوانا",
                "همان چیدمان مدرن با فونت و فاصلهٔ بزرگ‌تر برای مطالعه آسان.",
                "readable"),
            [ThermalId] = new(
                ThermalId,
                "فیش حرارتی",
                "شبیه فیش پرینتر حرارتی: جدول ساده، جمع عددی و مبلغ به حروف.",
                "thermal"),
            [FormalId] = new(
                FormalId,
                "رسمی",
                "فاکتور اداری با خطوط دوبل و ظاهر رسمی برای بایگانی.",
                "formal"),
            [ElegantId] = new(
                ElegantId,
                "لوکس",
                "طراحی پرمیوم با هدر تیره و تاکید روی جمع کل.",
                "elegant")
        };

        public static InvoicePrintTemplate Default => ById[ClassicId];

        public static IReadOnlyList<InvoicePrintTemplate> All => ById.Values.ToList();

        public static InvoicePrintTemplate FromTemplateId(string? templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                return Default;

            // Backward compatibility: old "compact" maps to thermal slip style.
            if (string.Equals(templateId.Trim(), "compact", StringComparison.OrdinalIgnoreCase))
                return ById[ThermalId];

            return ById.TryGetValue(templateId.Trim(), out var template) ? template : Default;
        }

        public static string Normalize(string? templateId) => FromTemplateId(templateId).Id;

        public static bool IsKnown(string? templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
                return false;
            var id = templateId.Trim();
            if (string.Equals(id, "compact", StringComparison.OrdinalIgnoreCase))
                return true;
            return ById.ContainsKey(id);
        }
    }
}
