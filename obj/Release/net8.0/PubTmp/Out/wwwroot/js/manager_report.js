// Global storage
window.chartInstances = {};
window.eventListeners = [];

function destroyAllCharts() {
    Object.values(window.chartInstances).forEach(chart => {
        if (chart && typeof chart.destroy === 'function') chart.destroy();
    });
    window.chartInstances = {};
}

function removeAllEventListeners() {
    window.eventListeners.forEach(item => {
        if (item.element && item.event && item.handler) {
            item.element.removeEventListener(item.event, item.handler);
        }
    });
    window.eventListeners = [];
}

// عدم تداخل با window.addEventListener
function registerListener(element, event, handler) {
    element.addEventListener(event, handler);
    window.eventListeners.push({ element, event, handler });
}

// اجرای اسکریپت‌های inline داخل پارشیال
function runInlineScripts(container) {
    if (!container) return;

    container.querySelectorAll('script').forEach(function (oldScript) {
        const code = oldScript.textContent || '';
        oldScript.remove();
        if (!code.trim()) return;

        const script = document.createElement('script');
        script.textContent = code;
        document.body.appendChild(script);
        script.remove();
    });
}

function sanitizePartialHtml(html) {
    const doc = new DOMParser().parseFromString(html, 'text/html');
    doc.querySelectorAll('link, script[src], style').forEach(function (node) {
        node.remove();
    });
    return doc.body.innerHTML;
}

function jalali_to_gregorian(jy, jm, jd) {
    jy -= 979; jm -= 1; jd -= 1;
    var j_day_no = 365 * jy + Math.floor(jy / 33) * 8 + Math.floor(((jy % 33) + 3) / 4);
    var j_days = [31, 31, 31, 31, 31, 31, 30, 30, 30, 30, 30, 29];
    for (var i = 0; i < jm; ++i) j_day_no += j_days[i];
    j_day_no += jd;
    var g_day_no = j_day_no + 79;
    var gy = 1600 + 400 * Math.floor(g_day_no / 146097); g_day_no %= 146097;
    var leap = true;
    if (g_day_no >= 36525) { g_day_no--; gy += 100 * Math.floor(g_day_no / 36524); g_day_no %= 36524; if (g_day_no >= 365) g_day_no++; else leap = false; }
    gy += 4 * Math.floor(g_day_no / 1461); g_day_no %= 1461;
    if (g_day_no >= 366) { leap = false; g_day_no--; gy += Math.floor(g_day_no / 365); g_day_no %= 365; }
    var g_days = [31, (leap ? 29 : 28), 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    var gm; for (gm = 0; g_day_no >= g_days[gm]; gm++) g_day_no -= g_days[gm];
    return [gy, gm + 1, g_day_no + 1];
}

function jalaliFaToIso(faDate) {
    if (!faDate) return '';
    const parts = faDate.split('/');
    if (parts.length !== 3) return '';
    const [jy, jm, jd] = parts.map(Number);
    if (isNaN(jy) || isNaN(jm) || isNaN(jd)) return '';
    const [gy, gm, gd] = jalali_to_gregorian(jy, jm, jd);
    return `${gy}-${String(gm).padStart(2, '0')}-${String(gd).padStart(2, '0')}`;
}

function clearQuickFilterActiveState() {
    document.querySelectorAll('.quick-filter-btn').forEach(btn => btn.classList.remove('active'));
}

function setActiveQuickFilter(period) {
    clearQuickFilterActiveState();
    if (!period) return;
    const btn = document.querySelector(`.quick-filter-btn[data-period="${period}"]`);
    if (btn) btn.classList.add('active');
}

function clearCustomDateFields() {
    ['fromIso', 'toIso', 'fromFa', 'toFa'].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.value = '';
    });
}

function syncCustomDateFields() {
    const fromFa = document.getElementById('fromFa');
    const toFa = document.getElementById('toFa');
    const fromIso = document.getElementById('fromIso');
    const toIso = document.getElementById('toIso');
    const periodInput = document.getElementById('periodInput');

    if (fromFa && fromIso && fromFa.value.trim()) {
        fromIso.value = jalaliFaToIso(fromFa.value.trim());
    }
    if (toFa && toIso && toFa.value.trim()) {
        toIso.value = jalaliFaToIso(toFa.value.trim());
    }

    if (periodInput && ((fromIso && fromIso.value) || (toIso && toIso.value))) {
        periodInput.value = '';
    }
}

function initializeDatepickers() {
    if (!window.datepickerInitialized) {
        jalaliDatepicker.startWatch({ time: false });
        window.datepickerInitialized = true;
    }
    document.querySelectorAll("input[data-jdp]").forEach(input => {
        const newInput = input.cloneNode(true);
        input.parentNode.replaceChild(newInput, input);
        newInput.addEventListener("change", () => {
            const faDate = newInput.value;
            if (!faDate) return;
            const iso = jalaliFaToIso(faDate);
            if (!iso) return;
            if (newInput.id === "fromFa") document.getElementById("fromIso").value = iso;
            if (newInput.id === "toFa") document.getElementById("toIso").value = iso;

            const periodInput = document.getElementById('periodInput');
            if (periodInput) periodInput.value = '';
            clearQuickFilterActiveState();
            wireExportLink();
        });
    });
}

function getFormQuery(form) {
    const data = new FormData(form);
    const params = new URLSearchParams();
    for (const [k, v] of data.entries()) {
        if ((v ?? "").toString().trim() !== "") params.append(k, v);
    }
    return params.toString();
}

function showLoading() {
    const overlay = document.querySelector('.loading-overlay');
    if (overlay) overlay.style.display = 'flex';
}

function hideLoading() {
    const overlay = document.querySelector('.loading-overlay');
    if (overlay) overlay.style.display = 'none';
}

function showExportLoading(format) {
    const overlay = document.getElementById('exportLoadingOverlay');
    if (!overlay) return;
    const textEl = overlay.querySelector('.export-loading-text');
    if (textEl) {
        textEl.textContent = format === 'pdf'
            ? 'در حال آماده‌سازی فایل PDF...'
            : 'در حال آماده‌سازی فایل اکسل...';
    }
    overlay.style.display = 'flex';
    overlay.setAttribute('aria-hidden', 'false');
}

function hideExportLoading() {
    const overlay = document.getElementById('exportLoadingOverlay');
    if (!overlay) return;
    overlay.style.display = 'none';
    overlay.setAttribute('aria-hidden', 'true');
}

function setupEventListeners() {
    const form = document.getElementById('reportFilterForm');
    if (!form) return;

    removeAllEventListeners();

    // ارسال فرم به صورت AJAX
    registerListener(form, 'submit', function (e) {
        e.preventDefault();
        syncCustomDateFields();
        const fromIso = document.getElementById('fromIso');
        const toIso = document.getElementById('toIso');
        const hasCustomDates = (fromIso && fromIso.value) || (toIso && toIso.value);
        if (hasCustomDates) {
            setActiveQuickFilter('custom');
        }
        const url = `${form.action}?${getFormQuery(form)}`;
        loadReports(url);
    });

    // تغییر هر فیلد فرم => به‌روزرسانی لینک خروجی
    registerListener(form, 'change', function () {
        wireExportLink();
    });

    const exportBtn = document.getElementById('exportBtn');
    if (exportBtn) {
        registerListener(exportBtn, 'click', openExportFormatModal);
    }

    const exportExcel = document.getElementById('exportFormatExcel');
    if (exportExcel) {
        registerListener(exportExcel, 'click', (e) => handleExportClick(e, 'excel'));
    }

    const exportPdf = document.getElementById('exportFormatPdf');
    if (exportPdf) {
        registerListener(exportPdf, 'click', (e) => handleExportClick(e, 'pdf'));
    }

    // فیلترهای سریع
    document.querySelectorAll('.quick-filter-btn').forEach(btn => {
        registerListener(btn, 'click', function (e) {
            e.preventDefault();
            if (btn.id === 'customRangeBtn') return;
            clearCustomDateFields();
            let period = btn.dataset.period || '';
            try {
                const u = new URL(btn.href, window.location.origin);
                period = u.searchParams.get('period') || period;
                const periodInput = document.getElementById('periodInput');
                if (periodInput) periodInput.value = period;
            } catch { }
            setActiveQuickFilter(period);
            loadReports(btn.href);
        });
    });
}

async function loadReports(url) {
    showLoading();
    destroyAllCharts();
    try {
        const res = await fetch(url, {
            headers: {
                'X-Requested-With': 'XMLHttpRequest',
                'X-Reports-Partial': 'true'
            },
            cache: 'no-cache'
        });
        if (res.ok) {
            const html = await res.text();
            const container = document.getElementById('reportsContainer');
            container.innerHTML = sanitizePartialHtml(html);
            runInlineScripts(container);

            if (typeof window.initManagerReportCharts === 'function') {
                window.initManagerReportCharts();
            }

            initializeDatepickers();
            wireExportLink();
            setupEventListeners();
        } else {
            console.error('Error loading reports:', res.status);
            document.getElementById('reportsContainer').innerHTML =
                '<div class="no-data-message">خطا در بارگذاری داده‌ها. لطفا دوباره تلاش کنید.</div>';
        }
    } catch (error) {
        console.error('Error loading reports:', error);
        document.getElementById('reportsContainer').innerHTML =
            '<div class="no-data-message">خطا در ارتباط با سرور. لطفا دوباره تلاش کنید.</div>';
    } finally {
        hideLoading();
    }
}

// wireExportLink stores resolved export URLs for the format chooser
function wireExportLink() {
    const form = document.getElementById('reportFilterForm');
    if (!form) return;
    const query = getFormQuery(form);
    if (window.__exportExcelUrl) {
        window.__exportExcelHref = `${window.__exportExcelUrl}?${query}`;
    }
    if (window.__exportPdfUrl) {
        window.__exportPdfHref = `${window.__exportPdfUrl}?${query}`;
    }
}

function openExportFormatModal(e) {
    if (e) e.preventDefault();
    syncCustomDateFields();
    wireExportLink();

    const modalEl = document.getElementById('exportFormatModal');
    if (!modalEl || typeof bootstrap === 'undefined') {
        handleExportClick(e || { preventDefault() {} }, 'excel');
        return;
    }
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function showExportNoOrdersModal(message) {
    const modalEl = document.getElementById('exportNoOrdersModal');
    if (!modalEl || typeof bootstrap === 'undefined') {
        if (typeof window.showToast === 'function') {
            window.showToast(message || 'هیچ سفارشی در این بازه زمانی یافت نشد.', 'error');
        }
        return;
    }

    const msgEl = document.getElementById('exportNoOrdersMessage');
    if (msgEl) {
        msgEl.textContent = message || 'هیچ سفارشی در این بازه زمانی یافت نشد.';
    }

    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function getExportFileName(contentDisposition, fallbackName) {
    if (!contentDisposition) return fallbackName;

    const utf8Match = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);
    if (utf8Match && utf8Match[1]) {
        try {
            return decodeURIComponent(utf8Match[1].trim());
        } catch {
            return utf8Match[1].trim();
        }
    }

    const match = contentDisposition.match(/filename="?([^";]+)"?/i);
    return match && match[1] ? match[1].trim() : fallbackName;
}

async function handleExportClick(e, format) {
    if (e) e.preventDefault();

    syncCustomDateFields();
    wireExportLink();

    const isPdf = format === 'pdf';
    const exportUrl = isPdf ? window.__exportPdfHref : window.__exportExcelHref;
    if (!exportUrl) return;

    const formatModalEl = document.getElementById('exportFormatModal');
    if (formatModalEl && typeof bootstrap !== 'undefined') {
        const formatModal = bootstrap.Modal.getInstance(formatModalEl);
        if (formatModal) formatModal.hide();
    }

    const exportBtn = document.getElementById('exportBtn');
    if (exportBtn) {
        exportBtn.classList.add('disabled');
        exportBtn.setAttribute('aria-disabled', 'true');
        exportBtn.disabled = true;
    }
    showExportLoading(format);

    try {
        const res = await fetch(exportUrl, { credentials: 'same-origin' });
        if (!res.ok) {
            const message = (await res.text()).trim() || 'هیچ سفارشی در این بازه زمانی یافت نشد.';
            showExportNoOrdersModal(message);
            return;
        }

        const blob = await res.blob();
        const fileName = getExportFileName(
            res.headers.get('Content-Disposition'),
            isPdf ? 'OrdersReport.pdf' : 'OrdersReport.xlsx'
        );
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Export error:', error);
        showExportNoOrdersModal(
            isPdf
                ? 'خطا در دانلود فایل PDF. لطفا دوباره تلاش کنید.'
                : 'خطا در دانلود فایل اکسل. لطفا دوباره تلاش کنید.'
        );
    } finally {
        hideExportLoading();
        if (exportBtn) {
            exportBtn.classList.remove('disabled');
            exportBtn.removeAttribute('aria-disabled');
            exportBtn.disabled = false;
        }
    }
}

function initManagerReportsPage() {
    if (!document.getElementById('reportFilterForm')) return;

    initializeDatepickers();
    wireExportLink();
    setupEventListeners();
    if (typeof window.initManagerReportCharts === 'function') {
        window.initManagerReportCharts();
    }
}

window.initManagerReportsPage = initManagerReportsPage;