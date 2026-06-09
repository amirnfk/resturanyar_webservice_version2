function toEnglishDigits(str) {
    if (!str) return str;
    const persianDigits = '۰۱۲۳۴۵۶۷۸۹';
    const arabicDigits = '٠١٢٣٤٥٦٧٨٩';
    return str.replace(/[۰-۹٠-٩]/g, function (d) {
        if (persianDigits.indexOf(d) !== -1)
            return persianDigits.indexOf(d);
        return arabicDigits.indexOf(d);
    });
}

document.addEventListener("DOMContentLoaded", function () {
    // ===== inputs =====
    const passwordPhoneInput = document.querySelector('input[name="Phone"]');
    const otpPhoneInput = document.getElementById("otpPhoneInput");
    const otpCodeInput = document.getElementById("otpCodeInput");

    // ===== sections & buttons =====
    const passwordSection = document.getElementById("login-password-section");
    const otpSection = document.getElementById("login-otp-section");
    const btnSwitchToOtp = document.getElementById("btnSwitchToOtp");
    const btnSwitchToRegister = document.getElementById("btnSwitchToRegister");
    const btnSwitchToPassword = document.getElementById("btnSwitchToPassword");
    const btnGetOtp = document.getElementById("btnGetOtp");
    const btnVerifyOtp = document.getElementById("btnVerifyOtp");
    const btnEditPhone = document.getElementById("btnEditPhone");
    const otpStepPhone = document.getElementById("otp-step-phone");
    const otpStepCode = document.getElementById("otp-step-code");
    const displayPhone = document.getElementById("displayPhone");

    // ===== auto convert digits (در لحظه تایپ) =====
    [passwordPhoneInput, otpPhoneInput, otpCodeInput].forEach(input => {
        if (input) {
            input.addEventListener("input", function () {
                this.value = toEnglishDigits(this.value);
            });
        }
    });

    // ===== switch tabs =====
    btnSwitchToOtp.addEventListener("click", function () {
        passwordSection.classList.add("d-none");
        otpSection.classList.remove("d-none");
        document.querySelector("#passwordForm .alert")?.remove();
    });
    btnSwitchToRegister.addEventListener("click", function () {
        passwordSection.classList.add("d-none");
        otpSection.classList.remove("d-none");
        btnSwitchToPassword.classList.add("d-none");
        document.querySelector("#passwordForm .alert")?.remove();
    });

    btnSwitchToPassword.addEventListener("click", function () {
        otpSection.classList.add("d-none");
        passwordSection.classList.remove("d-none");
    });

    // ===== request OTP =====
    btnGetOtp.addEventListener("click", function () {
        // نکته مهم: مقدار را دقیقاً اینجا می‌خوانیم و تبدیل می‌کنیم
        let phone = otpPhoneInput.value;
        phone = toEnglishDigits(phone); // تبدیل اطمینانی
        phone = phone.replace(/\s+/g, "").trim();

        const mobileRegex = /^09\d{9}$/;

        if (!mobileRegex.test(phone)) {
            Swal.fire('خطا', 'شماره موبایل معتبر نیست', 'error');
            return;
        }

        btnGetOtp.disabled = true;
        const originalText = btnGetOtp.textContent;
        btnGetOtp.innerHTML = '<span class="spinner-border spinner-border-sm"></span> در حال ارسال...';

        $.ajax({
            url: '/api/UserApi/otprequest',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ phoneNumber: phone }),
            success: function (data) {
                if (data.success) {
                    displayPhone.textContent = phone;
                    otpStepPhone.classList.add("d-none");
                    otpStepCode.classList.remove("d-none");
                    otpCodeInput.focus();
                } else {
                    Swal.fire('خطا', data.message, 'error');
                }
            },
            complete: function () {
                btnGetOtp.disabled = false;
                btnGetOtp.textContent = originalText;
            }
        });
    });

    // ===== verify OTP =====
    // ===== verify OTP =====
    btnVerifyOtp.addEventListener("click", function () {
        let phone = otpPhoneInput.value;
        let otp = otpCodeInput.value;

        phone = toEnglishDigits(phone).trim();
        otp = toEnglishDigits(otp).trim();

        if (otp.length < 4) {
            Swal.fire('توجه', 'کد تایید کامل نیست', 'warning');
            return;
        }

        btnVerifyOtp.disabled = true;
        btnVerifyOtp.innerHTML = '<span class="spinner-border spinner-border-sm"></span> در حال بررسی...';

        fetch('/api/UserApi/verifyotpweb', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber: phone, code: otp })
        })
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    window.location.href = data.redirectUrl ?? location.reload();
                } else if (data.needsRegistration) {
                    // باز کردن مودال ثبت‌نام
                    const modal = new bootstrap.Modal(document.getElementById('registerModal'));
                    modal.show();
                    // ذخیره شماره برای استفاده در ثبت‌نام
                    document.getElementById('btnRegisterSubmit').dataset.phone = data.phoneNumber;
                } else {
                    Swal.fire('خطا', data.message, 'error');
                }
            })
            .finally(() => {
                btnVerifyOtp.disabled = false;
                btnVerifyOtp.textContent = "تایید و ورود";
            });
    });

    // ===== register submit =====
    // ===== عناصر مودال =====
    const registerName = document.getElementById('registerName');
    const registerPassword = document.getElementById('registerPassword');
    const registerConfirm = document.getElementById('registerConfirmPassword');
    const registerSubmitBtn = document.getElementById('btnRegisterSubmit');

    // تبدیل اعداد فارسی به انگلیسی برای فیلدهای ثبت‌نام
    [registerName, registerPassword, registerConfirm].forEach(input => {
        if (input) {
            input.addEventListener('input', function () {
                this.value = toEnglishDigits(this.value);
            });
        }
    });

    // نمایش خطا در زیر فیلدهای مودال
    function showRegisterError(fieldId, message) {
        const errorDiv = document.getElementById(fieldId);
        if (errorDiv) {
            errorDiv.textContent = message;
            errorDiv.style.display = 'block';
            const input = errorDiv.previousElementSibling;
            if (input) input.classList.add('is-invalid');
        }
    }

    function clearRegisterError(fieldId) {
        const errorDiv = document.getElementById(fieldId);
        if (errorDiv) {
            errorDiv.textContent = '';
            errorDiv.style.display = 'none';
            const input = errorDiv.previousElementSibling;
            if (input) input.classList.remove('is-invalid');
        }
    }

    // اعتبارسنجی اصلی شامل بررسی تطابق رمزها
    function validateRegisterForm() {
        let name = registerName.value.trim();
        let password = registerPassword.value;
        let confirm = registerConfirm.value;

        // تبدیل مجدد برای اطمینان
        password = toEnglishDigits(password);
        confirm = toEnglishDigits(confirm);

        let isValid = true;

        // بررسی نام
        if (name === '') {
            showRegisterError('registerNameError', 'نام و نام خانوادگی الزامی است');
            isValid = false;
        } else if (name.length < 3) {
            showRegisterError('registerNameError', 'حداقل ۳ کاراکتر');
            isValid = false;
        } else {
            clearRegisterError('registerNameError');
        }

        // بررسی رمز عبور
        if (password.length < 6) {
            showRegisterError('registerPasswordError', 'رمز عبور حداقل ۶ کاراکتر');
            isValid = false;
        } else {
            clearRegisterError('registerPasswordError');
        }

        // *** مهمترین بخش: بررسی یکسان بودن رمز و تکرار آن ***
        if (password !== confirm) {
            showRegisterError('registerConfirmError', 'رمز عبور و تکرار آن یکسان نیستند');
            isValid = false;
        } else {
            clearRegisterError('registerConfirmError');
        }

        return isValid;
    }

    // ارسال درخواست ثبت‌نام
    registerSubmitBtn.addEventListener('click', function () {
        const phone = this.dataset.phone;
        if (!phone) {
            Swal.fire('خطا', 'شماره تلفن یافت نشد', 'error');
            return;
        }

        if (!validateRegisterForm()) {
            return; // توقف ارسال در صورت عدم اعتبار
        }

        let name = registerName.value.trim();
        let password = registerPassword.value;
        password = toEnglishDigits(password);

        this.disabled = true;
        this.innerHTML = '<span class="spinner-border spinner-border-sm"></span> در حال ثبت‌نام...';

        fetch('/api/UserApi/registerandlogin', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ phoneNumber: phone, name, password })
        })
            .then(r => r.json())
            .then(data => {
                if (data.success) {
                    window.location.href = data.redirectUrl;
                } else {
                    Swal.fire('خطا', data.message, 'error');
                }
            })
            .catch(err => {
                console.error(err);
                Swal.fire('خطا', 'مشکل در ارتباط با سرور', 'error');
            })
            .finally(() => {
                this.disabled = false;
                this.textContent = 'ثبت‌نام و ورود';
            });
    });

    // اعتبارسنجی لحظه‌ای برای تکرار رمز (هنگام تایپ)
    if (registerConfirm) {
        registerConfirm.addEventListener('input', function () {
            let password = registerPassword.value;
            let confirm = this.value;
            if (password !== confirm) {
                showRegisterError('registerConfirmError', 'رمز عبور و تکرار آن یکسان نیستند');
            } else {
                clearRegisterError('registerConfirmError');
            }
        });
    }

    // پاک کردن خطاهای دیگر هنگام تایپ
    if (registerName) {
        registerName.addEventListener('input', () => clearRegisterError('registerNameError'));
    }
    if (registerPassword) {
        registerPassword.addEventListener('input', () => clearRegisterError('registerPasswordError'));
    }

    // ریست مودال هنگام بسته شدن
    const modal = document.getElementById('registerModal');
    if (modal) {
        modal.addEventListener('hidden.bs.modal', function () {
            registerName.value = '';
            registerPassword.value = '';
            if (registerConfirm) registerConfirm.value = '';
            clearRegisterError('registerNameError');
            clearRegisterError('registerPasswordError');
            clearRegisterError('registerConfirmError');
        });
    }
    // ===== edit phone =====
    btnEditPhone.addEventListener("click", function () {
        otpStepCode.classList.add("d-none");
        otpStepPhone.classList.remove("d-none");
        otpCodeInput.value = "";
    });
});