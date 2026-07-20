(function () {
    'use strict';

    var MAX_LOGO_BYTES = 512000;
    var DEFAULT_PRIMARY = '#e85d04';
    var DEFAULT_SECONDARY = '#ff922b';
    var DEFAULT_LOGO = '/images/logo.png';
    var DEFAULT_BACKGROUND = '/images/backgrounds/default.jpg';

    var backgroundOptions = [];
    var pendingLogoFile = null;
    var currentLogoUrl = DEFAULT_LOGO;
    var settingsAbortController = null;

    function getPageRoot() {
        return document.querySelector('.menu-settings-page');
    }

    function getEl(id) {
        var page = getPageRoot();
        return page ? page.querySelector('#' + id) : document.getElementById(id);
    }

    function getSelectedBackgroundUrl() {
        var hidden = getEl('backgroundImageUrl');
        return hidden && hidden.value ? hidden.value : DEFAULT_BACKGROUND;
    }

    function getBackgroundOptionForUrl(url) {
        var effectiveUrl = url || getSelectedBackgroundUrl();
        return backgroundOptions.find(function (option) {
            return option.url === effectiveUrl;
        }) || null;
    }

    function getTemplateColors(url) {
        var option = getBackgroundOptionForUrl(url);
        return {
            primary: (option && option.primaryColor) || DEFAULT_PRIMARY,
            secondary: (option && option.secondaryColor) || DEFAULT_SECONDARY
        };
    }

    function getDefaultMenuTexts(url) {
        var option = getBackgroundOptionForUrl(url);
        if (option) {
            return {
                badge: option.heroBadge || 'منوی دیجیتال',
                tagline: option.tagline || 'طعم‌های خاص، لحظه‌های به‌یادماندنی'
            };
        }
        return {
            badge: 'منوی دیجیتال',
            tagline: 'طعم‌های خاص، لحظه‌های به‌یادماندنی'
        };
    }

    function getEffectiveMenuTexts() {
        var defaults = getDefaultMenuTexts();
        var badgeInput = getEl('menuHeroBadge');
        var taglineInput = getEl('menuTagline');
        var customBadge = badgeInput && badgeInput.value.trim();
        var customTagline = taglineInput && taglineInput.value.trim();
        return {
            badge: customBadge || defaults.badge,
            tagline: customTagline || defaults.tagline
        };
    }

    function updateMenuTextHints(url, data) {
        var defaults = getDefaultMenuTexts(url);
        if (data) {
            if (data.menuHeroBadgeDefault) defaults.badge = data.menuHeroBadgeDefault;
            if (data.menuTaglineDefault) defaults.tagline = data.menuTaglineDefault;
        }

        var badgeHint = getEl('menuHeroBadgeDefaultHint');
        var taglineHint = getEl('menuTaglineDefaultHint');
        if (badgeHint) badgeHint.textContent = defaults.badge;
        if (taglineHint) taglineHint.textContent = defaults.tagline;
    }

    function getPreviewLogoUrl() {
        if (pendingLogoFile) {
            return URL.createObjectURL(pendingLogoFile);
        }
        return currentLogoUrl || DEFAULT_LOGO;
    }

    function getRestaurantName() {
        var page = getPageRoot();
        return (page && page.dataset.restaurantName) || 'رستوران';
    }

    function rememberSavedBackground(backgroundUrl) {
        var page = getPageRoot();
        if (!page || !window.RestaurantBackgroundCache) return;

        window.RestaurantBackgroundCache.remember(
            page.dataset.restaurantId,
            backgroundUrl
        );
    }

    function getTemplateId(url) {
        var option = getBackgroundOptionForUrl(url);
        return (option && option.id) || 'default';
    }

    function updatePreview() {
        var page = getPageRoot();
        if (!page) return;

        var colors = getTemplateColors();
        var logoUrl = getPreviewLogoUrl();
        var backgroundUrl = getSelectedBackgroundUrl();
        var templateId = getTemplateId(backgroundUrl);
        var texts = getEffectiveMenuTexts();

        var previewLogo = getEl('previewLogo');
        if (previewLogo) previewLogo.src = logoUrl;

        var logoThumb = getEl('logoPreviewThumb');
        if (logoThumb) logoThumb.src = logoUrl;

        var badge = getEl('previewMenuBadge');
        if (badge) {
            badge.style.background = 'linear-gradient(135deg, ' + colors.primary + ', ' + colors.secondary + ')';
            badge.textContent = texts.badge;
        }

        var tagline = getEl('previewMenuTagline');
        if (tagline) tagline.textContent = texts.tagline;

        var restaurantNameEl = getEl('previewRestaurantName');
        if (restaurantNameEl) restaurantNameEl.textContent = getRestaurantName();

        var phonePreview = getEl('menuPhonePreview');
        if (phonePreview) {
            phonePreview.className = 'pm-app menu-template--' + templateId;
            phonePreview.style.setProperty('--pm-accent', colors.primary);
            phonePreview.style.setProperty('--pm-primary', colors.primary);
            phonePreview.style.setProperty('--pm-primary-2', colors.secondary);
        }

        var topbarGlow = getEl('previewTopbarGlow');
        if (topbarGlow) {
            topbarGlow.style.backgroundImage = 'url("' + backgroundUrl + '")';
        }
    }

    function downloadQr() {
        var qrImg = getEl('qrImage');
        if (!qrImg || !qrImg.src) return;

        var link = document.createElement('a');
        link.href = qrImg.src;
        link.download = 'QRCode_Menu.png';
        link.click();
    }

    function renderBackgroundPicker(selectedUrl) {
        var container = getEl('backgroundPicker');
        if (!container) return;

        container.innerHTML = '';
        var effectiveSelected = selectedUrl || DEFAULT_BACKGROUND;

        backgroundOptions.forEach(function (option) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'background-option' + (option.url === effectiveSelected ? ' is-selected' : '');
            btn.setAttribute('role', 'option');
            btn.setAttribute('aria-selected', option.url === effectiveSelected ? 'true' : 'false');
            btn.dataset.url = option.url;
            btn.title = option.label;

            var img = document.createElement('img');
            img.src = option.url;
            img.alt = option.label;
            img.loading = 'lazy';

            var label = document.createElement('span');
            label.className = 'background-option-label';
            label.textContent = option.label;

            btn.appendChild(img);
            btn.appendChild(label);
            btn.addEventListener('click', function () {
                var hidden = getEl('backgroundImageUrl');
                if (hidden) hidden.value = option.url;
                renderBackgroundPicker(option.url);
                updateMenuTextHints(option.url);
                updatePreview();
            });

            container.appendChild(btn);
        });
    }

    function showLogoError(message) {
        var el = getEl('logoFileError');
        if (!el) return;

        if (message) {
            el.textContent = message;
            el.hidden = false;
        } else {
            el.textContent = '';
            el.hidden = true;
        }
    }

    function validateLogoFile(file) {
        if (!file) return null;
        if (file.size > MAX_LOGO_BYTES) {
            return 'حجم لوگو نباید بیشتر از ۵۰۰ کیلوبایت باشد.';
        }
        var allowed = ['image/png', 'image/jpeg', 'image/webp'];
        if (allowed.indexOf(file.type) === -1) {
            return 'فرمت لوگو مجاز نیست. فقط PNG، JPG و WebP پذیرفته می‌شود.';
        }
        return null;
    }

    function onLogoFileChange(e) {
        var file = e.target.files && e.target.files[0];
        if (!file) {
            pendingLogoFile = null;
            showLogoError(null);
            updatePreview();
            return;
        }

        var error = validateLogoFile(file);
        if (error) {
            pendingLogoFile = null;
            e.target.value = '';
            showLogoError(error);
            updatePreview();
            return;
        }

        pendingLogoFile = file;
        showLogoError(null);
        updatePreview();
    }

    function bindInputs() {
        ['menuHeroBadge', 'menuTagline'].forEach(function (id) {
            var el = getEl(id);
            if (el) el.addEventListener('input', updatePreview);
        });

        var logoFile = getEl('logoFile');
        if (logoFile) logoFile.addEventListener('change', onLogoFileChange);

        var downloadBtn = getEl('downloadQrBtn');
        if (downloadBtn) downloadBtn.addEventListener('click', downloadQr);
    }

    function resolveBackgroundUrl(url) {
        if (!url) return DEFAULT_BACKGROUND;

        var legacyMap = {
            '/images/backgrounds/back1.jpg': DEFAULT_BACKGROUND,
            '/images/backgrounds/preset-warm.jpg': DEFAULT_BACKGROUND,
            '/images/backgrounds/preset-dark.jpg': '/images/backgrounds/fastfoodbackground.jpg',
            '/images/backgrounds/preset-fresh.jpg': '/images/backgrounds/caferestaurantbackground.jpg',
            '/images/backgrounds/preset-elegant.jpg': '/images/backgrounds/kababibackground.jpg',
            '/images/backgrounds/preset-cozy.jpg': '/images/backgrounds/sonatibackground.jpg',
            '/images/backgrounds/preset-minimal.jpg': '/images/backgrounds/modernbackground.jpg',
            '/images/modernbackground.jpg': '/images/backgrounds/modernbackground.jpg'
        };

        var normalized = url.trim();
        if (legacyMap[normalized]) normalized = legacyMap[normalized];

        return backgroundOptions.some(function (option) { return option.url === normalized; })
            ? normalized
            : DEFAULT_BACKGROUND;
    }

    function fillForm(data, options) {
        if (!getPageRoot()) return;

        backgroundOptions = options && options.length ? options : backgroundOptions;
        currentLogoUrl = data.logoUrl || DEFAULT_LOGO;
        pendingLogoFile = null;

        var logoFile = getEl('logoFile');
        if (logoFile) logoFile.value = '';

        var backgroundUrl = resolveBackgroundUrl(data.backgroundImageUrl);
        var hidden = getEl('backgroundImageUrl');
        if (hidden) hidden.value = backgroundUrl;

        var badgeInput = getEl('menuHeroBadge');
        var taglineInput = getEl('menuTagline');
        if (badgeInput) badgeInput.value = data.menuHeroBadgeCustom || '';
        if (taglineInput) taglineInput.value = data.menuTaglineCustom || '';

        updateMenuTextHints(backgroundUrl, data);
        renderBackgroundPicker(backgroundUrl);
        updatePreview();
    }

    function showSettingsForm() {
        var loading = getEl('settingsLoading');
        var form = getEl('settingsForm');
        if (loading) loading.hidden = true;
        if (form) form.hidden = false;
    }

    function loadSettings() {
        if (settingsAbortController) {
            settingsAbortController.abort();
        }
        settingsAbortController = new AbortController();

        fetch('/Home/GetRestaurantSettings', {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            signal: settingsAbortController.signal
        })
            .then(function (res) { return res.json(); })
            .then(function (response) {
                if (!getPageRoot()) return;
                showSettingsForm();
                if (response.success) {
                    fillForm(response.data || {}, response.backgroundOptions || []);
                } else {
                    fillForm({}, response.backgroundOptions || []);
                }
            })
            .catch(function (err) {
                if (err && err.name === 'AbortError') return;
                if (!getPageRoot()) return;
                showSettingsForm();
                fillForm({}, []);
            });
    }

    function saveSettings(event) {
        event.preventDefault();

        if (pendingLogoFile) {
            var logoError = validateLogoFile(pendingLogoFile);
            if (logoError) {
                showLogoError(logoError);
                return;
            }
        }

        var formData = new FormData();
        formData.append('backgroundImageUrl', getSelectedBackgroundUrl());

        var badgeInput = getEl('menuHeroBadge');
        var taglineInput = getEl('menuTagline');
        formData.append('menuHeroBadge', badgeInput ? badgeInput.value.trim() : '');
        formData.append('menuTagline', taglineInput ? taglineInput.value.trim() : '');

        if (pendingLogoFile) {
            formData.append('logo', pendingLogoFile);
        }

        var saveBtn = getEl('saveSettingsBtn');
        if (saveBtn) saveBtn.disabled = true;

        fetch('/Home/SaveRestaurantSettings', {
            method: 'POST',
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            body: formData
        })
            .then(function (res) { return res.json(); })
            .then(function (response) {
                if (!getPageRoot()) return;
                if (response.success) {
                    if (response.data) {
                        rememberSavedBackground(response.data.backgroundImageUrl);
                        fillForm(response.data, response.backgroundOptions || backgroundOptions);
                    } else {
                        rememberSavedBackground(getSelectedBackgroundUrl());
                    }
                    if (window.showToast) {
                        window.showToast(response.message || 'تنظیمات ذخیره شد.', 'success');
                    }
                } else if (window.showToast) {
                    window.showToast(response.message || 'خطا در ذخیره تنظیمات.', 'error');
                }
            })
            .catch(function () {
                if (window.showToast) {
                    window.showToast('خطا در ارتباط با سرور.', 'error');
                }
            })
            .finally(function () {
                var btn = getEl('saveSettingsBtn');
                if (btn) btn.disabled = false;
            });
    }

    function resetSettings() {
        var confirmPromise = window.showConfirm
            ? window.showConfirm({
                title: 'بازگشت به پیش‌فرض',
                message: 'لوگو، متن‌های منو و قالب منو به حالت پیش‌فرض بازمی‌گردند. ادامه می‌دهید؟',
                confirmText: 'بله، بازگردانی',
                cancelText: 'انصراف'
            })
            : Promise.resolve(window.confirm('تنظیمات منو به حالت پیش‌فرض بازگردانده شود؟'));

        confirmPromise.then(function (confirmed) {
            if (!confirmed || !getPageRoot()) return;

            var resetBtn = getEl('resetSettingsBtn');
            var saveBtn = getEl('saveSettingsBtn');
            if (resetBtn) resetBtn.disabled = true;
            if (saveBtn) saveBtn.disabled = true;

            fetch('/Home/ResetRestaurantSettings', {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(function (res) { return res.json(); })
                .then(function (response) {
                    if (!getPageRoot()) return;
                    if (response.success) {
                        if (response.data) {
                            rememberSavedBackground(response.data.backgroundImageUrl);
                            fillForm(response.data, response.backgroundOptions || backgroundOptions);
                        } else {
                            rememberSavedBackground(DEFAULT_BACKGROUND);
                        }
                        if (window.showToast) {
                            window.showToast(response.message || 'تنظیمات به پیش‌فرض بازگشت.', 'success');
                        }
                    } else if (window.showToast) {
                        window.showToast(response.message || 'خطا در بازگردانی تنظیمات.', 'error');
                    }
                })
                .catch(function () {
                    if (window.showToast) {
                        window.showToast('خطا در ارتباط با سرور.', 'error');
                    }
                })
                .finally(function () {
                    var rb = getEl('resetSettingsBtn');
                    var sb = getEl('saveSettingsBtn');
                    if (rb) rb.disabled = false;
                    if (sb) sb.disabled = false;
                });
        });
    }

    function destroyRestaurantSettingsPage() {
        if (settingsAbortController) {
            settingsAbortController.abort();
            settingsAbortController = null;
        }
        pendingLogoFile = null;
    }

    function initRestaurantSettingsPage() {
        var page = getPageRoot();
        if (!page) return;
        if (page.dataset.settingsReady === 'true') return;
        page.dataset.settingsReady = 'true';

        bindInputs();

        var form = getEl('settingsForm');
        var resetBtn = getEl('resetSettingsBtn');
        if (form) form.addEventListener('submit', saveSettings);
        if (resetBtn) resetBtn.addEventListener('click', resetSettings);

        loadSettings();
    }

    window.initRestaurantSettingsPage = initRestaurantSettingsPage;
    window.destroyRestaurantSettingsPage = destroyRestaurantSettingsPage;
})();
