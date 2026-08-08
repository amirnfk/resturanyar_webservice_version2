// Theme Manager (Existing Code)
const html = document.documentElement;
const key = "ry-theme";
try {
    const saved = localStorage.getItem(key);
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (saved) html.setAttribute('data-theme', saved);
    else if (prefersDark) html.setAttribute('data-theme', 'dark');
} catch (e) { }

const btn = document.getElementById('themeToggle');
const setIcon = () => {
    if (!btn) return;
    const dark = html.getAttribute('data-theme') === 'dark';
    btn.innerHTML = dark ? '<i class="fa-solid fa-sun"></i>' : '<i class="fa-solid fa-moon"></i>';
};
setIcon();
btn?.addEventListener('click', () => {
    const current = html.getAttribute('data-theme') || 'light';
    const next = current === 'dark' ? 'light' : 'dark';
    html.setAttribute('data-theme', next);
    try { localStorage.setItem(key, next); } catch (e) { }
    setIcon();
    // به‌روزرسانی رنگ چارت‌ها در صورت تغییر تم
    updateChartsTheme();
});

// Clock (Existing Code)
(function clock() {
    const el = document.getElementById('dashClock');
    if (!el) return;
    const tick = () => {
        const d = new Date();
        const df = new Intl.DateTimeFormat('fa-IR', { weekday: 'long', hour: '2-digit', minute: '2-digit' });
        el.textContent = df.format(d);
    };
    tick();
    setInterval(tick, 30000);
})();

// Reveal on scroll (Existing Code)
(function () {
    const els = document.querySelectorAll('.fade-up');
    if (!('IntersectionObserver' in window)) {
        els.forEach(e => e.classList.add('revealed'));
        return;
    }
    const io = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting) { e.target.classList.add('revealed'); io.unobserve(e.target); }
        });
    }, { threshold: 0.12 });
    els.forEach(el => io.observe(el));
})();

// Counters (Existing Code)
(function () {
    const counters = document.querySelectorAll('.num[data-count]');
    if (!counters.length) return;
    const easeOutCubic = t => 1 - Math.pow(1 - t, 3);

    function animate(el) {
        const end = +el.dataset.count || 0;
        const dur = +el.dataset.duration || 1200;
        const start = performance.now();

        function step(ts) {
            const p = Math.min(1, (ts - start) / dur);
            const eased = easeOutCubic(p);
            const val = Math.round(end * (0.1 + 0.9 * eased));
            el.textContent = new Intl.NumberFormat('fa-IR').format(val);
            if (p < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    }

    const io = new IntersectionObserver((entries, obs) => {
        entries.forEach(e => {
            if (e.isIntersecting) { animate(e.target); obs.unobserve(e.target); }
        });
    }, { threshold: 0.4 });
    counters.forEach(el => io.observe(el));
})();

// Ripple Effect for Tiles
(function () {
    const tiles = document.querySelectorAll('.tile');
    tiles.forEach(tile => {
        tile.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); tile.click(); }
        });
        tile.addEventListener('click', (e) => {
            const r = tile.querySelector('.ripple');
            if (!r) return;
            const rect = tile.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            r.style.width = r.style.height = size + 'px';
            r.style.left = (e.clientX - rect.left - size / 2) + 'px';
            r.style.top = (e.clientY - rect.top - size / 2) + 'px';
            r.classList.remove('show');
            void r.offsetWidth;
            r.classList.add('show');
        });
    });
})();

// ============================================
// NEW: Chart.js Configuration (Based on Real Model Data)
// ============================================
let orderStatusChartInstance = null;

function initCharts() {
    const isDark = html.getAttribute('data-theme') === 'dark';
    const textColor = isDark ? '#e9eef7' : '#1a1a2e';

    // 1. Order Status Chart (Donut Chart)
    // داده‌های این چارت از ویوو به صورت عدد به جاوااسکریپت منتقل می‌شود
    // برای سادگی، ما مقادیر را از DOM می‌خوانیم، اما شما می‌توانید آن‌ها را به یک متغیر جاوااسکریپت در ویوو پاس دهید
    const ctx = document.getElementById('orderStatusChart');
    if (ctx) {
        // خواندن داده‌های واقعی از المان‌های DOM
        const labels = ['دریافت شده', 'در حال آماده سازی', 'تحویل شده'];
        // رنگ‌ها: نارنجی، فیروزه‌ای، سبز
        const colors = ['#ff7a1a', '#2196F3', '#2dce89'];

        // دریافت مقادیر از لیبل‌های موجود در صفحه (برای اطمینان از داده‌های واقعی)
        // اما چون خواندن از DOM در زمان ایجاد چارت ممکن است مقداری پیچیده باشد، 
        // من از data-attribute استفاده می‌کنم که در ویوو به آنها پاس داده‌ام.
        // فرض کنید در ویوو به این المان‌ها داده پاس داده‌اید.
        // برای جلوگیری از پیچیدگی، می‌توانید مقادیر را در یک آرایه در ویوو به جاوااسکریپت پاس دهید.
        // در اینجا من یک روش ساده‌تر برای خواندن مقادیر از کارت مربوطه پیاده‌سازی کرده‌ام:
        const legendItems = ctx.closest('.kpi-card').querySelectorAll('.legend-grid strong');
        let data = [];
        legendItems.forEach(el => data.push(parseInt(el.textContent.trim()) || 0));

        // اگر به هر دلیلی خوانده نشد (برای جلوگیری از خطای چارت)
        if (data.length === 0) data = [0, 0, 0];

        if (orderStatusChartInstance) orderStatusChartInstance.destroy();
        orderStatusChartInstance = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colors,
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '70%',
                plugins: { legend: { display: false }, tooltip: { enabled: true } }
            }
        });
    }
}

function updateChartsTheme() {
    // به‌روزرسانی رنگ‌ها در صورت تغییر تم (اختیاری)
    initCharts();
}

// Initialize charts after DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    setTimeout(initCharts, 200); // کمی تاخیر برای بارگذاری کامل DOM
    initDashStockCard();
});

// ============================================
// Inventory critical-stock card (API summary)
// ============================================
function initDashStockCard() {
    const card = document.getElementById('dashStockCard');
    if (!card) return;

    const restaurantId = card.dataset.restaurantId;
    if (!restaurantId) return;

    const inventoryUrl = card.dataset.inventoryUrl || '/Home/Inventory';
    const lowStockUrl = card.dataset.lowStockUrl || '/Home/InventoryLowStock';

    waitForFetchWithAuth(10000)
        .then(function (fetchAuth) {
            return fetchAuth('/api/v2/inventory/summary?restaurantId=' + encodeURIComponent(restaurantId), {
                headers: { 'Accept': 'application/json' }
            });
        })
        .then(function (res) { return res.json().catch(function () { return {}; }); })
        .then(function (json) {
            if (!json || json.success === false || !json.data) {
                card.hidden = true;
                return;
            }
            renderDashStockCard(card, json.data, inventoryUrl, lowStockUrl);
        })
        .catch(function () {
            card.hidden = true;
        });
}

function waitForFetchWithAuth(timeoutMs) {
    timeoutMs = timeoutMs || 10000;
    if (typeof window.fetchWithAuth === 'function') {
        return Promise.resolve(window.fetchWithAuth);
    }
    return new Promise(function (resolve, reject) {
        var start = Date.now();
        var timer = setInterval(function () {
            if (typeof window.fetchWithAuth === 'function') {
                clearInterval(timer);
                resolve(window.fetchWithAuth);
            } else if (Date.now() - start > timeoutMs) {
                clearInterval(timer);
                reject(new Error('auth not ready'));
            }
        }, 40);
    });
}

function formatDashQty(n) {
    return Number(n).toLocaleString('fa-IR', { maximumFractionDigits: 3 });
}

function escapeDashHtml(s) {
    return String(s ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

function renderDashStockCard(card, data, inventoryUrl, lowStockUrl) {
    if (!data.isEnabled) {
        card.hidden = true;
        return;
    }

    const lowCount = Number(data.lowStockCount) || 0;
    const items = Array.isArray(data.lowStockItems) ? data.lowStockItems : [];
    const isCritical = lowCount > 0;

    card.classList.toggle('dash-stock-card--critical', isCritical);
    card.classList.toggle('dash-stock-card--ok', !isCritical);
    card.hidden = false;

    if (!isCritical) {
        card.innerHTML =
            '<div class="dash-stock-card__inner dash-stock-card__inner--ok">' +
                '<div class="dash-stock-card__lead">' +
                    '<div class="dash-stock-card__icon" aria-hidden="true"><i class="fa-solid fa-circle-check"></i></div>' +
                    '<div class="dash-stock-card__copy">' +
                        '<div class="dash-stock-card__title-row">' +
                            '<h2 class="dash-stock-card__title">وضعیت انبار</h2>' +
                            '<span class="dash-stock-card__badge dash-stock-card__badge--ok">مطلوب</span>' +
                        '</div>' +
                        '<p class="dash-stock-card__sub">همه موجودی‌ها بالای حداقل هستند و نیازی به توجه فوری نیست.</p>' +
                    '</div>' +
                '</div>' +
                '<a class="dash-stock-card__cta" href="' + escapeDashHtml(inventoryUrl) + '">' +
                    '<span>مدیریت انبار</span>' +
                    '<i class="fa-solid fa-arrow-left-long" aria-hidden="true"></i>' +
                '</a>' +
            '</div>';
        revealDashStockCard(card);
        return;
    }

    const countLabel = formatDashQty(lowCount) + ' مورد';
    const chips = items.map(function (item, index) {
        const current = Number(item.currentQuantity) || 0;
        const minimum = Number(item.minimumQuantity) || 0;
        const ratio = minimum > 0 ? Math.max(0, Math.min(1, current / minimum)) : 0;
        const unit = item.unitNameFa || item.unit || '';
        const severity = ratio <= 0.25 ? 'critical' : (ratio <= 0.6 ? 'warn' : 'low');
        return (
            '<div class="dash-stock-chip dash-stock-chip--' + severity + '" style="--chip-i:' + index + '">' +
                '<div class="dash-stock-chip__head">' +
                    '<span class="dash-stock-chip__name">' + escapeDashHtml(item.name) + '</span>' +
                    (unit ? '<span class="dash-stock-chip__unit">' + escapeDashHtml(unit) + '</span>' : '') +
                '</div>' +
                '<div class="dash-stock-chip__meta">' +
                    '<span class="dash-stock-chip__current" dir="ltr">' + formatDashQty(current) + '</span>' +
                    '<span class="dash-stock-chip__sep">از</span>' +
                    '<span class="dash-stock-chip__min" dir="ltr">' + formatDashQty(minimum) + '</span>' +
                '</div>' +
                '<div class="dash-stock-chip__bar" aria-hidden="true">' +
                    '<span class="dash-stock-chip__fill" style="width:' + (ratio * 100).toFixed(1) + '%"></span>' +
                '</div>' +
            '</div>'
        );
    }).join('');

    card.innerHTML =
        '<div class="dash-stock-card__inner">' +
            '<div class="dash-stock-card__lead">' +
                '<div class="dash-stock-card__icon" aria-hidden="true"><i class="fa-solid fa-triangle-exclamation"></i></div>' +
                '<div class="dash-stock-card__copy">' +
                    '<div class="dash-stock-card__title-row">' +
                        '<h2 class="dash-stock-card__title">کمبود موجودی انبار</h2>' +
                        '<span class="dash-stock-card__badge">' + escapeDashHtml(countLabel) + '</span>' +
                    '</div>' +
                    '<p class="dash-stock-card__sub">مواد اولیه‌ای که به حداقل موجودی رسیده‌اند یا کمتر هستند.</p>' +
                '</div>' +
            '</div>' +
            (chips ? '<div class="dash-stock-card__chips">' + chips + '</div>' : '<div class="dash-stock-card__chips"></div>') +
            '<a class="dash-stock-card__cta" href="' + escapeDashHtml(lowStockUrl) + '">' +
                '<span>مشاهده کمبودها</span>' +
                '<i class="fa-solid fa-arrow-left-long" aria-hidden="true"></i>' +
            '</a>' +
        '</div>';

    revealDashStockCard(card);
}

function revealDashStockCard(card) {
    requestAnimationFrame(function () {
        card.classList.add('revealed');
    });
}
