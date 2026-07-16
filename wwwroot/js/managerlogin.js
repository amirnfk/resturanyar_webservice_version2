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
    const passwordField = document.getElementById("passwordField"); // Input رمز
    const togglePassword = document.getElementById("togglePassword"); // آیکون چشم
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

    // ===== Toggle Password Visibility (جدید اضافه شده) =====
    if (togglePassword && passwordField) {
        togglePassword.addEventListener("click", function () {
            // تغییر نوع اینپوت بین رمز و متن
            const type = passwordField.getAttribute("type") === "password" ? "text" : "password";
            passwordField.setAttribute("type", type);

            // تغییر ظاهر آیکون (اختیاری)
            if (type === "text") {
                togglePassword.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="text-orange"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path><circle cx="12" cy="12" r="3"></circle></svg>';
            } else {
                togglePassword.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="text-muted"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"></path><line x1="1" y1="1" x2="23" y2="23"></line></svg>';
            }
        });
    }


    [passwordPhoneInput, otpPhoneInput, otpCodeInput].forEach(input => {
        if (input) {
            input.addEventListener("input", function () {
                this.value = toEnglishDigits(this.value);
            });
        }
    });

    btnSwitchToOtp.addEventListener("click", function (e) {
        e.preventDefault();
        passwordSection.classList.add("d-none");
        otpSection.classList.remove("d-none");
        document.querySelector("#passwordForm .alert")?.remove();
    });

    btnSwitchToRegister.addEventListener("click", function (e) {
        e.preventDefault();
        passwordSection.classList.add("d-none");
        otpSection.classList.remove("d-none");
        document.querySelector("#passwordForm .alert")?.remove();
    });

    btnSwitchToPassword.addEventListener("click", function (e) {
        e.preventDefault();
        otpSection.classList.add("d-none");
        passwordSection.classList.remove("d-none");
    });


    btnGetOtp.addEventListener("click", function () {
        let phone = otpPhoneInput.value;
        phone = toEnglishDigits(phone);
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
            // ADDED 'async' HERE
            .then(async data => {
                if (data.success) {
                    const tokenStored = await generateAndStoreV2Token(phone);
                    if (tokenStored) {
                        window.location.href = data.redirectUrl ?? location.reload();
                    } else {
                        Swal.fire('خطا', 'خطا در دریافت توکن. لطفاً دوباره تلاش کنید.', 'error');
                    }
                } else if (data.needsRegistration) {
                    const modal = new bootstrap.Modal(document.getElementById('registerModal'));
                    modal.show();
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


    const registerName = document.getElementById('registerName');
    const registerPassword = document.getElementById('registerPassword');
    const registerConfirm = document.getElementById('registerConfirmPassword');
    const registerSubmitBtn = document.getElementById('btnRegisterSubmit');

    [registerName, registerPassword, registerConfirm].forEach(input => {
        if (input) {
            input.addEventListener('input', function () {
                this.value = toEnglishDigits(this.value);
            });
        }
    });

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
    async function generateAndStoreV2Token(phone) {
        try {
            const response = await fetch('/api/v2/UserApi/generate-token', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ PhoneNumber: phone })
            });

            const data = await response.json();

            if (response.ok && data.success) {
                localStorage.setItem('accessToken', data.token);
                localStorage.setItem('refreshToken', data.refreshToken);
                localStorage.setItem('phone', phone);
                return true;
            }
            console.error('Token generation failed:', data.message || response.status);
            return false;
        } catch (error) {
            console.error('Token generation error:', error);
            return false;
        }
    }
    function validateRegisterForm() {
        let name = registerName.value.trim();
        let password = registerPassword.value;
        let confirm = registerConfirm.value;

        password = toEnglishDigits(password);
        confirm = toEnglishDigits(confirm);

        let isValid = true;

        if (name === '') {
            showRegisterError('registerNameError', 'نام و نام خانوادگی الزامی است');
            isValid = false;
        } else if (name.length < 3) {
            showRegisterError('registerNameError', 'حداقل ۳ کاراکتر');
            isValid = false;
        } else {
            clearRegisterError('registerNameError');
        }

        if (password.length < 6) {
            showRegisterError('registerPasswordError', 'رمز عبور حداقل ۶ کاراکتر');
            isValid = false;
        } else {
            clearRegisterError('registerPasswordError');
        }

        if (password !== confirm) {
            showRegisterError('registerConfirmError', 'رمز عبور و تکرار آن یکسان نیستند');
            isValid = false;
        } else {
            clearRegisterError('registerConfirmError');
        }

        return isValid;
    }

    registerSubmitBtn.addEventListener('click', function () {
        const phone = this.dataset.phone;
        if (!phone) {
            Swal.fire('خطا', 'شماره تلفن یافت نشد', 'error');
            return;
        }

        if (!validateRegisterForm()) {
            return;
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
            .then(async data => {
                if (data.success) {
                    const tokenStored = await generateAndStoreV2Token(phone);
                    if (tokenStored) {
                        window.location.href = data.redirectUrl;
                    } else {
                        Swal.fire('خطا', 'خطا در دریافت توکن. لطفاً دوباره تلاش کنید.', 'error');
                    }
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

    if (registerName) {
        registerName.addEventListener('input', () => clearRegisterError('registerNameError'));
    }
    if (registerPassword) {
        registerPassword.addEventListener('input', () => clearRegisterError('registerPasswordError'));
    }

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

    // ===== ورود با رمز عبور + ساخت توکن V2 قبل از ارسال فرم =====
    const passwordForm = document.getElementById('passwordForm');
    if (passwordForm) {
        passwordForm.addEventListener('submit', function (e) {
            e.preventDefault(); // جلوگیری از ارسال عادی

            const phone = passwordPhoneInput ? passwordPhoneInput.value.trim() : '';
            const submitBtn = passwordForm.querySelector('button[type="submit"]');
            const originalText = submitBtn.textContent;

            // غیرفعال کردن دکمه و نمایش اسپینر
            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> در حال ورود...';

            // تابع ارسال نهایی فرم
            const finalSubmit = () => {
                passwordForm.submit();
                // پس از submit صفحه رفرش می‌شود، نیازی به بازگردانی دکمه نیست
            };

            if (phone) {
                generateAndStoreV2Token(phone)
                    .then(tokenStored => {
                        if (tokenStored) {
                            finalSubmit();
                        } else {
                            Swal.fire('خطا', 'خطا در دریافت توکن. لطفاً دوباره تلاش کنید.', 'error');
                            submitBtn.disabled = false;
                            submitBtn.textContent = originalText;
                        }
                    });
            } else {
                // اگر شماره موجود نبود، مستقیماً فرم را ارسال کن
                finalSubmit();
            }
        });
    }
});