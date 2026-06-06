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
    btnVerifyOtp.addEventListener("click", function () {
        // نکته مهم: مقادیر را اینجا می‌خوانیم و تبدیل می‌کنیم
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
                } else {
                    Swal.fire('خطا', data.message, 'error');
                }
            })
            .finally(() => {
                btnVerifyOtp.disabled = false;
                btnVerifyOtp.textContent = "تایید و ورود";
            });
    });

    // ===== edit phone =====
    btnEditPhone.addEventListener("click", function () {
        otpStepCode.classList.add("d-none");
        otpStepPhone.classList.remove("d-none");
        otpCodeInput.value = "";
    });
});