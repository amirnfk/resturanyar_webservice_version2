// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// تعریف توکن ثابت
const STATIC_TOKEN = "stR@nG3_Stat1c_T0ken_Resturanyar_2025!#X9LpQ";

// تنظیم global برای همه AJAX call های jQuery
$.ajaxSetup({
    headers: {
        "Authorization": "Bearer " + STATIC_TOKEN
    },
    contentType: "application/json",
    error: function (xhr, status, error) {
        console.error("AJAX Error:", xhr.responseText);
        if (typeof showToast === 'function') {
            showToast('خطا در ارتباط با سرور: ' + xhr.status, 'error');
        } else {
            console.error('خطا در ارتباط با سرور: ' + xhr.status);
        }
    }
});

(function loadSupportBoot() {
    var s = document.createElement('script');
    s.src = '/js/support-chat-boot.js';
    s.async = true;
    document.head.appendChild(s);
})();
