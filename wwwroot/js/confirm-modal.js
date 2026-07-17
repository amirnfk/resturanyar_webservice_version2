(function () {
    'use strict';

    var resolvePromise = null;
    var isOpen = false;
    var overlay = null;
    var iconEl = null;
    var iconWrap = null;
    var titleEl = null;
    var messageEl = null;
    var okBtn = null;
    var cancelBtn = null;
    var escapeHandler = null;

    function getElements() {
        if (!overlay) {
            overlay = document.getElementById('appConfirmModal');
            if (!overlay) return false;
            iconWrap = overlay.querySelector('.custom-modal-icon');
            iconEl = document.getElementById('appConfirmIcon');
            titleEl = document.getElementById('appConfirmTitle');
            messageEl = document.getElementById('appConfirmMessage');
            okBtn = document.getElementById('appConfirmOk');
            cancelBtn = document.getElementById('appConfirmCancel');
        }
        return !!overlay;
    }

    function finish(result) {
        if (!overlay || !isOpen) return;

        overlay.classList.remove('active');
        overlay.setAttribute('aria-hidden', 'true');
        isOpen = false;

        if (escapeHandler) {
            document.removeEventListener('keydown', escapeHandler);
            escapeHandler = null;
        }

        var resolve = resolvePromise;
        resolvePromise = null;
        if (resolve) resolve(!!result);
    }

    function bindStaticHandlers() {
        if (!getElements()) return;

        cancelBtn.addEventListener('click', function () {
            finish(false);
        });

        okBtn.addEventListener('click', function () {
            finish(true);
        });

        overlay.addEventListener('click', function (e) {
            if (e.target === overlay) finish(false);
        });
    }

    window.showConfirm = function (options) {
        options = options || {};

        if (!getElements()) {
            return Promise.resolve(false);
        }

        if (isOpen) {
            return Promise.resolve(false);
        }

        if (!options.message) {
            return Promise.resolve(false);
        }

        var opts = {
            title: options.title || 'تایید عملیات',
            message: options.message,
            confirmText: options.confirmText || 'تایید',
            cancelText: options.cancelText || 'انصراف',
            iconClass: options.iconClass || 'fa-solid fa-triangle-exclamation',
            variant: options.variant || 'danger'
        };

        titleEl.textContent = opts.title;
        messageEl.textContent = opts.message;
        okBtn.textContent = opts.confirmText;
        cancelBtn.textContent = opts.cancelText;
        iconEl.className = opts.iconClass;

        if (opts.variant === 'default') {
            iconWrap.classList.add('custom-modal-icon--default');
            okBtn.className = 'custom-modal-btn btn-confirm-default';
        } else {
            iconWrap.classList.remove('custom-modal-icon--default');
            okBtn.className = 'custom-modal-btn btn-exit';
        }

        return new Promise(function (resolve) {
            resolvePromise = resolve;
            isOpen = true;
            overlay.classList.add('active');
            overlay.setAttribute('aria-hidden', 'false');
            okBtn.focus();

            escapeHandler = function (e) {
                if (e.key === 'Escape') finish(false);
            };
            document.addEventListener('keydown', escapeHandler);
        });
    };

    window.wireLogoutConfirm = function (formSelector) {
        var form = document.querySelector(formSelector);
        if (!form || form.dataset.logoutConfirmWired === 'true') return;
        form.dataset.logoutConfirmWired = 'true';

        form.addEventListener('submit', function (e) {
            if (form.dataset.confirmBypass === 'true') {
                delete form.dataset.confirmBypass;
                return;
            }

            e.preventDefault();

            showConfirm({
                title: 'خروج از حساب کاربری',
                message: 'آیا مطمئن هستید که می‌خواهید از پنل مدیریت خارج شوید؟',
                confirmText: 'بله، خارج می‌شوم',
                iconClass: 'fa-solid fa-right-from-bracket'
            }).then(function (confirmed) {
                if (confirmed) submitFormWithBypass(form);
            });
        });
    };

    function submitFormWithBypass(form) {
        form.dataset.confirmBypass = 'true';
        if (typeof form.requestSubmit === 'function') {
            form.requestSubmit();
        } else {
            form.submit();
        }
    }

    document.addEventListener('submit', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement)) return;

        var message = form.getAttribute('data-confirm');
        if (!message) return;

        if (form.dataset.confirmBypass === 'true') {
            delete form.dataset.confirmBypass;
            return;
        }

        e.preventDefault();

        var title = form.getAttribute('data-confirm-title') || 'تایید عملیات';

        showConfirm({
            title: title,
            message: message
        }).then(function (confirmed) {
            if (confirmed) submitFormWithBypass(form);
        });
    });

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindStaticHandlers);
    } else {
        bindStaticHandlers();
    }
})();
