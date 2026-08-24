(function () {
    var allDefinitions = [];
    var recalcTimer = null;
    var modalMode = 'issue'; // issue | edit
    var orderHasDiscountCode = false;
    var appliedDiscountCode = null;
    var discountSectionExpanded = false;

    function isReceiptChargesEnabled() {
        return window.receiptChargesEnabled === true;
    }

    function updateDiscountCodeState(previewData) {
        var code = (previewData && (previewData.orderDiscountCode || previewData.OrderDiscountCode)) || '';
        code = code ? String(code).trim().toUpperCase() : '';
        if (!code && previewData) {
            var lines = previewData.chargeLines || previewData.ChargeLines || [];
            for (var i = 0; i < lines.length; i++) {
                var lineCode = String(lines[i].code || lines[i].Code || '').toLowerCase();
                if (lineCode !== 'discount_code') continue;
                var title = String(lines[i].title || lines[i].Title || '');
                var open = title.lastIndexOf('(');
                var close = title.lastIndexOf(')');
                if (open >= 0 && close > open + 1) {
                    code = title.substring(open + 1, close).trim().toUpperCase();
                }
                break;
            }
        }
        // Code string is the source of truth for the applied UI state.
        appliedDiscountCode = code || null;
        orderHasDiscountCode = !!appliedDiscountCode
            || !!(previewData && (previewData.hasOrderDiscountCode || previewData.HasOrderDiscountCode));
    }

    function setDiscountCodeFeedback(message, isOk) {
        var el = document.getElementById('receiptDiscountCodeFeedback');
        if (!el) return;
        if (!message) {
            el.hidden = true;
            el.textContent = '';
            el.classList.remove('is-ok', 'is-error');
            return;
        }
        el.hidden = false;
        el.textContent = message;
        el.classList.toggle('is-ok', !!isOk);
        el.classList.toggle('is-error', !isOk);
    }

    function setDiscountSectionExpanded(expanded) {
        discountSectionExpanded = !!expanded;
        var section = document.getElementById('receiptDiscountSection');
        var header = document.getElementById('receiptDiscountHeader');
        var body = document.getElementById('receiptDiscountBody');
        if (section) section.classList.toggle('is-expanded', discountSectionExpanded);
        if (header) header.setAttribute('aria-expanded', discountSectionExpanded ? 'true' : 'false');
        if (body) body.hidden = !discountSectionExpanded;
        renderDiscountCodeUi();
        if (discountSectionExpanded && !appliedDiscountCode) {
            var input = document.getElementById('receiptDiscountCodeInput');
            if (input) {
                setTimeout(function () { input.focus(); }, 0);
            }
        }
    }

    function renderDiscountCodeUi() {
        var applied = !!(appliedDiscountCode && appliedDiscountCode.length);
        var entry = document.getElementById('receiptDiscountCodeEntry');
        var appliedBox = document.getElementById('receiptDiscountCodeApplied');
        var input = document.getElementById('receiptDiscountCodeInput');
        var appliedChip = document.getElementById('receiptAppliedDiscountCode');
        var headerChip = document.getElementById('receiptDiscountHeaderChip');
        var headerHint = document.getElementById('receiptDiscountHeaderHint');

        if (entry) entry.hidden = !(discountSectionExpanded && !applied);
        if (appliedBox) appliedBox.hidden = !(discountSectionExpanded && applied);

        if (applied) {
            if (appliedChip) appliedChip.textContent = appliedDiscountCode;
            if (input) input.value = appliedDiscountCode;
        }

        if (headerChip) {
            if (applied && !discountSectionExpanded) {
                headerChip.hidden = false;
                headerChip.textContent = appliedDiscountCode;
            } else {
                headerChip.hidden = true;
                headerChip.textContent = '';
            }
        }

        if (headerHint) {
            headerHint.classList.toggle('is-applied', applied);
            if (applied) {
                if (discountSectionExpanded) {
                    headerHint.hidden = true;
                    headerHint.textContent = '';
                } else {
                    headerHint.hidden = false;
                    headerHint.textContent = 'کد فعال است — برای جزئیات کلیک کنید';
                }
            } else {
                headerHint.hidden = false;
                headerHint.textContent = discountSectionExpanded
                    ? 'کد را وارد کنید و اعمال کنید'
                    : 'اختیاری — برای افزودن کلیک کنید';
            }
        }
    }

    function syncDiscountCodeInput(previewData) {
        updateDiscountCodeState(previewData);
        if (appliedDiscountCode && !discountSectionExpanded) {
            setDiscountSectionExpanded(true);
            return;
        }
        renderDiscountCodeUi();
    }

    function sanitizeReceiptDiscountCode(raw) {
        return String(raw || '')
            .toUpperCase()
            .replace(/[^A-Z0-9]/g, '');
    }

    async function refreshModalAfterDiscountCodeChange(previewData) {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;

        updateDiscountCodeState(previewData);
        if (!discountSectionExpanded) {
            setDiscountSectionExpanded(true);
        } else {
            renderDiscountCodeUi();
        }

        // Keep current charge selections; refresh totals from a live preview.
        // Do NOT reload PreviewDefaults — that resets charge rows and may return a stale issued snapshot.
        try {
            await recalculateModal(false);
        } catch (err) {
            console.error('refreshModalAfterDiscountCodeChange error:', err);
            renderPreviewBox(modal, previewData);
        }
    }

    async function applyReceiptDiscountCode() {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;
        const orderId = parseInt(modal.dataset.orderId, 10);
        if (!orderId) return;

        const input = document.getElementById('receiptDiscountCodeInput');
        const code = sanitizeReceiptDiscountCode(input && input.value);
        if (input) input.value = code;
        if (!code) {
            setDiscountCodeFeedback('کد تخفیف را وارد کنید.', false);
            return;
        }

        setModalBusy(true, 'در حال اعمال کد تخفیف...');
        try {
            const { data } = await fetchJson(`/Receipt/SetDiscountCode?orderId=${orderId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ code: code })
            });
            if (!data?.success) {
                setDiscountCodeFeedback(data?.message || 'اعمال کد ناموفق بود.', false);
                if (typeof showToast === 'function') showToast(data?.message || 'اعمال کد ناموفق بود.', 'error');
                return;
            }
            appliedDiscountCode = code;
            orderHasDiscountCode = true;
            setDiscountSectionExpanded(true);
            setDiscountCodeFeedback(data.message || 'کد روی سفارش ثبت شد.', true);
            if (typeof showToast === 'function') showToast(data.message || 'کد تخفیف ثبت شد.', 'success');
            await refreshModalAfterDiscountCodeChange(data.data);
        } catch (err) {
            console.error('applyReceiptDiscountCode error:', err);
            setDiscountCodeFeedback('خطا در اعمال کد تخفیف', false);
        } finally {
            setModalBusy(false);
        }
    }

    async function clearReceiptDiscountCode() {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;
        const orderId = parseInt(modal.dataset.orderId, 10);
        if (!orderId) return;

        setModalBusy(true, 'در حال حذف کد تخفیف...');
        try {
            const { data } = await fetchJson(`/Receipt/SetDiscountCode?orderId=${orderId}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ code: null })
            });
            if (!data?.success) {
                setDiscountCodeFeedback(data?.message || 'حذف کد ناموفق بود.', false);
                if (typeof showToast === 'function') showToast(data?.message || 'حذف کد ناموفق بود.', 'error');
                return;
            }
            appliedDiscountCode = null;
            orderHasDiscountCode = false;
            var input = document.getElementById('receiptDiscountCodeInput');
            if (input) input.value = '';
            setDiscountSectionExpanded(true);
            setDiscountCodeFeedback('کد تخفیف از سفارش حذف شد.', true);
            if (typeof showToast === 'function') showToast(data.message || 'کد حذف شد.', 'success');
            await refreshModalAfterDiscountCodeChange(data.data || { hasOrderDiscountCode: false, orderDiscountCode: null });
        } catch (err) {
            console.error('clearReceiptDiscountCode error:', err);
            setDiscountCodeFeedback('خطا در حذف کد تخفیف', false);
        } finally {
            setModalBusy(false);
        }
    }

    async function fetchJson(url, options) {
        const response = await fetch(url, options);
        let data = null;
        try {
            data = await response.json();
        } catch {
            data = { success: false, message: 'پاسخ سرور نامعتبر بود.' };
        }
        return { response, data };
    }

    function isReceiptIssued(statusData) {
        if (!statusData) return false;
        return statusData.isIssued === true || statusData.IsIssued === true;
    }

    function openReceiptHtml(orderId) {
        const htmlWindow = window.open(`/Receipt/Html?orderId=${orderId}`, '_blank', 'width=900,height=700');
        if (!htmlWindow && typeof showToast === 'function') {
            showToast('لطفا مسدودکننده پنجره را غیرفعال کنید', 'error');
        }
        return !!htmlWindow;
    }

    async function printLegacyInvoice(orderId) {
        if (typeof printInvoice === 'function') {
            printInvoice(orderId);
        }
    }

    function setButtonLoading(btn, isLoading, loadingLabel) {
        if (!btn) return;

        if (isLoading) {
            if (!btn.dataset.defaultLabel) {
                btn.dataset.defaultLabel = btn.textContent.trim();
            }
            btn.classList.add('is-loading');
            btn.disabled = true;
            btn.setAttribute('aria-busy', 'true');
            btn.innerHTML = `<span class="spinner-border spinner-border-sm ms-1" role="status" aria-hidden="true"></span>${loadingLabel || 'لطفا صبر کنید...'}`;
            return;
        }

        btn.classList.remove('is-loading');
        btn.disabled = false;
        btn.removeAttribute('aria-busy');
        btn.textContent = btn.dataset.defaultLabel || btn.textContent;
    }

    function setModalBusy(isBusy, message) {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;

        const overlay = modal.querySelector('#receiptModalLoading');
        const text = modal.querySelector('#receiptModalLoadingText');
        const previewBtn = modal.querySelector('#receiptPreviewBtn');
        const issueBtn = modal.querySelector('#receiptIssueBtn');
        const saveBtn = modal.querySelector('#receiptSaveBtn');
        const closeBtn = modal.querySelector('#receiptModalClose');

        if (text && message) text.textContent = message;
        if (overlay) overlay.hidden = !isBusy;

        modal.dataset.busy = isBusy ? '1' : '0';

        if (previewBtn) previewBtn.disabled = isBusy;
        if (issueBtn) issueBtn.disabled = isBusy;
        if (saveBtn) saveBtn.disabled = isBusy;
        if (closeBtn) closeBtn.disabled = isBusy;

        modal.querySelectorAll('.receipt-order-type__btn, .charge-enabled, .charge-value').forEach(function (el) {
            el.disabled = isBusy;
        });
    }

    function orderTypeToFlag(orderType) {
        switch (parseInt(orderType, 10)) {
            case 1: return 2; // بیرون‌بر
            case 2: return 4; // ارسال
            default: return 1; // سالن
        }
    }

    function getAppliesFlags(def) {
        const value = def.appliesToOrderTypes ?? def.AppliesToOrderTypes ?? 0;
        return Number(value) || 0;
    }

    function definitionApplies(def, orderType) {
        const flags = getAppliesFlags(def);
        if (!flags) return false;
        return (flags & orderTypeToFlag(orderType)) !== 0;
    }

    function getSelectedOrderType(modal) {
        const hidden = modal.querySelector('#receiptOrderType');
        return parseInt(hidden?.value || '0', 10);
    }

    function setSelectedOrderType(modal, orderType) {
        const value = String(orderType ?? 0);
        const hidden = modal.querySelector('#receiptOrderType');
        if (hidden) hidden.value = value;

        modal.querySelectorAll('.receipt-order-type__btn').forEach(function (btn) {
            const active = btn.getAttribute('data-order-type') === value;
            btn.classList.toggle('is-active', active);
            btn.setAttribute('aria-pressed', active ? 'true' : 'false');
        });
    }

    function toPersianDigits(value) {
        return String(value ?? '').replace(/\d/g, function (d) {
            return '۰۱۲۳۴۵۶۷۸۹'[d];
        });
    }

    function toEnglishDigits(value) {
        return String(value ?? '')
            .replace(/[۰-۹]/g, function (d) { return String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)); })
            .replace(/[٠-٩]/g, function (d) { return String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)); });
    }

    function parseNumber(value) {
        const cleaned = toEnglishDigits(value).replace(/[^\d.-]/g, '');
        if (!cleaned || cleaned === '-' || cleaned === '.' || cleaned === '-.') return 0;
        const num = parseFloat(cleaned);
        return Number.isNaN(num) ? 0 : num;
    }

    function clampChargeValue(isPercent, value) {
        var num = Number(value);
        if (Number.isNaN(num) || num < 0) num = 0;
        if (isPercent && num > 100) num = 100;
        return Math.round(num * 100) / 100;
    }

    function formatPercentDisplay(value) {
        var num = clampChargeValue(true, value);
        return toPersianDigits(String(num));
    }

    function formatFixedDisplay(value) {
        var num = clampChargeValue(false, value);
        return toPersianDigits(Math.round(num).toLocaleString('en-US'));
    }

    function formatChargeValueDisplay(isPercent, value) {
        return isPercent ? formatPercentDisplay(value) : formatFixedDisplay(value);
    }

    function categoryMeta(category) {
        switch (Number(category)) {
            case 0: return { label: 'تخفیف', className: 'is-discount' };
            case 2: return { label: 'مالیات', className: 'is-tax' };
            default: return { label: 'هزینه', className: 'is-fee' };
        }
    }

    function isPercentage(def) {
        const type = def.calculationType ?? def.CalculationType;
        return Number(type) === 0;
    }

    function buildChargeRow(def) {
        const meta = categoryMeta(def.chargeCategory ?? def.ChargeCategory);
        const percent = isPercentage(def);
        const unit = percent ? 'درصد' : 'تومان';
        const checked = def.isEnabled ? 'checked' : '';
        const value = clampChargeValue(percent, def.value ?? 0);
        const displayValue = formatChargeValueDisplay(percent, value);

        const row = document.createElement('div');
        row.className = 'receipt-charge-row';
        row.innerHTML = `
            <div class="receipt-charge-row__main">
                <input type="checkbox" class="form-check-input charge-enabled m-0"
                       data-id="${def.id}" data-code="${def.code || ''}"
                       ${checked} />
                <div>
                    <div class="receipt-charge-row__title">${def.title || 'بدون عنوان'}</div>
                    <span class="receipt-charge-row__badge ${meta.className}">${meta.label}</span>
                </div>
            </div>
            <div class="receipt-charge-row__value">
                <input type="text" class="form-control form-control-sm charge-value"
                       data-id="${def.id}" data-percent="${percent ? '1' : '0'}"
                       data-raw-value="${value}" value="${displayValue}"
                       inputmode="${percent ? 'decimal' : 'numeric'}" autocomplete="off" />
                <span class="receipt-charge-row__unit">${unit}</span>
            </div>`;
        return row;
    }

    function renderChargeList(modal, orderType) {
        const list = modal.querySelector('#receiptChargeList');
        const empty = modal.querySelector('#receiptChargeEmpty');
        if (!list) return;

        const applicable = allDefinitions.filter(function (def) {
            return definitionApplies(def, orderType);
        });

        list.innerHTML = '';
        applicable.forEach(function (def) {
            list.appendChild(buildChargeRow(def));
        });

        if (empty) empty.hidden = applicable.length > 0;
    }

    function formatMoney(value) {
        const num = Number(value);
        if (Number.isNaN(num)) return toPersianDigits(value ?? '');
        return toPersianDigits(Math.round(num).toLocaleString('en-US'));
    }

    function renderPreviewBox(modal, previewData) {
        const previewBox = modal.querySelector('#receiptPreviewBox');
        if (!previewBox) return;

        if (!previewData) {
            previewBox.innerHTML = '<div class="receipt-summary__placeholder">پس از محاسبه، خلاصه مبلغ اینجا نمایش داده می‌شود.</div>';
            return;
        }

        const itemsSubtotal = previewData.itemsSubtotal ?? previewData.ItemsSubtotal ?? 0;
        const grandTotal = previewData.grandTotal ?? previewData.GrandTotal ?? 0;
        const lines = previewData.chargeLines || previewData.ChargeLines || [];

        let linesHtml = '';
        lines.forEach(function (line) {
            const title = line.title || line.Title || 'هزینه';
            const amount = Number(line.calculatedAmount ?? line.CalculatedAmount ?? 0);
            const category = Number(line.category ?? line.Category ?? -1);
            const isDiscount = category === 0;
            const money = formatMoney(Math.abs(amount));
            const amountInner = isDiscount
                ? ('- ' + money + ' تومان')
                : (money + ' تومان');
            const amountClass = isDiscount
                ? 'receipt-summary__amount is-discount'
                : 'receipt-summary__amount';
            linesHtml += `
                <div class="receipt-summary__row is-line">
                    <span>${title}</span>
                    <span class="${amountClass}" dir="ltr">${amountInner}</span>
                </div>`;
        });

        previewBox.innerHTML = `
            <div class="receipt-summary__row">
                <span>جمع اقلام</span>
                <span class="receipt-summary__amount" dir="ltr">${formatMoney(itemsSubtotal)} تومان</span>
            </div>
            ${linesHtml}
            <div class="receipt-summary__row is-total">
                <span>جمع کل</span>
                <span class="receipt-summary__amount" dir="ltr">${formatMoney(grandTotal)} تومان</span>
            </div>`;
    }

    function parseUtcIssuedAt(issuedAt) {
        if (issuedAt == null || issuedAt === '') return null;
        if (typeof issuedAt === 'number') {
            const dt = new Date(issuedAt);
            return Number.isNaN(dt.getTime()) ? null : dt;
        }
        let s = String(issuedAt).trim();
        // DB/EF often returns UTC without "Z"; treat bare ISO as UTC so Tehran conversion is correct.
        if (/^\d{4}-\d{2}-\d{2}T/.test(s) && !/(Z|[+-]\d{2}:?\d{2})$/i.test(s)) {
            s += 'Z';
        }
        const dt = new Date(s);
        return Number.isNaN(dt.getTime()) ? null : dt;
    }

    function formatIssuedAt(issuedAt) {
        const dt = parseUtcIssuedAt(issuedAt);
        if (!dt) return '';
        const formatted = dt.toLocaleString('fa-IR', { timeZone: 'Asia/Tehran' });
        return `<span class="order-receipt-card__meta">صدور: ${toPersianDigits(formatted)}</span>`;
    }

    function updateOrderReceiptBadge(orderId, receipt) {
        const card = document.getElementById(`order-${orderId}`);
        if (!card || !receipt) return;

        const grandTotal = receipt.grandTotal ?? receipt.GrandTotal;
        if (grandTotal == null) return;

        card.querySelectorAll('.receipt-estimate-badge').forEach(function (el) {
            el.remove();
        });

        const issuedAt = receipt.issuedAt ?? receipt.IssuedAt;
        let badge = card.querySelector('.receipt-issued-badge');
        if (!badge) {
            badge = document.createElement('div');
            const totalBox = card.querySelector('.total-box');
            if (totalBox) {
                totalBox.insertAdjacentElement('afterend', badge);
            } else {
                card.appendChild(badge);
            }
        }

        badge.className = 'order-receipt-card order-receipt-card--issued receipt-issued-badge';
        badge.innerHTML = `
            <div class="order-receipt-card__icon" aria-hidden="true">
                <i class="fa-solid fa-circle-check"></i>
            </div>
            <div class="order-receipt-card__content">
                <span class="order-receipt-card__label">مبلغ فاکتور</span>
                <strong class="order-receipt-card__amount">${formatMoney(grandTotal)} تومان</strong>
                ${formatIssuedAt(issuedAt)}
            </div>`;

        ensureReceiptActionButtons(card, orderId);
    }

    function ensureReceiptActionButtons(card, orderId) {
        if (!card) return;

        const printBtn = card.querySelector('.receipt-print-btn');
        if (printBtn && !printBtn.classList.contains('is-loading')) {
            printBtn.dataset.defaultLabel = 'چاپ مجدد';
            printBtn.textContent = 'چاپ مجدد';
        }

        let editBtn = card.querySelector('.receipt-edit-btn');
        if (editBtn) return;

        editBtn = document.createElement('button');
        editBtn.type = 'button';
        editBtn.className = 'btn btn-sm btn-outline-primary receipt-edit-btn';
        editBtn.textContent = 'ویرایش فاکتور';

        var orderType = '0';
        if (printBtn) {
            var match = (printBtn.getAttribute('onclick') || '').match(/handleReceiptPrint\((\d+)\s*,\s*(\d+)/);
            if (match) orderType = match[2];
        }
        editBtn.setAttribute('onclick', 'handleReceiptEdit(' + orderId + ', ' + orderType + ', this)');

        if (printBtn && printBtn.parentNode) {
            printBtn.insertAdjacentElement('afterend', editBtn);
        } else {
            card.appendChild(editBtn);
        }
    }

    window.applyIssuedReceipt = function (orderId, receipt) {
        updateOrderReceiptBadge(orderId, receipt);
    };

    function collectRequest(modal) {
        const orderType = getSelectedOrderType(modal);
        const charges = Array.from(modal.querySelectorAll('.charge-enabled')).map(function (cb) {
            const id = cb.dataset.id;
            const valueInput = modal.querySelector(`.charge-value[data-id="${id}"]`);
            const isPercent = valueInput?.dataset.percent === '1';
            const value = clampChargeValue(isPercent, parseNumber(valueInput?.dataset.rawValue || valueInput?.value));
            if (valueInput) {
                valueInput.dataset.rawValue = String(value);
                valueInput.value = formatChargeValueDisplay(isPercent, value);
            }
            return {
                definitionId: parseInt(id, 10),
                code: cb.dataset.code || null,
                isEnabled: cb.checked,
                value: value
            };
        });

        // Send non-applicable charges as disabled so they never leak into the calculation payload.
        allDefinitions.forEach(function (def) {
            if (definitionApplies(def, orderType)) return;
            if (charges.some(function (c) { return c.definitionId === def.id; })) return;
            charges.push({
                definitionId: def.id,
                code: def.code || null,
                isEnabled: false,
                value: clampChargeValue(isPercentage(def), def.value)
            });
        });

        return { orderType: orderType, charges: charges };
    }

    async function previewReceipt(orderId, body) {
        const { data } = await fetchJson(`/Receipt/Preview?orderId=${orderId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        return data;
    }

    async function recalculateModal(showOverlay) {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal || modal.style.display === 'none') return;

        const orderId = parseInt(modal.dataset.orderId, 10);
        if (!orderId) return;

        const body = collectRequest(modal);
        if (showOverlay) setModalBusy(true, 'در حال محاسبه مجدد...');

        try {
            const preview = await previewReceipt(orderId, body);
            if (!preview?.success) {
                if (typeof showToast === 'function') showToast(preview?.message || 'خطا در محاسبه', 'error');
                return;
            }
            renderPreviewBox(modal, preview.data);
            updateDiscountCodeState(preview.data);
            syncDiscountCodeInput(preview.data);
        } catch (err) {
            console.error('recalculateModal error:', err);
            if (typeof showToast === 'function') showToast('خطا در محاسبه', 'error');
        } finally {
            if (showOverlay) setModalBusy(false);
        }
    }

    function scheduleRecalculate() {
        if (recalcTimer) clearTimeout(recalcTimer);
        recalcTimer = setTimeout(function () {
            recalculateModal(false);
        }, 350);
    }

    function showReceiptModal(orderId, orderType, definitions, previewData, mode) {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;

        modalMode = mode === 'edit' ? 'edit' : 'issue';
        updateDiscountCodeState(previewData);
        setDiscountCodeFeedback('', true);
        allDefinitions = Array.isArray(definitions) ? definitions.slice() : [];
        modal.dataset.orderId = orderId;
        modal.dataset.mode = modalMode;
        setSelectedOrderType(modal, orderType);
        renderChargeList(modal, orderType);
        renderPreviewBox(modal, previewData);
        // Collapsed by default; expand when a code is already on the order.
        setDiscountSectionExpanded(!!appliedDiscountCode);

        const title = modal.querySelector('#receiptModalTitle');
        const subtitle = modal.querySelector('#receiptModalSubtitle');
        const issueBtn = modal.querySelector('#receiptIssueBtn');
        const saveBtn = modal.querySelector('#receiptSaveBtn');

        if (modalMode === 'edit') {
            if (title) title.textContent = 'ویرایش فاکتور';
            if (subtitle) subtitle.textContent = 'مبالغ ذخیره‌شده را تغییر دهید. چاپ اختیاری است.';
            if (saveBtn) {
                saveBtn.hidden = false;
                saveBtn.dataset.defaultLabel = 'ذخیره';
                saveBtn.textContent = 'ذخیره';
            }
            if (issueBtn) {
                issueBtn.dataset.defaultLabel = 'ذخیره و چاپ';
                issueBtn.textContent = 'ذخیره و چاپ';
            }
        } else {
            if (title) title.textContent = 'چاپ فاکتور';
            if (subtitle) subtitle.textContent = 'نوع سفارش را انتخاب کنید و هزینه‌های مربوط را بررسی کنید';
            if (saveBtn) saveBtn.hidden = true;
            if (issueBtn) {
                issueBtn.dataset.defaultLabel = 'صدور و چاپ';
                issueBtn.textContent = 'صدور و چاپ';
            }
        }

        setModalBusy(false);
        modal.classList.add('show');
        modal.style.display = 'block';
        modal.removeAttribute('aria-hidden');
    }

    function hideReceiptModal() {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;
        if (recalcTimer) {
            clearTimeout(recalcTimer);
            recalcTimer = null;
        }
        setModalBusy(false);
        modal.classList.remove('show');
        modal.style.display = 'none';
        modal.setAttribute('aria-hidden', 'true');
    }

    async function issueAndPrint(orderId, body) {
        const { response, data: issue } = await fetchJson(`/Receipt/Issue?orderId=${orderId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        if (response.status === 409) {
            hideReceiptModal();
            if (typeof showToast === 'function') {
                showToast('فاکتور قبلاً صادر شده است. نسخه ثبت‌شده چاپ می‌شود.', 'success');
            }
            const { data: existing } = await fetchJson(`/Receipt/Data?orderId=${orderId}`);
            if (existing?.success && existing.data) {
                updateOrderReceiptBadge(orderId, existing.data);
            }
            openReceiptHtml(orderId);
            return;
        }

        if (!issue?.success) {
            if (typeof showToast === 'function') showToast(issue?.message || 'خطا در صدور فاکتور', 'error');
            return;
        }

        updateOrderReceiptBadge(orderId, issue.data);
        hideReceiptModal();
        openReceiptHtml(orderId);
    }

    async function reissueReceipt(orderId, body, shouldPrint) {
        const { data: issue } = await fetchJson(`/Receipt/Reissue?orderId=${orderId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        if (!issue?.success) {
            if (typeof showToast === 'function') showToast(issue?.message || 'خطا در ویرایش فاکتور', 'error');
            return false;
        }

        updateOrderReceiptBadge(orderId, issue.data);
        hideReceiptModal();
        if (typeof showToast === 'function') showToast('فاکتور با موفقیت به‌روز شد', 'success');
        if (shouldPrint) openReceiptHtml(orderId);
        return true;
    }

    function mergeDefinitionsWithIssuedReceipt(definitions, receipt) {
        if (!receipt) return definitions;
        var lines = receipt.chargeLines || receipt.ChargeLines || [];
        var orderType = receipt.orderType ?? receipt.OrderType;
        var byId = {};
        lines.forEach(function (line) {
            var id = line.definitionId ?? line.DefinitionId;
            if (id == null) return;
            byId[id] = line;
        });

        return definitions.map(function (def) {
            var line = byId[def.id];
            if (!line) {
                if (orderType != null && definitionApplies(def, orderType)) {
                    return Object.assign({}, def, { isEnabled: false });
                }
                return def;
            }
            return Object.assign({}, def, {
                isEnabled: true,
                value: line.value ?? line.Value ?? def.value
            });
        });
    }

    function mergeDefinitionsWithAppliedCharges(definitions, appliedCharges, orderType) {
        if (!appliedCharges || !appliedCharges.length) return definitions;

        var byId = {};
        appliedCharges.forEach(function (charge) {
            var id = charge.definitionId ?? charge.DefinitionId;
            if (id == null) return;
            byId[id] = charge;
        });

        return definitions.map(function (def) {
            var selection = byId[def.id];
            if (!selection) {
                if (definitionApplies(def, orderType)) {
                    return Object.assign({}, def, { isEnabled: false });
                }
                return def;
            }

            return Object.assign({}, def, {
                isEnabled: selection.isEnabled ?? selection.IsEnabled ?? false,
                value: selection.value ?? selection.Value ?? def.value
            });
        });
    }

    async function openChargeModal(orderId, defaultOrderType, triggerBtn, mode) {
        const btn = triggerBtn;
        setButtonLoading(btn, true, 'در حال آماده‌سازی...');
        try {
            const { data: defs } = await fetchJson('/Receipt/GetChargeDefinitions');
            var definitions = defs?.data || [];
            var orderType = defaultOrderType || 0;
            var previewData = null;

            if (mode === 'edit') {
                const { data: existing } = await fetchJson(
                    `/Receipt/Data?orderId=${orderId}&recordPrintHistory=false`);
                if (existing?.success && existing.data) {
                    orderType = existing.data.orderType ?? existing.data.OrderType ?? orderType;
                    updateDiscountCodeState(existing.data);
                    definitions = mergeDefinitionsWithIssuedReceipt(definitions, existing.data);
                    previewData = existing.data;
                }
            } else {
                const { data: defaultsRes } = await fetchJson(`/Receipt/PreviewDefaults?orderId=${orderId}`);
                if (!defaultsRes?.success || !defaultsRes.data) {
                    if (typeof showToast === 'function') {
                        showToast(defaultsRes?.message || 'خطا در آماده‌سازی فاکتور', 'error');
                    }
                    return;
                }

                const defaults = defaultsRes.data;
                const receipt = defaults.receipt ?? defaults.Receipt;
                const appliedCharges = defaults.appliedCharges ?? defaults.AppliedCharges ?? [];
                orderType = receipt?.orderType ?? receipt?.OrderType ?? orderType;
                updateDiscountCodeState(receipt);
                definitions = mergeDefinitionsWithAppliedCharges(definitions, appliedCharges, orderType);
                previewData = receipt;
            }

            if (mode === 'edit' || previewData == null) {
                const body = {
                    orderType: orderType,
                    charges: definitions
                        .filter(function (d) { return definitionApplies(d, orderType); })
                        .map(function (d) {
                            return {
                                definitionId: d.id,
                                code: d.code,
                                isEnabled: d.isEnabled,
                                value: d.value
                            };
                        })
                };

                setButtonLoading(btn, true, 'در حال محاسبه...');
                const preview = await previewReceipt(orderId, body);
                if (!preview?.success) {
                    if (typeof showToast === 'function') showToast(preview?.message || 'خطا در محاسبه', 'error');
                    return;
                }
                previewData = preview.data;
                updateDiscountCodeState(previewData);
            }

            showReceiptModal(orderId, orderType, definitions, previewData, mode);
        } catch (err) {
            console.error('openChargeModal error:', err);
            if (typeof showToast === 'function') showToast('خطا در آماده‌سازی فاکتور', 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    }

    async function handlePrintInvoice(orderId, defaultOrderType, triggerBtn) {
        const btn = triggerBtn || document.querySelector(`#order-${orderId} .receipt-print-btn`);

        if (!isReceiptChargesEnabled()) {
            setButtonLoading(btn, true, 'در حال چاپ...');
            try {
                await printLegacyInvoice(orderId);
            } finally {
                setButtonLoading(btn, false);
            }
            return;
        }

        setButtonLoading(btn, true, 'در حال آماده‌سازی...');
        try {
            const { data: status } = await fetchJson(`/Receipt/Status?orderId=${orderId}`);
            if (!status?.success) {
                if (typeof showToast === 'function') showToast(status?.message || 'خطا', 'error');
                return;
            }

            if (isReceiptIssued(status.data)) {
                setButtonLoading(btn, true, 'در حال چاپ...');
                const { data: existing } = await fetchJson(`/Receipt/Data?orderId=${orderId}`);
                if (existing?.success && existing.data) {
                    updateOrderReceiptBadge(orderId, existing.data);
                }
                openReceiptHtml(orderId);
                return;
            }

            await openChargeModal(orderId, defaultOrderType, btn, 'issue');
        } catch (err) {
            console.error('handlePrintInvoice error:', err);
            if (typeof showToast === 'function') showToast('خطا در آماده‌سازی فاکتور', 'error');
        } finally {
            setButtonLoading(btn, false);
        }
    }

    async function handleEditInvoice(orderId, defaultOrderType, triggerBtn) {
        const btn = triggerBtn || document.querySelector(`#order-${orderId} .receipt-edit-btn`);
        if (!isReceiptChargesEnabled()) {
            if (typeof showToast === 'function') showToast('قابلیت فاکتور فعال نیست', 'error');
            return;
        }
        await openChargeModal(orderId, defaultOrderType, btn, 'edit');
    }

    document.addEventListener('click', function (e) {
        const orderTypeBtn = e.target.closest('.receipt-order-type__btn');
        if (orderTypeBtn) {
            const modal = document.getElementById('receiptChargeModal');
            if (!modal || modal.dataset.busy === '1' || modal.style.display === 'none') return;

            const nextType = parseInt(orderTypeBtn.getAttribute('data-order-type') || '0', 10);
            if (getSelectedOrderType(modal) === nextType) return;

            setSelectedOrderType(modal, nextType);
            renderChargeList(modal, nextType);
            recalculateModal(true);
            return;
        }

        if (e.target.closest('#receiptDiscountHeader')) {
            const modal = document.getElementById('receiptChargeModal');
            if (!modal || modal.dataset.busy === '1' || modal.style.display === 'none') return;
            setDiscountSectionExpanded(!discountSectionExpanded);
            return;
        }

        if (e.target.closest('#receiptDiscountCodeApply')) {
            const modal = document.getElementById('receiptChargeModal');
            if (!modal || modal.dataset.busy === '1' || modal.style.display === 'none') return;
            applyReceiptDiscountCode();
            return;
        }

        if (e.target.closest('#receiptDiscountCodeClear')) {
            const modal = document.getElementById('receiptChargeModal');
            if (!modal || modal.dataset.busy === '1' || modal.style.display === 'none') return;
            clearReceiptDiscountCode();
            return;
        }

        const previewBtn = e.target.closest('#receiptPreviewBtn');
        if (previewBtn) {
            const modal = document.getElementById('receiptChargeModal');
            if (!modal || modal.dataset.busy === '1') return;

            setButtonLoading(previewBtn, true, 'در حال محاسبه...');
            recalculateModal(true).finally(function () {
                setButtonLoading(previewBtn, false);
            });
            return;
        }

        const issueBtn = e.target.closest('#receiptIssueBtn');
        if (issueBtn) {
            const modal = document.getElementById('receiptChargeModal');
            if (!modal || modal.dataset.busy === '1') return;

            const orderId = parseInt(modal.dataset.orderId, 10);
            const body = collectRequest(modal);
            const isEdit = modal.dataset.mode === 'edit';

            setButtonLoading(issueBtn, true, isEdit ? 'در حال ذخیره...' : 'در حال صدور...');
            setModalBusy(true, isEdit ? 'در حال ذخیره و چاپ فاکتور...' : 'در حال صدور و چاپ فاکتور...');

            const action = isEdit
                ? reissueReceipt(orderId, body, true)
                : issueAndPrint(orderId, body);

            Promise.resolve(action)
                .catch(function (err) {
                    console.error('receipt save/print error:', err);
                    if (typeof showToast === 'function') showToast('خطا در ذخیره فاکتور', 'error');
                })
                .finally(function () {
                    setButtonLoading(issueBtn, false);
                    setModalBusy(false);
                });
            return;
        }

        const saveBtn = e.target.closest('#receiptSaveBtn');
        if (saveBtn) {
            const modal = document.getElementById('receiptChargeModal');
            if (!modal || modal.dataset.busy === '1' || modal.dataset.mode !== 'edit') return;

            const orderId = parseInt(modal.dataset.orderId, 10);
            const body = collectRequest(modal);

            setButtonLoading(saveBtn, true, 'در حال ذخیره...');
            setModalBusy(true, 'در حال ذخیره فاکتور...');

            reissueReceipt(orderId, body, false)
                .catch(function (err) {
                    console.error('reissueReceipt error:', err);
                    if (typeof showToast === 'function') showToast('خطا در ویرایش فاکتور', 'error');
                })
                .finally(function () {
                    setButtonLoading(saveBtn, false);
                    setModalBusy(false);
                });
            return;
        }

        if (e.target.closest('#receiptModalClose') || e.target.classList.contains('receipt-modal-backdrop')) {
            const modal = document.getElementById('receiptChargeModal');
            if (modal && modal.dataset.busy === '1') return;
            hideReceiptModal();
        }
    });

    document.addEventListener('focusin', function (e) {
        if (!e.target.classList.contains('charge-value')) return;
        const modal = document.getElementById('receiptChargeModal');
        if (!modal || modal.style.display === 'none') return;
        const isPercent = e.target.dataset.percent === '1';
        const raw = clampChargeValue(isPercent, parseNumber(e.target.dataset.rawValue || e.target.value));
        e.target.value = isPercent ? String(raw) : String(Math.round(raw));
    });

    document.addEventListener('focusout', function (e) {
        if (!e.target.classList.contains('charge-value')) return;
        const modal = document.getElementById('receiptChargeModal');
        if (!modal || modal.style.display === 'none') return;
        const isPercent = e.target.dataset.percent === '1';
        const raw = clampChargeValue(isPercent, parseNumber(e.target.value));
        e.target.dataset.rawValue = String(raw);
        e.target.value = formatChargeValueDisplay(isPercent, raw);
        scheduleRecalculate();
    });

    document.addEventListener('change', function (e) {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal || modal.style.display === 'none' || modal.dataset.busy === '1') return;
        if (!e.target.closest('#receiptChargeModal')) return;

        if (e.target.classList.contains('charge-enabled')) {
            scheduleRecalculate();
        }
    });

    document.addEventListener('input', function (e) {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal || modal.style.display === 'none' || modal.dataset.busy === '1') return;
        if (!e.target.closest('#receiptChargeModal')) return;

        if (e.target.classList.contains('charge-value')) {
            const isPercent = e.target.dataset.percent === '1';
            let raw = parseNumber(e.target.value);
            if (isPercent && raw > 100) {
                e.target.value = '100';
                raw = 100;
            }
            if (raw < 0) {
                e.target.value = '0';
                raw = 0;
            }
            e.target.dataset.rawValue = String(raw);
            scheduleRecalculate();
        }

        if (e.target.id === 'receiptDiscountCodeInput') {
            var cleaned = sanitizeReceiptDiscountCode(e.target.value);
            if (e.target.value !== cleaned) e.target.value = cleaned;
            setDiscountCodeFeedback('', true);
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter') return;
        if (!e.target || e.target.id !== 'receiptDiscountCodeInput') return;
        const modal = document.getElementById('receiptChargeModal');
        if (!modal || modal.style.display === 'none' || modal.dataset.busy === '1') return;
        e.preventDefault();
        applyReceiptDiscountCode();
    });

    window.handleReceiptPrint = handlePrintInvoice;
    window.handleReceiptEdit = handleEditInvoice;
})();
