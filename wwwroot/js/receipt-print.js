(function () {
    var allDefinitions = [];
    var recalcTimer = null;
    var modalMode = 'issue'; // issue | edit

    function isReceiptChargesEnabled() {
        return window.receiptChargesEnabled === true;
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
                       data-id="${def.id}" data-code="${def.code || ''}" ${checked} />
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
            const amount = line.calculatedAmount ?? line.CalculatedAmount ?? 0;
            linesHtml += `
                <div class="receipt-summary__row is-line">
                    <span>${title}</span>
                    <span>${formatMoney(amount)} تومان</span>
                </div>`;
        });

        previewBox.innerHTML = `
            <div class="receipt-summary__row">
                <span>جمع اقلام</span>
                <span>${formatMoney(itemsSubtotal)} تومان</span>
            </div>
            ${linesHtml}
            <div class="receipt-summary__row is-total">
                <span>جمع کل</span>
                <span>${formatMoney(grandTotal)} تومان</span>
            </div>`;
    }

    function formatIssuedAt(issuedAt) {
        if (!issuedAt) return '';
        const dt = new Date(issuedAt);
        if (Number.isNaN(dt.getTime())) return '';
        return `<span class="text-muted small ms-2">(صدور: ${toPersianDigits(dt.toLocaleString('fa-IR'))})</span>`;
    }

    function updateOrderReceiptBadge(orderId, receipt) {
        const card = document.getElementById(`order-${orderId}`);
        if (!card || !receipt) return;

        const grandTotal = receipt.grandTotal ?? receipt.GrandTotal;
        if (grandTotal == null) return;

        const issuedAt = receipt.issuedAt ?? receipt.IssuedAt;
        let badge = card.querySelector('.receipt-issued-badge');
        if (!badge) {
            badge = document.createElement('div');
            badge.className = 'alert alert-success py-2 px-3 mt-2 mb-0 receipt-issued-badge';
            const totalBox = card.querySelector('.total-box');
            if (totalBox) {
                totalBox.insertAdjacentElement('afterend', badge);
            } else {
                card.appendChild(badge);
            }
        }

        badge.innerHTML = `
            <i class="fa-solid fa-receipt ms-1"></i>
            مبلغ فاکتور: ${formatMoney(grandTotal)} تومان
            ${formatIssuedAt(issuedAt)}`;

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
        allDefinitions = Array.isArray(definitions) ? definitions.slice() : [];
        modal.dataset.orderId = orderId;
        modal.dataset.mode = modalMode;
        setSelectedOrderType(modal, orderType);
        renderChargeList(modal, orderType);
        renderPreviewBox(modal, previewData);

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
                // Keep non-applicable defs; mark applicable-but-absent as disabled for edit.
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

    async function openChargeModal(orderId, defaultOrderType, triggerBtn, mode) {
        const btn = triggerBtn;
        setButtonLoading(btn, true, 'در حال آماده‌سازی...');
        try {
            const { data: defs } = await fetchJson('/Receipt/GetChargeDefinitions');
            var definitions = defs?.data || [];
            var orderType = defaultOrderType || 0;

            if (mode === 'edit') {
                const { data: existing } = await fetchJson(
                    `/Receipt/Data?orderId=${orderId}&recordPrintHistory=false`);
                if (existing?.success && existing.data) {
                    orderType = existing.data.orderType ?? existing.data.OrderType ?? orderType;
                    definitions = mergeDefinitionsWithIssuedReceipt(definitions, existing.data);
                }
            }

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

            showReceiptModal(orderId, orderType, definitions, preview.data, mode);
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
    });

    window.handleReceiptPrint = handlePrintInvoice;
    window.handleReceiptEdit = handleEditInvoice;
})();
