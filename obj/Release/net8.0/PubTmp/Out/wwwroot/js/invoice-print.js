function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/[&<>"']/g, function (m) {
        return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m];
    });
}

function toPersianDigits(value) {
    return String(value ?? '').replace(/\d/g, function (d) {
        return '۰۱۲۳۴۵۶۷۸۹'[d];
    });
}

function formatPersianMoney(value) {
    var num = Number(String(value).replace(/[^\d.-]/g, ''));
    if (Number.isNaN(num)) return toPersianDigits(value);
    return toPersianDigits(num.toLocaleString('en-US'));
}

function buildInvoiceHtml({ restaurant, orderNumber, tableNumber, orderStatus,
    createdAt, updatedAt, description, items, totalText,
    customerName, customerMobile, now }) {

    const css = `
        :root {
            --ink: #0f172a;
            --muted: #64748b;
            --line: #e2e8f0;
            --soft: #f8fafc;
            --accent: #ff7a00;
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
            border-bottom: 1px solid var(--line);
        }
        .restaurant-name { font-size: 22px; font-weight: 800; }
        .doc-label {
            display: inline-flex;
            margin-top: 6px;
            padding: 3px 10px;
            border-radius: 999px;
            background: #fff7ed;
            color: #c2410c;
            font-size: 12px;
            font-weight: 700;
        }
        .order-meta { text-align: left; }
        .order-number { font-size: 18px; font-weight: 800; }
        .order-number span {
            display: block;
            color: var(--muted);
            font-size: 12px;
            font-weight: 600;
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
        .info-item { display: flex; flex-direction: column; gap: 2px; }
        .info-item .label { color: var(--muted); font-size: 11px; font-weight: 700; }
        .info-item .value { font-weight: 700; font-size: 13px; }
        .section-title {
            font-size: 12px;
            font-weight: 800;
            color: var(--muted);
            margin: 4px 0 10px;
        }
        .items-table { width: 100%; border-collapse: collapse; }
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
        }
        .items-table td.name { text-align: right; font-weight: 700; }
        .items-table tbody tr:nth-child(even) { background: #fbfdff; }
        .total {
            margin-top: 18px;
            padding: 14px;
            border-radius: 14px;
            background: linear-gradient(135deg, #fff7ed, #ffedd5);
            border: 1px solid #fed7aa;
            text-align: center;
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
        @media print {
            body { background: #fff; padding: 0; }
            .sheet { border: none; border-radius: 0; box-shadow: none; max-width: none; }
            .brand-bar, .items-table th, .total, .doc-label, .info-grid {
                print-color-adjust: exact;
                -webkit-print-color-adjust: exact;
            }
        }
    `;

    const itemRows = (items || []).map(item => {
        const priceText = String(item.price || '').replace(/,/g, '');
        const originalText = String(item.originalPrice || '').replace(/,/g, '');
        const priceNum = parseFloat(priceText) || 0;
        const originalNum = parseFloat(originalText) || 0;
        const hasDiscount = originalNum > 0 && originalNum > priceNum;
        const unitPriceHtml = hasDiscount
            ? `<span style="display:block;text-decoration:line-through;color:#94a3b8;font-size:11px;font-weight:600;">${toPersianDigits(item.originalPrice)}</span><span>${toPersianDigits(item.price)}</span>`
            : toPersianDigits(item.price);
        return `
        <tr>
            <td class="name">${escapeHtml(item.name)}</td>
            <td>${toPersianDigits(item.quantity)}</td>
            <td>${unitPriceHtml}</td>
        </tr>`;
    }).join('');

    return `<!DOCTYPE html>
<html lang="fa" dir="rtl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>فاکتور سفارش ${toPersianDigits(orderNumber)}</title>
    <style>${css}</style>
</head>
<body>
    <div class="sheet">
        <div class="brand-bar"></div>
        <div class="header">
            <div>
                <div class="restaurant-name">${escapeHtml(restaurant)}</div>
                <div class="doc-label">فاکتور فروش</div>
            </div>
            <div class="order-meta">
                <div class="order-number">
                    <span>شماره سفارش</span>
                    ${toPersianDigits(orderNumber)}
                </div>
            </div>
        </div>
        <div class="body">
            <div class="info-grid">
                <div class="info-item"><span class="label">میز</span><span class="value">${toPersianDigits(tableNumber) || '—'}</span></div>
                <div class="info-item"><span class="label">وضعیت</span><span class="value">${escapeHtml(orderStatus)}</span></div>
                <div class="info-item"><span class="label">تاریخ ثبت</span><span class="value">${toPersianDigits(createdAt)}</span></div>
                ${updatedAt ? `<div class="info-item"><span class="label">آخرین تغییر</span><span class="value">${toPersianDigits(updatedAt)}</span></div>` : ''}
            </div>

            ${description ? `<div class="section-title">توضیحات</div><div style="margin-bottom:14px;color:#475569;">${escapeHtml(description)}</div>` : ''}

            <div class="section-title">اقلام سفارش</div>
            <table class="items-table">
                <thead>
                    <tr>
                        <th>نام غذا</th>
                        <th>تعداد</th>
                        <th>قیمت واحد (تومان)</th>
                    </tr>
                </thead>
                <tbody>${itemRows || '<tr><td colspan="3">آیتمی ثبت نشده است</td></tr>'}</tbody>
            </table>

            <div class="total">${toPersianDigits(totalText)}</div>

            <div class="customer">
                ${customerName ? `<div><strong>مشتری:</strong> ${escapeHtml(customerName)}</div>` : ''}
                ${customerMobile ? `<div><strong>تلفن:</strong> ${toPersianDigits(customerMobile)}</div>` : ''}
            </div>

            <div class="footer">
                <div class="thanks">از اعتماد شما سپاسگزاریم</div>
                <div>چاپ شده در: ${toPersianDigits(now)}</div>
                <div>سیستم مدیریت رستورانیار</div>
            </div>
        </div>
    </div>
</body>
</html>`;
}

function openPrintWindow(htmlContent) {
    const printWindow = window.open('', '_blank', 'width=900,height=700,scrollbars=yes');
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
