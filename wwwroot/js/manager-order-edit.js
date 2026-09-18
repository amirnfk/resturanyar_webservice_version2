(function () {
    'use strict';

    var API_BASE = '/api/v2/UserApi';
    var MAX_QTY = 999;
    var PLACEHOLDER_IMG = '/images/food_placeholder.jpg';
    var IMAGE_HOST = 'https://resturanyar.ir/';
    var NS = '.managerOrderEdit';

    var STATUS_LABELS = {
        1: 'در انتظار تأیید',
        2: 'ثبت شده',
        3: 'تأیید شده',
        4: 'در حال آماده‌سازی',
        5: 'آماده تحویل',
        6: 'در حال ارسال',
        7: 'تحویل شده',
        8: 'تکمیل شده',
        9: 'لغو شده',
        10: 'مرجوع شده',
        11: 'بسته شده',
        12: 'در انتظار ویرایش'
    };

    var state = {
        restaurantId: 0,
        orderId: 0,
        orderType: 0,
        statusId: 0,
        expectedStatusId: 0,
        cart: [],
        foods: [],
        categories: [],
        originalFoodIds: {},
        selectedCustomerId: null,
        selectedCustomerMobile: null,
        selectedCustomerName: null,
        selectedAddressId: null,
        selectedAddressText: null,
        addressesCache: [],
        isSaving: false,
        isSavingCustomer: false,
        searchTimer: null,
        offcanvas: null,
        customerModal: null,
        addressModal: null,
        baseline: null,
        forceClose: false,
        activeTab: 'catalog',
        initialized: false
    };

    function $(sel, root) {
        return (root || document).querySelector(sel);
    }

    function $all(sel, root) {
        return Array.prototype.slice.call((root || document).querySelectorAll(sel));
    }

    function toast(msg, type) {
        if (typeof window.showToast === 'function') window.showToast(msg, type || 'info');
        else if (type === 'error') alert(msg);
    }

    function escapeHtml(str) {
        return String(str == null ? '' : str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function toEnglishDigits(str) {
        if (!str) return '';
        return String(str)
            .replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); })
            .replace(/[٠-٩]/g, function (d) { return '٠١٢٣٤٥٦٧٨٩'.indexOf(d); });
    }

    function formatMoney(n) {
        var num = Number(n) || 0;
        return Math.round(num).toLocaleString('fa-IR');
    }

    function orderTypeLabel(type) {
        if (type === 1) return 'بیرون‌بر';
        if (type === 2) return 'ارسال';
        return 'حضوری';
    }

    function sellPrice(food) {
        var price = Number(food.price || food.Price || 0);
        var discount = Number(food.discountPrice || food.DiscountPrice || 0);
        if (discount > 0 && discount < price) return discount;
        return price;
    }

    function foodIdOf(food) {
        return Number(food.foodItemId || food.FoodItemId || food.id || 0);
    }

    function statusLabel(id) {
        return STATUS_LABELS[Number(id)] || '';
    }

    /* Order items may carry a host-relative image path while catalog items are
       already absolute; normalize both so <img> never points at the SPA route. */
    function resolveImg(url) {
        var raw = String(url || '').trim();
        if (!raw) return PLACEHOLDER_IMG;
        if (/^(https?:)?\/\//i.test(raw) || raw.indexOf('data:') === 0) return raw;
        if (raw.charAt(0) === '/') return raw;
        return IMAGE_HOST + raw.replace(/^\.?\//, '');
    }

    function setLoading(on) {
        var el = $('#moeLoading');
        if (el) el.hidden = !on;
    }

    function fieldValue(sel) {
        var el = $(sel);
        return el ? String(el.value || '') : '';
    }

    function signature() {
        return JSON.stringify({
            items: state.cart
                .map(function (i) { return i.id + ':' + i.quantity; })
                .sort(),
            customer: state.selectedCustomerId || '',
            addressId: state.selectedAddressId || '',
            addressText: (state.selectedAddressText || '').trim(),
            table: fieldValue('#moeTableSelect'),
            description: fieldValue('#moeDescription').trim()
        });
    }

    function markBaseline() {
        state.baseline = signature();
        updateDirty();
    }

    function isDirty() {
        return state.baseline !== null && state.baseline !== signature();
    }

    function updateDirty() {
        var badge = $('#moeDirtyBadge');
        if (badge) badge.hidden = !isDirty();
    }

    function setActiveTab(name) {
        state.activeTab = name;
        $all('#orderEditOffcanvas .moe-tab').forEach(function (btn) {
            var on = btn.getAttribute('data-moe-tab') === name;
            btn.classList.toggle('is-active', on);
            btn.setAttribute('aria-selected', on ? 'true' : 'false');
        });
        $all('#orderEditOffcanvas .moe-pane').forEach(function (pane) {
            pane.classList.toggle('is-active', pane.getAttribute('data-moe-pane') === name);
        });
    }

    function isDrawerOpen() {
        var el = $('#orderEditOffcanvas');
        return !!(el && el.classList.contains('show'));
    }

    window.isManagerOrderEditOpen = isDrawerOpen;

    /* Closes without asking about unsaved changes (used after a successful save,
       on load failure, and on page teardown). */
    function closeDrawer() {
        state.forceClose = true;
        if (!state.offcanvas) return;
        try { state.offcanvas.hide(); } catch (_) { /* ignore */ }
    }

    /* The shared confirm overlay lives outside the drawer, so the offcanvas focus
       trap would pull focus back off its buttons. Pause it for the dialog. */
    async function confirmDialog(options, fallbackMessage) {
        if (typeof window.showConfirm !== 'function') {
            return window.confirm(fallbackMessage || options.message);
        }
        var trap = state.offcanvas && state.offcanvas._focustrap;
        var paused = false;
        if (trap && typeof trap.deactivate === 'function' && typeof trap.activate === 'function') {
            try { trap.deactivate(); paused = true; } catch (_) { paused = false; }
        }
        try {
            return await window.showConfirm(options);
        } finally {
            if (paused && isDrawerOpen()) {
                try { trap.activate(); } catch (_) { /* ignore */ }
            }
        }
    }

    function confirmDiscard() {
        return confirmDialog({
            title: 'بستن بدون ذخیره؟',
            message: 'تغییرات این سفارش ذخیره نشده است. با بستن پنجره، تغییرات از دست می‌رود.',
            confirmText: 'بستن و صرف‌نظر',
            cancelText: 'ادامه ویرایش',
            iconClass: 'fa-solid fa-triangle-exclamation',
            variant: 'danger'
        }, 'تغییرات ذخیره نشده است. پنجره بسته شود؟');
    }

    async function api(url, options) {
        if (typeof window.fetchWithAuth !== 'function') {
            throw new Error('احراز هویت آماده نیست. صفحه را تازه کنید.');
        }
        var res = await window.fetchWithAuth(url, options || {});
        var data = {};
        try { data = await res.json(); } catch (_) { data = {}; }
        return { res: res, data: data };
    }

    function resetCustomer() {
        state.selectedCustomerId = null;
        state.selectedCustomerMobile = null;
        state.selectedCustomerName = null;
        state.selectedAddressId = null;
        state.selectedAddressText = null;
        state.addressesCache = [];
        renderCustomer();
        renderAddress();
    }

    function resetState() {
        state.orderId = 0;
        state.orderType = 0;
        state.statusId = 0;
        state.expectedStatusId = 0;
        state.cart = [];
        state.originalFoodIds = {};
        state.baseline = null;
        state.forceClose = false;
        resetCustomer();
        var desc = $('#moeDescription');
        if (desc) desc.value = '';
        var table = $('#moeTableSelect');
        if (table) table.value = '';
        var search = $('#moeFoodSearch');
        if (search) search.value = '';
        var cat = $('#moeFoodCategory');
        if (cat) cat.value = '';
        renderCart();
        renderFoods();
        applyOrderTypeUi();
        setActiveTab('catalog');
        updateDirty();
    }

    function applyOrderTypeUi() {
        var isDineIn = state.orderType === 0;
        var isDelivery = state.orderType === 2;
        var tableSec = $('#moeTableSection');
        var addrSec = $('#moeAddressSection');
        var custReq = $('#moeCustomerRequired');
        if (tableSec) tableSec.classList.toggle('d-none', !isDineIn);
        if (addrSec) addrSec.classList.toggle('d-none', !isDelivery);
        if (custReq) custReq.classList.toggle('d-none', !isDelivery);
        var typeBadge = $('#moeOrderTypeBadge');
        if (typeBadge) typeBadge.textContent = orderTypeLabel(state.orderType);
        renderStatusChip();
    }

    function renderStatusChip() {
        var chip = $('#moeOrderStatusChip');
        var label = $('#moeOrderStatusBadge');
        var text = statusLabel(state.statusId);
        if (chip) chip.hidden = !text;
        if (label) label.textContent = text;
    }

    function cartTotal() {
        return state.cart.reduce(function (s, i) { return s + (i.price * i.quantity); }, 0);
    }

    function renderCart() {
        var box = $('#moeItems');
        var totalEl = $('#moeTotalAmount');
        var countEl = $('#moeItemCount');
        var tabCountEl = $('#moeTabItemCount');
        if (!box) return;

        if (!state.cart.length) {
            box.innerHTML = '<div class="moe-empty moe-empty--sm">' +
                '<span class="moe-empty__icon"><i class="fa-solid fa-basket-shopping" aria-hidden="true"></i></span>' +
                '<span class="moe-empty__title">هنوز آیتمی به سفارش اضافه نشده است.</span>' +
                '<span class="moe-empty__hint">از منوی غذا روی یک آیتم بزنید.</span>' +
                '</div>';
        } else {
            box.innerHTML = state.cart.map(function (item, idx) {
                var line = item.price * item.quantity;
                var name = escapeHtml(item.name);
                return '<div class="moe-item" data-idx="' + idx + '">' +
                    '<img class="moe-item__img" src="' + escapeHtml(resolveImg(item.image)) + '" alt="" loading="lazy" ' +
                    'onerror="this.onerror=null;this.src=\'' + PLACEHOLDER_IMG + '\';" />' +
                    '<div class="moe-item__info">' +
                    '<p class="moe-item__name" title="' + name + '">' + name + '</p>' +
                    '<p class="moe-item__unit">واحد: ' + formatMoney(item.price) + ' تومان</p>' +
                    '</div>' +
                    '<button type="button" class="moe-item__remove" data-action="remove" data-idx="' + idx + '" ' +
                    'title="حذف آیتم" aria-label="حذف ' + name + '"><i class="fa-solid fa-trash-can" aria-hidden="true"></i></button>' +
                    '<div class="moe-item__qty">' +
                    '<button type="button" class="moe-qty-btn" data-action="minus" data-idx="' + idx + '" aria-label="کاهش تعداد ' + name + '">&minus;</button>' +
                    '<input class="moe-qty-input" type="text" inputmode="numeric" data-idx="' + idx + '" value="' + item.quantity + '" aria-label="تعداد ' + name + '" />' +
                    '<button type="button" class="moe-qty-btn" data-action="plus" data-idx="' + idx + '" aria-label="افزایش تعداد ' + name + '">+</button>' +
                    '</div>' +
                    '<div class="moe-item__total">' + formatMoney(line) + ' تومان</div>' +
                    '</div>';
            }).join('');
        }

        var totalCount = state.cart.reduce(function (s, i) { return s + i.quantity; }, 0);
        if (totalEl) totalEl.textContent = formatMoney(cartTotal());
        if (countEl) countEl.textContent = formatMoney(totalCount);
        if (tabCountEl) {
            tabCountEl.textContent = formatMoney(totalCount);
            tabCountEl.hidden = totalCount <= 0;
        }
        syncFoodCardStates();
        updateDirty();
    }

    /* Keeps the "already in cart" badge on catalog cards in sync without
       re-rendering the whole grid (which would drop scroll position). */
    function syncFoodCardStates() {
        var grid = $('#moeFoodGrid');
        if (!grid) return;
        var counts = {};
        state.cart.forEach(function (i) { counts[i.id] = (counts[i.id] || 0) + i.quantity; });
        $all('.moe-food-card', grid).forEach(function (card) {
            var id = Number(card.getAttribute('data-food-id'));
            var qty = counts[id] || 0;
            var badge = $('.moe-food-card__count', card);
            card.classList.toggle('is-added', qty > 0);
            if (badge) {
                badge.hidden = qty <= 0;
                if (qty > 0) badge.textContent = formatMoney(qty);
            }
        });
    }

    function changeQty(idx, delta) {
        if (!state.cart[idx]) return;
        state.cart[idx].quantity += delta;
        if (state.cart[idx].quantity < 1) state.cart.splice(idx, 1);
        else if (state.cart[idx].quantity > MAX_QTY) state.cart[idx].quantity = MAX_QTY;
        renderCart();
    }

    function setQty(idx, qty) {
        if (!state.cart[idx]) return;
        if (qty == null || qty < 1) state.cart.splice(idx, 1);
        else state.cart[idx].quantity = Math.min(qty, MAX_QTY);
        renderCart();
    }

    function parseQty(value) {
        var digits = toEnglishDigits(value).replace(/[^\d]/g, '');
        if (!digits) return null;
        var n = parseInt(digits, 10);
        if (isNaN(n)) return null;
        return Math.min(n, MAX_QTY);
    }

    function addFoodToCart(food, forceExisting) {
        var id = foodIdOf(food);
        if (!id) return;
        var available = food.isAvailable !== false && food.IsAvailable !== false;
        if (!available && !forceExisting && !state.originalFoodIds[id]) {
            toast('این غذا فعلاً ناموجود است.', 'error');
            return;
        }
        var existing = state.cart.find(function (i) { return i.id === id; });
        if (existing) {
            if (existing.quantity < MAX_QTY) existing.quantity += 1;
        } else {
            state.cart.push({
                id: id,
                name: food.name || food.Name || food.foodName || 'غذا',
                price: sellPrice(food),
                quantity: 1,
                image: food.imageUrl || food.ImageUrl || food.foodImageUrl || ''
            });
        }
        renderCart();
    }

    function filteredFoods() {
        var q = (($('#moeFoodSearch') || {}).value || '').trim().toLowerCase();
        var cat = (($('#moeFoodCategory') || {}).value || '');
        return state.foods.filter(function (f) {
            var name = String(f.name || f.Name || '').toLowerCase();
            var catId = String(f.categoryId || f.CategoryId || '');
            if (q && name.indexOf(q) === -1) return false;
            if (cat && catId !== String(cat)) return false;
            return true;
        });
    }

    function renderFoods() {
        var grid = $('#moeFoodGrid');
        if (!grid) return;
        var foods = filteredFoods();
        var countEl = $('#moeFoodResultCount');
        if (countEl) countEl.textContent = foods.length ? formatMoney(foods.length) + ' غذا' : '';

        if (!foods.length) {
            grid.innerHTML = '<div class="moe-empty">' +
                '<span class="moe-empty__icon"><i class="fa-solid fa-magnifying-glass" aria-hidden="true"></i></span>' +
                '<span class="moe-empty__title">غذایی با این مشخصات یافت نشد.</span>' +
                '<span class="moe-empty__hint">عبارت جستجو یا دسته‌بندی را تغییر دهید.</span>' +
                '</div>';
            return;
        }
        grid.innerHTML = foods.map(function (f) {
            var id = foodIdOf(f);
            var available = f.isAvailable !== false && f.IsAvailable !== false;
            var price = Number(f.price || f.Price || 0);
            var discount = Number(f.discountPrice || f.DiscountPrice || 0);
            var hasDiscount = discount > 0 && discount < price;
            var effective = sellPrice(f);
            var discountPercent = hasDiscount ? Math.round(((price - discount) / price) * 100) : 0;
            var img = resolveImg(f.imageUrl || f.ImageUrl);
            var name = escapeHtml(f.name || f.Name || '');
            var category = f.categoryName || f.CategoryName || '';

            var priceHtml = hasDiscount
                ? '<span class="moe-price-old">' + formatMoney(price) + '</span>' +
                  '<span class="moe-price-new">' + formatMoney(effective) + ' <small>تومان</small></span>'
                : '<span class="moe-price-new">' + formatMoney(effective) + ' <small>تومان</small></span>';

            var ribbonHtml = hasDiscount
                ? '<span class="moe-ribbon">' + formatMoney(discountPercent) + '٪ تخفیف</span>'
                : '';

            var actionHtml = available
                ? '<button type="button" class="moe-quick-add" data-food-id="' + id + '" aria-label="افزودن ' + name + ' به سفارش">' +
                  '<i class="fa-solid fa-plus" aria-hidden="true"></i></button>'
                : '<div class="moe-unavailable-overlay"><span>ناموجود</span></div>';

            return '<div class="moe-food-card' + (available ? '' : ' is-unavailable') + '" data-food-id="' + id + '" role="button" ' +
                'tabindex="' + (available ? '0' : '-1') + '" aria-label="' + name + '">' +
                '<div class="moe-food-img-wrap">' +
                '<img class="moe-food-card__img" src="' + escapeHtml(img) + '" alt="" loading="lazy" onerror="this.onerror=null;this.src=\'' + PLACEHOLDER_IMG + '\';" />' +
                ribbonHtml +
                '<span class="moe-food-card__count" hidden></span>' +
                actionHtml +
                '</div>' +
                '<div class="moe-food-card__body">' +
                '<span class="moe-food-cat">' + (category ? escapeHtml(category) : '&nbsp;') + '</span>' +
                '<h5 class="moe-food-card__name">' + name + '</h5>' +
                '<div class="moe-price-area">' + priceHtml + '</div>' +
                '</div></div>';
        }).join('');

        syncFoodCardStates();
    }

    function renderCategories() {
        var sel = $('#moeFoodCategory');
        if (!sel) return;
        var current = sel.value;
        var html = '<option value="">همه دسته‌ها</option>';
        state.categories.forEach(function (c) {
            var id = c.categoryId || c.CategoryId;
            var name = c.categoryName || c.CategoryName || '';
            html += '<option value="' + id + '">' + escapeHtml(name) + '</option>';
        });
        sel.innerHTML = html;
        if (current) sel.value = current;
    }

    function renderCustomer() {
        var searchWrap = $('#moeCustomerSearchWrap');
        var selectedWrap = $('#moeSelectedCustomer');
        if (!searchWrap || !selectedWrap) return;

        if (state.selectedCustomerId) {
            searchWrap.classList.add('d-none');
            selectedWrap.classList.remove('d-none');
            selectedWrap.innerHTML =
                '<span class="moe-entity__avatar"><i class="fa-solid fa-user" aria-hidden="true"></i></span>' +
                '<div class="moe-entity__info">' +
                '<strong>' + escapeHtml(state.selectedCustomerName || 'مشتری') + '</strong>' +
                '<span>' + escapeHtml(state.selectedCustomerMobile || 'بدون شماره تماس') + '</span></div>' +
                '<button type="button" class="moe-entity__action" id="moeChangeCustomerBtn">تغییر</button>';
        } else {
            selectedWrap.classList.add('d-none');
            selectedWrap.innerHTML = '';
            searchWrap.classList.remove('d-none');
        }
        updateDirty();
    }

    function renderAddress() {
        var picker = $('#moeAddressPicker');
        var selected = $('#moeSelectedAddress');
        if (!picker || !selected) return;

        if (state.orderType !== 2) {
            picker.classList.add('d-none');
            selected.classList.add('d-none');
            updateDirty();
            return;
        }

        if (state.selectedAddressId || state.selectedAddressText) {
            picker.classList.add('d-none');
            selected.classList.remove('d-none');
            var title = state.selectedAddressId ? 'آدرس ذخیره‌شده' : 'آدرس دستی';
            var text = state.selectedAddressText || '';
            if (state.selectedAddressId) {
                var found = state.addressesCache.find(function (a) {
                    return String(a.addressId || a.AddressId) === String(state.selectedAddressId);
                });
                if (found) {
                    title = found.title || found.Title || title;
                    text = found.addressText || found.AddressText || text;
                }
            }
            selected.innerHTML =
                '<span class="moe-entity__avatar"><i class="fa-solid fa-location-dot" aria-hidden="true"></i></span>' +
                '<div class="moe-entity__info"><strong>' + escapeHtml(title) + '</strong>' +
                '<span>' + escapeHtml(text) + '</span></div>' +
                '<button type="button" class="moe-entity__action" id="moeChangeAddressBtn">تغییر</button>';
        } else {
            selected.classList.add('d-none');
            selected.innerHTML = '';
            picker.classList.remove('d-none');
            var sel = $('#moeAddressSelect');
            if (sel) {
                var opts = '<option value="">انتخاب آدرس...</option>';
                state.addressesCache.forEach(function (a) {
                    var id = a.addressId || a.AddressId;
                    var title = a.title || a.Title || 'آدرس';
                    var text = a.addressText || a.AddressText || '';
                    opts += '<option value="' + id + '">' + escapeHtml(title + ' — ' + text) + '</option>';
                });
                sel.innerHTML = opts;
                if (state.selectedAddressId) sel.value = String(state.selectedAddressId);
            }
        }
        updateDirty();
    }

    async function loadTables() {
        var sel = $('#moeTableSelect');
        if (!sel || !state.restaurantId) return;
        try {
            var result = await api(API_BASE + '/gettablesbyrestaurant/' + state.restaurantId);
            var tables = (result.data && result.data.tables) || [];
            var html = '<option value="">انتخاب میز</option>';
            tables.forEach(function (t) {
                html += '<option value="' + escapeHtml(t.tableName) + '">' +
                    escapeHtml(t.tableName) + (t.seats ? ' (' + t.seats + ' صندلی)' : '') + '</option>';
            });
            sel.innerHTML = html;
        } catch (_) {
            sel.innerHTML = '<option value="">خطا در بارگذاری میزها</option>';
        }
    }

    async function loadFoodsAndCategories() {
        if (!state.restaurantId) return;
        var foodsRes = await api(API_BASE + '/getallFoods/' + state.restaurantId);
        state.foods = (foodsRes.data && (foodsRes.data.data || foodsRes.data.Data)) || [];
        try {
            var catRes = await api(API_BASE + '/getcategoriesbyrestaurant/' + state.restaurantId);
            state.categories = (catRes.data && (catRes.data.categories || catRes.data.Categories)) || [];
        } catch (_) {
            var seen = {};
            state.categories = [];
            state.foods.forEach(function (f) {
                var id = f.categoryId || f.CategoryId;
                if (id && !seen[id]) {
                    seen[id] = true;
                    state.categories.push({
                        categoryId: id,
                        categoryName: f.categoryName || f.CategoryName || ('دسته ' + id)
                    });
                }
            });
        }
        renderCategories();
        renderFoods();
    }

    async function loadCustomerAddresses(customerId) {
        if (!customerId || state.orderType !== 2) return;
        try {
            var result = await api(API_BASE + '/getaddresses/' + customerId);
            var payload = result.data || {};
            state.addressesCache = Array.isArray(payload.data)
                ? payload.data
                : (payload.addresses || payload.Addresses || []);
        } catch (_) {
            state.addressesCache = [];
        }
        renderAddress();
    }

    function selectCustomer(id, mobile, name) {
        state.selectedCustomerId = String(id);
        state.selectedCustomerMobile = mobile || '';
        state.selectedCustomerName = name || 'مشتری';
        state.selectedAddressId = null;
        state.selectedAddressText = null;
        renderCustomer();
        if (state.orderType === 2) loadCustomerAddresses(id);
        else renderAddress();
    }

    async function searchCustomers(query) {
        var box = $('#moeCustomerResults');
        if (!box) return;
        query = (query || '').trim();
        if (query.length < 2) {
            box.classList.add('d-none');
            box.innerHTML = '';
            return;
        }
        try {
            var result = await api(API_BASE + '/getcustomers/' + state.restaurantId +
                '?search=' + encodeURIComponent(query) + '&page=1&pageSize=8');
            var customers = (result.data && (result.data.customers || result.data.data || result.data.Customers)) || [];
            if (!customers.length) {
                box.innerHTML = '<div class="moe-customer-result is-static">مشتری یافت نشد</div>';
                box.classList.remove('d-none');
                return;
            }
            box.innerHTML = customers.map(function (c) {
                var id = c.customerId || c.CustomerId;
                var mobile = c.mobile || c.Mobile || '';
                var name = c.fullName || c.FullName || '';
                return '<div class="moe-customer-result" role="option" data-id="' + id + '" data-mobile="' + escapeHtml(mobile) +
                    '" data-name="' + escapeHtml(name) + '">' +
                    '<span class="moe-customer-result__avatar"><i class="fa-solid fa-user" aria-hidden="true"></i></span>' +
                    '<span class="moe-customer-result__text">' +
                    '<span class="moe-customer-result__mobile">' + escapeHtml(mobile || '—') + '</span>' +
                    (name ? '<span class="moe-customer-result__name">' + escapeHtml(name) + '</span>' : '') +
                    '</span></div>';
            }).join('');
            box.classList.remove('d-none');
        } catch (_) {
            box.innerHTML = '<div class="moe-customer-result is-static">خطا در جستجو</div>';
            box.classList.remove('d-none');
        }
    }

    async function loadOrder(orderId) {
        var result = await api(API_BASE + '/getOrderById/' + orderId);
        if (!result.res.ok || !result.data.success) {
            throw new Error((result.data && result.data.message) || 'دریافت سفارش ناموفق بود.');
        }
        var order = result.data.orderData || result.data.OrderData;
        if (!order) throw new Error('اطلاعات سفارش خالی است.');

        state.orderId = Number(order.orderId || order.OrderId);
        state.orderType = Number(order.orderType || order.OrderType || 0);
        state.statusId = Number(order.statusId || order.StatusId || 0);
        state.expectedStatusId = state.statusId;

        var title = $('#moeDrawerTitle');
        if (title) title.textContent = 'ویرایش سفارش #' + state.orderId;

        var items = order.orderItems || order.OrderItems || [];
        state.originalFoodIds = {};
        state.cart = items.map(function (oi) {
            var fid = Number(oi.foodItemId || oi.FoodItemId);
            state.originalFoodIds[fid] = true;
            var unit = Number(oi.unitPriceWithDiscount || oi.UnitPriceWithDiscount || 0);
            if (!(unit > 0)) unit = Number(oi.unitPrice || oi.UnitPrice || 0);
            return {
                id: fid,
                name: oi.foodName || oi.FoodName || 'غذا',
                price: unit,
                quantity: Number(oi.quantity || oi.Quantity || 1),
                image: oi.foodImageUrl || oi.FoodImageUrl || ''
            };
        }).filter(function (i) { return i.id > 0 && i.quantity > 0; });

        var custId = order.customerId || order.CustomerId;
        if (custId) {
            state.selectedCustomerId = String(custId);
            state.selectedCustomerName = order.customerFullName || order.CustomerFullName || order.customerNameSnapshot || order.CustomerNameSnapshot || 'مشتری';
            state.selectedCustomerMobile = order.customerMobile || order.CustomerMobile || order.phoneSnapshot || order.PhoneSnapshot || '';
        } else {
            state.selectedCustomerId = null;
            state.selectedCustomerName = null;
            state.selectedCustomerMobile = null;
        }

        state.selectedAddressId = order.customerAddressId || order.CustomerAddressId || null;
        if (state.selectedAddressId) state.selectedAddressId = String(state.selectedAddressId);
        state.selectedAddressText = (!state.selectedAddressId && (order.addressSnapshot || order.AddressSnapshot))
            ? (order.addressSnapshot || order.AddressSnapshot)
            : null;

        var desc = $('#moeDescription');
        if (desc) desc.value = order.description || order.Description || '';

        applyOrderTypeUi();
        renderCustomer();
        renderCart();

        await loadTables();
        if (state.orderType === 0) {
            var table = $('#moeTableSelect');
            var tableNumber = order.tableNumber || order.TableNumber || '';
            if (table && tableNumber) {
                table.value = tableNumber;
                if (table.value !== tableNumber) {
                    var opt = document.createElement('option');
                    opt.value = tableNumber;
                    opt.textContent = tableNumber;
                    opt.selected = true;
                    table.appendChild(opt);
                }
            }
        }

        if (state.selectedCustomerId && state.orderType === 2) {
            await loadCustomerAddresses(state.selectedCustomerId);
            if (state.selectedAddressId) {
                var found = state.addressesCache.find(function (a) {
                    return String(a.addressId || a.AddressId) === String(state.selectedAddressId);
                });
                if (!found && (order.addressSnapshot || order.AddressSnapshot)) {
                    state.selectedAddressText = order.addressSnapshot || order.AddressSnapshot;
                    state.selectedAddressId = null;
                }
            }
        }
        renderAddress();
        markBaseline();
    }

    async function openEdit(orderId) {
        if (!state.initialized) init();
        if (!state.restaurantId) {
            toast('رستوران مشخص نیست.', 'error');
            return;
        }
        resetState();
        setLoading(true);
        try {
            if (!state.foods.length) await loadFoodsAndCategories();
            else {
                renderCategories();
                renderFoods();
            }
            await loadOrder(orderId);
            if (state.offcanvas) state.offcanvas.show();
        } catch (err) {
            toast(err.message || 'خطا در باز کردن ویرایش سفارش', 'error');
            closeDrawer();
        } finally {
            setLoading(false);
        }
    }

    function buildPayload() {
        var tableName = (($('#moeTableSelect') || {}).value || '').trim();
        if (state.orderType === 0 && !tableName) {
            toast('لطفاً یک میز را انتخاب کنید.', 'error');
            return null;
        }
        if (state.orderType === 2 && !state.selectedCustomerId) {
            toast('برای سفارش ارسال، انتخاب مشتری الزامی است.', 'error');
            return null;
        }
        if (state.orderType === 2 && !state.selectedAddressId && !(state.selectedAddressText || '').trim()) {
            toast('برای سفارش ارسال، آدرس را انتخاب کنید.', 'error');
            return null;
        }
        if (!state.cart.length) {
            toast('هیچ آیتمی در سفارش وجود ندارد.', 'error');
            return null;
        }

        if (state.orderType === 1 && !tableName) tableName = 'بیرون‌بر';
        if (state.orderType === 2 && !tableName) tableName = 'پیک';

        var payload = {
            orderId: state.orderId,
            restaurantId: state.restaurantId,
            tableNumber: tableName,
            statusId: state.statusId,
            expectedStatusId: state.expectedStatusId,
            description: (($('#moeDescription') || {}).value || '').trim(),
            items: state.cart.map(function (i) {
                return { foodItemId: i.id, quantity: i.quantity };
            }),
            customerId: state.selectedCustomerId ? parseInt(state.selectedCustomerId, 10) : null,
            updateDiscountCode: false
        };
        if (state.selectedAddressId) payload.customerAddressId = parseInt(state.selectedAddressId, 10);
        if (state.selectedAddressText) payload.addressText = state.selectedAddressText.trim();
        return payload;
    }

    async function saveOrder() {
        if (state.isSaving) return;
        var payload = buildPayload();
        if (!payload) return;

        if (state.statusId === 4 || state.statusId === 5) {
            var ok = await confirmDialog({
                title: 'تأیید ویرایش سفارش',
                message: 'این سفارش در حال آماده‌سازی یا آماده تحویل است. آیا از ویرایش آیتم‌ها مطمئن هستید؟',
                confirmText: 'بله، ذخیره شود',
                cancelText: 'انصراف',
                iconClass: 'fa-solid fa-triangle-exclamation',
                variant: 'danger'
            }, 'این سفارش در حال آماده‌سازی است. ادامه می‌دهید؟');
            if (!ok) return;
        }

        state.isSaving = true;
        var btn = $('#moeSaveBtn');
        var original = btn ? btn.innerHTML : '';
        if (btn) {
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span> در حال ذخیره...';
        }

        try {
            var result = await api(API_BASE + '/UpdateOrder/' + state.orderId, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            if (result.res.ok && result.data.success) {
                toast(result.data.message || 'سفارش با موفقیت ویرایش شد.', 'success');
                markBaseline();
                closeDrawer();
                if (typeof window.reloadManagerOrdersList === 'function') {
                    window.reloadManagerOrdersList();
                }
            } else {
                toast((result.data && result.data.message) || 'ذخیره سفارش ناموفق بود.', 'error');
            }
        } catch (err) {
            toast(err.message || 'خطا در ذخیره سفارش', 'error');
        } finally {
            state.isSaving = false;
            if (btn) {
                btn.disabled = false;
                btn.innerHTML = original || 'ذخیره تغییرات';
            }
        }
    }

    async function saveNewCustomer() {
        if (state.isSavingCustomer) return;
        var fullName = (($('#moeNewCustomerName') || {}).value || '').trim();
        var mobile = toEnglishDigits((($('#moeNewCustomerMobile') || {}).value || '').trim());
        var description = (($('#moeNewCustomerDesc') || {}).value || '').trim();

        if (fullName) {
            if (!mobile) {
                var ts = Date.now().toString();
                mobile = '991' + ts.slice(2, 10) + Math.floor(Math.random() * 10);
            }
        } else {
            if (!/^09[0-9]{9}$/.test(mobile)) {
                toast('شماره موبایل معتبر (11 رقم، شروع با 09) وارد کنید', 'error');
                return;
            }
            fullName = 'مشتری مهمان';
        }

        state.isSavingCustomer = true;
        var btn = $('#moeSaveCustomerBtn');
        if (btn) btn.disabled = true;
        try {
            var result = await api(API_BASE + '/addcustomer', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    restaurantId: state.restaurantId,
                    mobile: mobile,
                    fullName: fullName,
                    description: description
                })
            });
            if (result.res.ok && result.data.success) {
                toast(result.data.message || 'مشتری ثبت شد', 'success');
                if (state.customerModal) state.customerModal.hide();
                selectCustomer(result.data.customerId, mobile, fullName);
            } else {
                toast((result.data && result.data.message) || 'ثبت مشتری ناموفق بود', 'error');
            }
        } catch (err) {
            toast(err.message || 'خطا در ثبت مشتری', 'error');
        } finally {
            state.isSavingCustomer = false;
            if (btn) btn.disabled = false;
        }
    }

    async function saveAddress() {
        if (!state.selectedCustomerId) {
            toast('ابتدا مشتری را انتخاب کنید.', 'error');
            return;
        }
        var addressText = (($('#moeNewAddrText') || {}).value || '').trim();
        if (!addressText) {
            toast('متن آدرس الزامی است.', 'error');
            return;
        }
        var payload = {
            customerId: parseInt(state.selectedCustomerId, 10),
            title: (($('#moeNewAddrTitle') || {}).value || '').trim() || 'آدرس',
            addressText: addressText,
            unit: (($('#moeNewAddrUnit') || {}).value || '').trim().substring(0, 10) || '',
            floor: (($('#moeNewAddrFloor') || {}).value || '').trim().substring(0, 10) || '',
            plateNumber: (($('#moeNewAddrPlate') || {}).value || '').trim().substring(0, 10) || '',
            isDefault: !!($('#moeNewAddrDefault') && $('#moeNewAddrDefault').checked),
            description: (($('#moeNewAddrDesc') || {}).value || '').trim() || ''
        };
        var btn = $('#moeSaveAddressBtn');
        if (btn) btn.disabled = true;
        try {
            var result = await api(API_BASE + '/addaddress', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            if (result.res.ok && result.data.success) {
                toast(result.data.message || 'آدرس ذخیره شد', 'success');
                state.selectedAddressId = result.data.addressId ? String(result.data.addressId) : null;
                state.selectedAddressText = null;
                if (state.addressModal) state.addressModal.hide();
                await loadCustomerAddresses(state.selectedCustomerId);
            } else {
                toast((result.data && result.data.message) || 'ذخیره آدرس ناموفق بود', 'error');
            }
        } catch (err) {
            toast(err.message || 'خطا در ذخیره آدرس', 'error');
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    function bindEvents() {
        var root = $('#orderEditOffcanvas');
        if (!root) return;

        var $doc = window.jQuery ? window.jQuery(document) : null;
        var nativeHandlers = state._nativeHandlers || [];

        function clearNative() {
            nativeHandlers.forEach(function (h) {
                h.el.removeEventListener(h.event, h.fn);
            });
            state._nativeHandlers = [];
            nativeHandlers = state._nativeHandlers;
        }

        if ($doc) $doc.off(NS);
        clearNative();

        function onRaw(el, event, fn) {
            el.addEventListener(event, fn);
            nativeHandlers.push({ el: el, event: event, fn: fn });
        }

        function on(sel, event, handler) {
            if ($doc) {
                $doc.on(event + NS, sel, handler);
            } else {
                var fn = function (e) {
                    var t = e.target.closest(sel);
                    if (!t) return;
                    handler.call(t, e);
                };
                document.addEventListener(event, fn);
                nativeHandlers.push({ el: document, event: event, fn: fn });
            }
        }

        on('#moeFoodGrid .moe-food-card', 'click', function (e) {
            if (this.classList.contains('is-unavailable')) return;
            if (e.target.closest('.moe-quick-add')) return;
            var id = Number(this.getAttribute('data-food-id'));
            var food = state.foods.find(function (f) { return foodIdOf(f) === id; });
            if (food) addFoodToCart(food, false);
        });

        on('#moeFoodGrid .moe-quick-add', 'click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            var id = Number(this.getAttribute('data-food-id'));
            var food = state.foods.find(function (f) { return foodIdOf(f) === id; });
            if (food) addFoodToCart(food, false);
        });

        on('#moeFoodGrid .moe-food-card', 'keydown', function (e) {
            if (e.key !== 'Enter' && e.key !== ' ') return;
            e.preventDefault();
            if (this.classList.contains('is-unavailable')) return;
            var id = Number(this.getAttribute('data-food-id'));
            var food = state.foods.find(function (f) { return foodIdOf(f) === id; });
            if (food) addFoodToCart(food, false);
        });

        on('#moeItems .moe-qty-btn', 'click', function () {
            var idx = parseInt(this.getAttribute('data-idx'), 10);
            var action = this.getAttribute('data-action');
            changeQty(idx, action === 'plus' ? 1 : -1);
        });

        on('#moeItems .moe-item__remove', 'click', function () {
            var idx = parseInt(this.getAttribute('data-idx'), 10);
            if (!state.cart[idx]) return;
            state.cart.splice(idx, 1);
            renderCart();
        });

        on('#moeItems .moe-qty-input', 'change', function () {
            var idx = parseInt(this.getAttribute('data-idx'), 10);
            var qty = parseQty(this.value);
            if (qty == null) {
                renderCart();
                return;
            }
            setQty(idx, qty);
        });

        on('#moeFoodSearch', 'input', function () { renderFoods(); });
        on('#moeFoodCategory', 'change', function () { renderFoods(); });

        on('#orderEditOffcanvas .moe-tab', 'click', function () {
            setActiveTab(this.getAttribute('data-moe-tab'));
        });

        on('#moeTableSelect', 'change', updateDirty);
        on('#moeDescription', 'input', updateDirty);

        on('#moeCustomerSearch', 'input', function () {
            var q = this.value;
            clearTimeout(state.searchTimer);
            state.searchTimer = setTimeout(function () { searchCustomers(q); }, 280);
        });

        on('#moeCustomerResults .moe-customer-result', 'click', function () {
            var id = this.getAttribute('data-id');
            if (!id) return;
            selectCustomer(id, this.getAttribute('data-mobile'), this.getAttribute('data-name'));
            var box = $('#moeCustomerResults');
            if (box) {
                box.classList.add('d-none');
                box.innerHTML = '';
            }
            var input = $('#moeCustomerSearch');
            if (input) input.value = '';
        });

        on('#moeChangeCustomerBtn', 'click', function () {
            resetCustomer();
            var input = $('#moeCustomerSearch');
            if (input) input.focus();
        });

        on('#moeClearCustomerBtn', 'click', function () {
            if (state.orderType === 2) {
                toast('برای سفارش ارسال، مشتری الزامی است.', 'error');
                return;
            }
            resetCustomer();
        });

        on('#moeNewCustomerBtn', 'click', function () {
            var name = $('#moeNewCustomerName');
            var mobile = $('#moeNewCustomerMobile');
            var desc = $('#moeNewCustomerDesc');
            if (name) name.value = '';
            if (mobile) mobile.value = '';
            if (desc) desc.value = '';
            if (state.customerModal) state.customerModal.show();
        });

        on('#moeSaveCustomerBtn', 'click', function () { saveNewCustomer(); });
        on('#moeSaveBtn', 'click', function () { saveOrder(); });
        on('#moeCancelBtn', 'click', function () {
            if (state.offcanvas) state.offcanvas.hide();
        });

        on('#moeAddressSelect', 'change', function () {
            var val = this.value;
            if (!val) {
                state.selectedAddressId = null;
                return;
            }
            state.selectedAddressId = String(val);
            state.selectedAddressText = null;
            renderAddress();
        });

        on('#moeUseCustomAddressBtn', 'click', function () {
            var text = (($('#moeCustomAddressText') || {}).value || '').trim();
            if (!text) {
                toast('متن آدرس را وارد کنید.', 'error');
                return;
            }
            state.selectedAddressId = null;
            state.selectedAddressText = text;
            renderAddress();
        });

        on('#moeOpenAddressFormBtn', 'click', function () {
            if (!state.selectedCustomerId) {
                toast('ابتدا مشتری را انتخاب کنید.', 'error');
                return;
            }
            ['moeNewAddrTitle', 'moeNewAddrText', 'moeNewAddrUnit', 'moeNewAddrFloor', 'moeNewAddrPlate', 'moeNewAddrDesc']
                .forEach(function (id) {
                    var el = $('#' + id);
                    if (el) el.value = '';
                });
            var def = $('#moeNewAddrDefault');
            if (def) def.checked = !state.addressesCache.length;
            if (state.addressModal) state.addressModal.show();
        });

        on('#moeSaveAddressBtn', 'click', function () { saveAddress(); });
        on('#moeChangeAddressBtn', 'click', function () {
            state.selectedAddressId = null;
            state.selectedAddressText = null;
            renderAddress();
        });

        onRaw(document, 'click', function (e) {
            var box = $('#moeCustomerResults');
            if (!box || box.classList.contains('d-none')) return;
            if (e.target.closest('#moeCustomerSearchWrap')) return;
            box.classList.add('d-none');
            box.innerHTML = '';
        });

        onRaw(root, 'hide.bs.offcanvas', function (e) {
            if (state.forceClose || state.isSaving || !isDirty()) return;
            e.preventDefault();
            confirmDiscard().then(function (ok) {
                if (ok) closeDrawer();
            });
        });

        onRaw(root, 'hidden.bs.offcanvas', function () {
            state.forceClose = false;
            var results = $('#moeCustomerResults');
            if (results) {
                results.classList.add('d-none');
                results.innerHTML = '';
            }
        });
    }

    function init() {
        var root = $('#orderEditOffcanvas');
        if (!root) return;

        var container = document.getElementById('ordersContainer');
        state.restaurantId = Number((container && container.getAttribute('data-restaurant-id')) || root.getAttribute('data-restaurant-id') || 0);

        if (window.bootstrap) {
            // Bootstrap 5.1 Offcanvas accepts backdrop as boolean only (not 'static').
            state.offcanvas = bootstrap.Offcanvas.getOrCreateInstance(root, { backdrop: true, scroll: false });
            var custEl = $('#moeNewCustomerModal');
            var addrEl = $('#moeNewAddressModal');
            if (custEl) state.customerModal = bootstrap.Modal.getOrCreateInstance(custEl);
            if (addrEl) state.addressModal = bootstrap.Modal.getOrCreateInstance(addrEl);
        }

        bindEvents();
        state.initialized = true;
    }

    function destroy() {
        if (window.jQuery) window.jQuery(document).off(NS);
        clearTimeout(state.searchTimer);
        if (state._nativeHandlers && state._nativeHandlers.length) {
            state._nativeHandlers.forEach(function (h) {
                h.el.removeEventListener(h.event, h.fn);
            });
            state._nativeHandlers = [];
        }
        if (state.offcanvas && isDrawerOpen()) {
            state.forceClose = true;
            try { state.offcanvas.hide(); } catch (_) { /* ignore */ }
        }
        state.initialized = false;
        state.foods = [];
        state.cart = [];
    }

    window.initManagerOrderEdit = init;
    window.destroyManagerOrderEdit = destroy;
    window.openManagerOrderEdit = openEdit;
})();
