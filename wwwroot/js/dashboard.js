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
});


 