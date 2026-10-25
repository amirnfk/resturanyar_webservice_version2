using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using resturanyar.Helpers;
using resturanyar.Models;
using resturanyar.Models.Receipt;
using resturanyar.Utility;
using System.Text.Json;

namespace resturanyar.Services
{
    /// <summary>
    /// Landscape PDF order report for Android and web Manager Reports.
    /// </summary>
    public static class OrdersPdfExportService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private static readonly object FontLock = new();
        private static bool _fontRegistered;
        private const string FontFamily = "Vazirmatn";

        public static byte[] BuildPdf(
            IList<Order> orders,
            IReadOnlyDictionary<int, OrderReceiptSnapshot> snapshotMap,
            string fromDateLabel,
            string toDateLabel,
            string? webRootPath = null)
        {
            EnsureFontRegistered(webRootPath);
            QuestPDF.Settings.License = LicenseType.Community;

            decimal salesTotal = 0m;
            decimal paidTotal = 0m;
            decimal debtTotal = 0m;
            foreach (var o in orders)
            {
                var invoiceAmount = GetInvoiceAmount(o, snapshotMap);
                salesTotal += invoiceAmount;
                var settlement = ResolveSettlementAmounts(o, invoiceAmount);
                if (!settlement.Settled) continue;
                paidTotal += settlement.Paid;
                debtTotal += settlement.Debt;
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(8).DirectionFromRightToLeft());
                    page.ContentFromRightToLeft();

                    page.Header().Column(col =>
                    {
                        col.Item().Background(Colors.Amber.Lighten4).Padding(8).AlignRight().Text(
                            $"مجموع فروش از تاریخ {fromDateLabel} تا تاریخ {toDateLabel} (مبالغ به تومان)"
                        ).Bold().FontSize(12);

                        col.Item().Background(Colors.Green.Lighten4).Padding(8).AlignRight().Text(
                            $"مبلغ کل فروش: {salesTotal:N0} تومان  |  مجموع دریافتی: {paidTotal:N0} تومان  |  مجموع بدهی: {debtTotal:N0} تومان  |  تعداد سفارش‌ها: {orders.Count}"
                        ).Bold().FontSize(10);

                        col.Item().PaddingTop(10).AlignRight().Text("خلاصه سفارش‌ها — مبالغ به تومان").Bold().FontSize(11);
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.55f); // id
                                columns.RelativeColumn(0.95f); // date
                                columns.RelativeColumn(0.55f); // table
                                columns.RelativeColumn(0.95f); // status
                                columns.RelativeColumn(1.05f); // customer
                                columns.RelativeColumn(0.9f);  // mobile
                                columns.RelativeColumn(0.9f);  // items
                                columns.RelativeColumn(0.8f);  // fees
                                columns.RelativeColumn(0.8f);  // tax
                                columns.RelativeColumn(0.95f); // invoice
                                columns.RelativeColumn(0.9f);  // paid
                                columns.RelativeColumn(0.9f);  // debt
                                columns.RelativeColumn(0.9f);  // issued
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "شناسه");
                                HeaderCell(header, "تاریخ");
                                HeaderCell(header, "میز");
                                HeaderCell(header, "وضعیت");
                                HeaderCell(header, "مشتری");
                                HeaderCell(header, "موبایل");
                                HeaderCell(header, "جمع اقلام");
                                HeaderCell(header, "هزینه‌ها");
                                HeaderCell(header, "مالیات");
                                HeaderCell(header, "مبلغ فاکتور");
                                HeaderCell(header, "دریافتی");
                                HeaderCell(header, "بدهی");
                                HeaderCell(header, "صدور فاکتور");
                            });

                            foreach (var o in orders)
                            {
                                var itemsTotal = o.OrderItems?.Sum(i => GetFinalPrice(i) * i.Quantity) ?? 0m;
                                snapshotMap.TryGetValue(o.OrderId, out var snapshot);
                                var chargeLines = ParseChargeLines(snapshot?.ChargeLinesJson);
                                var fees = chargeLines.Where(c => c.Category == ChargeCategory.Fee).Sum(c => c.CalculatedAmount);
                                var tax = chargeLines.Where(c => c.Category == ChargeCategory.Tax).Sum(c => c.CalculatedAmount);

                                string mobile = o.Customer?.Mobile ?? "-";
                                if (!string.IsNullOrEmpty(mobile) && mobile.StartsWith("991"))
                                    mobile = "-";

                                BodyCell(table, o.OrderId.ToString());
                                BodyCell(table, o.CreatedAtShamsi ?? DateHelper.ToShamsi(o.CreatedAt));
                                BodyCell(table, o.TableNumber ?? "-");
                                BodyCell(table, GetStatusName(o));
                                BodyCell(table, o.Customer?.FullName ?? "مشتری مهمان");
                                BodyCell(table, mobile);

                                decimal invoiceAmount;
                                if (snapshot != null)
                                {
                                    BodyCell(table, snapshot.ItemsSubtotal.ToString("N0"));
                                    BodyCell(table, fees.ToString("N0"));
                                    BodyCell(table, tax.ToString("N0"));
                                    invoiceAmount = snapshot.GrandTotal;
                                }
                                else
                                {
                                    BodyCell(table, itemsTotal.ToString("N0"));
                                    BodyCell(table, "-");
                                    BodyCell(table, "-");
                                    invoiceAmount = itemsTotal;
                                }

                                // Canceled: keep item breakdown for audit, but sales amount is 0.
                                var salesAmount = o.StatusId is 9 or 10 ? 0m : invoiceAmount;
                                BodyCell(table, salesAmount.ToString("N0"));

                                var settlement = ResolveSettlementAmounts(o, invoiceAmount);
                                BodyCell(table, FormatSettlementAmount(
                                    settlement.Settled ? settlement.Paid : null, settlement.Settled));
                                BodyCell(table, FormatSettlementAmount(
                                    settlement.Settled ? settlement.Debt : null, settlement.Settled));
                                BodyCell(table, snapshot != null ? DateHelper.ToShamsi(snapshot.IssuedAt) : "-");
                            }
                        });

                        col.Item().PaddingTop(18).AlignRight().Text("جزئیات سفارش‌ها — مبالغ به تومان").Bold().FontSize(11);

                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1f);
                                columns.RelativeColumn(3f);
                                columns.RelativeColumn(1f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "شناسه سفارش");
                                HeaderCell(header, "نام غذا");
                                HeaderCell(header, "تعداد");
                                HeaderCell(header, "قیمت واحد (تومان)");
                                HeaderCell(header, "مبلغ کل (تومان)");
                            });

                            foreach (var o in orders)
                            {
                                if (o.OrderItems == null) continue;
                                foreach (var i in o.OrderItems)
                                {
                                    var unit = GetFinalPrice(i);
                                    BodyCell(table, o.OrderId.ToString());
                                    BodyCell(table, i.FoodName ?? "-");
                                    BodyCell(table, i.Quantity.ToString());
                                    BodyCell(table, unit.ToString("N0"));
                                    BodyCell(table, (unit * i.Quantity).ToString("N0"));
                                }
                            }
                        });
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
            header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(3)
                .AlignCenter().AlignMiddle().Text(text).Bold().FontSize(7);
        }

        private static void BodyCell(TableDescriptor table, string text)
        {
            table.Cell().Border(0.5f).Padding(2).AlignCenter().AlignMiddle()
                .Text(text ?? "-").FontSize(7);
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

                // Fall back to default font if TTF is missing (Latin digits still work).
                _fontRegistered = true;
            }
        }

        private static bool HasExplicitSettlement(Order order) =>
            order.SettlementPaidAmount.HasValue
            || order.SettlementDebtAmount.HasValue
            || !string.IsNullOrWhiteSpace(order.SettlementType);

        /// <summary>
        /// Explicit settlement wins. Closed (11) or paid (8) without settlement fields
        /// are treated as fully collected cash (instant close / legacy). Canceled stays blank.
        /// </summary>
        private static (bool Settled, decimal Paid, decimal Debt) ResolveSettlementAmounts(
            Order order,
            decimal invoiceAmount)
        {
            if (order.StatusId is 9 or 10)
                return (false, 0m, 0m);

            if (HasExplicitSettlement(order))
            {
                return (
                    true,
                    order.SettlementPaidAmount ?? 0m,
                    order.SettlementDebtAmount ?? 0m);
            }

            if (order.StatusId is 8 or 11)
                return (true, invoiceAmount, 0m);

            return (false, 0m, 0m);
        }

        private static string FormatSettlementAmount(decimal? amount, bool settled) =>
            settled ? (amount ?? 0m).ToString("N0") : "-";

        private static decimal GetInvoiceAmount(
            Order order,
            IReadOnlyDictionary<int, OrderReceiptSnapshot> snapshotMap)
        {
            // Canceled orders do not count toward sales totals.
            if (order.StatusId is 9 or 10)
                return 0m;

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

        private static string GetStatusName(Order order)
        {
            if (order.StatusId == 8)
            {
                return OrderStatusDisplay.BadgeLabel(
                    order.StatusId,
                    order.SettlementDebtAmount,
                    order.SettlementType,
                    "پرداخت‌شده");
            }

            return order.StatusId switch
            {
                1 => "در انتظار ثبت نهایی",
                2 => "در انتظار تایید",
                3 => "تایید شده",
                4 => "در حال آماده‌سازی",
                5 => "آماده تحویل",
                6 => "تحویل داده شده",
                7 => "در انتظار پرداخت",
                9 => "لغو شده توسط مشتری",
                10 => "لغو شده توسط رستوران",
                11 => "بسته شده",
                12 => "در انتظار اصلاح سفارش",
                _ => "نامشخص"
            };
        }
    }
}
