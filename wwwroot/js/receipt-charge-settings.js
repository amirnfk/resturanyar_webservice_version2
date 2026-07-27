(function () {
    const config = window.receiptChargeSettings || {};
    const page = document.querySelector('.receipt-charge-settings-page');
    const saveBtn = document.getElementById('saveChargeDefinitionsBtn');
    const list = document.getElementById('chargeDefinitionsList');
    const dirtyPill = document.getElementById('chargeDirtyPill');
    if (!page || !saveBtn || !config.saveUrl || !list) return;

    const sampleBase = Number(config.sampleBase) || 100000;
    let isDirty = false;

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

    function clampChargeValue(calcType, value) {
        var num = Number(value);
        if (Number.isNaN(num) || num < 0) num = 0;
        if (calcType === 0 && num > 100) num = 100;
        return Math.round(num * 100) / 100;
    }

    function formatPercentDisplay(value) {
        var num = clampChargeValue(0, value);
        var text = Number.isInteger(num) ? String(num) : String(num);
        return toPersianDigits(text);
    }

    function formatFixedDisplay(value) {
        var num = clampChargeValue(1, value);
        return toPersianDigits(Math.round(num).toLocaleString('en-US'));
    }

    function formatMoney(value) {
        const num = Number(value);
        if (Number.isNaN(num)) return toPersianDigits(value);
        return toPersianDigits(Math.round(num).toLocaleString('en-US'));
    }

    function formatValueInput(input, calcType, rawValue) {
        if (!input) return;
        var clamped = clampChargeValue(calcType, rawValue);
        input.dataset.rawValue = String(clamped);
        input.value = calcType === 0 ? formatPercentDisplay(clamped) : formatFixedDisplay(clamped);
        input.setAttribute('max', calcType === 0 ? '100' : '');
        input.setAttribute('inputmode', calcType === 0 ? 'decimal' : 'numeric');
        return clamped;
    }

    function readValueInput(card) {
        var calcType = syncCalcType(card);
        var input = card.querySelector('.value-input');
        var raw = parseNumber(input?.value);
        return formatValueInput(input, calcType, raw);
    }

    function showMessage(message, isSuccess) {
        if (typeof window.showAppToast === 'function') {
            window.showAppToast(message, isSuccess ? 'success' : 'error');
            return;
        }
        if (typeof window.showToast === 'function') {
            window.showToast(message, isSuccess ? 'success' : 'error');
            return;
        }
        alert(message);
    }

    function setDirty(dirty) {
        isDirty = dirty;
        if (dirtyPill) dirtyPill.hidden = !dirty;
    }

    function getCards() {
        return Array.from(list.querySelectorAll('.charge-definition-card'));
    }

    function syncOrderTypeFlags(card) {
        const flags = Array.from(card.querySelectorAll('.order-type-flag:checked'))
            .reduce(function (sum, input) {
                return sum + (parseInt(input.value, 10) || 0);
            }, 0);
        const hidden = card.querySelector('.order-types-input');
        if (hidden) hidden.value = String(flags);
        return flags;
    }

    function syncCalcType(card) {
        const checked = card.querySelector('.calc-type-radio:checked');
        const hidden = card.querySelector('.calc-type-input');
        const unit = card.querySelector('[data-value-unit]');
        const value = checked ? checked.value : '0';
        if (hidden) hidden.value = value;
        if (unit) unit.textContent = value === '0' ? 'درصد' : 'تومان';
        return parseInt(value, 10) || 0;
    }

    function syncEnabledState(card) {
        const enabled = !!card.querySelector('.enabled-input')?.checked;
        card.classList.toggle('is-enabled', enabled);
        card.classList.toggle('is-disabled', !enabled);
        return enabled;
    }

    function syncLiveTitle(card) {
        const title = card.querySelector('.title-input')?.value?.trim() || 'بدون عنوان';
        const live = card.querySelector('[data-live-title]');
        if (live) live.textContent = title;
    }

    function syncOrderLabels() {
        getCards().forEach(function (card, index) {
            const label = card.querySelector('[data-order-label]');
            const orderInput = card.querySelector('.order-input');
            if (label) label.textContent = toPersianDigits(index + 1);
            if (orderInput) orderInput.value = String((index + 1) * 10);

            const upBtn = card.querySelector('[data-move="up"]');
            const downBtn = card.querySelector('[data-move="down"]');
            if (upBtn) upBtn.disabled = index === 0;
            if (downBtn) downBtn.disabled = index === getCards().length - 1;
        });
    }

    function updateStats() {
        const cards = getCards();
        const enabledCount = cards.filter(function (card) {
            return !!card.querySelector('.enabled-input')?.checked;
        }).length;

        const totalEl = document.getElementById('statTotalCount');
        const enabledEl = document.getElementById('statEnabledCount');
        if (totalEl) totalEl.textContent = toPersianDigits(cards.length);
        if (enabledEl) enabledEl.textContent = toPersianDigits(enabledCount);
    }

    function updatePreview(card) {
        const preview = card.querySelector('[data-preview-text]');
        if (!preview) return;

        const title = card.querySelector('.title-input')?.value?.trim() || 'این مورد';
        const calcType = syncCalcType(card);
        const value = readValueInput(card);
        const flags = syncOrderTypeFlags(card);
        const enabled = syncEnabledState(card);
        const category = parseInt(card.querySelector('.category-input')?.value || '1', 10);

        if (!flags) {
            preview.textContent = 'حداقل یک نوع سفارش را انتخاب کنید تا این مورد در فاکتور ظاهر شود.';
            return;
        }

        const types = [];
        if (flags & 1) types.push('سالن');
        if (flags & 2) types.push('بیرون‌بر');
        if (flags & 4) types.push('ارسال');

        let amountText;
        if (calcType === 0) {
            const amount = sampleBase * value / 100;
            amountText = `${formatPercentDisplay(value)}٪ از ${formatMoney(sampleBase)} تومان = ${formatMoney(amount)} تومان`;
        } else {
            amountText = `${formatFixedDisplay(value)} تومان ثابت`;
        }

        const effect = category === 0 ? 'کاهش' : 'افزایش';
        const state = enabled ? 'به‌صورت پیش‌فرض فعال' : 'به‌صورت پیش‌فرض غیرفعال';
        preview.textContent = `${title}: ${amountText} — ${effect} مبلغ فاکتور برای ${types.join('، ')} (${state})`;
    }

    function refreshCard(card) {
        syncCalcType(card);
        syncOrderTypeFlags(card);
        syncEnabledState(card);
        syncLiveTitle(card);
        updatePreview(card);
    }

    function refreshAll() {
        getCards().forEach(function (card) {
            var calcType = syncCalcType(card);
            var input = card.querySelector('.value-input');
            var initial = input?.dataset.rawValue || input?.value || '0';
            formatValueInput(input, calcType, parseNumber(initial));
            refreshCard(card);
        });
        syncOrderLabels();
        updateStats();
    }

    function collectDefinitions() {
        return getCards().map(function (card) {
            syncCalcType(card);
            syncOrderTypeFlags(card);
            return {
                id: parseInt(card.dataset.id || '0', 10),
                code: card.querySelector('.code-input')?.value?.trim() || '',
                title: card.querySelector('.title-input')?.value?.trim() || '',
                chargeCategory: parseInt(card.querySelector('.category-input')?.value || '1', 10),
                calculationType: parseInt(card.querySelector('.calc-type-input')?.value || '0', 10),
                value: readValueInput(card),
                isEnabled: !!card.querySelector('.enabled-input')?.checked,
                isTaxable: !!card.querySelector('.taxable-input')?.checked,
                percentageBase: 0,
                displayOrder: parseInt(card.querySelector('.order-input')?.value || '0', 10),
                appliesToOrderTypes: parseInt(card.querySelector('.order-types-input')?.value || '0', 10)
            };
        });
    }

    function validateDefinitions(definitions) {
        for (var i = 0; i < definitions.length; i++) {
            var def = definitions[i];
            if (!def.title) {
                return 'عنوان نمایشی برای همه موارد الزامی است.';
            }
            if (!(def.appliesToOrderTypes > 0)) {
                return `برای «${def.title}» حداقل یک نوع سفارش را انتخاب کنید.`;
            }
            if (Number.isNaN(def.value) || def.value < 0) {
                return `مقدار «${def.title}» نامعتبر است.`;
            }
            if (def.calculationType === 0 && def.value > 100) {
                return `درصد «${def.title}» نمی‌تواند بیشتر از ۱۰۰ باشد.`;
            }
        }
        return null;
    }

    function moveCard(card, direction) {
        const cards = getCards();
        const index = cards.indexOf(card);
        if (index < 0) return;

        if (direction === 'up' && index > 0) {
            list.insertBefore(card, cards[index - 1]);
        } else if (direction === 'down' && index < cards.length - 1) {
            list.insertBefore(cards[index + 1], card);
        }

        syncOrderLabels();
        setDirty(true);
    }

    list.addEventListener('focusin', function (e) {
        if (!e.target.classList.contains('value-input')) return;
        var card = e.target.closest('.charge-definition-card');
        if (!card) return;
        var calcType = syncCalcType(card);
        var raw = parseNumber(e.target.value);
        e.target.value = calcType === 0
            ? String(clampChargeValue(0, raw))
            : String(Math.round(clampChargeValue(1, raw)));
    });

    list.addEventListener('focusout', function (e) {
        if (!e.target.classList.contains('value-input')) return;
        var card = e.target.closest('.charge-definition-card');
        if (!card) return;
        refreshCard(card);
    });

    list.addEventListener('input', function (e) {
        const card = e.target.closest('.charge-definition-card');
        if (!card) return;

        if (e.target.classList.contains('value-input')) {
            var calcType = syncCalcType(card);
            var raw = parseNumber(e.target.value);
            if (calcType === 0 && raw > 100) {
                e.target.value = '100';
                raw = 100;
            }
            if (raw < 0) {
                e.target.value = '0';
                raw = 0;
            }
            e.target.dataset.rawValue = String(raw);
            syncLiveTitle(card);
            syncEnabledState(card);
            // lightweight preview while typing without reformatting separators mid-keystroke
            var preview = card.querySelector('[data-preview-text]');
            if (preview) {
                var title = card.querySelector('.title-input')?.value?.trim() || 'این مورد';
                if (calcType === 0) {
                    preview.textContent = `${title}: ${toPersianDigits(raw)}٪ از ${formatMoney(sampleBase)} تومان = ${formatMoney(sampleBase * raw / 100)} تومان`;
                } else {
                    preview.textContent = `${title}: ${formatMoney(raw)} تومان ثابت`;
                }
            }
            updateStats();
            setDirty(true);
            return;
        }

        if (e.target.classList.contains('calc-type-radio')) {
            var type = syncCalcType(card);
            var input = card.querySelector('.value-input');
            formatValueInput(input, type, parseNumber(input?.dataset.rawValue || input?.value));
        }

        refreshCard(card);
        updateStats();
        setDirty(true);
    });

    list.addEventListener('change', function (e) {
        const card = e.target.closest('.charge-definition-card');
        if (!card) return;

        if (e.target.classList.contains('calc-type-radio')) {
            var type = syncCalcType(card);
            var input = card.querySelector('.value-input');
            formatValueInput(input, type, parseNumber(input?.dataset.rawValue || input?.value));
        }

        refreshCard(card);
        updateStats();
        setDirty(true);
    });

    list.addEventListener('click', function (e) {
        const moveBtn = e.target.closest('[data-move]');
        if (!moveBtn) return;
        const card = moveBtn.closest('.charge-definition-card');
        if (!card) return;
        moveCard(card, moveBtn.getAttribute('data-move'));
    });

    window.addEventListener('beforeunload', function (e) {
        if (!isDirty) return;
        e.preventDefault();
        e.returnValue = '';
    });

    saveBtn.addEventListener('click', async function () {
        const definitions = collectDefinitions();
        if (!definitions.length) {
            showMessage('هیچ موردی برای ذخیره وجود ندارد.', false);
            return;
        }

        const validationError = validateDefinitions(definitions);
        if (validationError) {
            showMessage(validationError, false);
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
                body: JSON.stringify({ definitions: definitions })
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

            setDirty(false);
            showMessage(data.message || 'تنظیمات با موفقیت ذخیره شد.', true);
        } catch (error) {
            showMessage('خطا در ارتباط با سرور.', false);
        } finally {
            saveBtn.disabled = false;
            saveBtn.innerHTML = originalHtml;
        }
    });

    refreshAll();
})();
