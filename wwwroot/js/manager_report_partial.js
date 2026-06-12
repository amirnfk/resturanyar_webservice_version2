(function () {
    if (!window.chartInstances) window.chartInstances = {};

    const data = window.managerReportData || {};
    const {
        labels = [],
        revenues = [],
        orders = [],
        statusLabels = [],
        statusValues = [],
        statusBg = [],
        topQtyLabels = [],
        topQtyValues = [],
        topRevLabels = [],
        topRevValues = []
    } = data;

    const hasSalesData = labels.length > 0 && revenues.length > 0;
    const hasStatusData = statusLabels.length > 0;
    const hasQtyData = topQtyLabels.length > 0;
    const hasRevData = topRevLabels.length > 0;

    // فرمت اعداد به تومان (فارسی)
    function mkCurrency(v) {
        try {
            return new Intl.NumberFormat('fa-IR').format(v);
        } catch {
            return (v ?? 0).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
        }
    }

    // فونت فارسی برای نمودارها
    const persianFont = {
        family: "'IRANYekan', 'IRANYekan', 'Tahoma', sans-serif",
        size: 11
    };

    function applyFontOptions(options) {
        if (!options) options = {};
        options.font = persianFont;
        if (!options.plugins) options.plugins = {};
        if (!options.plugins.legend) options.plugins.legend = {};
        if (!options.plugins.legend.labels) options.plugins.legend.labels = {};
        options.plugins.legend.labels.font = persianFont;
        if (!options.plugins.tooltip) options.plugins.tooltip = {};
        options.plugins.tooltip.titleFont = persianFont;
        options.plugins.tooltip.bodyFont = persianFont;
        if (options.scales) {
            if (options.scales.x && options.scales.x.ticks) options.scales.x.ticks.font = persianFont;
            if (options.scales.y1 && options.scales.y1.ticks) options.scales.y1.ticks.font = persianFont;
            if (options.scales.y2 && options.scales.y2.ticks) options.scales.y2.ticks.font = persianFont;
            if (options.scales.y && options.scales.y.ticks) options.scales.y.ticks.font = persianFont;
        }
        return options;
    }

    // نابودی نمودارهای قبلی
    ['salesChart', 'statusChart', 'topQtyChart', 'topRevChart'].forEach(id => {
        if (window.chartInstances[id]) {
            window.chartInstances[id].destroy();
            delete window.chartInstances[id];
        }
    });

    // نمودار روند فروش و تعداد سفارش
    if (hasSalesData) {
        const ctx = document.getElementById('salesChart');
        if (ctx) {
            window.chartInstances.salesChart = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: 'مبلغ فروش',
                            data: revenues,
                            yAxisID: 'y1',
                            borderColor: '#0d6efd',
                            backgroundColor: 'rgba(13,110,253,.1)',
                            tension: 0.3, fill: true, pointRadius: 3, pointHoverRadius: 5
                        },
                        {
                            label: 'تعداد سفارش',
                            data: orders,
                            yAxisID: 'y2',
                            borderColor: '#20c997',
                            backgroundColor: 'rgba(32,201,151,.1)',
                            tension: 0.3, fill: true, pointRadius: 3, pointHoverRadius: 5
                        }
                    ]
                },
                options: applyFontOptions({
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    scales: {
                        y1: {
                            type: 'linear', position: 'left',
                            ticks: { callback: (v) => mkCurrency(v) },
                            grid: { drawBorder: false }
                        },
                        y2: {
                            type: 'linear', position: 'right',
                            grid: { drawOnChartArea: false },
                            ticks: { precision: 0 }
                        }
                    },
                    plugins: {
                        legend: { labels: { usePointStyle: true, padding: 15 } },
                        tooltip: {
                            callbacks: {
                                label: (ctx) => {
                                    if (ctx.dataset.yAxisID === 'y1') {
                                        return `${ctx.dataset.label}: ${mkCurrency(ctx.parsed.y)} تومان`;
                                    }
                                    return `${ctx.dataset.label}: ${ctx.parsed.y}`;
                                }
                            }
                        }
                    }
                })
            });
        }
    }

    
    if (hasStatusData) {
        const ctx = document.getElementById('statusChart');
        if (ctx) {
            window.chartInstances.statusChart = new Chart(ctx, {
                type: 'doughnut',
                data: {
                    labels: statusLabels,
                    datasets: [{
                        data: statusValues,
                        backgroundColor: statusBg,
                        borderWidth: 1, borderColor: '#fff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'bottom', labels: { padding: 15, usePointStyle: true } }
                    }
                }
            });
        }
    }

    
    if (hasQtyData) {
        const ctx = document.getElementById('topQtyChart');
        if (ctx) {
            window.chartInstances.topQtyChart = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: topQtyLabels,
                    datasets: [{ label: 'تعداد', data: topQtyValues, backgroundColor: '#6f42c1', borderWidth: 0 }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { beginAtZero: true, ticks: { precision: 0 } }
                    }
                }
            });
        }
    }

    
    if (hasRevData) {
        const ctx = document.getElementById('topRevChart');
        if (ctx) {
            window.chartInstances.topRevChart = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: topRevLabels,
                    datasets: [{
                        label: 'مبلغ',
                        data: topRevValues,
                        backgroundColor: '#fd7e14',
                        borderWidth: 0
                    }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: { callbacks: { label: (ctx) => `مبلغ: ${mkCurrency(ctx.parsed.x)} تومان` } }
                    },
                    scales: {
                        x: { beginAtZero: true, ticks: { callback: (v) => mkCurrency(v) } }
                    }
                }
            });
        }
    }

    
    window.addEventListener('beforeunload', () => {
        Object.values(window.chartInstances).forEach(c => {
            if (c && typeof c.destroy === 'function') c.destroy();
        });
        window.chartInstances = {};
    });
})();