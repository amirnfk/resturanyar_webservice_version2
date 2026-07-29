window.initManagerReportCharts = function initManagerReportCharts() {
    if (!window.chartInstances) window.chartInstances = {};
    if (typeof Chart === 'undefined') return;

    const data = window.managerReportData || {};
    const {
        labels = [],
        revenues = [],
        orders = [],
        hourLabels = [],
        hourOrders = [],
        hourRevenues = [],
        statusLabels = [],
        statusValues = [],
        statusBg = [],
        topQtyLabels = [],
        topQtyValues = [],
        topRevLabels = [],
        topRevValues = [],
        chargeLabels = [],
        chargeValues = []
    } = data;

    const hasSalesData = labels.length > 0 && revenues.length > 0;
    const hasStatusData = statusLabels.length > 0;
    const hasQtyData = topQtyLabels.length > 0;
    const hasRevData = topRevLabels.length > 0;
    const hasChargeData = chargeLabels.length > 0 && chargeValues.some(function (v) { return Number(v) !== 0; });
    const hasPeakData = hourOrders.some(function (v) { return Number(v) > 0; });

    function mkCurrency(v) {
        try {
            return new Intl.NumberFormat('fa-IR').format(v);
        } catch {
            return (v ?? 0).toString().replace(/\B(?=(\d{3})+(?!\d))/g, ",");
        }
    }

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

    ['salesChart', 'statusChart', 'topQtyChart', 'topRevChart', 'chargeBreakdownChart', 'peakHoursChart'].forEach(id => {
        if (window.chartInstances[id]) {
            window.chartInstances[id].destroy();
            delete window.chartInstances[id];
        }
    });

    const chargePalette = ['#ff7a00', '#0d6efd', '#20c997', '#6f42c1', '#fd7e14', '#dc3545', '#198754', '#6610f2'];

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

    if (hasChargeData) {
        const ctx = document.getElementById('chargeBreakdownChart');
        if (ctx) {
            window.chartInstances.chargeBreakdownChart = new Chart(ctx, {
                type: 'doughnut',
                data: {
                    labels: chargeLabels,
                    datasets: [{
                        data: chargeValues,
                        backgroundColor: chargeLabels.map((_, i) => chargePalette[i % chargePalette.length]),
                        borderWidth: 1,
                        borderColor: '#fff'
                    }]
                },
                options: applyFontOptions({
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'bottom', labels: { padding: 12, usePointStyle: true } },
                        tooltip: {
                            callbacks: {
                                label: (ctx) => `${ctx.label}: ${mkCurrency(ctx.parsed)} تومان`
                            }
                        }
                    }
                })
            });
        }
    }

    if (hasPeakData) {
        const ctx = document.getElementById('peakHoursChart');
        if (ctx) {
            window.chartInstances.peakHoursChart = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: hourLabels,
                    datasets: [{
                        label: 'تعداد سفارش',
                        data: hourOrders,
                        backgroundColor: 'rgba(255, 122, 0, 0.75)',
                        borderRadius: 4,
                        borderWidth: 0
                    }]
                },
                options: applyFontOptions({
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: {
                            callbacks: {
                                label: (ctx) => {
                                    const idx = ctx.dataIndex;
                                    const rev = hourRevenues[idx] ?? 0;
                                    return [
                                        `سفارش: ${ctx.parsed.y}`,
                                        `مبلغ: ${mkCurrency(rev)} تومان`
                                    ];
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            ticks: {
                                maxRotation: 0,
                                autoSkip: true,
                                maxTicksLimit: 12
                            }
                        },
                        y: {
                            beginAtZero: true,
                            ticks: { precision: 0 }
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
                options: applyFontOptions({
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'bottom', labels: { padding: 15, usePointStyle: true } }
                    }
                })
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
                options: applyFontOptions({
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { beginAtZero: true, ticks: { precision: 0 } }
                    }
                })
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
                options: applyFontOptions({
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
                })
            });
        }
    }
};
