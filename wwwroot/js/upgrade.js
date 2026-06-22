(function () {
    let subscriptions = [];
    let currentDuration = 'monthly'; // 'monthly', '3monthly', '6monthly'
    let currentMonths = 1;

    // ========== تابع کمکی برای تبدیل ماه به رشته ==========
    function getPeriodString(months) {
        if (months === 1) return 'Monthly';
        if (months === 3) return '3Monthly';
        if (months === 6) return '6Monthly';
        return 'Monthly';
    }

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
        }).then(function (data) {
            if (Array.isArray(data) && data.length > 0) {
                return data.map(function (item) {
                    let tier = 'free';
                    const code = (item.code || '').toUpperCase();
                    if (code === 'BRONZE') tier = 'bronze';
                    else if (code === 'SILVER') tier = 'silver';
                    else if (code === 'GOLD') tier = 'gold';
                    return { ...item, tier };
                });
            }
            return [];
        }).catch(function (err) {
            console.warn('⚠️ خطا در دریافت اشتراک‌ها:', err);
            return [];
        });
    }

    // ========== دریافت قیمت بر اساس دوره انتخابی ==========
    function getPriceData(sub, durationKey) {
        const priceMap = {
            'monthly': { price: sub.priceMonthly, discount: sub.discountPriceMonthly },
            '3monthly': { price: sub.price3Monthly, discount: sub.discountPrice3Monthly },
            '6monthly': { price: sub.price6Monthly, discount: sub.discountPrice6Monthly },
        };
        return priceMap[durationKey] || { price: sub.priceMonthly, discount: sub.discountPriceMonthly };
    }

    // ========== لیست ویژگی‌ها برای نمایش ==========
    function getFeatureList(sub) {
        return [
            { key: 'foodLimit', label: 'محدودیت غذا', value: sub.foodLimit, format: v => v === 0 ? 'نامحدود' : toPersianNum(v) + ' عدد', icon: 'neutral' },
            { key: 'tableLimit', label: 'محدودیت میز', value: sub.tableLimit, format: v => v === 0 ? 'نامحدود' : toPersianNum(v) + ' عدد', icon: 'neutral' },
            { key: 'employeeLimit', label: 'کارمندان قابل ثبت', value: sub.employeeLimit, format: v => v === 0 ? 'نامحدود' : toPersianNum(v) + ' نفر', icon: 'neutral' },
            { key: 'canAddImages', label: 'افزودن تصویر غذا', value: sub.canAddImages, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
            { key: 'canUsePrinter', label: 'اتصال به پرینتر', value: sub.canUsePrinter, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
            { key: 'canShareMenu', label: 'اشتراک‌گذاری منو', value: sub.canShareMenu, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
            { key: 'canUseRealtime', label: 'به‌روزرسانی لحظه‌ای', value: sub.canUseRealtime, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
            { key: 'canManageUsers', label: 'مدیریت کاربران', value: sub.canManageUsers, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
            { key: 'canAccessReports', label: 'گزارش‌های پیشرفته', value: sub.canAccessReports, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
            { key: 'canManageMultipleRestaurants', label: 'مدیریت چند رستوران', value: sub.canManageMultipleRestaurants, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
            { key: 'canUseWeb', label: 'دسترسی نسخه وب', value: sub.canUseWeb, format: v => v ? '' : '', icon: v => v ? 'check' : 'cross' },
        ];
    }

    function getTierIcon(tier) {
        const icons = { 'free': '🌱', 'bronze': '🥉', 'silver': '🥈', 'gold': '👑' };
        return icons[tier] || '📦';
    }

    function getCtaClass(tier) {
        const classes = { 'free': 'cta-free', 'bronze': 'cta-bronze', 'silver': 'cta-silver', 'gold': 'cta-gold' };
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
        const ctaClass = getCtaClass(tier);

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

    // ========== باز کردن مودال با اطلاعات اشتراک ==========
    window.startZarinpalPayment = async function (planId, months) {
        const plan = subscriptions.find(s => s.id === planId);
        if (!plan) {
            alert('اشتراک مورد نظر یافت نشد.');
            return;
        }

        const priceData = getPriceData(plan, currentDuration);
        const hasDiscount = priceData.discount !== null && priceData.discount > 0 && priceData.discount < priceData.price;
        const basePrice = hasDiscount ? priceData.discount : priceData.price;

        document.getElementById('invoicePlanName').textContent = plan.name;
        document.getElementById('invoiceDuration').textContent = toPersianNum(months) + ' ماه';
        document.getElementById('invoiceBasePrice').innerHTML = formatPriceShort(basePrice);
        document.getElementById('invoiceFinalPrice').innerHTML = formatPriceShort(basePrice);

        window._invoiceData = {
            planId: planId,
            months: months,
            basePrice: basePrice,
            appliedDiscount: 0,
            finalPrice: basePrice,
            couponId: null,
            discountCode: '',
            plan: plan
        };

        document.querySelector('.discount-row').style.display = 'none';
        document.getElementById('discountCodeInput').value = '';
        document.getElementById('discountMessage').textContent = '';
        document.getElementById('discountMessage').className = 'discount-message';

        document.getElementById('invoiceModal').style.display = 'flex';
        const toggleBtn = document.getElementById('toggleDiscountArea');
        const discountArea = document.querySelector('.discount-input-area');

        if (discountArea) {
            discountArea.classList.remove('expanded');
            discountArea.classList.add('collapsed');
        }
        if (toggleBtn) {
            toggleBtn.classList.remove('active');
            const icon = toggleBtn.querySelector('.toggle-icon');
            if (icon) icon.textContent = '+';
        }
    };

    // ========== رویداد اعمال کد تخفیف — از طریق /coupon/validate ==========
    document.getElementById('applyDiscountBtn').addEventListener('click', async function () {
        const input = document.getElementById('discountCodeInput');
        const code = input.value.trim().toUpperCase();
        const msg = document.getElementById('discountMessage');

        if (!code) {
            msg.textContent = 'لطفاً کد تخفیف را وارد کنید.';
            msg.className = 'discount-message error';
            return;
        }

        const data = window._invoiceData;
        if (!data) {
            msg.textContent = '⚠️ خطا در اطلاعات پرداخت.';
            msg.className = 'discount-message error';
            return;
        }

        const applyBtn = this;
        applyBtn.disabled = true;
        applyBtn.textContent = '...';

        try {
            const response = await fetch('/coupon/validate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    Code: code,
                    PlanId: data.planId,
                    BaseAmount: data.basePrice,
                    RestaurantId: currentRestaurantIdFromServer
                }),
                credentials: 'include'
            });

            const result = await response.json();

            if (!result.success) {
                msg.textContent = `❌ ${result.message || 'کد تخفیف نامعتبر است.'}`;
                msg.className = 'discount-message error';
                applyBtn.disabled = false;
                applyBtn.textContent = 'اعمال';
                return;
            }

            // ذخیره اطلاعات تخفیف از پاسخ سرور
            const discountAmount = result.data.discountAmount;
            const finalPrice = result.data.finalPrice;

            data.appliedDiscount = discountAmount;
            data.finalPrice = finalPrice;
            data.couponId = result.data.id;
            data.discountCode = result.data.code;

            document.querySelector('.discount-row').style.display = 'flex';
            document.getElementById('invoiceDiscountAmount').textContent = formatPriceShort(discountAmount);
            document.getElementById('invoiceFinalPrice').innerHTML = formatPriceShort(finalPrice);

            msg.innerHTML = `
        
        <span class="sub-message">🎉  از تخفیف خود لذت ببرید   🎉</span>
    `;
            msg.className = 'discount-message success';

            // فراخوانی جشن (کنفتی + تاست)
            triggerCelebration();

        } catch (err) {
            console.error('خطا در اعتبارسنجی کد تخفیف:', err);
            msg.textContent = '⚠️ خطا در ارتباط با سرور. لطفاً مجدداً تلاش کنید.';
            msg.className = 'discount-message error';
        }

        applyBtn.disabled = false;
        applyBtn.textContent = 'اعمال';
    });

    // ========== رویداد پرداخت نهایی ==========
    document.getElementById('confirmPaymentBtn').addEventListener('click', async function () {
        const data = window._invoiceData;
        if (!data) {
            alert('❌ اطلاعات پرداخت یافت نشد. لطفاً صفحه را مجدداً بارگذاری کنید.');
            return;
        }

        const payBtn = this;
        payBtn.disabled = true;
        payBtn.textContent = '⏳ در حال پردازش...';

        const payload = {
            RestaurantId: currentRestaurantIdFromServer,
            SubscriptionPlanId: data.planId,
            SubscriptionPeriod: getPeriodString(data.months),
            DiscountCode: data.discountCode,
            FinalPrice: data.finalPrice
        };

        try {
            const response = await fetch('/zarinpal/create', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload),
                credentials: 'include'
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`خطای سرور (${response.status}): ${errorText}`);
            }

            const result = await response.json();

            if (result.success && result.url) {
                window.location.href = result.url;
            } else {
                alert(`❌ ${result.message || 'خطا در ایجاد پرداخت. لطفاً مجدداً تلاش کنید.'}`);
                payBtn.disabled = false;
                payBtn.textContent = 'پرداخت نهایی';
            }
        } catch (err) {
            console.error('❌ خطا در ارتباط با سرور:', err);
            alert('❌ خطا در ارتباط با سرور. لطفاً اتصال اینترنت خود را بررسی کنید و مجدداً تلاش نمایید.');
            payBtn.disabled = false;
            payBtn.textContent = 'پرداخت نهایی';
        }
    });

    // ========== توابع بستن مودال ==========
    // ========== تابع جشن و تبریک ==========
    // ========== جشن و تبریک ==========
    function triggerCelebration() {
        // کنفتی
        if (typeof confetti !== 'undefined') {
            confetti({
                particleCount: 120,
                spread: 80,
                origin: { y: 0.5 },
                colors: ['#ff6f3c', '#ff9a3c', '#d4a853', '#34d399', '#fb7185']
            });
            setTimeout(() => {
                confetti({
                    particleCount: 70,
                    spread: 60,
                    origin: { y: 0.4 },
                    startVelocity: 30,
                    colors: ['#f0d78c', '#b8943a', '#ff6f3c']
                });
            }, 200);
        }

        // تاست تبریک زیبا
        showCelebrationToast('🎉 تبریک! کد تخفیف با موفقیت اعمال شد');
    }

    function showCelebrationToast(message) {
        const toast = document.createElement('div');
        toast.textContent = message;
        Object.assign(toast.style, {
            position: 'fixed',
            bottom: '30px',
            left: '50%',
            transform: 'translateX(-50%) scale(0.7)',
            background: 'linear-gradient(135deg, #1b5e20, #2e7d32)',
            color: '#fff',
            padding: '16px 32px',
            borderRadius: '50px',
            fontSize: '17px',
            fontWeight: '700',
            boxShadow: '0 12px 40px rgba(0,0,0,0.25)',
            zIndex: '10000',
            border: '2px solid #81c784',
            opacity: '0',
            transition: 'transform 0.5s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.4s ease',
            direction: 'rtl',
            fontFamily: 'inherit',
            pointerEvents: 'none',
            whiteSpace: 'nowrap'
        });
        document.body.appendChild(toast);

        requestAnimationFrame(() => {
            toast.style.opacity = '1';
            toast.style.transform = 'translateX(-50%) scale(1)';
        });

        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(-50%) scale(0.7)';
            setTimeout(() => toast.remove(), 500);
        }, 4000);
    }

    // تابع نمایش یک پیام شناور تبریک
    function showFloatingCelebration(message) {
        const toast = document.createElement('div');
        toast.textContent = message;
        toast.style.cssText = `
        position: fixed;
        bottom: 30px;
        left: 50%;
        transform: translateX(-50%) scale(0.5);
        background: linear-gradient(135deg, #2d7d46, #1a5a32);
        color: #fff;
        padding: 18px 32px;
        border-radius: 50px;
        font-size: 18px;
        font-weight: 700;
        box-shadow: 0 15px 40px rgba(0,0,0,0.25);
        z-index: 10000;
        border: 2px solid #a5d6a7;
        opacity: 0;
        transition: transform 0.5s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.4s ease;
        pointer-events: none;
        direction: rtl;
        font-family: inherit;
    `;
        document.body.appendChild(toast);

        // نمایش با انیمیشن
        requestAnimationFrame(() => {
            toast.style.opacity = '1';
            toast.style.transform = 'translateX(-50%) scale(1)';
        });

        // مخفی شدن بعد از ۴ ثانیه
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(-50%) scale(0.5)';
            setTimeout(() => toast.remove(), 500);
        }, 4000);
    }
    function closeModal() {
        document.getElementById('invoiceModal').style.display = 'none';
    }

    document.getElementById('closeModalBtn').addEventListener('click', closeModal);
    document.getElementById('cancelPaymentBtn').addEventListener('click', closeModal);
    document.getElementById('invoiceModal').addEventListener('click', function (e) {
        if (e.target === this) closeModal();
    });

    // ========== بررسی نتیجه پرداخت ==========
    function checkPaymentResult() {
        const urlParams = new URLSearchParams(window.location.search);
        const authority = urlParams.get('Authority');
        const status = urlParams.get('Status');
        if (authority && status) {
            if (status === 'OK') {
                alert('✅ پرداخت با موفقیت انجام شد. اشتراک شما فعال گردید.');
                setTimeout(() => location.reload(), 2000);
            } else {
                alert('❌ پرداخت لغو شد یا با خطا مواجه گردید.');
            }
            window.history.replaceState({}, document.title, window.location.pathname);
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
        // ========== تاگل ناحیه کد تخفیف ==========
        const toggleDiscountBtn = document.getElementById('toggleDiscountArea');
        const discountArea = document.querySelector('.discount-input-area');

        if (toggleDiscountBtn && discountArea) {
            toggleDiscountBtn.addEventListener('click', function () {
                const isExpanded = discountArea.classList.contains('expanded');

                if (isExpanded) {
                    // بستن
                    discountArea.classList.remove('expanded');
                    discountArea.classList.add('collapsed');
                    toggleDiscountBtn.classList.remove('active');
                    toggleDiscountBtn.querySelector('.toggle-icon').textContent = '+';
                } else {
                    // باز کردن
                    discountArea.classList.add('expanded');
                    discountArea.classList.remove('collapsed');
                    toggleDiscountBtn.classList.add('active');
                    toggleDiscountBtn.querySelector('.toggle-icon').textContent = '−';
                }
            });
        }
    } else {
        init();
    }
})();
