(function () {

    var MAX_LOGO_BYTES = 512000;

    var DEFAULT_PRIMARY = '#f97316';

    var DEFAULT_SECONDARY = '#fff7ed';

    var DEFAULT_LOGO = '/images/logo.png';

    var DEFAULT_BACKGROUND = '/images/backgrounds/preset-warm.jpg';



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



        var badge = document.getElementById('settingsPreview')?.querySelector('.preview-badge');

        if (badge) {

            badge.style.background = 'linear-gradient(135deg, ' + primary + ', ' + secondary + ')';

        }



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



    function fillForm(data, options) {

        backgroundOptions = options && options.length ? options : backgroundOptions;



        document.getElementById('primaryColor').value = data.primaryColor || DEFAULT_PRIMARY;

        document.getElementById('secondaryColor').value = data.secondaryColor || DEFAULT_SECONDARY;

        currentLogoUrl = data.logoUrl || DEFAULT_LOGO;

        pendingLogoFile = null;

        document.getElementById('logoFile').value = '';



        var backgroundUrl = data.backgroundImageUrl || DEFAULT_BACKGROUND;

        document.getElementById('backgroundImageUrl').value = backgroundUrl;



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

                message: 'رنگ‌ها، لوگو و پس‌زمینه به حالت پیش‌فرض بازمی‌گردند. ادامه می‌دهید؟',

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

