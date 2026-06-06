 
   
    (function() {
        let subscriptions = [];
    let currentDuration = 'monthly'; // 'monthly', '3monthly', '6monthly'
    let currentMonths = 1;

    // ========== توابع کمکی فرمت اعداد ==========
    function toPersianNum(num) {
            if (num === null || num === undefined || num < 0) return 'ناموجود';
    const persianDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];
    return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',').replace(/\d/g, d => persianDigits[d]);
        }

    function formatPriceShort(price) {
            if (price === null || price === undefined || price < 0) return null;
    if (price === 0) return '۰';
    const inThousands = Math.round(price / 1000);
    if (inThousands < 10) return toPersianNum(price) + ' تومان';
    return toPersianNum(inThousands) + ' هزار تومان';
        }

    // ========== دریافت لیست اشتراک‌ها از API ==========
    function fetchSubscriptions() {
            return $.ajax({
        url: 'https://resturanyar.ir/api/UserApi/getallsubscriptions',
    method: 'GET'
            }).then(function(data) {
                if (Array.isArray(data) && data.length > 0) {
                    return data.map(function(item) {
        let tier = 'free';
    const code = (item.code || '').toUpperCase();
    if (code === 'BRONZE') tier = 'bronze';
    else if (code === 'SILVER') tier = 'silver';
    else if (code === 'GOLD') tier = 'gold';
    return {...item, tier};
                    });
                }
    return [];
            }).catch(function(err) {
        console.warn('⚠️ خطا در دریافت اشتراک‌ها:', err);
    return [];
            });
        }

    // ========== دریافت قیمت بر اساس دوره انتخابی ==========
    function getPriceData(sub, durationKey) {
            const priceMap = {
        'monthly': {price: sub.priceMonthly, discount: sub.discountPriceMonthly },
    '3monthly': {price: sub.price3Monthly, discount: sub.discountPrice3Monthly },
    '6monthly': {price: sub.price6Monthly, discount: sub.discountPrice6Monthly },
            };
    return priceMap[durationKey] || {price: sub.priceMonthly, discount: sub.discountPriceMonthly };
        }

    // ========== لیست ویژگی‌ها برای نمایش ==========
    function getFeatureList(sub) {
            return [
    {key: 'foodLimit', label: 'محدودیت غذا', value: sub.foodLimit, format: v => v === 0 ? 'نامحدود' : toPersianNum(v) + ' عدد', icon: 'neutral' },
    {key: 'tableLimit', label: 'محدودیت میز', value: sub.tableLimit, format: v => v === 0 ? 'نامحدود' : toPersianNum(v) + ' عدد', icon: 'neutral' },
    {key: 'employeeLimit', label: 'کارمندان قابل ثبت', value: sub.employeeLimit, format: v => v === 0 ? 'نامحدود' : toPersianNum(v) + ' نفر', icon: 'neutral' },
    {key: 'canAddImages', label: 'افزودن تصویر غذا', value: sub.canAddImages, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    {key: 'canUsePrinter', label: 'اتصال به پرینتر', value: sub.canUsePrinter, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    {key: 'canShareMenu', label: 'اشتراک‌گذاری منو', value: sub.canShareMenu, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    {key: 'canUseRealtime', label: 'به‌روزرسانی لحظه‌ای', value: sub.canUseRealtime, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    {key: 'canManageUsers', label: 'مدیریت کاربران', value: sub.canManageUsers, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    {key: 'canAccessReports', label: 'گزارش‌های پیشرفته', value: sub.canAccessReports, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    {key: 'canManageMultipleRestaurants', label: 'مدیریت چند رستوران', value: sub.canManageMultipleRestaurants, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    {key: 'canUseWeb', label: 'دسترسی نسخه وب', value: sub.canUseWeb, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
    ];
        }

    function getTierIcon(tier) {
            const icons = {'free': '🌱', 'bronze': '🥉', 'silver': '🥈', 'gold': '👑' };
    return icons[tier] || '📦';
        }

    function getCtaClass(tier) {
            const classes = {'free': 'cta-free', 'bronze': 'cta-bronze', 'silver': 'cta-silver', 'gold': 'cta-gold' };
    return classes[tier] || 'cta-free';
        }

    function getPopularBadge(tier, sub) {
            if (tier === 'gold') return '<span class="popular-badge gold-badge">⭐ پیشنهاد ویژه</span>';

    return '';
        }


    function buildCard(sub) {
        const tier = sub.tier || 'free';
    const priceData = getPriceData(sub, currentDuration);
        const hasDiscount = priceData.discount !== null && priceData.discount > 0 && priceData.discount < priceData.price;
    const finalPrice = hasDiscount ? priceData.discount : priceData.price;
    const isFree = finalPrice === 0;
    const isUnavailable = finalPrice === null || finalPrice === undefined || finalPrice < 0;

    // 🏷️ محاسبه درصد تخفیف
    let discountPercent = 0;
        if (hasDiscount && priceData.price > 0) {
        discountPercent = Math.round((1 - (priceData.discount / priceData.price)) * 100);
        }

    const features = getFeatureList(sub);
        const featureItems = features.map(f => {
        let iconClass = 'neutral';
    let iconSymbol = '✓';
    if (typeof f.icon === 'function') {
        iconClass = f.icon(f.value);
    iconSymbol = f.value ? '✓' : '✗';
            }
    return `
    <li>
        <span class="feature-icon ${iconClass}">${iconSymbol}</span>
        <span>${f.label} <strong>${f.format(f.value)}</strong></span>
    </li>
    `;
        }).join('');


    let priceHtml = '';
    if (isUnavailable) {
        priceHtml = '<span class="price-unavailable">ناموجود</span>';
        } else if (isFree) {
        priceHtml = '<span class="price-free">رایگان</span>';
        } else {
        priceHtml = `
                ${hasDiscount ? '<span class="price-original">' + formatPriceShort(priceData.price) + '</span>' : '<span class="price-original">&nbsp;</span>'}
                <span class="price-current">${formatPriceShort(finalPrice)}</span>
                <span class="price-unit">/ ${toPersianNum(currentMonths)} ماه</span>
            `;

           
            if (hasDiscount && discountPercent > 0) {
        priceHtml += `
                    <div class="discount-badge">
                        <span class="discount-amount">${discountPercent}٪</span>
                        <span class="discount-text">تخفیف</span>
                    </div>
                `;
            }




           
            if (hasDiscount && currentMonths > 1) {
                const savings = priceData.price - priceData.discount;
    priceHtml += `<div style="font-size:11px;color:#4caf50;margin-top:4px;font-weight:600;">صرفه‌جویی ${formatPriceShort(savings)}</div>`;
            }
        }

    const popularBadge = getPopularBadge(tier, sub);
    const tierIcon = getTierIcon(tier);
    const ctaClass = getCtaClass(tier);

    // دکمه‌ی CTA
    let ctaHtml = '';
    if (tier === 'free' || isFree) {
        ctaHtml = `
                <a href="https://cafebazaar.ir/app/com.musiclrc.resturanyar"
                   target="_blank"
                   rel="noopener noreferrer"
                   class="card-cta ${ctaClass}">
                   استفاده در اپلیکیشن
                </a>
            `;
        } else {
        ctaHtml = `
                <button onclick="window.startZarinpalPayment(${sub.id}, ${currentMonths})"
                    class="card-cta ${ctaClass}">
                    پرداخت و فعال‌سازی
                </button>
            `;
        }


    return `
    <div class="pricing-card tier-${tier}">
        ${popularBadge}
        <h3 class="card-tier-name">${sub.name}</h3>
        <span class="card-tier-code">${sub.code || ''}</span>
        <p class="card-description">${sub.description || ''}</p>
        <div class="card-pricing">${priceHtml}</div>
        <ul class="card-features">${featureItems}</ul>
        ${ctaHtml}
    </div>
    `;
    }

    function renderCards() {
            const grid = document.getElementById('pricingGrid');
    if (!subscriptions.length) {
        grid.innerHTML = '<div class="error-state"><p>🚫 اشتراکی برای نمایش یافت نشد.</p><button onclick="location.reload()">تلاش مجدد</button></div>';
    return;
            }
            grid.innerHTML = subscriptions.map(sub => buildCard(sub)).join('');
        }


    function setupDurationSelector() {
            const selector = document.getElementById('durationSelector');
    if (!selector) return;
    const buttons = selector.querySelectorAll('.duration-option');
            buttons.forEach(btn => {
        btn.addEventListener('click', function () {
            buttons.forEach(b => b.classList.remove('active'));
            this.classList.add('active');
            currentDuration = this.dataset.duration;
            currentMonths = parseInt(this.dataset.months) || 1;
            renderCards();
        });
            });
        }


    window.startZarinpalPayment = async function(planId, months) {
         
       const currentRestaurantId = currentRestaurantIdFromServer;
    if (!currentRestaurantId || currentRestaurantId === 0) {
        alert('شناسه رستوران مشخص نیست. لطفاً از مسیر درست وارد شوید.');
    return;
    }



    let subscriptionPeriod = '';
    if (months === 1) subscriptionPeriod = 'Monthly';
    else if (months === 3) subscriptionPeriod = '3Monthly';
    else if (months === 6) subscriptionPeriod = '6Monthly';

    const payload = {
        RestaurantId: currentRestaurantId,
    SubscriptionPlanId: planId,
    SubscriptionPeriod: subscriptionPeriod
            
        };

    try {
            // credentials: 'include' باعث می‌شود کوکی احراز هویت همراه درخواست ارسال شود
            const response = await fetch('/zarinpal/create', {
        method: 'POST',
    headers: {'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
    credentials: 'include'
            });

    const result = await response.json();

    if (result.success && result.url) {
        window.location.href = result.url;
            } else {
        alert(result.message || 'خطا در ایجاد پرداخت');
            }
        } catch (err) {
        console.error(err);
    alert('خطا در ارتباط با سرور.');
        }
    };


    // ========== بررسی نتیجه پرداخت هنگام بازگشت از درگاه ==========
    function checkPaymentResult() {
            const urlParams = new URLSearchParams(window.location.search);
    const authority = urlParams.get('Authority');
    const status = urlParams.get('Status');
    if (authority && status) {
                if (status === 'OK') {
        alert('✅ پرداخت با موفقیت انجام شد. اشتراک شما فعال گردید.');
                    // می‌توانید صفحه را رفرش کنید یا وضعیت را آپدیت کنید
                    setTimeout(() => location.reload(), 2000);
                } else {
        alert('❌ پرداخت لغو شد یا با خطا مواجه گردید.');
                }
    // پاک کردن پارامترها از URL
    window.history.replaceState({ }, document.title, window.location.pathname);
            }
        }

    // ========== مقداردهی اولیه ==========
    async function init() {
            const grid = document.getElementById('pricingGrid');
    grid.innerHTML = '<div class="loading-state"><div class="loading-spinner"></div><p>در حال بارگذاری اشتراک‌ها...</p></div>';

    subscriptions = await fetchSubscriptions();
            subscriptions = subscriptions.filter(s => s.isActive !== false);
            subscriptions.sort((a, b) => (a.id || 0) - (b.id || 0));

    renderCards();
    setupDurationSelector();
    checkPaymentResult();
        }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
        } else {
        init();
        }
    })();
 