(function () {
    'use strict';

    function getPageRoot() {
        return document.querySelector('.restaurant-general-settings-page');
    }

    function showToast(message, type) {
        if (window.showToast) {
            window.showToast(message, type);
        }
    }

    function copyText(text) {
        if (!text) return Promise.reject(new Error('empty'));

        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text);
        }

        return new Promise(function (resolve, reject) {
            var textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.setAttribute('readonly', '');
            textarea.style.position = 'absolute';
            textarea.style.left = '-9999px';
            document.body.appendChild(textarea);
            textarea.select();
            try {
                document.execCommand('copy');
                resolve();
            } catch (err) {
                reject(err);
            } finally {
                document.body.removeChild(textarea);
            }
        });
    }

    function getCopyText(targetId, useTextContent) {
        var el = document.getElementById(targetId);
        if (!el) return '';

        if (useTextContent) {
            return (el.textContent || el.innerText || '').trim();
        }

        return (el.value || el.textContent || el.innerText || '').trim();
    }

    function bindCopyButtons(page) {
        page.querySelectorAll('.btn-copy-value:not([data-copy-bound])').forEach(function (btn) {
            btn.setAttribute('data-copy-bound', 'true');
            btn.addEventListener('click', function () {
                var targetId = btn.getAttribute('data-copy-target');
                var useText = btn.getAttribute('data-copy-text') === 'true';
                var text = getCopyText(targetId, useText);

                copyText(text)
                    .then(function () {
                        showToast('در کلیپ‌بورد کپی شد', 'success');
                    })
                    .catch(function () {
                        showToast('کپی انجام نشد', 'error');
                    });
            });
        });
    }

    function bindNameForm(page) {
        var form = page.querySelector('#restaurantNameForm');
        if (!form || form.dataset.formBound === 'true') {
            return;
        }

        form.dataset.formBound = 'true';
        form.addEventListener('submit', function (event) {
            event.preventDefault();

            var input = page.querySelector('#restaurantNameInput');
            var saveBtn = page.querySelector('#saveRestaurantNameBtn');
            var name = input ? input.value.trim() : '';

            if (!name) {
                showToast('نام رستوران الزامی است', 'error');
                if (input) input.focus();
                return;
            }

            if (saveBtn) saveBtn.disabled = true;

            fetch('/Home/UpdateRestaurantName', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: JSON.stringify({ name: name })
            })
                .then(function (res) { return res.json(); })
                .then(function (response) {
                    if (!getPageRoot()) return;

                    if (response.success) {
                        if (input && response.name) {
                            input.value = response.name;
                        }
                        document.body.dataset.restaurantName = response.name || name;
                        showToast(response.message || 'نام رستوران ذخیره شد', 'success');
                    } else {
                        showToast(response.message || 'خطا در ذخیره نام', 'error');
                    }
                })
                .catch(function () {
                    showToast('خطا در ارتباط با سرور', 'error');
                })
                .finally(function () {
                    var btn = page.querySelector('#saveRestaurantNameBtn');
                    if (btn) btn.disabled = false;
                });
        });
    }

    function initRestaurantGeneralSettingsPage() {
        var page = getPageRoot();
        if (!page) return;
        if (page.dataset.generalSettingsReady === 'true') return;

        page.dataset.generalSettingsReady = 'true';
        bindCopyButtons(page);
        bindNameForm(page);
    }

    window.initRestaurantGeneralSettingsPage = initRestaurantGeneralSettingsPage;
})();
