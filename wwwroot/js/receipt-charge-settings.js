(function () {
    const config = window.receiptChargeSettings || {};
    const saveBtn = document.getElementById('saveChargeDefinitionsBtn');
    if (!saveBtn || !config.saveUrl) return;

    function showMessage(message, isSuccess) {
        if (typeof window.showAppToast === 'function') {
            window.showAppToast(message, isSuccess ? 'success' : 'error');
            return;
        }
        alert(message);
    }

    function collectDefinitions() {
        return Array.from(document.querySelectorAll('.charge-definition-card')).map(function (card) {
            return {
                id: parseInt(card.dataset.id || '0', 10),
                code: card.querySelector('.code-input')?.value?.trim() || '',
                title: card.querySelector('.title-input')?.value?.trim() || '',
                chargeCategory: parseInt(card.querySelector('.category-input')?.value || '1', 10),
                calculationType: parseInt(card.querySelector('.calc-type-input')?.value || '0', 10),
                value: parseFloat(card.querySelector('.value-input')?.value || '0'),
                isEnabled: !!card.querySelector('.enabled-input')?.checked,
                isTaxable: !!card.querySelector('.taxable-input')?.checked,
                percentageBase: 0,
                displayOrder: parseInt(card.querySelector('.order-input')?.value || '0', 10),
                appliesToOrderTypes: parseInt(card.querySelector('.order-types-input')?.value || '7', 10)
            };
        });
    }

    saveBtn.addEventListener('click', async function () {
        const definitions = collectDefinitions();
        if (!definitions.length) {
            showMessage('هیچ کارمزدی برای ذخیره وجود ندارد.', false);
            return;
        }

        saveBtn.disabled = true;
        const originalHtml = saveBtn.innerHTML;
        saveBtn.innerHTML = '<i class="fas fa-spinner fa-spin ms-1"></i> در حال ذخیره...';

        try {
            const response = await fetch(config.saveUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: JSON.stringify({ definitions })
            });

            let data = null;
            try {
                data = await response.json();
            } catch {
                data = { success: false, message: 'پاسخ سرور نامعتبر بود.' };
            }

            if (!response.ok || !data?.success) {
                showMessage(data?.message || 'ذخیره تنظیمات انجام نشد.', false);
                return;
            }

            showMessage(data.message || 'تنظیمات با موفقیت ذخیره شد.', true);
        } catch (error) {
            showMessage('خطا در ارتباط با سرور.', false);
        } finally {
            saveBtn.disabled = false;
            saveBtn.innerHTML = originalHtml;
        }
    });
})();
