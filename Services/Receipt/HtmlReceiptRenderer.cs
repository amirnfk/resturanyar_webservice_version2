using resturanyar.Models.Receipt;
using resturanyar.Utility;

namespace resturanyar.Services.Receipt
{
    public interface IReceiptRenderer
    {
        string RenderHtml(ReceiptDto receipt);
    }

    public class HtmlReceiptRenderer : IReceiptRenderer
    {
        public string RenderHtml(ReceiptDto receipt)
        {
            var css = """
                :root {
                    --ink: #0f172a;
                    --muted: #64748b;
                    --line: #e2e8f0;
                    --soft: #f8fafc;
                    --accent: #ff7a00;
                    --accent-soft: #fff7ed;
                }
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: 'IRANYekan', 'IRANSans', Tahoma, 'Segoe UI', sans-serif;
                    background: #eef2f7;
                    color: var(--ink);
                    direction: rtl;
                    font-size: 13px;
                    line-height: 1.7;
                    padding: 24px 16px 40px;
                }
                .toolbar {
                    max-width: 760px;
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
                .toolbar .btn-print {
                    background: var(--accent);
                    color: #fff;
                }
                .toolbar .btn-close-win {
                    background: #fff;
                    color: #334155;
                    border: 1px solid var(--line);
                }
                .sheet {
                    max-width: 760px;
                    margin: 0 auto;
                    background: #fff;
                    border: 1px solid var(--line);
                    border-radius: 18px;
                    overflow: hidden;
                    box-shadow: 0 18px 40px rgba(15, 23, 42, 0.08);
                }
                .brand-bar {
                    height: 6px;
                    background: linear-gradient(90deg, #ff7a00, #fb923c, #fdba74);
                }
                .header {
                    padding: 22px 28px 16px;
                    display: flex;
                    justify-content: space-between;
                    gap: 16px;
                    align-items: flex-start;
                    border-bottom: 1px solid var(--line);
                }
                .restaurant-name {
                    font-size: 22px;
                    font-weight: 800;
                    letter-spacing: -0.02em;
                }
                .doc-label {
                    display: inline-flex;
                    align-items: center;
                    margin-top: 6px;
                    padding: 3px 10px;
                    border-radius: 999px;
                    background: var(--accent-soft);
                    color: #c2410c;
                    font-size: 12px;
                    font-weight: 700;
                }
                .order-meta {
                    text-align: left;
                    min-width: 160px;
                }
                .order-number {
                    font-size: 18px;
                    font-weight: 800;
                }
                .order-number span {
                    color: var(--muted);
                    font-size: 12px;
                    font-weight: 600;
                    display: block;
                }
                .issued-badge {
                    margin-top: 8px;
                    display: inline-block;
                    padding: 2px 8px;
                    border-radius: 6px;
                    background: #ecfdf5;
                    color: #047857;
                    font-size: 11px;
                    font-weight: 700;
                }
                .body { padding: 18px 28px 24px; }
                .info-grid {
                    display: grid;
                    grid-template-columns: repeat(2, minmax(0, 1fr));
                    gap: 10px 18px;
                    padding: 14px 16px;
                    background: var(--soft);
                    border: 1px solid var(--line);
                    border-radius: 14px;
                    margin-bottom: 18px;
                }
                .info-item {
                    display: flex;
                    flex-direction: column;
                    gap: 2px;
                }
                .info-item .label {
                    color: var(--muted);
                    font-size: 11px;
                    font-weight: 700;
                }
                .info-item .value {
                    font-weight: 700;
                    font-size: 13px;
                }
                .section-title {
                    font-size: 12px;
                    font-weight: 800;
                    color: var(--muted);
                    margin: 4px 0 10px;
                    letter-spacing: 0.02em;
                }
                .items-table, .charges-table {
                    width: 100%;
                    border-collapse: collapse;
                }
                .items-table th {
                    background: #0f172a;
                    color: #fff;
                    font-weight: 700;
                    padding: 10px 8px;
                    text-align: center;
                    font-size: 12px;
                }
                .items-table th:first-child { text-align: right; border-radius: 0 10px 0 0; }
                .items-table th:last-child { border-radius: 10px 0 0 0; }
                .items-table td {
                    padding: 11px 8px;
                    border-bottom: 1px solid var(--line);
                    text-align: center;
                    vertical-align: middle;
                }
                .items-table td.name {
                    text-align: right;
                    font-weight: 700;
                }
                .items-table tbody tr:nth-child(even) { background: #fbfdff; }
                .items-table tbody tr:last-child td { border-bottom: none; }
                .charges-wrap {
                    margin-top: 16px;
                    display: grid;
                    grid-template-columns: 1.1fr 0.9fr;
                    gap: 14px;
                    align-items: start;
                }
                .charges-card, .totals-card {
                    border: 1px solid var(--line);
                    border-radius: 14px;
                    background: #fff;
                    overflow: hidden;
                }
                .charges-card .card-head,
                .totals-card .card-head {
                    padding: 10px 14px;
                    background: var(--soft);
                    border-bottom: 1px solid var(--line);
                    font-weight: 800;
                    font-size: 12px;
                    color: #334155;
                }
                .charges-table td {
                    padding: 9px 14px;
                    border-bottom: 1px solid #f1f5f9;
                }
                .charges-table tr:last-child td { border-bottom: none; }
                .charges-table td.title { text-align: right; color: #334155; }
                .charges-table td.amount { text-align: left; font-weight: 700; white-space: nowrap; }
                .charges-table td.amount.is-discount { color: #047857; }
                .totals-card .rows { padding: 8px 0; }
                .summary-row {
                    display: flex;
                    justify-content: space-between;
                    gap: 12px;
                    padding: 7px 14px;
                    color: #475569;
                    font-size: 13px;
                }
                .summary-row strong { color: var(--ink); font-weight: 700; }
                .grand-total {
                    margin-top: 4px;
                    padding: 14px;
                    background: linear-gradient(135deg, #fff7ed, #ffedd5);
                    border-top: 1px solid #fed7aa;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    font-size: 16px;
                    font-weight: 800;
                    color: #9a3412;
                }
                .customer {
                    margin-top: 16px;
                    padding: 12px 14px;
                    border-radius: 12px;
                    border: 1px dashed #cbd5e1;
                    background: #f8fafc;
                    display: flex;
                    flex-wrap: wrap;
                    gap: 8px 18px;
                }
                .customer .chip {
                    font-size: 12px;
                    color: #334155;
                }
                .customer .chip strong { color: var(--ink); }
                .note {
                    margin-top: 12px;
                    color: var(--muted);
                    font-size: 12px;
                }
                .footer {
                    margin-top: 22px;
                    padding-top: 14px;
                    border-top: 1px solid var(--line);
                    text-align: center;
                    color: var(--muted);
                    font-size: 12px;
                }
                .footer .thanks {
                    color: var(--ink);
                    font-weight: 800;
                    margin-bottom: 4px;
                }
                @media (max-width: 640px) {
                    .header { flex-direction: column; }
                    .order-meta { text-align: right; }
                    .info-grid, .charges-wrap { grid-template-columns: 1fr; }
                    body { padding: 12px 8px 24px; }
                    .body, .header { padding-left: 16px; padding-right: 16px; }
                }
                @media print {
                    body { background: #fff; padding: 0; }
                    .toolbar { display: none !important; }
                    .sheet {
                        border: none;
                        border-radius: 0;
                        box-shadow: none;
                        max-width: none;
                    }
                    .brand-bar { print-color-adjust: exact; -webkit-print-color-adjust: exact; }
                    .items-table th,
                    .grand-total,
                    .info-grid,
                    .doc-label,
                    .issued-badge {
                        print-color-adjust: exact;
                        -webkit-print-color-adjust: exact;
                    }
                }
                """;

            var itemRows = receipt.Items.Select((item, index) => $"""
                <tr>
                    <td class="name">{Escape(item.Name)}</td>
                    <td>{ToFa(item.Quantity)}</td>
                    <td>{FormatMoney(item.UnitPrice)}</td>
                    <td>{FormatMoney(item.LineTotal)}</td>
                </tr>
                """).ToList();

            var chargeRows = receipt.ChargeLines
                .Where(c => c.CalculatedAmount != 0 && c.Category != ChargeCategory.Discount)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => $"""
                        <tr>
                            <td class="title">{Escape(c.Title)}</td>
                            <td class="amount">{FormatSignedMoney(c.Category, c.CalculatedAmount)}</td>
                        </tr>
                        """)
                .ToList();

            var hasChargeDetails = chargeRows.Count > 0
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
                                        <tr><td class="title">جمع اقلام</td><td class="amount">{FormatMoney(receipt.ItemsSubtotal)} تومان</td></tr>
                                        {(receipt.FeesTotal > 0 ? $"<tr><td class=\"title\">کارمزدها</td><td class=\"amount\">{FormatMoney(receipt.FeesTotal)} تومان</td></tr>" : "")}
                                        {(receipt.TaxTotal > 0 ? $"<tr><td class=\"title\">مالیات</td><td class=\"amount\">{FormatMoney(receipt.TaxTotal)} تومان</td></tr>" : "")}
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

            var customerBlock = "";
            if (!string.IsNullOrWhiteSpace(receipt.CustomerName) || !string.IsNullOrWhiteSpace(receipt.CustomerMobile))
            {
                customerBlock = $"""
                    <div class="customer">
                        {(string.IsNullOrWhiteSpace(receipt.CustomerName) ? "" : $"<div class=\"chip\"><strong>مشتری:</strong> {Escape(receipt.CustomerName)}</div>")}
                        {(string.IsNullOrWhiteSpace(receipt.CustomerMobile) ? "" : $"<div class=\"chip\"><strong>تلفن:</strong> {ToFa(receipt.CustomerMobile)}</div>")}
                    </div>
                    """;
            }

            var descriptionBlock = string.IsNullOrWhiteSpace(receipt.Description)
                ? ""
                : $"<div class=\"note\"><strong>توضیحات:</strong> {Escape(receipt.Description)}</div>";

            var issuedBadge = receipt.IsIssued
                ? """<div class="issued-badge">صادر شده</div>"""
                : "";

            var issuedAtText = receipt.IssuedAt.HasValue
                ? $"<div class=\"info-item\"><span class=\"label\">زمان صدور</span><span class=\"value\">{Escape(receipt.IssuedAt.Value.ToPersianDateTimeTehran())}</span></div>"
                : "";

            return $"""
                <!DOCTYPE html>
                <html lang="fa" dir="rtl">
                <head>
                    <meta charset="UTF-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>فاکتور سفارش {ToFa(receipt.OrderNumber)}</title>
                    <style>{css}</style>
                </head>
                <body>
                    <div class="toolbar">
                        <button type="button" class="btn-close-win" onclick="window.close()">بستن</button>
                        <button type="button" class="btn-print" onclick="window.print()">چاپ فاکتور</button>
                    </div>
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
                                <div class="info-item"><span class="label">میز</span><span class="value">{(string.IsNullOrWhiteSpace(receipt.TableNumber) ? "—" : ToFa(receipt.TableNumber))}</span></div>
                                <div class="info-item"><span class="label">نوع سفارش</span><span class="value">{Escape(receipt.OrderTypeLabel)}</span></div>
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

                            <div class="charges-wrap">
                                {chargesCard}
                                <div class="totals-card">
                                    <div class="card-head">خلاصه مبلغ</div>
                                    <div class="rows">
                                        <div class="summary-row"><span>جمع اقلام</span><strong>{FormatMoney(receipt.ItemsSubtotal)} تومان</strong></div>
                                        {(receipt.FeesTotal > 0 ? $"<div class=\"summary-row\"><span>کارمزدها</span><strong>{FormatMoney(receipt.FeesTotal)} تومان</strong></div>" : "")}
                                        {(receipt.TaxTotal > 0 ? $"<div class=\"summary-row\"><span>مالیات</span><strong>{FormatMoney(receipt.TaxTotal)} تومان</strong></div>" : "")}
                                    </div>
                                    <div class="grand-total">
                                        <span>جمع کل</span>
                                        <span>{FormatMoney(receipt.GrandTotal)} تومان</span>
                                    </div>
                                </div>
                            </div>

                            {customerBlock}
                            {descriptionBlock}

                            <div class="footer">
                                <div class="thanks">از اعتماد شما سپاسگزاریم</div>
                                <div>سیستم مدیریت رستورانیار</div>
                            </div>
                        </div>
                    </div>
                    {PrintAutoScript}
                </body>
                </html>
                """;
        }

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
            return category == ChargeCategory.Discount ? $"-{formatted} تومان" : $"{formatted} تومان";
        }
    }
}
