using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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

            decimal salesTotal = orders.Sum(o => GetInvoiceAmount(o, snapshotMap));

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
                            $"مجموع فروش از تاریخ {fromDateLabel} تا تاریخ {toDateLabel} (مبالغ به تومان)"
                        ).Bold().FontSize(13);

                        col.Item().Background(Colors.Green.Lighten4).Padding(8).AlignRight().Text(
                            $"مبلغ کل فروش: {salesTotal:N0} تومان  |  تعداد سفارش‌ها: {orders.Count}"
                        ).Bold().FontSize(11);

                        col.Item().PaddingTop(10).AlignRight().Text("خلاصه سفارش‌ها — مبالغ به تومان").Bold().FontSize(12);
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(0.7f); // id
                                columns.RelativeColumn(1.1f); // date
                                columns.RelativeColumn(0.7f); // table
                                columns.RelativeColumn(1.2f); // status
                                columns.RelativeColumn(1.3f); // customer
                                columns.RelativeColumn(1.1f); // mobile
                                columns.RelativeColumn(1.1f); // items
                                columns.RelativeColumn(1.0f); // fees
                                columns.RelativeColumn(1.0f); // tax
                                columns.RelativeColumn(1.2f); // invoice
                                columns.RelativeColumn(1.1f); // issued
                            });

                            table.Header(header =>
                            {
                                HeaderCell(header, "شناسه");
                                HeaderCell(header, "تاریخ");
                                HeaderCell(header, "میز");
                                HeaderCell(header, "وضعیت");
                                HeaderCell(header, "مشتری");
                                HeaderCell(header, "موبایل");
                                HeaderCell(header, "جمع اقلام (تومان)");
                                HeaderCell(header, "هزینه‌ها (تومان)");
                                HeaderCell(header, "مالیات (تومان)");
                                HeaderCell(header, "مبلغ فاکتور (تومان)");
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
                                BodyCell(table, GetStatusName(o.StatusId));
                                BodyCell(table, o.Customer?.FullName ?? "مشتری مهمان");
                                BodyCell(table, mobile);

                                if (snapshot != null)
                                {
                                    BodyCell(table, snapshot.ItemsSubtotal.ToString("N0"));
                                    BodyCell(table, fees.ToString("N0"));
                                    BodyCell(table, tax.ToString("N0"));
                                    BodyCell(table, snapshot.GrandTotal.ToString("N0"));
                                    BodyCell(table, DateHelper.ToShamsi(snapshot.IssuedAt));
                                }
                                else
                                {
                                    BodyCell(table, itemsTotal.ToString("N0"));
                                    BodyCell(table, "-");
                                    BodyCell(table, "-");
                                    BodyCell(table, itemsTotal.ToString("N0"));
                                    BodyCell(table, "-");
                                }
                            }
                        });

                        col.Item().PaddingTop(18).AlignRight().Text("جزئیات سفارش‌ها — مبالغ به تومان").Bold().FontSize(12);

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
            header.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4)
                .AlignCenter().AlignMiddle().Text(text).Bold().FontSize(8);
        }

        private static void BodyCell(TableDescriptor table, string text)
        {
            table.Cell().Border(0.5f).Padding(3).AlignCenter().AlignMiddle()
                .Text(text ?? "-").FontSize(8);
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
