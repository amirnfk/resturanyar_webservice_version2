(function () {
    const receiptChargesEnabled = window.receiptChargesEnabled === true;

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

    function buildChargeRow(def) {
        const row = document.createElement('div');
        row.className = 'border rounded p-2 mb-2';
        row.innerHTML = `
            <div class="d-flex align-items-center justify-content-between gap-2">
                <div>
                    <input type="checkbox" class="charge-enabled" data-id="${def.id}" data-code="${def.code}" ${def.isEnabled ? 'checked' : ''} />
                    <strong class="ms-2">${def.title}</strong>
                    <small class="text-muted">(${def.code})</small>
                </div>
                <div class="d-flex align-items-center gap-2">
                    <input type="number" class="form-control form-control-sm charge-value" style="width:120px"
                           data-id="${def.id}" value="${def.value}" step="0.01" />
                    <span class="small text-muted">${def.calculationType === 0 ? '%' : 'تومان'}</span>
                </div>
            </div>`;
        return row;
    }

    function showReceiptModal(orderId, orderType, definitions, previewData) {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;

        modal.dataset.orderId = orderId;
        const orderTypeSelect = modal.querySelector('#receiptOrderType');
        orderTypeSelect.value = String(orderType);

        const list = modal.querySelector('#receiptChargeList');
        list.innerHTML = '';
        definitions.forEach(def => list.appendChild(buildChargeRow(def)));

        const previewBox = modal.querySelector('#receiptPreviewBox');
        if (previewData) {
            previewBox.innerHTML = `
                <div>جمع اقلام: ${previewData.itemsSubtotal?.toLocaleString('fa-IR')} تومان</div>
                <div>جمع کل: <strong>${previewData.grandTotal?.toLocaleString('fa-IR')} تومان</strong></div>`;
        } else {
            previewBox.textContent = '';
        }

        modal.classList.add('show');
        modal.style.display = 'block';
        modal.removeAttribute('aria-hidden');
    }

    function hideReceiptModal() {
        const modal = document.getElementById('receiptChargeModal');
        if (!modal) return;
        modal.classList.remove('show');
        modal.style.display = 'none';
        modal.setAttribute('aria-hidden', 'true');
    }

    function collectRequest(modal) {
        const orderType = parseInt(modal.querySelector('#receiptOrderType').value, 10);
        const charges = Array.from(modal.querySelectorAll('.charge-enabled')).map(cb => ({
            definitionId: parseInt(cb.dataset.id, 10),
            code: cb.dataset.code,
            isEnabled: cb.checked,
            value: parseFloat(modal.querySelector(`.charge-value[data-id="${cb.dataset.id}"]`).value || '0')
        }));
        return { orderType, charges };
    }

    async function previewReceipt(orderId, body) {
        const { data } = await fetchJson(`/Receipt/Preview?orderId=${orderId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        return data;
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
            openReceiptHtml(orderId);
            return;
        }

        if (!issue?.success) {
            if (typeof showToast === 'function') showToast(issue?.message || 'خطا در صدور فاکتور', 'error');
            return;
        }

        hideReceiptModal();
        openReceiptHtml(orderId);
    }

    async function handlePrintInvoice(orderId, defaultOrderType) {
        if (!receiptChargesEnabled) {
            await printLegacyInvoice(orderId);
            return;
        }

        const { data: status } = await fetchJson(`/Receipt/Status?orderId=${orderId}`);
        if (!status?.success) {
            if (typeof showToast === 'function') showToast(status?.message || 'خطا', 'error');
            return;
        }

        if (isReceiptIssued(status.data)) {
            openReceiptHtml(orderId);
            return;
        }

        const { data: defs } = await fetchJson('/Receipt/GetChargeDefinitions');
        const definitions = defs?.data || [];
        const body = {
            orderType: defaultOrderType || 0,
            charges: definitions.map(d => ({
                definitionId: d.id,
                code: d.code,
                isEnabled: d.isEnabled,
                value: d.value
            }))
        };

        const preview = await previewReceipt(orderId, body);
        if (!preview?.success) {
            if (typeof showToast === 'function') showToast(preview?.message || 'خطا در پیش‌نمایش', 'error');
            return;
        }

        showReceiptModal(orderId, body.orderType, definitions, preview.data);
    }

    document.addEventListener('click', function (e) {
        if (e.target.id === 'receiptPreviewBtn') {
            const modal = document.getElementById('receiptChargeModal');
            const orderId = parseInt(modal.dataset.orderId, 10);
            const body = collectRequest(modal);
            previewReceipt(orderId, body).then(preview => {
                if (!preview?.success) {
                    if (typeof showToast === 'function') showToast(preview?.message || 'خطا', 'error');
                    return;
                }
                const previewBox = modal.querySelector('#receiptPreviewBox');
                previewBox.innerHTML = `
                    <div>جمع اقلام: ${preview.data.itemsSubtotal?.toLocaleString('fa-IR')} تومان</div>
                    <div>جمع کل: <strong>${preview.data.grandTotal?.toLocaleString('fa-IR')} تومان</strong></div>`;
            });
        }

        if (e.target.id === 'receiptIssueBtn') {
            const modal = document.getElementById('receiptChargeModal');
            const orderId = parseInt(modal.dataset.orderId, 10);
            const body = collectRequest(modal);
            issueAndPrint(orderId, body);
        }

        if (e.target.id === 'receiptModalClose' || e.target.classList.contains('receipt-modal-backdrop')) {
            hideReceiptModal();
        }
    });

    window.handleReceiptPrint = handlePrintInvoice;
})();
