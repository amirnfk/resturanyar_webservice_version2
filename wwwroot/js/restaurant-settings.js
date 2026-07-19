(function () {

    var MAX_LOGO_BYTES = 512000;

    var DEFAULT_PRIMARY = '#f97316';

    var DEFAULT_SECONDARY = '#fff7ed';

    var DEFAULT_LOGO = '/images/logo.png';

    var DEFAULT_BACKGROUND = '/images/backgrounds/default.jpg';



    var backgroundOptions = [];

    var pendingLogoFile = null;

    var currentLogoUrl = DEFAULT_LOGO;



    function normalizeHex(value, fallback) {

        if (!value) return fallback;

        var trimmed = value.trim();

        if (/^#[0-9A-Fa-f]{6}$/.test(trimmed)) return trimmed;

        return fallback;

    }



    function syncColorPickers() {

        var primary = normalizeHex(document.getElementById('primaryColor').value, DEFAULT_PRIMARY);

        var secondary = normalizeHex(document.getElementById('secondaryColor').value, DEFAULT_SECONDARY);

        document.getElementById('primaryColorPicker').value = primary;

        document.getElementById('secondaryColorPicker').value = secondary;

        document.getElementById('primaryColor').value = primary;

        document.getElementById('secondaryColor').value = secondary;

    }



    function getSelectedBackgroundUrl() {

        var hidden = document.getElementById('backgroundImageUrl');

        return hidden && hidden.value ? hidden.value : DEFAULT_BACKGROUND;

    }



    function getBackgroundOptionForUrl(url) {

        var effectiveUrl = url || getSelectedBackgroundUrl();

        return backgroundOptions.find(function (option) {

            return option.url === effectiveUrl;

        }) || null;

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

        var badgeInput = document.getElementById('menuHeroBadge');

        var taglineInput = document.getElementById('menuTagline');

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



        var badgeHint = document.getElementById('menuHeroBadgeDefaultHint');

        var taglineHint = document.getElementById('menuTaglineDefaultHint');

        if (badgeHint) badgeHint.textContent = defaults.badge;

        if (taglineHint) taglineHint.textContent = defaults.tagline;

    }



    function getPreviewLogoUrl() {

        if (pendingLogoFile) {

            return URL.createObjectURL(pendingLogoFile);

        }

        return currentLogoUrl || DEFAULT_LOGO;

    }



    function updatePreview() {

        var primary = normalizeHex(document.getElementById('primaryColor').value, DEFAULT_PRIMARY);

        var secondary = normalizeHex(document.getElementById('secondaryColor').value, DEFAULT_SECONDARY);

        var logoUrl = getPreviewLogoUrl();

        var backgroundUrl = getSelectedBackgroundUrl();



        document.getElementById('previewLogo').src = logoUrl;

        var previewBg = document.querySelector('.preview-bg');

        if (previewBg) previewBg.style.backgroundImage = 'url("' + backgroundUrl + '")';



        var logoThumb = document.getElementById('logoPreviewThumb');

        if (logoThumb) logoThumb.src = logoUrl;



        var badge = document.getElementById('previewMenuBadge');

        if (badge) {

            badge.style.background = 'linear-gradient(135deg, ' + primary + ', ' + secondary + ')';

            badge.textContent = getEffectiveMenuTexts().badge;

        }



        var tagline = document.getElementById('previewMenuTagline');

        if (tagline) tagline.textContent = getEffectiveMenuTexts().tagline;



        var primarySwatch = document.getElementById('previewPrimary');

        var secondarySwatch = document.getElementById('previewSecondary');

        if (primarySwatch) primarySwatch.style.background = primary;

        if (secondarySwatch) secondarySwatch.style.background = secondary;

    }



    function renderBackgroundPicker(selectedUrl) {

        var container = document.getElementById('backgroundPicker');

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

                document.getElementById('backgroundImageUrl').value = option.url;

                renderBackgroundPicker(option.url);

                updateMenuTextHints(option.url);

                updatePreview();

            });



            container.appendChild(btn);

        });

    }



    function showLogoError(message) {

        var el = document.getElementById('logoFileError');

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



    function bindInputs() {

        ['primaryColor', 'secondaryColor'].forEach(function (id) {

            var el = document.getElementById(id);

            if (el) el.addEventListener('input', updatePreview);

        });



        document.getElementById('primaryColorPicker').addEventListener('input', function (e) {

            document.getElementById('primaryColor').value = e.target.value;

            updatePreview();

        });



        document.getElementById('secondaryColorPicker').addEventListener('input', function (e) {

            document.getElementById('secondaryColor').value = e.target.value;

            updatePreview();

        });



        document.getElementById('primaryColor').addEventListener('change', syncColorPickers);

        document.getElementById('secondaryColor').addEventListener('change', function () {

            syncColorPickers();

            updatePreview();

        });



        ['menuHeroBadge', 'menuTagline'].forEach(function (id) {

            var el = document.getElementById(id);

            if (el) el.addEventListener('input', updatePreview);

        });



        document.getElementById('logoFile').addEventListener('change', function (e) {

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

        });

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

        backgroundOptions = options && options.length ? options : backgroundOptions;



        document.getElementById('primaryColor').value = data.primaryColor || DEFAULT_PRIMARY;

        document.getElementById('secondaryColor').value = data.secondaryColor || DEFAULT_SECONDARY;

        currentLogoUrl = data.logoUrl || DEFAULT_LOGO;

        pendingLogoFile = null;

        document.getElementById('logoFile').value = '';



        var backgroundUrl = resolveBackgroundUrl(data.backgroundImageUrl);

        document.getElementById('backgroundImageUrl').value = backgroundUrl;



        var badgeInput = document.getElementById('menuHeroBadge');

        var taglineInput = document.getElementById('menuTagline');

        if (badgeInput) badgeInput.value = data.menuHeroBadgeCustom || '';

        if (taglineInput) taglineInput.value = data.menuTaglineCustom || '';



        updateMenuTextHints(backgroundUrl, data);



        syncColorPickers();

        renderBackgroundPicker(backgroundUrl);

        updatePreview();

    }



    function loadSettings() {

        fetch('/Home/GetRestaurantSettings', {

            headers: { 'X-Requested-With': 'XMLHttpRequest' }

        })

            .then(function (res) { return res.json(); })

            .then(function (response) {

                document.getElementById('settingsLoading').hidden = true;

                document.getElementById('settingsForm').hidden = false;



                if (response.success) {

                    fillForm(response.data || {}, response.backgroundOptions || []);

                } else {

                    fillForm({}, response.backgroundOptions || []);

                }

            })

            .catch(function () {

                document.getElementById('settingsLoading').hidden = true;

                document.getElementById('settingsForm').hidden = false;

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

        formData.append('primaryColor', normalizeHex(document.getElementById('primaryColor').value, DEFAULT_PRIMARY));

        formData.append('secondaryColor', normalizeHex(document.getElementById('secondaryColor').value, DEFAULT_SECONDARY));

        formData.append('backgroundImageUrl', getSelectedBackgroundUrl());



        var badgeInput = document.getElementById('menuHeroBadge');

        var taglineInput = document.getElementById('menuTagline');

        formData.append('menuHeroBadge', badgeInput ? badgeInput.value.trim() : '');

        formData.append('menuTagline', taglineInput ? taglineInput.value.trim() : '');



        if (pendingLogoFile) {

            formData.append('logo', pendingLogoFile);

        }



        var saveBtn = document.getElementById('saveSettingsBtn');

        saveBtn.disabled = true;



        fetch('/Home/SaveRestaurantSettings', {

            method: 'POST',

            headers: { 'X-Requested-With': 'XMLHttpRequest' },

            body: formData

        })

            .then(function (res) { return res.json(); })

            .then(function (response) {

                if (response.success) {

                    if (response.data) {

                        fillForm(response.data, response.backgroundOptions || backgroundOptions);

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

                saveBtn.disabled = false;

            });

    }



    function resetSettings() {

        var confirmPromise = window.showConfirm

            ? window.showConfirm({

                title: 'بازگشت به پیش‌فرض',

                message: 'رنگ‌ها، لوگو، متن‌های منو و پس‌زمینه به حالت پیش‌فرض بازمی‌گردند. ادامه می‌دهید؟',

                confirmText: 'بله، بازگردانی',

                cancelText: 'انصراف'

            })

            : Promise.resolve(window.confirm('تنظیمات به حالت پیش‌فرض بازگردانده شود؟'));



        confirmPromise.then(function (confirmed) {

            if (!confirmed) return;



            var resetBtn = document.getElementById('resetSettingsBtn');

            var saveBtn = document.getElementById('saveSettingsBtn');

            resetBtn.disabled = true;

            saveBtn.disabled = true;



            fetch('/Home/ResetRestaurantSettings', {

                method: 'POST',

                headers: { 'X-Requested-With': 'XMLHttpRequest' }

            })

                .then(function (res) { return res.json(); })

                .then(function (response) {

                    if (response.success) {

                        if (response.data) {

                            fillForm(response.data, response.backgroundOptions || backgroundOptions);

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

                    resetBtn.disabled = false;

                    saveBtn.disabled = false;

                });

        });

    }



    function initRestaurantSettingsPage() {

        var page = document.querySelector('.settings-page');

        if (!page) return;



        bindInputs();

        document.getElementById('settingsForm').addEventListener('submit', saveSettings);

        document.getElementById('resetSettingsBtn').addEventListener('click', resetSettings);

        loadSettings();

    }



    window.initRestaurantSettingsPage = initRestaurantSettingsPage;



    if (document.querySelector('.settings-page') && !window.__sidebarNavActive) {

        initRestaurantSettingsPage();

    }

})();

