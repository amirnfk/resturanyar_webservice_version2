using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using resturanyar.Models.ViewModels;

namespace resturanyar.Services
{
    public static class CustomersPdfExportService
    {
        private static readonly object FontLock = new();
        private static bool _fontRegistered;
        private const string FontFamily = "Vazirmatn";

        public static byte[] BuildPdf(
            IList<CustomerStatsViewModel> customers,
            string periodLabel,
            string sortLabel,
            string? search,
            string? webRootPath = null)
        {
            EnsureFontRegistered(webRootPath);
            QuestPDF.Settings.License = LicenseType.Community;

            var totalSpent = customers.Sum(c => c.TotalSpent);
            var totalOrders = customers.Sum(c => c.TotalOrders);
            var searchPart = string.IsNullOrWhiteSpace(search) ? "بدون جستجو" : $"جستجو: {search}";
            var isAllPeriod = string.IsNullOrWhiteSpace(periodLabel) || periodLabel == "همه زمان‌ها";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9).DirectionFromRightToLeft());
                    page.ContentFromRightToLeft();

                    page.Header().Column(col =>
                    {
                        col.Item().Background(Colors.Amber.Lighten4).Padding(8).AlignRight().Text(
                            $"گزارش مشتریان — بازه: {periodLabel}  |  {searchPart}  |  مرتب‌سازی: {sortLabel} (مبالغ به تومان)"
                        ).Bold().FontSize(12);

                        col.Item().Background(Colors.Green.Lighten4).Padding(8).AlignRight().Text(
                            $"تعداد مشتریان: {customers.Count}  |  سفارش‌های بسته‌شده در بازه: {totalOrders}  |  خرید در بازه {periodLabel}: {totalSpent:N0} تومان"
                        ).Bold().FontSize(11);

                        col.Item().Background(Colors.Blue.Lighten4).Padding(6).AlignRight().Text(
                            isAllPeriod
                                ? "مجموع خرید، میانگین خرید و آخرین خرید از سفارش‌های بسته‌شده محاسبه شده‌اند. مبلغ صفر یعنی سفارش بسته‌شده‌ای ثبت نشده است."
                                : $"مجموع خرید، میانگین خرید و آخرین خرید فقط مربوط به بازه «{periodLabel}» هستند. مبلغ صفر یعنی در این بازه خریدی ثبت نشده، نه اینکه مشتری هرگز خرید نداشته باشد."
                        ).FontSize(8);

                        col.Item().PaddingTop(10).AlignRight().Text("لیست مشتریان — مبالغ به تومان").Bold().FontSize(12);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(32);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.6f);
                        });

                        table.Header(header =>
                        {
                            HeaderCell(header, "ردیف");
                            HeaderCell(header, "نام مشتری");
                            HeaderCell(header, "موبایل");
                            HeaderCell(header, "عضویت");
                            HeaderCell(header, isAllPeriod ? "سفارش" : $"سفارش {periodLabel}");
                            HeaderCell(header, isAllPeriod ? "روز حضور" : $"روز حضور {periodLabel}");
                            HeaderCell(header, isAllPeriod ? "مجموع خرید" : $"خرید {periodLabel}");
                            HeaderCell(header, isAllPeriod ? "میانگین خرید" : $"میانگین {periodLabel}");
                            HeaderCell(header, isAllPeriod ? "آخرین خرید" : $"آخرین خرید {periodLabel}");
                            HeaderCell(header, "توضیحات");
                        });

                        int index = 1;
                        foreach (var c in customers)
                        {
                            BodyCell(table, index.ToString());
                            BodyCell(table, string.IsNullOrWhiteSpace(c.FullName) ? "مشتری مهمان" : c.FullName);
                            BodyCell(table, FormatMobile(c.Mobile));
                            BodyCell(table, c.CreatedAtShamsi ?? "-");
                            BodyCell(table, c.TotalOrders.ToString());
                            BodyCell(table, c.TotalDistinctDays.ToString());
                            BodyCell(table, c.TotalSpent.ToString("N0"));
                            BodyCell(table, Math.Round(c.AverageOrderValue).ToString("N0"));
                            BodyCell(table, string.IsNullOrWhiteSpace(c.LastOrderDateShamsi) ? "-" : c.LastOrderDateShamsi);
                            BodyCell(table, string.IsNullOrWhiteSpace(c.Description) ? "-" : c.Description);
                            index++;
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("صفحه ");
                        text.CurrentPageNumber();
                        text.Span(" از ");
                        text.TotalPages();
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static void HeaderCell(TableCellDescriptor header, string text)
        {
            header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4)
                .AlignCenter().AlignMiddle().Text(text).Bold().FontSize(8);
        }

        private static void BodyCell(TableDescriptor table, string text)
        {
            table.Cell().Border(0.5f).Padding(3).AlignCenter().AlignMiddle()
                .Text(text ?? "-").FontSize(8);
        }

        private static string FormatMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile) || mobile.StartsWith("991"))
                return "-";
            return mobile;
        }

        private static void EnsureFontRegistered(string? webRootPath)
        {
            lock (FontLock)
            {
                if (_fontRegistered) return;

                var candidates = new List<string>();
                if (!string.IsNullOrWhiteSpace(webRootPath))
                    candidates.Add(Path.Combine(webRootPath, "fonts", "Vazirmatn-Regular.ttf"));

                candidates.Add(Path.Combine(AppContext.BaseDirectory, "wwwroot", "fonts", "Vazirmatn-Regular.ttf"));
                candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "Vazirmatn-Regular.ttf"));

                foreach (var path in candidates)
                {
                    if (!File.Exists(path)) continue;
                    using var stream = File.OpenRead(path);
                    FontManager.RegisterFontWithCustomName(FontFamily, stream);
                    _fontRegistered = true;
                    return;
                }

                _fontRegistered = true;
            }
        }
    }
}
