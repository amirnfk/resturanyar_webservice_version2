(function () {
  'use strict';

  var lastTotalUnread = 0;
  var notifyAudio = null;

  function setBadge(total) {
    var el = document.getElementById('ryAdminSupportBadge');
    if (!el) return;
    var n = Math.max(0, parseInt(total, 10) || 0);
    if (n <= 0) {
      el.hidden = true;
      el.textContent = '0';
      el.classList.remove('is-single-digit');
      return;
    }
    el.hidden = false;
    el.textContent = n > 99 ? '99+' : String(n);
    el.classList.toggle('is-single-digit', n < 10);
  }

  function playNotifySound() {
    try {
      if (!notifyAudio) {
        notifyAudio = new Audio('/sounds/support-notify.mp3');
        notifyAudio.preload = 'auto';
        notifyAudio.volume = 0.6;
      }
      notifyAudio.currentTime = 0;
      var p = notifyAudio.play();
      if (p && typeof p.catch === 'function') {
        p.catch(function () {
          try {
            var ctx = new (window.AudioContext || window.webkitAudioContext)();
            var o = ctx.createOscillator();
            var g = ctx.createGain();
            o.connect(g);
            g.connect(ctx.destination);
            o.frequency.value = 880;
            g.gain.value = 0.05;
            o.start();
            setTimeout(function () { o.stop(); ctx.close(); }, 180);
          } catch (e2) { }
        });
      }
    } catch (e) { }
  }

  window.__rySetSupportUnreadBadge = setBadge;

  var initial = document.body && document.body.getAttribute('data-support-unread');
  lastTotalUnread = Math.max(0, parseInt(initial || '0', 10) || 0);
  setBadge(lastTotalUnread);

  // SupportChat page handles its own sound; skip here to avoid double play
  var isSupportPage = document.body && document.body.getAttribute('data-support-chat-page') === '1';
  if (isSupportPage) return;

  function poll() {
    fetch('/api/support-chat/admin/unread-total', { credentials: 'same-origin' })
      .then(function (r) { return r.json(); })
      .then(function (json) {
        if (json && json.success && json.data) {
          var total = json.data.totalUnread || 0;
          if (total > lastTotalUnread) playNotifySound();
          lastTotalUnread = total;
          setBadge(total);
        }
      })
      .catch(function () { });
  }

  function startBadgeHub() {
    if (!window.signalR) {
      poll();
      setInterval(poll, 45000);
      return;
    }

    var connection = new signalR.HubConnectionBuilder()
      .withUrl('/supportChatHub')
      .withAutomaticReconnect()
      .build();

    connection.on('UnreadUpdated', function (u) {
      if (!u || typeof u.totalUnread !== 'number') return;
      lastTotalUnread = u.totalUnread;
      setBadge(u.totalUnread);
    });

    // Fired only on new customer messages (not admin replies / mark-read)
    connection.on('ConversationUpdated', function (c) {
      if (!c) return;
      if ((c.unreadBySupport || 0) > 0) playNotifySound();
    });

    connection.onreconnected(function () {
      return connection.invoke('JoinAdminBadge').catch(function () { });
    });

    connection.start()
      .then(function () { return connection.invoke('JoinAdminBadge'); })
      .catch(function () {
        poll();
        setInterval(poll, 45000);
      });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', startBadgeHub);
  } else {
    startBadgeHub();
  }
})();
