using ClosedXML.Excel;
using resturanyar.Models.ViewModels;

namespace resturanyar.Services
{
    public static class CustomersExcelExportService
    {
        public static byte[] BuildWorkbook(
            IList<CustomerStatsViewModel> customers,
            string periodLabel,
            string sortLabel,
            string? search)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("لیست مشتریان");
            ws.RightToLeft = true;

            var totalSpent = customers.Sum(c => c.TotalSpent);
            var totalOrders = customers.Sum(c => c.TotalOrders);
            var searchPart = string.IsNullOrWhiteSpace(search) ? "بدون جستجو" : $"جستجو: {search}";
            var isAllPeriod = string.IsNullOrWhiteSpace(periodLabel) || periodLabel == "همه زمان‌ها";

            ws.Range(1, 1, 1, 10).Merge();
            ws.Cell(1, 1).Value = $"گزارش مشتریان — بازه: {periodLabel}  |  {searchPart}  |  مرتب‌سازی: {sortLabel} (مبالغ به تومان)";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3CD");
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(1).Height = 28;

            ws.Range(2, 1, 2, 10).Merge();
            ws.Cell(2, 1).Value =
                $"تعداد مشتریان: {customers.Count}  |  سفارش‌های بسته‌شده در بازه: {totalOrders}  |  خرید در بازه {periodLabel}: {totalSpent:N0} تومان";
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 12;
            ws.Cell(2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D1E7DD");
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(2, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(2).Height = 24;

            ws.Range(3, 1, 3, 10).Merge();
            ws.Cell(3, 1).Value = isAllPeriod
                ? "مجموع خرید، میانگین خرید و آخرین خرید از سفارش‌های بسته‌شده محاسبه شده‌اند. مبلغ صفر یعنی سفارش بسته‌شده‌ای ثبت نشده است."
                : $"مجموع خرید، میانگین خرید و آخرین خرید فقط مربوط به بازه «{periodLabel}» هستند. مبلغ صفر یعنی در این بازه خریدی ثبت نشده، نه اینکه مشتری هرگز خرید نداشته باشد.";
            ws.Cell(3, 1).Style.Font.FontSize = 10;
            ws.Cell(3, 1).Style.Font.Italic = true;
            ws.Cell(3, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(3, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(3).Height = 22;

            const int headerRow = 4;
            string[] headers =
            {
                "ردیف",
                "نام مشتری",
                "موبایل",
                "تاریخ عضویت",
                isAllPeriod ? "تعداد سفارش" : $"سفارش در {periodLabel}",
                isAllPeriod ? "روزهای حضور" : $"روز حضور در {periodLabel}",
                isAllPeriod ? "مجموع خرید (تومان)" : $"خرید در بازه {periodLabel} (تومان)",
                isAllPeriod ? "میانگین خرید (تومان)" : $"میانگین در {periodLabel} (تومان)",
                isAllPeriod ? "آخرین خرید" : $"آخرین خرید در {periodLabel}",
                "توضیحات"
            };

            for (int i = 0; i < headers.Length; i++)
                ws.Cell(headerRow, i + 1).Value = headers[i];

            var headerRange = ws.Range(headerRow, 1, headerRow, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = headerRow + 1;
            int index = 1;
            foreach (var c in customers)
            {
                ws.Cell(row, 1).Value = index;
                ws.Cell(row, 2).Value = string.IsNullOrWhiteSpace(c.FullName) ? "مشتری مهمان" : c.FullName;
                ws.Cell(row, 3).Value = FormatMobile(c.Mobile);
                ws.Cell(row, 4).Value = c.CreatedAtShamsi ?? "-";
                ws.Cell(row, 5).Value = c.TotalOrders;
                ws.Cell(row, 6).Value = c.TotalDistinctDays;
                ws.Cell(row, 7).Value = c.TotalSpent;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 8).Value = Math.Round(c.AverageOrderValue);
                ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 9).Value = string.IsNullOrWhiteSpace(c.LastOrderDateShamsi) ? "-" : c.LastOrderDateShamsi;
                ws.Cell(row, 10).Value = string.IsNullOrWhiteSpace(c.Description) ? "-" : c.Description;
                row++;
                index++;
            }

            ws.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Columns().AdjustToContents();
            ws.Column(10).Width = Math.Min(ws.Column(10).Width, 40);
            ws.Column(10).Style.Alignment.WrapText = true;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static string FormatMobile(string? mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile) || mobile.StartsWith("991"))
                return "-";
            return mobile;
        }
    }
}
