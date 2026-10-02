(function () {
    'use strict';

    function normalize(text) {
        return (text || '')
            .toString()
            .toLowerCase()
            .replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d))
            .replace(/[٠-٩]/g, d => '٠١٢٣٤٥٦٧٨٩'.indexOf(d))
            .replace(/ي/g, 'ی')
            .replace(/ك/g, 'ک')
            .replace(/[\u200c\u200f]/g, ' ')
            .replace(/\s+/g, ' ')
            .trim();
    }

    function normalizePhone(text) {
        return (text || '')
            .toString()
            .replace(/[۰-۹]/g, d => '۰۱۲۳۴۵۶۷۸۹'.indexOf(d))
            .replace(/[٠-٩]/g, d => '٠١٢٣٤٥٦٧٨٩'.indexOf(d))
            .replace(/\D/g, '');
    }

    function initSendMessagePage() {
        const typeCards = document.querySelectorAll('.sm-type-card');
        const typeRadios = document.querySelectorAll('.msg-type-radio');
        const picker = document.getElementById('restaurantPicker');
        const searchInput = document.getElementById('restaurantSearch');
        const items = Array.from(document.querySelectorAll('.sm-picker__item'));
        const emptyState = document.getElementById('pickerEmpty');
        const visibleCountEl = document.getElementById('visibleCount');
        const selectedCountEl = document.getElementById('selectedCount');
        const selectVisibleBtn = document.getElementById('selectVisibleBtn');
        const clearSelectionBtn = document.getElementById('clearSelectionBtn');

        function syncTypeCards() {
            typeCards.forEach(card => {
                const radio = card.querySelector('.msg-type-radio');
                card.classList.toggle('is-active', !!(radio && radio.checked));
            });
        }

        function togglePicker() {
            const isPrivate = document.getElementById('typePrivate')?.checked;
            if (!picker) return;
            picker.classList.toggle('is-visible', !!isPrivate);
            picker.style.display = isPrivate ? 'block' : 'none';
        }

        function updateCounts() {
            const visible = items.filter(item => !item.classList.contains('is-hidden'));
            const selected = items.filter(item => item.querySelector('input[type="checkbox"]')?.checked);

            if (visibleCountEl) visibleCountEl.textContent = visible.length.toLocaleString('fa-IR');
            if (selectedCountEl) selectedCountEl.textContent = selected.length.toLocaleString('fa-IR');

            items.forEach(item => {
                const checked = item.querySelector('input[type="checkbox"]')?.checked;
                item.classList.toggle('is-checked', !!checked);
            });

            if (emptyState) {
                emptyState.classList.toggle('is-visible', visible.length === 0);
            }
        }

        function applySearch() {
            const raw = searchInput?.value || '';
            const q = normalize(raw);
            const qPhone = normalizePhone(raw);

            items.forEach(item => {
                if (!q) {
                    item.classList.remove('is-hidden');
                    return;
                }

                const hay = normalize(
                    (item.getAttribute('data-search') || '') + ' ' + (item.textContent || '')
                );
                const phone = normalizePhone(item.getAttribute('data-phone') || item.textContent || '');
                const matchText = hay.includes(q);
                const matchPhone = qPhone.length > 0 && phone.includes(qPhone);
                item.classList.toggle('is-hidden', !(matchText || matchPhone));
            });

            updateCounts();
        }

        typeCards.forEach(card => {
            card.addEventListener('click', () => {
                // label already checks the radio; just sync UI after native click
                requestAnimationFrame(() => {
                    syncTypeCards();
                    togglePicker();
                });
            });
        });

        typeRadios.forEach(radio => {
            radio.addEventListener('change', () => {
                syncTypeCards();
                togglePicker();
            });
        });

        searchInput?.addEventListener('input', applySearch);

        items.forEach(item => {
            const checkbox = item.querySelector('input[type="checkbox"]');
            checkbox?.addEventListener('change', updateCounts);
        });

        selectVisibleBtn?.addEventListener('click', () => {
            items.forEach(item => {
                if (item.classList.contains('is-hidden')) return;
                const checkbox = item.querySelector('input[type="checkbox"]');
                if (checkbox) checkbox.checked = true;
            });
            updateCounts();
        });

        clearSelectionBtn?.addEventListener('click', () => {
            items.forEach(item => {
                const checkbox = item.querySelector('input[type="checkbox"]');
                if (checkbox) checkbox.checked = false;
            });
            updateCounts();
        });

        syncTypeCards();
        togglePicker();
        applySearch();
    }

    document.addEventListener('DOMContentLoaded', initSendMessagePage);
})();
