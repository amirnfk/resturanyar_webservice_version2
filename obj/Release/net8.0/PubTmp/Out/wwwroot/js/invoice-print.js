

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/[&<>"']/g, function (m) {
        return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m];
    });
}

function buildInvoiceHtml({ restaurant, orderNumber, tableNumber, orderStatus,
    createdAt, updatedAt, description, items, totalText,
    customerName, customerMobile, now }) {

    const css = `
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
        .total { font-size: 18px; font-weight: bold; text-align: center; margin-top: 20px; padding-top: 10px; border-top: 2px solid #000; }
        .customer { background: #f9f9f9; padding: 10px; border-radius: 8px; margin: 15px 0; text-align: center; }
        .footer { text-align: center; margin-top: 30px; font-size: 12px; color: #555; }
        @media print { body { padding: 0; margin: 0; } }
    `;

    const itemRows = items.map(item => `
        <tr>
            <td class="name">${escapeHtml(item.name)}</td>
            <td>${escapeHtml(item.quantity)}</td>
            <td>${escapeHtml(item.price)}</td>
        </tr>
    `).join('');

    return `<!DOCTYPE html>
<html dir="rtl">
<head>
    <meta charset="UTF-8">
    <title>فاکتور سفارش #${escapeHtml(orderNumber)}</title>
    <style>${css}</style>
</head>
<body>
    <div class="print-container">
        <div class="header">
            <div class="restaurant-name">${escapeHtml(restaurant)}</div>
            <div class="order-title">فاکتور #${escapeHtml(orderNumber)}</div>
        </div>

        <div class="info-row"><span class="info-label">میز:</span><span>${escapeHtml(tableNumber) || '-'}</span></div>
        <div class="info-row"><span class="info-label">وضعیت:</span><span>${escapeHtml(orderStatus)}</span></div>
        <div class="info-row"><span class="info-label">تاریخ ثبت:</span><span>${escapeHtml(createdAt)}</span></div>
        ${updatedAt ? `<div class="info-row"><span class="info-label">آخرین تغییر:</span><span>${escapeHtml(updatedAt)}</span></div>` : ''}
        ${description ? `<div class="info-row"><span class="info-label">توضیحات:</span><span>${escapeHtml(description)}</span></div>` : ''}

        <div class="divider"></div>

        <table class="items-table">
            <thead><tr><th>نام غذا</th><th>تعداد</th><th>قیمت واحد (تومان)</th></tr></thead>
            <tbody>${itemRows}</tbody>
        </table>

        <div class="total">${escapeHtml(totalText)}</div>

        <div class="customer">
            ${customerName ? `<div><strong>مشتری:</strong> ${escapeHtml(customerName)}</div>` : ''}
            ${customerMobile ? `<div><strong>تلفن:</strong> ${escapeHtml(customerMobile)}</div>` : ''}
        </div>

        <div class="footer">
            <div>چاپ شده در: ${now}</div>
            <div>با تشکر از خرید شما</div>
        </div>
    </div>
</body>
</html>`;
}

function openPrintWindow(htmlContent) {
    const printWindow = window.open('', '_blank', 'width=800,height=600,scrollbars=yes');
    if (!printWindow) return false;

    printWindow.document.write(htmlContent);
    printWindow.document.close();
    printWindow.focus();

    printWindow.onload = () => {
        printWindow.print();
        printWindow.onafterprint = () => printWindow.close();
    };
  

    return true;
}