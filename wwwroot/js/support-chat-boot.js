(function (window, document) {
  'use strict';

  if (window.__rySupportBootStarted) return;
  window.__rySupportBootStarted = true;

  // Admin pages use the Support Chat inbox, not the floating customer widget
  if (document.body && document.body.getAttribute('data-ry-admin') === '1') return;

  function startBuiltIn() {
    if (window.__rySupportChatInstance) return;

    function loadChat() {
      var s = document.createElement('script');
      s.src = '/js/support-chat.js';
      s.async = true;
      s.onload = function () {
        if (typeof window.__ryStartSupportChat === 'function') {
          window.__ryStartSupportChat({ embed: false, autoOpen: false, forceBuiltIn: true });
        }
      };
      document.head.appendChild(s);
    }

    if (typeof window.__ryPrepareSupportImage === 'function') {
      loadChat();
      return;
    }
    var prep = document.createElement('script');
    prep.src = '/js/support-image-prep.js';
    prep.async = true;
    prep.onload = loadChat;
    prep.onerror = loadChat;
    document.head.appendChild(prep);
  }

  function boot() {
    // Always use built-in chat (Goftino removed)
    startBuiltIn();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', boot);
  } else {
    boot();
  }
})(window, document);
