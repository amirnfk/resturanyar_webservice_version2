using ClosedXML.Excel;
using resturanyar.Models;
using resturanyar.Models.Receipt;
using resturanyar.Utility;
using System.Text.Json;

namespace resturanyar.Services
{
    /// <summary>
    /// Shared Excel workbook for web Manager Reports and Android order-history export.
    /// </summary>
    public static class OrdersExcelExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        public static byte[] BuildWorkbook(
            IList<Order> orders,
            IReadOnlyDictionary<int, OrderReceiptSnapshot> snapshotMap,
            string fromDateLabel,
            string toDateLabel)
        {
            using var workbook = new XLWorkbook();

            var wsOrders = workbook.Worksheets.Add("خلاصه سفارش‌ها");

            decimal salesTotal = 0m;
            foreach (var o in orders)
            {
                salesTotal += GetInvoiceAmount(o, snapshotMap);
            }

            // Banner rows 1–2
            wsOrders.Range(1, 1, 1, 18).Merge();
            wsOrders.Cell(1, 1).Value =
                $"مجموع فروش از تاریخ {fromDateLabel} تا تاریخ {toDateLabel} (مبالغ به تومان)";
            wsOrders.Cell(1, 1).Style.Font.Bold = true;
            wsOrders.Cell(1, 1).Style.Font.FontSize = 14;
            wsOrders.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3CD");
            wsOrders.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsOrders.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            wsOrders.Row(1).Height = 28;

            wsOrders.Range(2, 1, 2, 18).Merge();
            wsOrders.Cell(2, 1).Value =
                $"مبلغ کل فروش: {salesTotal:N0} تومان  |  تعداد سفارش‌ها: {orders.Count}";
            wsOrders.Cell(2, 1).Style.Font.Bold = true;
            wsOrders.Cell(2, 1).Style.Font.FontSize = 12;
            wsOrders.Cell(2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D1E7DD");
            wsOrders.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsOrders.Cell(2, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            wsOrders.Row(2).Height = 24;

            const int headerRow = 3;
            wsOrders.Cell(headerRow, 1).Value = "شناسه سفارش";
            wsOrders.Cell(headerRow, 2).Value = "تاریخ ایجاد (شمسی)";
            wsOrders.Cell(headerRow, 3).Value = "شماره میز";
            wsOrders.Cell(headerRow, 4).Value = "وضعیت";
            wsOrders.Cell(headerRow, 5).Value = "نام مشتری";
            wsOrders.Cell(headerRow, 6).Value = "شماره موبایل";
            wsOrders.Cell(headerRow, 7).Value = "توضیحات";
            wsOrders.Cell(headerRow, 8).Value = "تعداد غذا";
            wsOrders.Cell(headerRow, 9).Value = "جمع اقلام (تومان)";
            wsOrders.Cell(headerRow, 10).Value = "جمع هزینه‌ها (تومان)";
            wsOrders.Cell(headerRow, 11).Value = "مالیات (تومان)";
            wsOrders.Cell(headerRow, 12).Value = "تخفیف (تومان)";
            wsOrders.Cell(headerRow, 13).Value = "حق سرویس (تومان)";
            wsOrders.Cell(headerRow, 14).Value = "مالیات ارزش افزوده (تومان)";
            wsOrders.Cell(headerRow, 15).Value = "بسته‌بندی (تومان)";
            wsOrders.Cell(headerRow, 16).Value = "ارسال (تومان)";
            wsOrders.Cell(headerRow, 17).Value = "مبلغ فاکتور (تومان)";
            wsOrders.Cell(headerRow, 18).Value = "تاریخ صدور فاکتور";

            var headerRange = wsOrders.Range(headerRow, 1, headerRow, 18);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = headerRow + 1;
            foreach (var o in orders)
            {
                var itemsTotal = o.OrderItems?.Sum(i => GetFinalPrice(i) * i.Quantity) ?? 0m;
                var foodQty = o.OrderItems?.Sum(i => i.Quantity) ?? 0;
                snapshotMap.TryGetValue(o.OrderId, out var snapshot);
                var chargeLines = ParseChargeLines(snapshot?.ChargeLinesJson);
                var fees = chargeLines.Where(c => c.Category == ChargeCategory.Fee).Sum(c => c.CalculatedAmount);
                var tax = chargeLines.Where(c => c.Category == ChargeCategory.Tax).Sum(c => c.CalculatedAmount);
                var discount = chargeLines.Where(c => c.Category == ChargeCategory.Discount).Sum(c => c.CalculatedAmount);

                wsOrders.Cell(row, 1).Value = o.OrderId;
                wsOrders.Cell(row, 2).Value = o.CreatedAtShamsi ?? DateHelper.ToShamsi(o.CreatedAt);
                wsOrders.Cell(row, 3).Value = o.TableNumber;
                wsOrders.Cell(row, 4).Value = GetStatusName(o.StatusId);

                wsOrders.Cell(row, 5).Value = o.Customer?.FullName ?? "مشتری مهمان";
                string mobile = o.Customer?.Mobile ?? "-";
                if (!string.IsNullOrEmpty(mobile) && mobile.StartsWith("991"))
                    mobile = "-";
                wsOrders.Cell(row, 6).Value = mobile;
                wsOrders.Cell(row, 7).Value = o.Description ?? "-";
                wsOrders.Cell(row, 8).Value = foodQty;

                if (snapshot != null)
                {
                    wsOrders.Cell(row, 9).Value = snapshot.ItemsSubtotal;
                    wsOrders.Cell(row, 10).Value = fees;
                    wsOrders.Cell(row, 11).Value = tax;
                    wsOrders.Cell(row, 12).Value = discount;
                    SetChargeCodeCell(wsOrders.Cell(row, 13), chargeLines, "service");
                    SetChargeCodeCell(wsOrders.Cell(row, 14), chargeLines, "vat");
                    SetChargeCodeCell(wsOrders.Cell(row, 15), chargeLines, "packaging");
                    SetChargeCodeCell(wsOrders.Cell(row, 16), chargeLines, "delivery");
                    wsOrders.Cell(row, 17).Value = snapshot.GrandTotal;
                    wsOrders.Cell(row, 18).Value = DateHelper.ToShamsi(snapshot.IssuedAt);
                }
                else
                {
                    wsOrders.Cell(row, 9).Value = itemsTotal;
                    wsOrders.Cell(row, 10).Value = "-";
                    wsOrders.Cell(row, 11).Value = "-";
                    wsOrders.Cell(row, 12).Value = "-";
                    wsOrders.Cell(row, 13).Value = "-";
                    wsOrders.Cell(row, 14).Value = "-";
                    wsOrders.Cell(row, 15).Value = "-";
                    wsOrders.Cell(row, 16).Value = "-";
                    wsOrders.Cell(row, 17).Value = itemsTotal;
                    wsOrders.Cell(row, 18).Value = "-";
                }

                row++;
            }

            wsOrders.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsOrders.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            wsOrders.Columns().AdjustToContents();

            // Sheet 2: item details
            var wsItems = workbook.Worksheets.Add("جزئیات سفارش‌ها");
            wsItems.Cell(1, 1).Value = "شناسه سفارش";
            wsItems.Cell(1, 2).Value = "شناسه آیتم";
            wsItems.Cell(1, 3).Value = "نام غذا";
            wsItems.Cell(1, 4).Value = "تعداد";
            wsItems.Cell(1, 5).Value = "قیمت واحد (تومان)";
            wsItems.Cell(1, 6).Value = "قیمت نهایی واحد (تومان)";
            wsItems.Cell(1, 7).Value = "مبلغ کل (تومان)";

            int itemRow = 2;
            foreach (var o in orders)
            {
                if (o.OrderItems == null) continue;
                foreach (var i in o.OrderItems)
                {
                    var total = GetFinalPrice(i) * i.Quantity;
                    wsItems.Cell(itemRow, 1).Value = o.OrderId;
                    wsItems.Cell(itemRow, 2).Value = i.OrderItemId;
                    wsItems.Cell(itemRow, 3).Value = i.FoodName ?? "-";
                    wsItems.Cell(itemRow, 4).Value = i.Quantity;
                    wsItems.Cell(itemRow, 5).Value = i.UnitPrice;
                    wsItems.Cell(itemRow, 6).Value = GetFinalPrice(i);
                    wsItems.Cell(itemRow, 7).Value = total;
                    itemRow++;
                }
            }

            var headerRange2 = wsItems.Range("A1:G1");
            headerRange2.Style.Font.Bold = true;
            headerRange2.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange2.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange2.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            wsItems.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            wsItems.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            wsItems.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private static decimal GetInvoiceAmount(
            Order order,
            IReadOnlyDictionary<int, OrderReceiptSnapshot> snapshotMap)
        {
            if (snapshotMap.TryGetValue(order.OrderId, out var snapshot))
                return snapshot.GrandTotal;

            return order.OrderItems?.Sum(i => GetFinalPrice(i) * i.Quantity) ?? 0m;
        }

        private static decimal GetFinalPrice(OrderItem item)
        {
            return (item.UnitPriceWithDiscount.HasValue && item.UnitPriceWithDiscount.Value > 0)
                ? item.UnitPriceWithDiscount.Value
                : item.UnitPrice;
        }

        private static List<ReceiptChargeLineDto> ParseChargeLines(string? chargeLinesJson)
        {
            if (string.IsNullOrWhiteSpace(chargeLinesJson))
                return new List<ReceiptChargeLineDto>();

            try
            {
                return JsonSerializer.Deserialize<List<ReceiptChargeLineDto>>(chargeLinesJson, JsonOptions)
                       ?? new List<ReceiptChargeLineDto>();
            }
            catch
            {
                return new List<ReceiptChargeLineDto>();
            }
        }

        private static void SetChargeCodeCell(
            IXLCell cell,
            IEnumerable<ReceiptChargeLineDto> lines,
            string code)
        {
            var amount = lines
                .Where(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                .Sum(c => c.CalculatedAmount);
            if (amount == 0)
                cell.Value = "-";
            else
                cell.Value = amount;
        }

        private static string GetStatusName(int statusId)
        {
            return statusId switch
            {
                1 => "در انتظار ثبت نهایی",
                2 => "در انتظار تایید",
                3 => "تایید شده",
                4 => "در حال آماده‌سازی",
                5 => "آماده تحویل",
                6 => "تحویل داده شده",
                7 => "در انتظار پرداخت",
                8 => "پرداخت شده",
                9 => "لغو شده توسط مشتری",
                10 => "لغو شده توسط رستوران",
                11 => "بسته شده",
                12 => "در انتظار اصلاح سفارش",
                _ => "نامشخص"
            };
        }
    }
}
