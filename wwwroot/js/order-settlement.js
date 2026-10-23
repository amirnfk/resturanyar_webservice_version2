/**
 * Shared order settlement modal (cash / partial / credit) for transition → status 8.
 * Compact by default; debt fields appear when «ثبت بدهی» is toggled on.
 */
(function (global) {
    'use strict';

    var MODAL_ID = 'orderSettlementModal';
    var CSS_ID = 'orderSettlementStyles';
    var CSS_VER = '9';
    var searchTimer = null;
    var paidFormatting = false;
    var submitting = false;
    var state = {
        orderId: null,
        currentStatusId: 7,
        orderTotal: 0,
        customerId: null,
        customerLabel: '',
        priorAccountBalance: 0,
        includePriorDebt: false,
        debtMode: false,
        urls: null,
        getToken: null,
        resolve: null
    };

    function toast(msg, type) {
        if (typeof global.showToast === 'function') global.showToast(msg, type || 'info');
        else if (type === 'error') alert(msg);
    }

    function toFaDigits(str) {
        return String(str).replace(/\d/g, function (d) {
            return '۰۱۲۳۴۵۶۷۸۹'[d];
        });
    }

    function formatMoney(n) {
        var v = Math.round(Number(n) || 0);
        try {
            return toFaDigits(v.toLocaleString('en-US'));
        } catch (e) {
            return toFaDigits(String(v));
        }
    }

    function parseAmount(raw) {
        if (raw == null) return NaN;
        var s = String(raw).trim()
            .replace(/[۰-۹]/g, function (d) { return String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)); })
            .replace(/[٠-٩]/g, function (d) { return String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)); })
            .replace(/[^\d.]/g, '');
        if (!s) return NaN;
        return parseFloat(s);
    }

    function priorBalancePositive() {
        return Number(state.priorAccountBalance || 0) > 0;
    }

    function includePriorEnabled() {
        return !!(state.debtMode && state.includePriorDebt && priorBalancePositive() && state.customerId);
    }

    function maxCashAllowed() {
        if (includePriorEnabled()) {
            return Math.round((state.orderTotal + Number(state.priorAccountBalance || 0)) * 100) / 100;
        }
        return state.orderTotal;
    }

    function splitCash(cash) {
        var c = Number(cash) || 0;
        if (c < 0) c = 0;
        var max = maxCashAllowed();
        if (c > max) c = max;
        var orderPaid = Math.min(c, state.orderTotal);
        var priorPaid = includePriorEnabled()
            ? Math.min(c - orderPaid, Number(state.priorAccountBalance || 0))
            : 0;
        var orderDebt = Math.round((state.orderTotal - orderPaid) * 100) / 100;
        var priorRemaining = Math.round((Number(state.priorAccountBalance || 0) - priorPaid) * 100) / 100;
        if (priorRemaining < 0) priorRemaining = 0;
        return {
            cash: Math.round(c * 100) / 100,
            orderPaid: Math.round(orderPaid * 100) / 100,
            priorPaid: Math.round(priorPaid * 100) / 100,
            orderDebt: orderDebt,
            priorRemaining: priorRemaining
        };
    }

    function ensureStyles() {
        var link = document.getElementById(CSS_ID);
        if (!link) {
            link = document.createElement('link');
            link.id = CSS_ID;
            link.rel = 'stylesheet';
            document.head.appendChild(link);
        }
        link.href = '/css/order-settlement.css?v=' + CSS_VER;
    }

    function setPaidAmountFormatted(amount) {
        var el = document.getElementById('osPaidAmount');
        if (!el) return;
        paidFormatting = true;
        el.value = isNaN(amount) ? '' : formatMoney(amount);
        paidFormatting = false;
    }

    function moneyCell(amount) {
        return '<span class="os-money" dir="ltr">' + formatMoney(amount) + '</span>';
    }

    function setDebtMode(on) {
        state.debtMode = !!on;
        var modalEl = document.getElementById(MODAL_ID);
        var dialog = modalEl && modalEl.querySelector('.modal-dialog');
        var toggle = document.getElementById('osDebtModeToggle');
        var toggleWrap = document.getElementById('osDebtToggleWrap');

        if (modalEl) modalEl.classList.toggle('is-debt', state.debtMode);
        if (dialog) dialog.classList.toggle('is-debt', state.debtMode);
        if (toggle) toggle.checked = state.debtMode;
        if (toggleWrap) toggleWrap.classList.toggle('is-on', state.debtMode);

        if (!state.debtMode) {
            state.includePriorDebt = false;
            var priorCb = document.getElementById('osIncludePriorDebt');
            if (priorCb) priorCb.checked = false;
            setPaidAmountFormatted(state.orderTotal);
        } else {
            // Debt mode has no «full pay» chip — default to zero received.
            setPaidAmountFormatted(0);
        }

        refreshDebtUi();
    }

    function ensureModal() {
        ensureStyles();
        var existing = document.getElementById(MODAL_ID);
        if (existing) return existing;

        var html =
            '<div class="modal fade" id="' + MODAL_ID + '" tabindex="-1" aria-hidden="true" dir="rtl">' +
            '  <div class="modal-dialog modal-dialog-centered">' +
            '    <div class="modal-content">' +
            '      <div class="os-header modal-header">' +
            '        <div class="d-flex align-items-center gap-3 flex-grow-1">' +
            '          <span class="os-header__icon" aria-hidden="true"><i class="fa-solid fa-cash-register"></i></span>' +
            '          <div>' +
            '            <h5 class="os-header__title modal-title">ثبت پرداخت سفارش</h5>' +
            '            <p class="os-header__sub" id="osOrderMeta">—</p>' +
            '          </div>' +
            '        </div>' +
            '        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="بستن"></button>' +
            '      </div>' +
            '      <div class="modal-body">' +
            '        <div class="os-layout" id="osLayout">' +
            '          <div class="os-hero">' +
            '            <span class="os-hero__label">مبلغ قابل پرداخت</span>' +
            '            <span class="os-hero__value" id="osOrderTotal">—</span>' +
            '            <div class="os-hero__meta d-none" id="osHeroMeta">' +
            '              <span class="os-hero__meta-item">مانده قبلی: <strong id="osPriorBalance">۰</strong></span>' +
            '              <span class="os-hero__meta-item d-none" id="osHeroDueItem">قابل دریافت: <strong id="osDueTotal">۰</strong></span>' +
            '            </div>' +
            '          </div>' +
            '          <label class="os-debt-toggle" id="osDebtToggleWrap" for="osDebtModeToggle">' +
            '            <span class="os-debt-toggle__text">ثبت بدهی</span>' +
            '            <span class="os-switch">' +
            '              <input type="checkbox" id="osDebtModeToggle" />' +
            '              <span class="os-switch__track"><span class="os-switch__thumb"></span></span>' +
            '            </span>' +
            '          </label>' +
            '          <div class="os-debt-panel" id="osDebtPanel">' +
            '            <div class="os-field">' +
            '              <label class="os-label" for="osPaidAmount" id="osPaidLabel">مبلغ دریافتی</label>' +
            '              <div class="os-money-input">' +
            '                <input type="text" inputmode="numeric" id="osPaidAmount" autocomplete="off" placeholder="۰" />' +
            '                <span class="os-money-input__unit">تومان</span>' +
            '              </div>' +
            '              <div class="os-quick" id="osQuickChips">' +
            '                <button type="button" class="os-chip" id="osChipZero" data-tip="credit">مبلغی دریافت نشد</button>' +
            '                <button type="button" class="os-chip" id="osChipHalf" data-tip="half">نصف مبلغ دریافت شد</button>' +
            '                <button type="button" class="os-chip d-none" id="osChipFull" data-tip="full" tabindex="-1" aria-hidden="true">کامل</button>' +
            '                <button type="button" class="os-chip d-none" id="osChipOrderOnly" data-tip="order" tabindex="-1" aria-hidden="true">فقط سفارش</button>' +
            '              </div>' +
            '            </div>' +
            '            <div class="os-debt-summary d-none" id="osDebtSummary">' +
            '              <span>بدهی سفارش</span>' +
            '              <strong id="osDebtAmount">۰</strong>' +
            '            </div>' +
            '            <label class="os-prior-card d-none" id="osIncludePriorWrap">' +
            '              <input type="checkbox" id="osIncludePriorDebt" />' +
            '              <span class="os-prior-card__title">تسویه مانده قبلی</span>' +
            '            </label>' +
            '            <div class="os-split d-none" id="osAllocSummary" aria-live="polite">' +
            '              <div class="os-split__row">' +
            '                <span>به سفارش</span>' +
            '                <strong id="osAllocOrder">۰</strong>' +
            '              </div>' +
            '              <div class="os-split__row" id="osAllocPriorRow">' +
            '                <span>به مانده قبلی</span>' +
            '                <strong id="osAllocPrior">۰</strong>' +
            '              </div>' +
            '              <div class="os-split__row is-muted" id="osAllocRemainRow">' +
            '                <span>مانده بعد از تسویه</span>' +
            '                <strong id="osAllocRemain">۰</strong>' +
            '              </div>' +
            '            </div>' +
            '            <div id="osCustomerBlock" class="os-customer d-none">' +
            '              <label class="os-label" id="osCustomerLabel">مشتری <span class="text-danger">*</span></label>' +
            '              <div id="osSelectedCustomer" class="os-customer__selected">' +
            '                <i class="fa-regular fa-user"></i><span>مشتری انتخاب نشده</span>' +
            '              </div>' +
            '              <input type="text" class="form-control" id="osCustomerSearch" placeholder="جستجو با نام یا موبایل" autocomplete="off" />' +
            '              <div id="osCustomerResults" class="list-group"></div>' +
            '            </div>' +
            '            <div class="os-side-note">' +
            '              <label class="os-label" for="osNote">یادداشت <span class="fw-normal text-muted">(اختیاری)</span></label>' +
            '              <input type="text" class="form-control" id="osNote" maxlength="500" placeholder="مثلاً تسویه در پایان ماه" />' +
            '            </div>' +
            '          </div>' +
            '        </div>' +
            '      </div>' +
            '      <div class="modal-footer">' +
            '        <button type="button" class="btn os-btn-confirm" id="osBtnConfirm">ثبت پرداخت</button>' +
            '        <button type="button" class="btn btn-outline-secondary os-btn-cancel" data-bs-dismiss="modal">انصراف</button>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';

        document.body.insertAdjacentHTML('beforeend', html);
        var modalEl = document.getElementById(MODAL_ID);
        modalEl.setAttribute('data-os-ver', CSS_VER);

        modalEl.querySelector('#osDebtModeToggle').addEventListener('change', function (e) {
            setDebtMode(!!e.target.checked);
        });

        modalEl.querySelector('#osPaidAmount').addEventListener('input', function (e) {
            if (paidFormatting) return;
            var el = e.target;
            var raw = el.value;
            var paid = parseAmount(raw);
            if (raw === '' || isNaN(paid)) {
                paidFormatting = true;
                el.value = '';
                paidFormatting = false;
                refreshDebtUi();
                return;
            }
            if (paid < 0) paid = 0;
            var max = maxCashAllowed();
            if (paid > max) paid = max;
            setPaidAmountFormatted(paid);
            refreshDebtUi();
        });

        modalEl.querySelector('#osIncludePriorDebt').addEventListener('change', function (e) {
            state.includePriorDebt = !!e.target.checked;
            if (state.includePriorDebt && !state.customerId) {
                toast('برای تسویه مانده قبلی، مشتری را انتخاب کنید.', 'error');
                e.target.checked = false;
                state.includePriorDebt = false;
                document.getElementById('osCustomerBlock').classList.remove('d-none');
                return;
            }
            if (state.includePriorDebt) {
                setPaidAmountFormatted(maxCashAllowed());
            } else {
                var cash = parseAmount(document.getElementById('osPaidAmount').value);
                if (isNaN(cash) || cash > state.orderTotal) cash = state.orderTotal;
                setPaidAmountFormatted(cash);
            }
            refreshDebtUi();
        });

        modalEl.querySelector('#osChipFull').addEventListener('click', function () {
            setPaidAmountFormatted(maxCashAllowed());
            refreshDebtUi();
        });
        modalEl.querySelector('#osChipOrderOnly').addEventListener('click', function () {
            setPaidAmountFormatted(state.orderTotal);
            refreshDebtUi();
        });
        modalEl.querySelector('#osChipHalf').addEventListener('click', function () {
            setPaidAmountFormatted(Math.floor(maxCashAllowed() / 2));
            refreshDebtUi();
        });
        modalEl.querySelector('#osChipZero').addEventListener('click', function () {
            setPaidAmountFormatted(0);
            refreshDebtUi();
        });

        modalEl.querySelector('#osBtnConfirm').addEventListener('click', submitSettlement);
        modalEl.querySelector('#osCustomerSearch').addEventListener('input', function (e) {
            clearTimeout(searchTimer);
            var q = e.target.value;
            searchTimer = setTimeout(function () { searchCustomers(q); }, 280);
        });

        modalEl.addEventListener('hidden.bs.modal', function () {
            if (state.resolve) {
                state.resolve(false);
                state.resolve = null;
            }
        });

        return modalEl;
    }

    function refreshDebtUi() {
        var cash = parseAmount(document.getElementById('osPaidAmount').value);
        if (isNaN(cash)) cash = state.debtMode ? 0 : state.orderTotal;
        if (!state.debtMode) cash = state.orderTotal;

        var split = splitCash(cash);
        var debt = split.orderDebt;
        var paid = split.orderPaid;
        var priorOn = includePriorEnabled();
        var priorBal = Number(state.priorAccountBalance || 0);

        var paidLabel = document.getElementById('osPaidLabel');
        if (paidLabel) {
            paidLabel.textContent = priorOn ? 'مبلغ دریافتی کل' : 'مبلغ دریافتی';
        }

        var heroMeta = document.getElementById('osHeroMeta');
        var dueItem = document.getElementById('osHeroDueItem');
        var priorBalEl = document.getElementById('osPriorBalance');
        var dueEl = document.getElementById('osDueTotal');
        if (state.debtMode && priorBalancePositive() && state.customerId) {
            if (heroMeta) heroMeta.classList.remove('d-none');
            if (priorBalEl) priorBalEl.textContent = formatMoney(priorBal);
            if (priorOn) {
                if (dueItem) dueItem.classList.remove('d-none');
                if (dueEl) dueEl.textContent = formatMoney(maxCashAllowed());
            } else if (dueItem) {
                dueItem.classList.add('d-none');
            }
        } else if (heroMeta) {
            heroMeta.classList.add('d-none');
        }

        var toggleWrap = document.getElementById('osIncludePriorWrap');
        if (toggleWrap) {
            toggleWrap.classList.toggle('d-none', !(state.debtMode && priorBalancePositive() && state.customerId));
            toggleWrap.classList.toggle('is-on', priorOn);
        }

        var alloc = document.getElementById('osAllocSummary');
        if (alloc) {
            if (priorOn) {
                alloc.classList.remove('d-none');
                document.getElementById('osAllocOrder').innerHTML = moneyCell(split.orderPaid);
                document.getElementById('osAllocPrior').innerHTML = moneyCell(split.priorPaid);
                document.getElementById('osAllocRemain').innerHTML = moneyCell(split.priorRemaining);
                var priorRow = document.getElementById('osAllocPriorRow');
                if (priorRow) {
                    priorRow.classList.toggle('is-zero', split.priorPaid <= 0);
                    priorRow.classList.toggle('is-ok', split.priorPaid > 0);
                }
                var remainRow = document.getElementById('osAllocRemainRow');
                if (remainRow) {
                    remainRow.classList.toggle('is-cleared', split.priorRemaining <= 0);
                }
            } else {
                alloc.classList.add('d-none');
            }
        }

        var debtSummary = document.getElementById('osDebtSummary');
        var debtAmountEl = document.getElementById('osDebtAmount');
        if (debtAmountEl) debtAmountEl.textContent = formatMoney(debt);
        if (debtSummary) {
            debtSummary.classList.toggle('d-none', !(state.debtMode && debt > 0));
        }

        var btn = document.getElementById('osBtnConfirm');
        var type = debt <= 0 ? 'cash' : (paid > 0 ? 'partial' : 'credit');
        if (btn) {
            btn.textContent = type === 'credit' ? 'ثبت بدهی' : 'ثبت پرداخت';
        }

        if (state.debtMode) {
            var halfTarget = Math.floor(maxCashAllowed() / 2);
            document.getElementById('osChipZero').classList.toggle('is-active', type === 'credit');
            document.getElementById('osChipHalf').classList.toggle('is-active',
                split.cash > 0 && Math.round(split.cash) === halfTarget);
        }

        var needCustomer = state.debtMode && (debt > 0 || priorOn || state.includePriorDebt);
        var block = document.getElementById('osCustomerBlock');
        var custLabel = document.getElementById('osCustomerLabel');
        if (needCustomer) {
            block.classList.remove('d-none');
            if (custLabel) {
                custLabel.innerHTML = (priorOn || state.includePriorDebt)
                    ? 'مشتری <span class="text-danger">*</span>'
                    : 'مشتری <span class="text-danger">*</span>';
            }
        } else {
            block.classList.add('d-none');
        }
    }

    function updatePriorDebtBanner(balance) {
        state.priorAccountBalance = Number(balance || 0);
        var toggleWrap = document.getElementById('osIncludePriorWrap');
        if (!(state.debtMode && state.priorAccountBalance > 0 && state.customerId)) {
            if (toggleWrap) toggleWrap.classList.add('d-none');
            state.includePriorDebt = false;
            var cb = document.getElementById('osIncludePriorDebt');
            if (cb) cb.checked = false;
        }
        refreshDebtUi();
    }

    function setSelectedCustomer(id, label, balance) {
        state.customerId = id;
        state.customerLabel = label || '';
        var el = document.getElementById('osSelectedCustomer');
        if (id) {
            el.className = 'os-customer__selected is-picked';
            el.innerHTML = '<i class="fa-solid fa-circle-check"></i><span>' +
                escapeHtml(state.customerLabel) + '</span>';
            if (balance !== undefined && balance !== null) {
                updatePriorDebtBanner(balance);
            } else {
                refreshDebtUi();
            }
        } else {
            el.className = 'os-customer__selected';
            el.innerHTML = '<i class="fa-regular fa-user"></i><span>مشتری انتخاب نشده</span>';
            state.includePriorDebt = false;
            var cb = document.getElementById('osIncludePriorDebt');
            if (cb) cb.checked = false;
            updatePriorDebtBanner(0);
        }
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    async function searchCustomers(query) {
        var box = document.getElementById('osCustomerResults');
        if (!state.urls || !state.urls.searchCustomersUrl) {
            box.innerHTML = '';
            return;
        }
        try {
            var url = state.urls.searchCustomersUrl +
                (state.urls.searchCustomersUrl.indexOf('?') >= 0 ? '&' : '?') +
                'search=' + encodeURIComponent(query || '') + '&page=1&pageSize=8';
            var res = await fetch(url, { method: 'GET', credentials: 'same-origin' });
            var data = await res.json();
            if (!data.success) {
                box.innerHTML = '<div class="list-group-item text-danger">' + escapeHtml(data.message || 'خطا') + '</div>';
                return;
            }
            var items = data.data || [];
            if (!items.length) {
                box.innerHTML = '<div class="list-group-item text-muted">موردی یافت نشد</div>';
                return;
            }
            box.innerHTML = items.map(function (c) {
                var id = c.customerId || c.CustomerId;
                var mobile = c.mobile || c.Mobile || '';
                var name = c.fullName || c.FullName || '';
                var bal = Number(c.accountBalance ?? c.AccountBalance ?? 0);
                var label = (name ? name + ' — ' : '') + mobile;
                var badge = bal > 0
                    ? ' <span class="debtor-badge">بدهکار · ' + formatMoney(bal) + '</span>'
                    : '';
                return '<button type="button" class="list-group-item list-group-item-action" data-id="' + id +
                    '" data-label="' + escapeHtml(label) + '" data-balance="' + bal + '">' +
                    escapeHtml(label) + badge + '</button>';
            }).join('');
            box.querySelectorAll('button[data-id]').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    setSelectedCustomer(
                        parseInt(btn.getAttribute('data-id'), 10),
                        btn.getAttribute('data-label'),
                        btn.getAttribute('data-balance'));
                    box.innerHTML = '';
                    document.getElementById('osCustomerSearch').value = '';
                });
            });
        } catch (err) {
            box.innerHTML = '<div class="list-group-item text-danger">خطا در جستجو</div>';
        }
    }

    async function loadPreview() {
        var url = state.urls.previewUrl +
            (state.urls.previewUrl.indexOf('?') >= 0 ? '&' : '?') +
            'orderId=' + encodeURIComponent(state.orderId);
        var res = await fetch(url, { method: 'GET', credentials: 'same-origin' });
        var data = await res.json();
        if (!data.success) throw new Error(data.message || 'خطا در دریافت مبلغ سفارش');
        var d = data.data || {};
        state.orderTotal = Number(d.orderTotal || d.OrderTotal || 0);

        var liveStatus = Number(d.statusId || d.StatusId || state.currentStatusId);
        if (!isNaN(liveStatus) && liveStatus > 0) state.currentStatusId = liveStatus;

        if (d.alreadySettled || d.AlreadySettled) {
            throw new Error('پرداخت این سفارش قبلاً ثبت شده است. صفحه را تازه کنید.');
        }

        document.getElementById('osOrderMeta').textContent = 'سفارش #' + toFaDigits(state.orderId);
        document.getElementById('osOrderTotal').textContent = formatMoney(state.orderTotal);
        setPaidAmountFormatted(state.orderTotal);

        state.includePriorDebt = false;
        var cb = document.getElementById('osIncludePriorDebt');
        if (cb) cb.checked = false;

        var priorBal = Number(d.customerAccountBalance ?? d.CustomerAccountBalance ?? 0);
        if (d.customerId || d.CustomerId) {
            var cid = d.customerId || d.CustomerId;
            var label = ((d.customerFullName || d.CustomerFullName || '') +
                (d.customerMobile || d.CustomerMobile ? ' — ' + (d.customerMobile || d.CustomerMobile) : '')).trim();
            setSelectedCustomer(cid, label || ('#' + cid), priorBal);
        } else {
            setSelectedCustomer(null, '', 0);
        }

        setDebtMode(false);
    }

    async function submitSettlement() {
        if (submitting) return;

        var cash = state.debtMode
            ? parseAmount(document.getElementById('osPaidAmount').value)
            : state.orderTotal;
        if (isNaN(cash) || cash < 0) {
            toast('مبلغ دریافتی نامعتبر است.', 'error');
            return;
        }
        var max = maxCashAllowed();
        if (cash > max) {
            toast(includePriorEnabled()
                ? 'مبلغ دریافتی بیشتر از مجموع سفارش و مانده قبلی است.'
                : 'مبلغ دریافتی بیشتر از مبلغ سفارش است.', 'error');
            return;
        }

        if (state.includePriorDebt && !state.customerId) {
            toast('برای تسویه مانده قبلی، مشتری را انتخاب کنید.', 'error');
            document.getElementById('osCustomerBlock').classList.remove('d-none');
            return;
        }

        var split = splitCash(cash);
        if (split.orderDebt > 0 && !state.customerId) {
            toast('برای ثبت بدهی، مشتری را انتخاب کنید.', 'error');
            if (!state.debtMode) setDebtMode(true);
            document.getElementById('osCustomerBlock').classList.remove('d-none');
            return;
        }

        var btn = document.getElementById('osBtnConfirm');
        submitting = true;
        btn.disabled = true;

        try {
            var form = new URLSearchParams();
            form.append('orderId', String(state.orderId));
            form.append('currentStatusId', String(state.currentStatusId));
            form.append('paidAmount', String(split.orderPaid));
            form.append('cashReceived', String(split.cash));
            form.append('includePriorBalancePayment', includePriorEnabled() ? 'true' : 'false');
            if (state.customerId) form.append('customerId', String(state.customerId));
            var note = document.getElementById('osNote').value;
            if (note) form.append('note', note);

            var token = state.getToken ? state.getToken() : '';
            var res = await fetch(state.urls.settleUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                    'RequestVerificationToken': token
                },
                body: form.toString(),
                credentials: 'same-origin'
            });
            var data = await res.json();
            if (!data.success) {
                toast(data.message || 'خطا در ثبت پرداخت', 'error');
                return;
            }

            toast(data.message || 'پرداخت سفارش ثبت شد.', 'success');
            var resolve = state.resolve;
            state.resolve = null;
            var modalEl = document.getElementById(MODAL_ID);
            var modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();
            if (resolve) resolve({ success: true, data: data });
        } catch (err) {
            console.error(err);
            toast('خطا در ارتباط با سرور.', 'error');
        } finally {
            submitting = false;
            btn.disabled = false;
        }
    }

    /**
     * @returns {Promise<false|{success:true,data:any}>}
     */
    function settleViaModal(options) {
        state.orderId = options.orderId;
        state.currentStatusId = options.currentStatusId || 7;
        state.urls = options.urls;
        state.getToken = options.getToken || function () {
            var inp = document.querySelector('input[name="__RequestVerificationToken"]');
            return inp ? inp.value : '';
        };
        state.customerId = null;
        state.includePriorDebt = false;
        state.debtMode = false;
        state.resolve = null;
        submitting = false;

        var modalEl = ensureModal();
        // Rebuild if an older modal shell is still in the DOM.
        if (modalEl.getAttribute('data-os-ver') !== CSS_VER ||
            !document.getElementById('osLayout') ||
            !document.getElementById('osDebtModeToggle') ||
            !document.getElementById('osDebtPanel') ||
            !document.getElementById('osIncludePriorDebt')) {
            modalEl.remove();
            modalEl = ensureModal();
        }

        document.getElementById('osNote').value = '';
        document.getElementById('osCustomerResults').innerHTML = '';
        document.getElementById('osCustomerSearch').value = '';
        var cb = document.getElementById('osIncludePriorDebt');
        if (cb) cb.checked = false;
        updatePriorDebtBanner(0);

        return new Promise(function (resolve) {
            state.resolve = resolve;
            loadPreview()
                .then(function () {
                    var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
                    modal.show();
                })
                .catch(function (err) {
                    toast(err.message || 'خطا در بارگذاری اطلاعات پرداخت', 'error');
                    state.resolve = null;
                    resolve(false);
                });
        });
    }

    global.OrderSettlement = { settleViaModal: settleViaModal };
})(window);
