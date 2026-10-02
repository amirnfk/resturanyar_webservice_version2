using resturanyar.Models.Receipt;
using resturanyar.Utility;

namespace resturanyar.Services.Receipt
{
    public interface IReceiptRenderer
    {
        string RenderHtml(ReceiptDto receipt);
        string RenderHtml(ReceiptDto receipt, bool includeToolbarAndAutoPrint);
        /// <param name="offlineCapture">
        /// Self-contained HTML for Android WebView→bitmap (no external font/network URLs).
        /// </param>
        string RenderHtml(ReceiptDto receipt, bool includeToolbarAndAutoPrint, bool offlineCapture, string? templateRevision = null);
    }

    public class HtmlReceiptRenderer : IReceiptRenderer
    {
        public string RenderHtml(ReceiptDto receipt) =>
            RenderHtml(receipt, includeToolbarAndAutoPrint: true, offlineCapture: false);

        public string RenderHtml(ReceiptDto receipt, bool includeToolbarAndAutoPrint) =>
            RenderHtml(receipt, includeToolbarAndAutoPrint, offlineCapture: false);

        public string RenderHtml(
            ReceiptDto receipt,
            bool includeToolbarAndAutoPrint,
            bool offlineCapture,
            string? templateRevision = null)
        {
            var templateId = InvoicePrintTemplates.Normalize(receipt.PrintTemplateId);
            var css = BuildCss(templateId, offlineCapture);
            var sheet = templateId switch
            {
                InvoicePrintTemplates.ThermalId => BuildThermalSheet(receipt),
                InvoicePrintTemplates.FormalId => BuildFormalSheet(receipt),
                InvoicePrintTemplates.ElegantId => BuildElegantSheet(receipt),
                InvoicePrintTemplates.ReadableId => BuildModernSheet(receipt, large: true),
                _ => BuildModernSheet(receipt, large: false)
            };

            var rev = string.IsNullOrWhiteSpace(templateRevision)
                ? DateTime.UtcNow.Ticks.ToString()
                : templateRevision.Trim();

            // Fixed desktop viewport for Android capture so max-width:640px mobile CSS
            // does not collapse classic / readable / elegant to a phone layout.
            var viewport = offlineCapture
                ? "width=800"
                : "width=device-width, initial-scale=1";

            return $"""
                <!DOCTYPE html>
                <html lang="fa" dir="rtl" data-ry-offline="{(offlineCapture ? "1" : "0")}">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="{viewport}">
                    <meta name="ry-print-template" content="{templateId}">
                    <meta name="ry-print-rev" content="{Escape(rev)}">
                    <title>فاکتور سفارش {ToFa(receipt.OrderNumber)}</title>
                    <style>{css}</style>
                </head>
                <body class="tpl-{templateId}" data-print-template="{templateId}" data-print-rev="{Escape(rev)}">
                    {(includeToolbarAndAutoPrint ? """
                    <div class="toolbar">
                        <button type="button" class="btn-close-win" onclick="window.close()">بستن</button>
                        <button type="button" class="btn-print" onclick="window.print()">چاپ فاکتور</button>
                    </div>
                    """ : "")}
                    {sheet}
                    {(includeToolbarAndAutoPrint ? PrintAutoScript : "")}
                </body>
                </html>
                """;
        }

        // ---------- Modern (classic / readable) ----------

        private static string BuildModernSheet(ReceiptDto receipt, bool large)
        {
            var strike = large ? "13px" : "11px";
            var itemRows = BuildItemRows(receipt, strike);
            var chargesAndTotals = BuildModernChargesAndTotals(receipt);
            var customerBlock = BuildCustomerChips(receipt);
            var descriptionBlock = BuildDescription(receipt);
            var issuedBadge = receipt.IsIssued ? """<div class="issued-badge">صادر شده</div>""" : "";
            var issuedAtText = receipt.IssuedAt.HasValue
                ? $"<div class=\"info-item\"><span class=\"label\">زمان صدور</span><span class=\"value\">{Escape(receipt.IssuedAt.Value.ToPersianDateTimeTehran())}</span></div>"
                : "";
            var tableInfoItem = receipt.OrderType == OrderTypeKind.DineIn
                ? $"<div class=\"info-item\"><span class=\"label\">میز</span><span class=\"value\">{(string.IsNullOrWhiteSpace(receipt.TableNumber) ? "—" : ToFa(receipt.TableNumber))}</span></div>"
                : "";

            return $"""
                <div class="sheet">
                    <div class="brand-bar"></div>
                    <div class="header">
                        <div>
                            <div class="restaurant-name">{Escape(receipt.RestaurantName)}</div>
                            <div class="doc-label">فاکتور فروش</div>
                        </div>
                        <div class="order-meta">
                            <div class="order-number">
                                <span>شماره سفارش</span>
                                {ToFa(receipt.OrderNumber)}
                            </div>
                            {issuedBadge}
                        </div>
                    </div>
                    <div class="body">
                        <div class="info-grid">
                            {tableInfoItem}
                            <div class="info-item"><span class="label">نوع سفارش</span><span class="value">{Escape(receipt.OrderTypeLabel)}</span></div>
                            {(string.IsNullOrWhiteSpace(receipt.DeliveryAddress) ? "" : $"<div class=\"info-item\"><span class=\"label\">آدرس</span><span class=\"value\">{Escape(receipt.DeliveryAddress)}</span></div>")}
                            <div class="info-item"><span class="label">وضعیت</span><span class="value">{Escape(receipt.OrderStatus)}</span></div>
                            <div class="info-item"><span class="label">تاریخ ثبت</span><span class="value">{ToFa(receipt.CreatedAt)}</span></div>
                            {(string.IsNullOrWhiteSpace(receipt.UpdatedAt) ? "" : $"<div class=\"info-item\"><span class=\"label\">آخرین تغییر</span><span class=\"value\">{ToFa(receipt.UpdatedAt)}</span></div>")}
                            {issuedAtText}
                        </div>
                        <div class="section-title">اقلام سفارش</div>
                        <table class="items-table">
                            <thead>
                                <tr>
                                    <th>نام غذا</th>
                                    <th>تعداد</th>
                                    <th>قیمت واحد</th>
                                    <th>جمع</th>
                                </tr>
                            </thead>
                            <tbody>
                                {(itemRows.Count > 0 ? string.Join("", itemRows) : "<tr><td colspan=\"4\">آیتمی ثبت نشده است</td></tr>")}
                            </tbody>
                        </table>
                        {chargesAndTotals}
                        {customerBlock}
                        {descriptionBlock}
                        <div class="footer">
                            <div class="thanks">از اعتماد شما سپاسگزاریم</div>
                            <div>سیستم مدیریت رستورانیار</div>
                        </div>
                    </div>
                </div>
                """;
        }

        private static string BuildModernChargesAndTotals(ReceiptDto receipt)
        {
            var chargeRows = BuildChargeRows(receipt);
            var hasChargeDetails = chargeRows.Count > 0
                || receipt.DiscountTotal > 0
                || receipt.FeesTotal > 0
                || receipt.TaxTotal > 0;

            var chargesCard = hasChargeDetails
                ? $"""
                    <div class="charges-card">
                        <div class="card-head">جزئیات هزینه‌ها</div>
                        <table class="charges-table">
                            <tbody>
                                {(chargeRows.Count > 0
                                    ? string.Join("", chargeRows)
                                    : $"""
                                        <tr><td class="title">جمع اقلام</td><td class="amount" dir="ltr">{FormatMoney(receipt.ItemsSubtotal)} تومان</td></tr>
                                        {(receipt.DiscountTotal > 0 ? $"<tr><td class=\"title\">تخفیف</td><td class=\"amount is-discount\" dir=\"ltr\">- {FormatMoney(receipt.DiscountTotal)} تومان</td></tr>" : "")}
                                        {(receipt.FeesTotal > 0 ? $"<tr><td class=\"title\">کارمزدها</td><td class=\"amount\" dir=\"ltr\">{FormatMoney(receipt.FeesTotal)} تومان</td></tr>" : "")}
                                        {(receipt.TaxTotal > 0 ? $"<tr><td class=\"title\">مالیات</td><td class=\"amount\" dir=\"ltr\">{FormatMoney(receipt.TaxTotal)} تومان</td></tr>" : "")}
                                      """)}
                            </tbody>
                        </table>
                    </div>
                    """
                : """
                    <div class="charges-card">
                        <div class="card-head">جزئیات هزینه‌ها</div>
                        <table class="charges-table">
                            <tbody>
                                <tr><td class="title">بدون هزینهٔ اضافه</td><td class="amount">—</td></tr>
                            </tbody>
                        </table>
                    </div>
                    """;

            return $"""
                <div class="charges-wrap">
                    {chargesCard}
                    <div class="totals-card">
                        <div class="card-head">خلاصه مبلغ</div>
                        <div class="rows">
                            <div class="summary-row"><span>جمع اقلام</span><strong class="is-amount" dir="ltr">{FormatMoney(receipt.ItemsSubtotal)} تومان</strong></div>
                            {(receipt.DiscountTotal > 0 ? $"<div class=\"summary-row\"><span>تخفیف</span><strong class=\"is-amount is-discount\" dir=\"ltr\">- {FormatMoney(receipt.DiscountTotal)} تومان</strong></div>" : "")}
                            {(receipt.FeesTotal > 0 ? $"<div class=\"summary-row\"><span>کارمزدها</span><strong class=\"is-amount\" dir=\"ltr\">{FormatMoney(receipt.FeesTotal)} تومان</strong></div>" : "")}
                            {(receipt.TaxTotal > 0 ? $"<div class=\"summary-row\"><span>مالیات</span><strong class=\"is-amount\" dir=\"ltr\">{FormatMoney(receipt.TaxTotal)} تومان</strong></div>" : "")}
                        </div>
                        <div class="grand-total">
                            <span>جمع کل</span>
                            <span>{FormatMoney(receipt.GrandTotal)} تومان</span>
                        </div>
                    </div>
                </div>
                """;
        }

        // ---------- Thermal slip (matches attached photo) ----------

        private static string BuildThermalSheet(ReceiptDto receipt)
        {
            var dateText = !string.IsNullOrWhiteSpace(receipt.CreatedAt)
                ? ToFa(receipt.CreatedAt)
                : (receipt.IssuedAt.HasValue ? Escape(receipt.IssuedAt.Value.ToPersianDateTimeTehran()) : "—");

            var customerName = string.IsNullOrWhiteSpace(receipt.CustomerName) ? "مهمان" : receipt.CustomerName;
            var itemRows = BuildItemRows(receipt, "10px", includeToman: false);
            var extraRows = BuildThermalExtraRows(receipt);
            var words = PersianMoneyWords.ToTomanWords(receipt.GrandTotal).ToPersianDigits();

            return $"""
                <div class="sheet thermal-sheet">
                    <div class="thermal-brand">{Escape(receipt.RestaurantName)}</div>
                    <div class="thermal-meta">
                        <span class="thermal-order">شماره {ToFa(receipt.OrderNumber)}</span>
                        <span class="thermal-date">{dateText}</span>
                    </div>
                    <div class="thermal-customer">مشتری : {Escape(customerName)}</div>
                    {(string.IsNullOrWhiteSpace(receipt.OrderTypeLabel) ? "" : $"<div class=\"thermal-submeta\">{Escape(receipt.OrderTypeLabel)}{(receipt.OrderType == OrderTypeKind.DineIn && !string.IsNullOrWhiteSpace(receipt.TableNumber) ? " · میز " + ToFa(receipt.TableNumber) : "")}</div>")}
                    <table class="thermal-table">
                        <thead>
                            <tr>
                                <th>شرح کالا و نام</th>
                                <th>تعداد/مقدار</th>
                                <th>قیمت واحد</th>
                                <th>جمع کل</th>
                            </tr>
                        </thead>
                        <tbody>
                            {(itemRows.Count > 0 ? string.Join("", itemRows) : "<tr><td colspan=\"4\">—</td></tr>")}
                            {string.Join("", extraRows)}
                        </tbody>
                    </table>
                    <div class="thermal-total-line">
                        <span>جمع کل [تومان]</span>
                        <strong dir="ltr">{FormatMoney(receipt.GrandTotal)} تومان</strong>
                    </div>
                    <div class="thermal-total-words">{Escape(words)}</div>
                    {(string.IsNullOrWhiteSpace(receipt.Description) ? "" : $"<div class=\"thermal-note\">{Escape(receipt.Description)}</div>")}
                    <div class="thermal-seal" aria-hidden="true"></div>
                </div>
                """;
        }

        private static List<string> BuildThermalExtraRows(ReceiptDto receipt)
        {
            var rows = new List<string>();
            foreach (var c in receipt.ChargeLines.Where(c => c.CalculatedAmount != 0).OrderBy(c => c.DisplayOrder))
            {
                var amount = c.Category == ChargeCategory.Discount
                    ? $"- {FormatMoney(Math.Abs(c.CalculatedAmount))}"
                    : FormatMoney(c.CalculatedAmount);
                rows.Add($"""
                    <tr class="thermal-extra">
                        <td class="name" colspan="3">{Escape(c.Title)}</td>
                        <td dir="ltr">{amount}</td>
                    </tr>
                    """);
            }
            return rows;
        }

        // ---------- Formal ----------

        private static string BuildFormalSheet(ReceiptDto receipt)
        {
            var itemRows = BuildItemRows(receipt, "11px");
            var chargeSummary = BuildInlineChargeSummary(receipt);
            var customerLine = string.IsNullOrWhiteSpace(receipt.CustomerName) && string.IsNullOrWhiteSpace(receipt.CustomerMobile)
                ? ""
                : $"""
                    <div class="formal-party">
                        <span>خریدار: <strong>{Escape(string.IsNullOrWhiteSpace(receipt.CustomerName) ? "—" : receipt.CustomerName)}</strong></span>
                        {(string.IsNullOrWhiteSpace(receipt.CustomerMobile) ? "" : $"<span>تلفن: {ToFa(receipt.CustomerMobile)}</span>")}
                    </div>
                    """;

            return $"""
                <div class="sheet formal-sheet">
                    <div class="formal-top">
                        <div class="formal-title-block">
                            <div class="formal-kicker">فاکتور فروش</div>
                            <div class="formal-restaurant">{Escape(receipt.RestaurantName)}</div>
                        </div>
                        <div class="formal-box">
                            <div><span>شماره</span><strong>{ToFa(receipt.OrderNumber)}</strong></div>
                            <div><span>تاریخ</span><strong>{ToFa(receipt.CreatedAt)}</strong></div>
                            <div><span>نوع</span><strong>{Escape(receipt.OrderTypeLabel)}</strong></div>
                        </div>
                    </div>
                    {customerLine}
                    {(string.IsNullOrWhiteSpace(receipt.DeliveryAddress) ? "" : $"<div class=\"formal-address\">آدرس: {Escape(receipt.DeliveryAddress)}</div>")}
                    <table class="formal-table">
                        <thead>
                            <tr>
                                <th>ردیف</th>
                                <th>شرح کالا</th>
                                <th>تعداد</th>
                                <th>فی</th>
                                <th>مبلغ</th>
                            </tr>
                        </thead>
                        <tbody>
                            {BuildFormalItemRows(receipt)}
                        </tbody>
                    </table>
                    <div class="formal-summary">
                        <div class="formal-summary-rows">
                            <div><span>جمع اقلام</span><strong dir="ltr">{FormatMoney(receipt.ItemsSubtotal)} تومان</strong></div>
                            {chargeSummary}
                        </div>
                        <div class="formal-grand">
                            <span>مبلغ قابل پرداخت</span>
                            <strong dir="ltr">{FormatMoney(receipt.GrandTotal)} تومان</strong>
                        </div>
                        <div class="formal-words">{Escape(PersianMoneyWords.ToTomanWords(receipt.GrandTotal).ToPersianDigits())}</div>
                    </div>
                    <div class="formal-footer">
                        <span>با تشکر از انتخاب شما</span>
                        <span>رستورانیار</span>
                    </div>
                </div>
                """;
        }

        private static string BuildFormalItemRows(ReceiptDto receipt)
        {
            if (receipt.Items.Count == 0)
                return "<tr><td colspan=\"5\">آیتمی ثبت نشده است</td></tr>";

            return string.Join("", receipt.Items.Select((item, i) => $"""
                <tr>
                    <td>{ToFa(i + 1)}</td>
                    <td class="name">{Escape(item.Name)}</td>
                    <td>{ToFa(item.Quantity)}</td>
                    <td dir="ltr">{FormatMoney(item.UnitPrice)}</td>
                    <td dir="ltr">{FormatMoney(item.LineTotal)}</td>
                </tr>
                """));
        }

        // ---------- Elegant ----------

        private static string BuildElegantSheet(ReceiptDto receipt)
        {
            var itemRows = BuildItemRows(receipt, "11px");
            var chargeSummary = BuildInlineChargeSummary(receipt);

            return $"""
                <div class="sheet elegant-sheet">
                    <div class="elegant-hero">
                        <div class="elegant-hero__right">
                            <div class="elegant-kicker">INVOICE</div>
                            <div class="elegant-restaurant">{Escape(receipt.RestaurantName)}</div>
                            <div class="elegant-doc">فاکتور فروش</div>
                        </div>
                        <div class="elegant-hero__left">
                            <div class="elegant-no">#{ToFa(receipt.OrderNumber)}</div>
                            <div class="elegant-date">{ToFa(receipt.CreatedAt)}</div>
                        </div>
                    </div>
                    <div class="elegant-meta">
                        <div><span>نوع سفارش</span><strong>{Escape(receipt.OrderTypeLabel)}</strong></div>
                        {(receipt.OrderType == OrderTypeKind.DineIn ? $"<div><span>میز</span><strong>{(string.IsNullOrWhiteSpace(receipt.TableNumber) ? "—" : ToFa(receipt.TableNumber))}</strong></div>" : "")}
                        <div><span>وضعیت</span><strong>{Escape(receipt.OrderStatus)}</strong></div>
                        {(string.IsNullOrWhiteSpace(receipt.CustomerName) ? "" : $"<div><span>مشتری</span><strong>{Escape(receipt.CustomerName)}</strong></div>")}
                    </div>
                    <table class="elegant-table">
                        <thead>
                            <tr>
                                <th>نام غذا</th>
                                <th>تعداد</th>
                                <th>قیمت واحد</th>
                                <th>جمع</th>
                            </tr>
                        </thead>
                        <tbody>
                            {(itemRows.Count > 0 ? string.Join("", itemRows) : "<tr><td colspan=\"4\">آیتمی ثبت نشده است</td></tr>")}
                        </tbody>
                    </table>
                    <div class="elegant-bottom">
                        <div class="elegant-notes">
                            {(string.IsNullOrWhiteSpace(receipt.Description) ? "<div class=\"muted\">از اعتماد شما سپاسگزاریم</div>" : $"<div><strong>توضیحات:</strong> {Escape(receipt.Description)}</div>")}
                            {(string.IsNullOrWhiteSpace(receipt.DeliveryAddress) ? "" : $"<div><strong>آدرس:</strong> {Escape(receipt.DeliveryAddress)}</div>")}
                        </div>
                        <div class="elegant-totals">
                            <div class="row"><span>جمع اقلام</span><strong dir="ltr">{FormatMoney(receipt.ItemsSubtotal)}</strong></div>
                            {chargeSummary}
                            <div class="elegant-grand">
                                <span>جمع کل</span>
                                <strong dir="ltr">{FormatMoney(receipt.GrandTotal)} تومان</strong>
                            </div>
                        </div>
                    </div>
                </div>
                """;
        }

        private static string BuildInlineChargeSummary(ReceiptDto receipt)
        {
            var parts = new List<string>();
            if (receipt.DiscountTotal > 0)
                parts.Add($"<div class=\"row is-discount\"><span>تخفیف</span><strong dir=\"ltr\">- {FormatMoney(receipt.DiscountTotal)}</strong></div>");
            if (receipt.FeesTotal > 0)
                parts.Add($"<div class=\"row\"><span>کارمزدها</span><strong dir=\"ltr\">{FormatMoney(receipt.FeesTotal)}</strong></div>");
            if (receipt.TaxTotal > 0)
                parts.Add($"<div class=\"row\"><span>مالیات</span><strong dir=\"ltr\">{FormatMoney(receipt.TaxTotal)}</strong></div>");

            foreach (var c in receipt.ChargeLines.Where(c => c.CalculatedAmount != 0).OrderBy(c => c.DisplayOrder))
            {
                // Prefer aggregated totals above; skip duplicate line items when aggregates exist
                if (receipt.DiscountTotal > 0 || receipt.FeesTotal > 0 || receipt.TaxTotal > 0)
                    continue;
                var sign = c.Category == ChargeCategory.Discount ? "- " : "";
                parts.Add($"<div class=\"row {(c.Category == ChargeCategory.Discount ? "is-discount" : "")}\"><span>{Escape(c.Title)}</span><strong dir=\"ltr\">{sign}{FormatMoney(Math.Abs(c.CalculatedAmount))}</strong></div>");
            }

            return string.Join("", parts);
        }

        // ---------- Shared pieces ----------

        private static List<string> BuildItemRows(ReceiptDto receipt, string strikeFontSize, bool includeToman = false)
        {
            return receipt.Items.Select(item =>
            {
                var hasDiscount = item.OriginalUnitPrice > 0 && item.OriginalUnitPrice > item.UnitPrice;
                var unit = hasDiscount
                    ? $"""<span style="display:block;text-decoration:line-through;color:#94a3b8;font-size:{strikeFontSize};font-weight:600;">{FormatMoney(item.OriginalUnitPrice)}</span><span>{FormatMoney(item.UnitPrice)}</span>"""
                    : FormatMoney(item.UnitPrice);
                var total = FormatMoney(item.LineTotal);
                if (includeToman)
                {
                    unit += " تومان";
                    total += " تومان";
                }
                return $"""
                <tr>
                    <td class="name">{Escape(item.Name)}</td>
                    <td>{ToFa(item.Quantity)}</td>
                    <td dir="ltr">{unit}</td>
                    <td dir="ltr">{total}</td>
                </tr>
                """;
            }).ToList();
        }

        private static List<string> BuildChargeRows(ReceiptDto receipt)
        {
            return receipt.ChargeLines
                .Where(c => c.CalculatedAmount != 0)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => $"""
                        <tr>
                            <td class="title">{Escape(c.Title)}</td>
                            <td class="amount {(c.Category == ChargeCategory.Discount ? "is-discount" : "")}" dir="ltr">{FormatSignedMoney(c.Category, c.CalculatedAmount)}</td>
                        </tr>
                        """)
                .ToList();
        }

        private static string BuildCustomerChips(ReceiptDto receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt.CustomerName)
                && string.IsNullOrWhiteSpace(receipt.CustomerMobile)
                && string.IsNullOrWhiteSpace(receipt.DeliveryAddress))
                return "";

            return $"""
                <div class="customer">
                    {(string.IsNullOrWhiteSpace(receipt.CustomerName) ? "" : $"<div class=\"chip\"><strong>مشتری:</strong> {Escape(receipt.CustomerName)}</div>")}
                    {(string.IsNullOrWhiteSpace(receipt.CustomerMobile) ? "" : $"<div class=\"chip\"><strong>تلفن:</strong> {ToFa(receipt.CustomerMobile)}</div>")}
                    {(string.IsNullOrWhiteSpace(receipt.DeliveryAddress) ? "" : $"<div class=\"chip\"><strong>آدرس:</strong> {Escape(receipt.DeliveryAddress)}</div>")}
                </div>
                """;
        }

        private static string BuildDescription(ReceiptDto receipt) =>
            string.IsNullOrWhiteSpace(receipt.Description)
                ? ""
                : $"<div class=\"note\"><strong>توضیحات:</strong> {Escape(receipt.Description)}</div>";

        private static string BuildCss(string templateId, bool offlineCapture)
        {
            // Android WebView→bitmap must not wait on /fonts/* network fetches (blank/black after template switch).
            var fonts = offlineCapture
                ? ""
                : """
                @font-face {
                    font-family: 'IRANYekan';
                    src: url('/fonts/iranyekan/iranyekanwebregularfanum.woff') format('woff');
                    font-weight: 400;
                    font-style: normal;
                    font-display: swap;
                }
                @font-face {
                    font-family: 'IRANYekan';
                    src: url('/fonts/iranyekan/iranyekanwebboldfanum.woff') format('woff');
                    font-weight: 700;
                    font-style: normal;
                    font-display: swap;
                }
                @font-face {
                    font-family: 'IRANYekan';
                    src: url('/fonts/iranyekan/iranyekanwebextraboldfanum.woff') format('woff');
                    font-weight: 800;
                    font-style: normal;
                    font-display: swap;
                }
                """;

            var bodyFont = offlineCapture
                ? "Tahoma, 'Segoe UI', sans-serif"
                : "'IRANYekan', 'IRANSans', Tahoma, 'Segoe UI', sans-serif";
            // Match laptop on-screen chrome; Android bitmap should look like the web preview/print sheet.
            var bodyBg = offlineCapture ? "#eef2f7" : "#eef2f7";

            var shared = $$"""
                * { margin: 0; padding: 0; box-sizing: border-box; }
                html, body { background: {{bodyBg}} !important; }
                body {
                    font-family: {{bodyFont}};
                    background: {{bodyBg}};
                    color: #0f172a;
                    direction: rtl;
                    padding: {{(offlineCapture ? "24px 16px 40px" : "24px 16px 40px")}};
                }
                .toolbar {
                    max-width: 820px;
                    margin: 0 auto 14px;
                    display: flex;
                    justify-content: flex-end;
                    gap: 8px;
                }
                .toolbar button {
                    border: none;
                    border-radius: 10px;
                    padding: 10px 16px;
                    font: inherit;
                    font-weight: 700;
                    cursor: pointer;
                }
                .toolbar .btn-print { background: #ff7a00; color: #fff; }
                .toolbar .btn-close-win { background: #fff; color: #334155; border: 1px solid #e2e8f0; }
                @media print {
                    body { background: #fff !important; padding: 0 !important; }
                    .toolbar { display: none !important; }
                }
                """;

            // Force desktop sheet layout on Android even if a device reports a narrow CSS width.
            var offlineDesktopGuard = offlineCapture
                ? """
                html[data-ry-offline="1"] .sheet,
                html[data-ry-offline="1"] .elegant-sheet {
                    margin: 0 auto !important;
                    border: 1px solid #e2e8f0 !important;
                    box-shadow: 0 18px 40px rgba(15, 23, 42, 0.08) !important;
                }
                html[data-ry-offline="1"] .sheet {
                    max-width: 820px !important;
                    border-radius: 18px !important;
                }
                html[data-ry-offline="1"] .elegant-sheet {
                    max-width: 760px !important;
                    border-radius: 22px !important;
                    box-shadow: 0 12px 32px rgba(15, 23, 42, 0.12) !important;
                }
                html[data-ry-offline="1"] .header,
                html[data-ry-offline="1"] .elegant-hero {
                    display: flex !important;
                    grid-template-columns: none !important;
                }
                html[data-ry-offline="1"] .elegant-hero__left { text-align: left !important; }
                html[data-ry-offline="1"] .elegant-meta {
                    grid-template-columns: repeat(2, minmax(0, 1fr)) !important;
                }
                html[data-ry-offline="1"] .elegant-bottom {
                    display: grid !important;
                    grid-template-columns: 1.1fr 0.9fr !important;
                }
                html[data-ry-offline="1"] .info-grid {
                    grid-template-columns: repeat(2, minmax(0, 1fr)) !important;
                }
                html[data-ry-offline="1"] .elegant-hero {
                    background-color: #1f2937 !important;
                    background-image: none !important;
                    color: #fff !important;
                    -webkit-print-color-adjust: exact;
                    print-color-adjust: exact;
                }
                html[data-ry-offline="1"] .elegant-kicker,
                html[data-ry-offline="1"] .elegant-date { color: #cbd5e1 !important; }
                html[data-ry-offline="1"] .elegant-restaurant,
                html[data-ry-offline="1"] .elegant-no { color: #fff !important; }
                html[data-ry-offline="1"] .elegant-doc { color: #fdba74 !important; }
                """
                : "";

            var layout = templateId switch
            {
                InvoicePrintTemplates.ThermalId => ThermalCss,
                InvoicePrintTemplates.FormalId => FormalCss,
                InvoicePrintTemplates.ElegantId => ElegantCss,
                InvoicePrintTemplates.ReadableId => ModernCss(large: true),
                _ => ModernCss(large: false)
            };

            return fonts + shared + layout + offlineDesktopGuard;
        }

        private static string ModernCss(bool large)
        {
            var bodySize = large ? "17px" : "13px";
            var restaurant = large ? "30px" : "22px";
            var orderNo = large ? "22px" : "18px";
            var td = large ? "16px" : "13px";
            var grand = large ? "20px" : "16px";
            var max = large ? "820px" : "760px";
            var padH = large ? "32px" : "28px";
            var padB = large ? "22px 32px 28px" : "18px 28px 24px";

            return $$"""
                body { font-size: {{bodySize}}; line-height: {{(large ? "1.85" : "1.7")}}; }
                .sheet {
                    max-width: {{max}};
                    margin: 0 auto;
                    background: #fff;
                    border: 1px solid #e2e8f0;
                    border-radius: 18px;
                    overflow: hidden;
                    box-shadow: 0 18px 40px rgba(15, 23, 42, 0.08);
                }
                .brand-bar { height: 6px; background: linear-gradient(90deg, #ff7a00, #fb923c, #fdba74); }
                .header {
                    padding: 22px {{padH}} 16px;
                    display: flex;
                    justify-content: space-between;
                    gap: 16px;
                    border-bottom: 1px solid #e2e8f0;
                }
                .restaurant-name { font-size: {{restaurant}}; font-weight: 800; }
                .doc-label {
                    display: inline-flex; margin-top: 6px; padding: 3px 10px; border-radius: 999px;
                    background: #fff7ed; color: #c2410c; font-size: {{(large ? "14px" : "12px")}}; font-weight: 700;
                }
                .order-meta { text-align: left; min-width: 160px; }
                .order-number { font-size: {{orderNo}}; font-weight: 800; }
                .order-number span { display: block; color: #64748b; font-size: {{(large ? "13px" : "12px")}}; font-weight: 600; }
                .issued-badge {
                    margin-top: 8px; display: inline-block; padding: 2px 8px; border-radius: 6px;
                    background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700;
                }
                .body { padding: {{padB}}; }
                .info-grid {
                    display: grid; grid-template-columns: repeat(2, minmax(0, 1fr));
                    gap: {{(large ? "14px 20px" : "10px 18px")}}; padding: {{(large ? "18px" : "14px 16px")}};
                    background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 14px; margin-bottom: 18px;
                }
                .info-item .label { color: #64748b; font-size: {{(large ? "13px" : "11px")}}; font-weight: 700; }
                .info-item .value { font-weight: 700; font-size: {{td}}; }
                .section-title { font-size: {{(large ? "15px" : "12px")}}; font-weight: 800; color: #64748b; margin: 4px 0 10px; }
                .items-table { width: 100%; border-collapse: collapse; }
                .items-table th {
                    background: #0f172a; color: #fff; font-weight: 700; padding: {{(large ? "14px 10px" : "10px 8px")}};
                    text-align: center; font-size: {{(large ? "14px" : "12px")}};
                }
                .items-table th:first-child { text-align: right; border-radius: 0 10px 0 0; }
                .items-table th:last-child { border-radius: 10px 0 0 0; }
                .items-table td {
                    padding: {{(large ? "14px 10px" : "11px 8px")}}; border-bottom: 1px solid #e2e8f0;
                    text-align: center; vertical-align: middle; font-size: {{td}};
                }
                .items-table td.name { text-align: right; font-weight: 700; }
                .items-table tbody tr:nth-child(even) { background: #fbfdff; }
                .charges-wrap { margin-top: 16px; display: grid; grid-template-columns: 1.1fr 0.9fr; gap: 14px; }
                .charges-card, .totals-card { border: 1px solid #e2e8f0; border-radius: 14px; background: #fff; overflow: hidden; }
                .card-head {
                    padding: {{(large ? "12px 16px" : "10px 14px")}}; background: #f8fafc; border-bottom: 1px solid #e2e8f0;
                    font-weight: 800; font-size: {{(large ? "14px" : "12px")}}; color: #334155;
                }
                .charges-table { width: 100%; border-collapse: collapse; }
                .charges-table td { padding: {{(large ? "12px 16px" : "9px 14px")}}; border-bottom: 1px solid #f1f5f9; font-size: {{td}}; }
                .charges-table td.title { text-align: right; }
                .charges-table td.amount { text-align: left; font-weight: 700; white-space: nowrap; }
                .charges-table td.amount.is-discount { color: #047857; }
                .summary-row {
                    display: flex; justify-content: space-between; gap: 12px;
                    padding: {{(large ? "10px 16px" : "7px 14px")}}; color: #475569; font-size: {{td}};
                }
                .summary-row strong.is-discount { color: #047857; }
                .grand-total {
                    margin-top: 4px; padding: {{(large ? "18px 16px" : "14px")}};
                    background: linear-gradient(135deg, #fff7ed, #ffedd5); border-top: 1px solid #fed7aa;
                    display: flex; justify-content: space-between; align-items: center;
                    font-size: {{grand}}; font-weight: 800; color: #9a3412;
                }
                .customer {
                    margin-top: 16px; padding: 12px 14px; border-radius: 12px; border: 1px dashed #cbd5e1;
                    background: #f8fafc; display: flex; flex-wrap: wrap; gap: 8px 18px;
                }
                .customer .chip { font-size: {{(large ? "15px" : "12px")}}; color: #334155; }
                .note { margin-top: 12px; color: #64748b; font-size: {{(large ? "14px" : "12px")}}; }
                .footer { margin-top: 22px; padding-top: 14px; border-top: 1px solid #e2e8f0; text-align: center; color: #64748b; font-size: 12px; }
                .footer .thanks { color: #0f172a; font-weight: 800; margin-bottom: 4px; font-size: {{(large ? "17px" : "14px")}}; }
                @media (max-width: 640px) {
                    .header { flex-direction: column; }
                    .order-meta { text-align: right; }
                    .info-grid, .charges-wrap { grid-template-columns: 1fr; }
                }
                @media print {
                    .sheet { border: none; border-radius: 0; box-shadow: none; max-width: none; }
                    .brand-bar, .items-table th, .grand-total, .info-grid, .doc-label, .issued-badge {
                        print-color-adjust: exact; -webkit-print-color-adjust: exact;
                    }
                }
                """;
        }

        private const string ThermalCss = """
            body { font-size: 13px; line-height: 1.55; background: #f1f5f9; }
            .thermal-sheet {
                max-width: 420px;
                margin: 0 auto;
                background: #fff;
                padding: 18px 14px 22px;
                border: 1px solid #cbd5e1;
                box-shadow: 0 10px 28px rgba(15, 23, 42, 0.08);
            }
            .thermal-brand {
                text-align: center;
                font-size: 22px;
                font-weight: 800;
                margin-bottom: 10px;
                letter-spacing: -0.02em;
            }
            .thermal-meta {
                display: flex;
                justify-content: space-between;
                gap: 12px;
                font-size: 13px;
                font-weight: 700;
                margin-bottom: 6px;
            }
            .thermal-customer {
                font-size: 13px;
                font-weight: 700;
                margin: 4px 0 8px;
            }
            .thermal-submeta {
                font-size: 11px;
                color: #64748b;
                margin-bottom: 8px;
            }
            .thermal-table {
                width: 100%;
                border-collapse: collapse;
                margin: 6px 0 12px;
            }
            .thermal-table th,
            .thermal-table td {
                border: 1.5px solid #0f172a;
                padding: 7px 5px;
                text-align: center;
                vertical-align: middle;
                font-size: 12px;
            }
            .thermal-table th {
                font-weight: 800;
                font-size: 11px;
                background: #fff;
            }
            .thermal-table td.name {
                text-align: right;
                font-weight: 700;
            }
            .thermal-table tr.thermal-extra td {
                font-size: 11px;
                background: #f8fafc;
            }
            .thermal-total-line {
                display: flex;
                justify-content: space-between;
                align-items: baseline;
                gap: 10px;
                font-size: 14px;
                font-weight: 800;
                margin-top: 4px;
            }
            .thermal-total-words {
                margin-top: 8px;
                font-size: 13px;
                font-weight: 800;
                text-align: right;
            }
            .thermal-note {
                margin-top: 10px;
                font-size: 11px;
                color: #475569;
            }
            .thermal-seal {
                width: 28px;
                height: 28px;
                margin: 16px auto 0;
                border-radius: 50%;
                border: 2px solid #0f172a;
                background:
                    radial-gradient(circle at 50% 50%, #0f172a 0 28%, transparent 29%),
                    conic-gradient(from 0deg, #0f172a 0 12%, transparent 12% 25%, #0f172a 25% 37%, transparent 37% 50%, #0f172a 50% 62%, transparent 62% 75%, #0f172a 75% 87%, transparent 87% 100%);
                opacity: 0.85;
            }
            @media print {
                body { background: #fff; }
                .thermal-sheet {
                    max-width: none;
                    border: none;
                    box-shadow: none;
                    padding: 0;
                }
            }
            """;

        private const string FormalCss = """
            body { font-size: 13px; line-height: 1.7; }
            .formal-sheet {
                max-width: 780px;
                margin: 0 auto;
                background: #fff;
                border: 2px solid #0f172a;
                padding: 28px 30px 24px;
            }
            .formal-top {
                display: flex;
                justify-content: space-between;
                gap: 20px;
                padding-bottom: 16px;
                border-bottom: 3px double #0f172a;
                margin-bottom: 14px;
            }
            .formal-kicker {
                font-size: 11px;
                letter-spacing: 0.14em;
                color: #64748b;
                font-weight: 700;
                margin-bottom: 4px;
            }
            .formal-restaurant { font-size: 24px; font-weight: 800; }
            .formal-box {
                min-width: 180px;
                border: 1.5px solid #0f172a;
                padding: 8px 12px;
                display: grid;
                gap: 6px;
            }
            .formal-box span { display: block; font-size: 10px; color: #64748b; font-weight: 700; }
            .formal-box strong { font-size: 13px; }
            .formal-party, .formal-address {
                margin-bottom: 10px;
                display: flex;
                flex-wrap: wrap;
                gap: 8px 18px;
                font-size: 13px;
            }
            .formal-table { width: 100%; border-collapse: collapse; margin-top: 8px; }
            .formal-table th, .formal-table td {
                border: 1px solid #0f172a;
                padding: 8px 6px;
                text-align: center;
                font-size: 12px;
            }
            .formal-table th { background: #f1f5f9; font-weight: 800; }
            .formal-table td.name { text-align: right; font-weight: 700; }
            .formal-summary { margin-top: 14px; }
            .formal-summary-rows {
                display: grid;
                gap: 6px;
                max-width: 320px;
                margin-right: auto;
            }
            .formal-summary-rows > div, .formal-grand {
                display: flex;
                justify-content: space-between;
                gap: 16px;
            }
            .formal-grand {
                margin-top: 10px;
                padding: 10px 12px;
                border: 2px solid #0f172a;
                font-size: 15px;
                font-weight: 800;
                max-width: 320px;
                margin-right: auto;
            }
            .formal-words {
                margin-top: 10px;
                text-align: left;
                font-weight: 700;
                font-size: 12px;
            }
            .formal-footer {
                margin-top: 22px;
                padding-top: 12px;
                border-top: 1px solid #cbd5e1;
                display: flex;
                justify-content: space-between;
                color: #64748b;
                font-size: 11px;
            }
            @media print {
                .formal-sheet { border-width: 1.5px; max-width: none; }
                .formal-table th { print-color-adjust: exact; -webkit-print-color-adjust: exact; }
            }
            """;

        private const string ElegantCss = """
            /* Light page chrome so Android WebView→bitmap print is not washed in black. */
            body { font-size: 13px; line-height: 1.7; background: #f1f5f9; }
            .elegant-sheet {
                max-width: 760px;
                margin: 0 auto;
                background: #fff;
                border-radius: 22px;
                overflow: hidden;
                box-shadow: 0 12px 32px rgba(15, 23, 42, 0.12);
                border: 1px solid #e2e8f0;
            }
            .elegant-hero {
                display: flex;
                justify-content: space-between;
                gap: 16px;
                padding: 28px 28px 22px;
                /* Solid fallback first: WebView bitmap capture often flattens gradients to black. */
                background-color: #1f2937;
                background-image: linear-gradient(135deg, #1f2937 0%, #334155 50%, #9a3412 100%);
                color: #fff;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            .elegant-kicker {
                font-size: 11px;
                letter-spacing: 0.22em;
                color: #e2e8f0;
                margin-bottom: 6px;
            }
            .elegant-restaurant { font-size: 26px; font-weight: 800; color: #fff; }
            .elegant-doc { margin-top: 4px; color: #fdba74; font-weight: 700; font-size: 13px; }
            .elegant-hero__left { text-align: left; }
            .elegant-no { font-size: 22px; font-weight: 800; color: #fff; }
            .elegant-date { margin-top: 4px; color: #cbd5e1; font-size: 12px; }
            .elegant-meta {
                display: grid;
                grid-template-columns: repeat(2, minmax(0, 1fr));
                gap: 10px 16px;
                padding: 16px 28px;
                background: #fff7ed;
                border-bottom: 1px solid #fed7aa;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            .elegant-meta span { display: block; font-size: 11px; color: #9a3412; font-weight: 700; }
            .elegant-meta strong { font-size: 13px; color: #0f172a; }
            .elegant-table { width: 100%; border-collapse: collapse; }
            .elegant-table th {
                text-align: center;
                padding: 12px 10px;
                font-size: 11px;
                letter-spacing: 0.04em;
                color: #64748b;
                border-bottom: 1px solid #e2e8f0;
                background: #f8fafc;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            .elegant-table th:first-child { text-align: right; }
            .elegant-table td {
                padding: 14px 10px;
                text-align: center;
                border-bottom: 1px solid #f1f5f9;
                font-size: 13px;
                color: #0f172a;
                background: #fff;
            }
            .elegant-table td.name { text-align: right; font-weight: 700; }
            .elegant-bottom {
                display: grid;
                grid-template-columns: 1.1fr 0.9fr;
                gap: 18px;
                padding: 20px 28px 28px;
                background: #fff;
            }
            .elegant-notes { color: #475569; font-size: 12px; display: grid; gap: 8px; align-content: start; }
            .elegant-notes .muted { color: #64748b; }
            .elegant-totals {
                border: 1px solid #e2e8f0;
                border-radius: 16px;
                padding: 12px 14px;
                background: #f8fafc;
                -webkit-print-color-adjust: exact;
                print-color-adjust: exact;
            }
            .elegant-totals .row {
                display: flex;
                justify-content: space-between;
                gap: 12px;
                padding: 6px 0;
                font-size: 13px;
                color: #475569;
            }
            .elegant-totals .row.is-discount strong { color: #047857; }
            .elegant-grand {
                margin-top: 8px;
                padding-top: 10px;
                border-top: 1px dashed #cbd5e1;
                display: flex;
                justify-content: space-between;
                align-items: center;
                font-size: 16px;
                font-weight: 800;
                color: #9a3412;
            }
            @media (max-width: 640px) {
                .elegant-hero, .elegant-bottom { grid-template-columns: 1fr; display: grid; }
                .elegant-hero__left { text-align: right; }
                .elegant-meta { grid-template-columns: 1fr; }
            }
            @media print {
                body { background: #fff !important; }
                .elegant-sheet { border-radius: 0; box-shadow: none; max-width: none; border: none; }
                .elegant-hero {
                    background: #1f2937 !important;
                    background-image: none !important;
                    color: #fff !important;
                    -webkit-print-color-adjust: exact;
                    print-color-adjust: exact;
                }
                .elegant-meta, .elegant-table th, .elegant-totals {
                    -webkit-print-color-adjust: exact;
                    print-color-adjust: exact;
                }
            }
            """;

        private const string PrintAutoScript = """
            <script>
                window.addEventListener('load', function () {
                    setTimeout(function () { window.print(); }, 250);
                });
            </script>
            """;

        private static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        private static string ToFa(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value ?? string.Empty;
            return value.ToPersianDigits();
        }

        private static string ToFa(int value) => value.ToString().ToPersianDigits();

        private static string FormatMoney(decimal value) => value.ToString("N0").ToPersianDigits();

        private static string FormatSignedMoney(ChargeCategory category, decimal value)
        {
            var formatted = FormatMoney(Math.Abs(value));
            return category == ChargeCategory.Discount ? $"- {formatted} تومان" : $"{formatted} تومان";
        }
    }
}
