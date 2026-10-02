(function () {
    'use strict';

    function formatNumber(n) {
        try {
            return Number(n || 0).toLocaleString('fa-IR');
        } catch {
            return String(n || 0);
        }
    }

    function paymentLabel(method) {
        const map = {
            FreeTrial: 'فری‌ترایال',
            CafeBazar: 'کافه‌بازار',
            Zarinpal: 'زرین‌پال'
        };
        if (!method) return 'نامشخص';
        return map[method] || method;
    }

    function statusLabel(status) {
        if (status === 'Active') return 'فعال';
        if (status === 'Expired') return 'منقضی';
        return 'بدون اشتراک';
    }

    function statusClass(status) {
        if (status === 'Active') return 'ap-status--active';
        if (status === 'Expired') return 'ap-status--expired';
        return 'ap-status--none';
    }

    function initTabs() {
        const buttons = document.querySelectorAll('.ap-tab-btn');
        const panels = document.querySelectorAll('.ap-tab-panel');
        buttons.forEach(btn => {
            btn.addEventListener('click', () => {
                const target = btn.getAttribute('data-tab');
                buttons.forEach(b => b.classList.remove('is-active'));
                panels.forEach(p => p.classList.remove('is-active'));
                btn.classList.add('is-active');
                const panel = document.getElementById('tab-' + target);
                if (panel) panel.classList.add('is-active');
            });
        });
    }

    function filterTable(tableId, searchInputId, statusSelectId, planSelectId) {
        const table = document.getElementById(tableId);
        if (!table) return;
        const rows = Array.from(table.querySelectorAll('tbody tr'));
        const searchEl = document.getElementById(searchInputId);
        const statusEl = statusSelectId ? document.getElementById(statusSelectId) : null;
        const planEl = planSelectId ? document.getElementById(planSelectId) : null;

        function apply() {
            const q = (searchEl?.value || '').trim().toLowerCase();
            const status = statusEl?.value || '';
            const plan = planEl?.value || '';

            rows.forEach(row => {
                const text = (row.getAttribute('data-search') || row.textContent || '').toLowerCase();
                const rowStatus = row.getAttribute('data-status') || '';
                const rowPlan = row.getAttribute('data-plan') || '';
                const matchQ = !q || text.includes(q);
                const matchStatus = !status || rowStatus === status;
                const matchPlan = !plan || rowPlan === plan;
                row.style.display = matchQ && matchStatus && matchPlan ? '' : 'none';
            });
        }

        searchEl?.addEventListener('input', apply);
        statusEl?.addEventListener('change', apply);
        planEl?.addEventListener('change', apply);
    }

    function createCharts(config) {
        if (typeof Chart === 'undefined') return;

        Chart.defaults.font.family = "Vazir, Tahoma, sans-serif";
        Chart.defaults.color = '#64748b';

        const monthly = config.monthlyStats || [];
        const monthlyCanvas = document.getElementById('monthlyRevenueChart');
        if (monthlyCanvas) {
            new Chart(monthlyCanvas, {
                type: 'line',
                data: {
                    labels: monthly.map(m => m.label),
                    datasets: [
                        {
                            label: 'درآمد (تومان)',
                            data: monthly.map(m => m.revenue),
                            borderColor: '#ff7a00',
                            backgroundColor: 'rgba(255, 122, 0, 0.12)',
                            fill: true,
                            tension: 0.35,
                            pointRadius: 4,
                            pointBackgroundColor: '#ff7a00'
                        },
                        {
                            label: 'تعداد اشتراک',
                            data: monthly.map(m => m.newSubscriptions),
                            borderColor: '#0284c8',
                            backgroundColor: 'transparent',
                            tension: 0.35,
                            pointRadius: 3,
                            yAxisID: 'y1'
                        }
                    ]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { position: 'bottom', labels: { boxWidth: 12, usePointStyle: true } }
                    },
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                callback: v => formatNumber(v)
                            },
                            grid: { color: 'rgba(148,163,184,0.15)' }
                        },
                        y1: {
                            beginAtZero: true,
                            position: 'right',
                            grid: { drawOnChartArea: false },
                            ticks: { callback: v => formatNumber(v) }
                        },
                        x: {
                            grid: { display: false },
                            ticks: {
                                maxRotation: 40,
                                minRotation: 0,
                                autoSkip: false,
                                font: { size: 11 }
                            }
                        }
                    }
                }
            });
        }

        const statusCanvas = document.getElementById('subscriptionStatusChart');
        if (statusCanvas) {
            new Chart(statusCanvas, {
                type: 'doughnut',
                data: {
                    labels: ['فعال', 'منقضی', 'بدون اشتراک'],
                    datasets: [{
                        data: [
                            config.activeRestaurants || 0,
                            config.expiredRestaurants || 0,
                            config.noSubscription || 0
                        ],
                        backgroundColor: ['#059669', '#e11d48', '#94a3b8'],
                        borderWidth: 0,
                        hoverOffset: 6
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: '62%',
                    plugins: {
                        legend: { position: 'bottom', labels: { boxWidth: 12, usePointStyle: true } }
                    }
                }
            });
        }
    }

    async function loadSubscriptionCharts() {
        try {
            const res = await fetch('/Admin/GetSubscriptionStats');
            if (!res.ok) return;
            const json = await res.json();
            if (!json.success || !json.data) return;

            const data = json.data;
            const summary = json.summary || {};
            const badge = document.getElementById('planChartBadge');
            if (badge) {
                badge.textContent = formatNumber(summary.totalActiveSubscriptions || 0) + ' فعال';
            }

            const byPlan = {};
            const byMethod = {};
            data.forEach(item => {
                byPlan[item.planName] = (byPlan[item.planName] || 0) + item.count;
                const method = paymentLabel(item.paymentMethod);
                byMethod[method] = (byMethod[method] || 0) + item.count;
            });

            const planCanvas = document.getElementById('planDistributionChart');
            if (planCanvas && typeof Chart !== 'undefined') {
                const planColors = {
                    'رایگان': '#94a3b8',
                    'برنزی': '#cd7f32',
                    'نقره‌ای': '#64748b',
                    'طلایی': '#f59e0b'
                };
                const labels = Object.keys(byPlan);
                new Chart(planCanvas, {
                    type: 'doughnut',
                    data: {
                        labels,
                        datasets: [{
                            data: labels.map(l => byPlan[l]),
                            backgroundColor: labels.map(l => planColors[l] || '#ff7a00'),
                            borderWidth: 0,
                            hoverOffset: 6
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        cutout: '58%',
                        plugins: {
                            legend: { position: 'bottom', labels: { boxWidth: 12, usePointStyle: true } }
                        }
                    }
                });
            }

            const methodCanvas = document.getElementById('paymentMethodChart');
            if (methodCanvas && typeof Chart !== 'undefined') {
                const labels = Object.keys(byMethod);
                new Chart(methodCanvas, {
                    type: 'bar',
                    data: {
                        labels,
                        datasets: [{
                            label: 'تعداد',
                            data: labels.map(l => byMethod[l]),
                            backgroundColor: '#ff7a00',
                            borderRadius: 8,
                            maxBarThickness: 42
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { display: false } },
                        scales: {
                            y: {
                                beginAtZero: true,
                                ticks: { callback: v => formatNumber(v) },
                                grid: { color: 'rgba(148,163,184,0.15)' }
                            },
                            x: { grid: { display: false } }
                        }
                    }
                });
            }

            const tbody = document.querySelector('#subscriptionStatsTable tbody');
            if (tbody) {
                const total = summary.totalActiveSubscriptions || data.reduce((s, i) => s + i.count, 0) || 1;
                tbody.innerHTML = data.map(item => {
                    const pct = ((item.count / total) * 100).toFixed(1);
                    return `<tr>
                        <td>${item.planName || '-'}</td>
                        <td>${paymentLabel(item.paymentMethod)}</td>
                        <td>${formatNumber(item.count)}</td>
                        <td class="ap-money">${formatNumber(item.totalRevenue)}</td>
                        <td>${pct}٪</td>
                    </tr>`;
                }).join('');
            }
        } catch (err) {
            console.error('subscription stats failed', err);
        }
    }

    async function loadActivityTable() {
        const badge = document.getElementById('activityCount');
        try {
            const res = await fetch('/Admin/GetRestaurantActivityStatsDetailed');
            if (!res.ok) throw new Error('bad response');
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('no data');

            if (badge) badge.textContent = formatNumber(json.data.length) + ' رستوران';

            const tbody = document.querySelector('#activityTable tbody');
            if (!tbody) return;

            tbody.innerHTML = json.data.map(r => `
                <tr>
                    <td>${r.restaurantName || '-'}</td>
                    <td>${formatNumber(r.orders1Day)}</td>
                    <td>${formatNumber(r.orders7Day)}</td>
                    <td>${formatNumber(r.orders30Day)}</td>
                    <td>${formatNumber(r.foodItems1Day)}</td>
                    <td>${formatNumber(r.foodItems7Day)}</td>
                    <td>${formatNumber(r.foodItems30Day)}</td>
                </tr>
            `).join('');

            if (window.jQuery && $.fn.DataTable) {
                if ($.fn.DataTable.isDataTable('#activityTable')) {
                    $('#activityTable').DataTable().destroy();
                }
                $('#activityTable').DataTable({
                    order: [[3, 'desc']],
                    pageLength: 15,
                    language: {
                        search: 'جستجو:',
                        lengthMenu: 'نمایش _MENU_',
                        info: '_START_ تا _END_ از _TOTAL_',
                        paginate: { previous: 'قبلی', next: 'بعدی' },
                        zeroRecords: 'موردی یافت نشد',
                        emptyTable: 'داده‌ای موجود نیست'
                    }
                });
            }
        } catch (err) {
            console.error('activity stats failed', err);
            if (badge) badge.textContent = 'خطا در بارگذاری';
        }
    }

    window.AdminPanelDash = {
        init: function (config) {
            initTabs();
            filterTable('restaurantsTable', 'restaurantSearch', 'statusFilter', 'planFilter');
            filterTable('ownersTable', 'ownerSearch', null, null);
            filterTable('recentTable', 'recentSearch', null, null);
            createCharts(config || {});
            loadSubscriptionCharts();
            loadActivityTable();
        },
        helpers: { formatNumber, paymentLabel, statusLabel, statusClass }
    };
})();
