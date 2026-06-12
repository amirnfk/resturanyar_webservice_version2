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

// اجرای اسکریپت‌های داخل پارشیال پس از تزریق HTML
function replaceAndExecuteScripts(container) {
    if (!container) return;
    const scripts = container.querySelectorAll('script');
    scripts.forEach(oldScript => {
        const newScript = document.createElement('script');
        // کپی ویژگی‌ها (src و ...)
        [...oldScript.attributes].forEach(attr => newScript.setAttribute(attr.name, attr.value));

        // اگر Chart.js قبلا لود شده، دوباره لود نکن
        const isChartJs = newScript.src && /chart(\.umd)?\.min\.js/i.test(newScript.src);
        if (isChartJs && window.Chart) {
            oldScript.remove();
            return;
        }

        // کپی محتوای inline
        if (!newScript.src) {
            newScript.text = oldScript.text || oldScript.textContent || '';
        }

        // جایگزینی و اجرا
        oldScript.parentNode.replaceChild(newScript, oldScript);
    });
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
            const parts = faDate.split('/');
            if (parts.length !== 3) return;
            const [jy, jm, jd] = parts.map(Number);
            if (isNaN(jy) || isNaN(jm) || isNaN(jd)) return;
            const [gy, gm, gd] = jalali_to_gregorian(jy, jm, jd);
            const iso = `${gy}-${String(gm).padStart(2, '0')}-${String(gd).padStart(2, '0')}`;
            if (newInput.id === "fromFa") document.getElementById("fromIso").value = iso;
            if (newInput.id === "toFa") document.getElementById("toIso").value = iso;
            // با تغییر تاریخ‌ها لینک خروجی را آپدیت کن
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

function setupEventListeners() {
    const form = document.getElementById('reportFilterForm');
    if (!form) return;

    removeAllEventListeners();

    // ارسال فرم به صورت AJAX
    registerListener(form, 'submit', function (e) {
        e.preventDefault();
        const url = `${form.action}?${getFormQuery(form)}`;
        loadReports(url);
    });

    // تغییر هر فیلد فرم => به‌روزرسانی لینک خروجی
    registerListener(form, 'change', function () {
        wireExportLink();
    });

    // فیلترهای سریع
    document.querySelectorAll('.quick-filter-btn').forEach(btn => {
        registerListener(btn, 'click', function (e) {
            e.preventDefault();
            try {
                const u = new URL(btn.href, window.location.origin);
                const p = u.searchParams.get('period') || '';
                const periodInput = document.getElementById('periodInput');
                if (periodInput) periodInput.value = p;
            } catch { }
            loadReports(btn.href);
        });
    });
}

async function loadReports(url) {
    showLoading();
    destroyAllCharts();
    try {
        const res = await fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            cache: 'no-cache'
        });
        if (res.ok) {
            const html = await res.text();
            const container = document.getElementById('reportsContainer');
            container.innerHTML = html;

            // اجرای اسکریپت‌های داخل پارشیال (برای ساخت نمودارها)
            replaceAndExecuteScripts(container);

            initializeDatepickers();
            wireExportLink();
            setupEventListeners();
            if (window.gc) window.gc();
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

// wireExportLink relies on window.__exportExcelUrl injected by the Razor view
function wireExportLink() {
    const form = document.getElementById('reportFilterForm');
    if (!form) return;
    const params = new URLSearchParams(new FormData(form));
    const exp = document.getElementById('exportBtn');
    if (exp && window.__exportExcelUrl) {
        exp.href = `${window.__exportExcelUrl}?${params.toString()}`;
    }
}

document.addEventListener('DOMContentLoaded', () => {
    initializeDatepickers();
    wireExportLink();
    setupEventListeners();
    window.addEventListener('beforeunload', () => {
        destroyAllCharts();
        removeAllEventListeners();
    });
});