(function () {
    'use strict';

    var icons = {
        success: '<i class="fa-solid fa-circle-check" aria-hidden="true"></i>',
        error: '<i class="fa-solid fa-circle-xmark" aria-hidden="true"></i>',
        info: '<i class="fa-solid fa-circle-info" aria-hidden="true"></i>'
    };

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text == null ? '' : String(text);
        return div.innerHTML;
    }

    window.showToast = function (message, type) {
        type = type || 'success';
        if (!icons[type]) type = 'success';

        document.querySelectorAll('.app-toast').forEach(function (toast) {
            toast.remove();
        });

        var toast = document.createElement('div');
        toast.className = 'app-toast app-toast--' + type;
        toast.setAttribute('role', 'alert');
        toast.innerHTML =
            '<span class="app-toast__icon" aria-hidden="true">' + icons[type] + '</span>' +
            '<span class="app-toast__message">' + escapeHtml(message) + '</span>' +
            '<button type="button" class="app-toast__close" aria-label="\u0628\u0633\u062a\u0646">&times;</button>';

        document.body.appendChild(toast);

        requestAnimationFrame(function () {
            toast.classList.add('app-toast--visible');
        });

        var dismissed = false;
        function dismiss() {
            if (dismissed) return;
            dismissed = true;
            toast.classList.remove('app-toast--visible');
            setTimeout(function () {
                if (toast.parentNode) toast.remove();
            }, 260);
        }

        toast.querySelector('.app-toast__close').addEventListener('click', dismiss);
        setTimeout(dismiss, 3000);
    };
})();
