using resturanyar.Models.Receipt;

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
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body {
                    font-family: 'IRANYekan', Tahoma, 'Segoe UI', sans-serif;
                    background: white; padding: 15px; direction: rtl;
                    font-size: 14px; line-height: 1.5; color: #000;
                }
                .print-container { max-width: 800px; margin: 0 auto; }
                .header { text-align: center; margin-bottom: 20px; border-bottom: 2px solid #ddd; padding-bottom: 10px; }
                .restaurant-name { font-size: 22px; font-weight: bold; margin-bottom: 5px; }
                .order-title { font-size: 18px; font-weight: bold; margin-top: 10px; }
                .info-row { display: flex; justify-content: space-between; margin: 5px 0; flex-wrap: wrap; }
                .info-label { font-weight: bold; }
                .divider { border-top: 1px dashed #aaa; margin: 12px 0; }
                .items-table { width: 100%; border-collapse: collapse; margin: 15px 0; }
                .items-table th, .items-table td { border-bottom: 1px solid #eee; padding: 8px 4px; text-align: center; }
                .items-table th { background: #f5f5f5; font-weight: bold; }
                .items-table td.name { text-align: right; }
                .charges-table { width: 100%; border-collapse: collapse; margin: 10px 0; }
                .charges-table td { padding: 6px 4px; border-bottom: 1px solid #f0f0f0; }
                .charges-table td.title { text-align: right; }
                .charges-table td.amount { text-align: left; font-weight: 600; }
                .summary-row { display: flex; justify-content: space-between; margin: 4px 0; }
                .total { font-size: 18px; font-weight: bold; text-align: center; margin-top: 20px; padding-top: 10px; border-top: 2px solid #000; }
                .customer { background: #f9f9f9; padding: 10px; border-radius: 8px; margin: 15px 0; text-align: center; }
                .footer { text-align: center; margin-top: 30px; font-size: 12px; color: #555; }
                @media print { body { padding: 0; margin: 0; } }
                """;

            var itemRows = receipt.Items.Select(item => $"""
                <tr>
                    <td class="name">{Escape(item.Name)}</td>
                    <td>{item.Quantity}</td>
                    <td>{FormatMoney(item.UnitPrice)}</td>
                    <td>{FormatMoney(item.LineTotal)}</td>
                </tr>
                """).ToList();

            var chargeRows = receipt.ChargeLines
                .Where(c => c.CalculatedAmount != 0)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => $"""
                    <tr>
                        <td class="title">{Escape(c.Title)}</td>
                        <td class="amount">{FormatSignedMoney(c.Category, c.CalculatedAmount)}</td>
                    </tr>
                    """)
                .ToList();

            var chargesSection = chargeRows.Count > 0
                ? $"""
                    <div class="divider"></div>
                    <table class="charges-table">
                        <tbody>{string.Join("", chargeRows)}</tbody>
                    </table>
                    <div class="summary-row"><span>جمع اقلام:</span><span>{FormatMoney(receipt.ItemsSubtotal)} تومان</span></div>
                    """
                : "";

            if (receipt.DiscountTotal > 0)
                chargesSection += $"<div class=\"summary-row\"><span>تخفیف:</span><span>-{FormatMoney(receipt.DiscountTotal)} تومان</span></div>";
            if (receipt.FeesTotal > 0)
                chargesSection += $"<div class=\"summary-row\"><span>کارمزدها:</span><span>{FormatMoney(receipt.FeesTotal)} تومان</span></div>";
            if (receipt.TaxTotal > 0)
                chargesSection += $"<div class=\"summary-row\"><span>مالیات:</span><span>{FormatMoney(receipt.TaxTotal)} تومان</span></div>";

            return $"""
                <!DOCTYPE html>
                <html dir="rtl">
                <head>
                    <meta charset="UTF-8">
                    <title>فاکتور سفارش #{Escape(receipt.OrderNumber)}</title>
                    <style>{css}</style>
                </head>
                <body>
                    <div class="print-container">
                        <div class="header">
                            <div class="restaurant-name">{Escape(receipt.RestaurantName)}</div>
                            <div class="order-title">فاکتور #{Escape(receipt.OrderNumber)}</div>
                        </div>
                        <div class="info-row"><span class="info-label">میز:</span><span>{Escape(receipt.TableNumber) ?? "-"}</span></div>
                        <div class="info-row"><span class="info-label">نوع سفارش:</span><span>{Escape(receipt.OrderTypeLabel)}</span></div>
                        <div class="info-row"><span class="info-label">وضعیت:</span><span>{Escape(receipt.OrderStatus)}</span></div>
                        <div class="info-row"><span class="info-label">تاریخ ثبت:</span><span>{Escape(receipt.CreatedAt)}</span></div>
                        {(string.IsNullOrWhiteSpace(receipt.UpdatedAt) ? "" : $"<div class=\"info-row\"><span class=\"info-label\">آخرین تغییر:</span><span>{Escape(receipt.UpdatedAt)}</span></div>")}
                        {(string.IsNullOrWhiteSpace(receipt.Description) ? "" : $"<div class=\"info-row\"><span class=\"info-label\">توضیحات:</span><span>{Escape(receipt.Description)}</span></div>")}
                        <div class="divider"></div>
                        <table class="items-table">
                            <thead><tr><th>نام غذا</th><th>تعداد</th><th>قیمت واحد</th><th>جمع</th></tr></thead>
                            <tbody>{string.Join("", itemRows)}</tbody>
                        </table>
                        {chargesSection}
                        <div class="total">جمع کل: {FormatMoney(receipt.GrandTotal)} تومان</div>
                        <div class="customer">
                            {(string.IsNullOrWhiteSpace(receipt.CustomerName) ? "" : $"<div><strong>مشتری:</strong> {Escape(receipt.CustomerName)}</div>")}
                            {(string.IsNullOrWhiteSpace(receipt.CustomerMobile) ? "" : $"<div><strong>تلفن:</strong> {Escape(receipt.CustomerMobile)}</div>")}
                        </div>
                        <div class="footer">با تشکر از انتخاب شما — رستورانیار</div>
                    </div>
                </body>
                </html>
                """;
        }

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

        private static string FormatMoney(decimal value) => value.ToString("N0");

        private static string FormatSignedMoney(ChargeCategory category, decimal value)
        {
            var formatted = FormatMoney(Math.Abs(value));
            return category == ChargeCategory.Discount ? $"-{formatted} تومان" : $"{formatted} تومان";
        }
    }
}
