(function (window, document) {
  'use strict';

  var GUEST_KEY = 'ry_support_guest_key';
  var STATE_KEY = 'ry_support_panel_state';
  var hubUrl = '/supportChatHub';
  var apiBase = '/api/support-chat';

  function uuid() {
    if (window.crypto && crypto.randomUUID) return crypto.randomUUID();
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
      var r = Math.random() * 16 | 0;
      var v = c === 'x' ? r : (r & 0x3 | 0x8);
      return v.toString(16);
    });
  }

  function getGuestKey() {
    try {
      var existing = localStorage.getItem(GUEST_KEY);
      if (existing) return existing;
      var key = 'g_' + uuid().replace(/-/g, '');
      localStorage.setItem(GUEST_KEY, key);
      return key;
    } catch (e) {
      return 'g_session_' + uuid().replace(/-/g, '');
    }
  }

  function readBodyContext() {
    var body = document.body || {};
    var injected = window.__resturanyarSupport || {};
    var restaurantId = injected.restaurantId || body.getAttribute('data-restaurant-id') || '';
    var restaurantName = injected.restaurantName || body.getAttribute('data-restaurant-name') || '';
    var ownerId = injected.ownerId || body.getAttribute('data-owner-id') || '';
    var ownerName = injected.ownerName || body.getAttribute('data-owner-name') || '';
    var ownerPhone = injected.phone || injected.ownerPhone || body.getAttribute('data-owner-phone') || '';
    return {
      restaurantId: restaurantId ? parseInt(restaurantId, 10) : null,
      ownerId: ownerId ? parseInt(ownerId, 10) : null,
      restaurantName: restaurantName || null,
      ownerName: ownerName || null,
      ownerPhone: ownerPhone || null,
      pageUrl: location.href,
      userAgent: navigator.userAgent
    };
  }

  function toTehranDate(iso) {
    if (!iso) return null;
    var s = String(iso).trim();
    // Server stores UTC; if JSON has no timezone, treat as UTC
    if (!/[zZ]$|[+-]\d{2}:?\d{2}$/.test(s)) {
      s = s.replace(' ', 'T');
      if (s.indexOf('T') === -1) return null;
      s += 'Z';
    }
    var d = new Date(s);
    return isNaN(d.getTime()) ? null : d;
  }

  function formatTime(iso) {
    try {
      var d = toTehranDate(iso);
      if (!d) return '';
      return d.toLocaleTimeString('fa-IR', {
        timeZone: 'Asia/Tehran',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch (e) {
      return '';
    }
  }

  function statusLabel(status) {
    if (status === 'sending') return 'در حال ارسال…';
    if (status === 'sent') return 'ارسال شد';
    if (status === 'failed') return 'ناموفق — تلاش مجدد';
    return '';
  }

  function ResturanyarSupportChat(options) {
    this.options = options || {};
    this.embed = !!this.options.embed;
    this.autoOpen = !!this.options.autoOpen || this.embed;
    this.connection = null;
    this.conversationId = null;
    this.isOnline = false;
    this.pending = {};
    this.root = null;
    this.panel = null;
    this.messagesEl = null;
    this.inputEl = null;
    this.statusEl = null;
    this.badgeEl = null;
    this.unreadCustomer = 0;
    this.replyTo = null;
  }

  ResturanyarSupportChat.prototype.init = function () {
    var self = this;
    if (this._inited) return;
    this._inited = true;

    this.ensureDom();
    this.bindUi();
    this.updateStatus();

    // Embed (Android): show panel once immediately — never play open animation later
    if (this.embed) {
      this.panel.classList.add('is-open');
      this.root.classList.add('is-chat-open');
      document.documentElement.classList.add('ry-sc-noscroll');
      document.body.classList.add('ry-sc-noscroll');
      this.saveState('open');
    } else {
      this.restoreState();
    }

    function afterHub() {
      if (self.embed) {
        self.showLoading('در حال آماده‌سازی گفتگو…');
        return self.openConversation();
      }
      if (self.autoOpen || self._wantOpen) {
        return self.open();
      }
    }

    // Embed: show loading immediately while hub/conversation start
    if (this.embed) {
      this.showLoading('در حال آماده‌سازی گفتگو…');
    }

    fetch(apiBase + '/settings', { credentials: 'same-origin' })
      .then(function (r) { return r.json(); })
      .then(function (json) {
        self.updateStatus();
        return self.connectHub().then(afterHub);
      })
      .catch(function () {
        self.updateStatus();
        return self.connectHub().then(afterHub);
      })
      .finally(function () {
        if (self.embed) self.hideLoading();
      });
  };

  ResturanyarSupportChat.prototype.destroy = function () {
    if (this.root && this.root.parentNode) this.root.parentNode.removeChild(this.root);
  };

  ResturanyarSupportChat.prototype.ensureDom = function () {
    if (document.getElementById('ry-support-root')) {
      this.root = document.getElementById('ry-support-root');
      this.panel = this.root.querySelector('.ry-sc-panel');
      this.messagesEl = this.root.querySelector('.ry-sc-messages');
      this.inputEl = this.root.querySelector('.ry-sc-input');
      this.statusEl = this.root.querySelector('.ry-sc-status');
      this.loadingEl = this.root.querySelector('.ry-sc-loading');
      this.loadingTextEl = this.root.querySelector('.ry-sc-loading-text');
      this.badgeEl = this.root.querySelector('.ry-sc-badge');
      this.composerEl = this.root.querySelector('.ry-sc-composer');
      this.ensureReplyUi();
      return;
    }

    var root = document.createElement('div');
    root.id = 'ry-support-root';
    if (this.embed) root.classList.add('is-embed');
    root.innerHTML =
      '<button type="button" class="ry-sc-fab" aria-label="پشتیبانی">' +
      '<span class="ry-sc-fab-ring"></span>' +
      '<i class="fas fa-headset"></i><span class="ry-sc-badge">0</span></button>' +
      '<div class="ry-sc-panel" role="dialog" aria-label="چت پشتیبانی">' +
      '<div class="ry-sc-header">' +
      '<div class="ry-sc-brand">' +
      '<div class="ry-sc-avatar" aria-hidden="true"><i class="fas fa-headset"></i></div>' +
      '<div class="ry-sc-header-main">' +
      '<h3 class="ry-sc-title">پشتیبانی رستورانیار</h3>' +
      '<div class="ry-sc-status"><span class="ry-sc-status-label">معمولاً ظرف حدود ۵ دقیقه پاسخ می‌دهیم</span></div>' +
      '</div></div>' +
      '<div class="ry-sc-header-actions">' +
      (this.embed ? '' : '<button type="button" class="ry-sc-icon-btn ry-sc-minimize" title="کوچک کردن"><i class="fas fa-minus"></i></button>') +
      '<button type="button" class="ry-sc-icon-btn ry-sc-close" title="بستن"><i class="fas fa-times"></i></button>' +
      '</div></div>' +
      '<div class="ry-sc-body">' +
      '<div class="ry-sc-loading" hidden aria-live="polite">' +
      '<div class="ry-sc-loading-card">' +
      '<div class="ry-sc-spinner" aria-hidden="true"></div>' +
      '<p class="ry-sc-loading-text">لطفاً کمی صبر کنید…</p>' +
      '</div></div>' +
      '<div class="ry-sc-messages">' +
      '<div class="ry-sc-empty">' +
      '<div class="ry-sc-empty-icon"><i class="fas fa-comments"></i></div>' +
      '<strong>سلام، خوش آمدید</strong>' +
      '<p>پیام خود را بنویسید؛ معمولاً ظرف حدود ۵ دقیقه پاسخ می‌دهیم.</p>' +
      '</div></div>' +
      '<div class="ry-sc-composer">' +
      '<div class="ry-sc-reply-bar" hidden>' +
      '<div class="ry-sc-reply-bar-main">' +
      '<strong class="ry-sc-reply-bar-who"></strong>' +
      '<span class="ry-sc-reply-bar-text"></span>' +
      '</div>' +
      '<button type="button" class="ry-sc-reply-bar-close" title="لغو پاسخ"><i class="fas fa-times"></i></button>' +
      '</div>' +
      '<div class="ry-sc-composer-row">' +
      '<button type="button" class="ry-sc-attach" title="ارسال تصویر"><i class="fas fa-image"></i></button>' +
      '<input type="file" class="ry-sc-file" accept="image/*" hidden />' +
      '<div class="ry-sc-input-wrap"><textarea class="ry-sc-input" rows="1" placeholder="پیام خود را بنویسید…"></textarea></div>' +
      '<button type="button" class="ry-sc-send" title="ارسال"><i class="fas fa-paper-plane"></i></button>' +
      '</div></div></div></div>';

    document.body.appendChild(root);
    this.root = root;
    this.panel = root.querySelector('.ry-sc-panel');
    this.messagesEl = root.querySelector('.ry-sc-messages');
    this.inputEl = root.querySelector('.ry-sc-input');
    this.statusEl = root.querySelector('.ry-sc-status');
    this.loadingEl = root.querySelector('.ry-sc-loading');
    this.loadingTextEl = root.querySelector('.ry-sc-loading-text');
    this.badgeEl = root.querySelector('.ry-sc-badge');
    this.composerEl = root.querySelector('.ry-sc-composer');
    this.ensureReplyUi();
  };

  ResturanyarSupportChat.prototype.ensureReplyUi = function () {
    if (!this.composerEl) return;
    if (!this.composerEl.querySelector('.ry-sc-composer-row')) {
      var row = document.createElement('div');
      row.className = 'ry-sc-composer-row';
      while (this.composerEl.firstChild) row.appendChild(this.composerEl.firstChild);
      this.composerEl.appendChild(row);
    }
    if (!this.composerEl.querySelector('.ry-sc-reply-bar')) {
      var bar = document.createElement('div');
      bar.className = 'ry-sc-reply-bar';
      bar.hidden = true;
      bar.innerHTML =
        '<div class="ry-sc-reply-bar-main">' +
        '<strong class="ry-sc-reply-bar-who"></strong>' +
        '<span class="ry-sc-reply-bar-text"></span>' +
        '</div>' +
        '<button type="button" class="ry-sc-reply-bar-close" title="لغو پاسخ"><i class="fas fa-times"></i></button>';
      this.composerEl.insertBefore(bar, this.composerEl.firstChild);
    }
    this.replyBarEl = this.composerEl.querySelector('.ry-sc-reply-bar');
  };

  ResturanyarSupportChat.prototype.replyWhoLabel = function (senderType) {
    return Number(senderType) === 1 ? 'پشتیبانی' : 'شما';
  };

  ResturanyarSupportChat.prototype.setReplyTarget = function (msg) {
    if (!msg || !msg.id) return;
    this.replyTo = {
      id: msg.id,
      senderType: msg.senderType,
      body: msg.body || '',
      hasImage: !!msg.imageUrl || !!msg.replyToHasImage
    };
    this.renderReplyBar();
    if (this.inputEl) this.inputEl.focus();
  };

  ResturanyarSupportChat.prototype.clearReplyTarget = function () {
    this.replyTo = null;
    this.renderReplyBar();
  };

  ResturanyarSupportChat.prototype.renderReplyBar = function () {
    if (!this.replyBarEl) return;
    if (!this.replyTo) {
      this.replyBarEl.hidden = true;
      return;
    }
    var who = this.replyBarEl.querySelector('.ry-sc-reply-bar-who');
    var text = this.replyBarEl.querySelector('.ry-sc-reply-bar-text');
    if (who) who.textContent = this.replyWhoLabel(this.replyTo.senderType);
    if (text) text.textContent = this.replyTo.body
      ? this.replyTo.body
      : (this.replyTo.hasImage ? 'تصویر' : '');
    this.replyBarEl.hidden = false;
  };

  ResturanyarSupportChat.prototype.bindUi = function () {
    var self = this;
    var fab = this.root.querySelector('.ry-sc-fab');
    var minBtn = this.root.querySelector('.ry-sc-minimize');
    var closeBtn = this.root.querySelector('.ry-sc-close');
    var sendBtn = this.root.querySelector('.ry-sc-send');
    var attachBtn = this.root.querySelector('.ry-sc-attach');
    var fileInput = this.root.querySelector('.ry-sc-file');

    if (fab) fab.addEventListener('click', function () { self.open(); });
    if (minBtn) minBtn.addEventListener('click', function () { self.minimize(); });
    if (closeBtn) closeBtn.addEventListener('click', function () { self.close(); });
    if (sendBtn) sendBtn.addEventListener('click', function () { self.sendText(); });
    if (attachBtn && fileInput) {
      attachBtn.addEventListener('click', function () { fileInput.click(); });
      fileInput.addEventListener('change', function () {
        if (fileInput.files && fileInput.files[0]) {
          self.sendImage(fileInput.files[0]);
          fileInput.value = '';
        }
      });
    }

    this.inputEl.addEventListener('keydown', function (e) {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        self.sendText();
      }
    });

    var replyClose = this.root.querySelector('.ry-sc-reply-bar-close');
    if (replyClose) {
      replyClose.addEventListener('click', function () { self.clearReplyTarget(); });
    }

    this.messagesEl.addEventListener('click', function (e) {
      var failed = e.target.closest('.ry-sc-status-text.failed');
      if (failed) {
        var clientId = failed.getAttribute('data-client-id');
        if (clientId && self.pending[clientId]) self.retry(clientId);
        return;
      }
      var quote = e.target.closest('.ry-sc-quote');
      if (quote) {
        var gotoId = quote.getAttribute('data-goto');
        var target = gotoId && self.messagesEl.querySelector('.ry-sc-bubble[data-id="' + gotoId + '"]');
        if (target) {
          target.scrollIntoView({ behavior: 'smooth', block: 'center' });
          target.classList.add('is-flash');
          setTimeout(function () { target.classList.remove('is-flash'); }, 1200);
        }
        return;
      }
      var replyBtn = e.target.closest('.ry-sc-reply-btn');
      if (!replyBtn) return;
      var bubble = replyBtn.closest('.ry-sc-bubble');
      if (!bubble || !bubble.getAttribute('data-id')) return;
      self.setReplyTarget({
        id: parseInt(bubble.getAttribute('data-id'), 10),
        senderType: parseInt(bubble.getAttribute('data-sender') || '0', 10),
        body: bubble.getAttribute('data-body') || '',
        imageUrl: bubble.getAttribute('data-has-image') === '1' ? '1' : ''
      });
    });

    document.querySelectorAll('#open_chat, #toggle_chat').forEach(function (el) {
      el.addEventListener('click', function (e) {
        e.preventDefault();
        if (el.id === 'toggle_chat' && self.panel.classList.contains('is-open')) self.minimize();
        else self.open();
      });
    });
    var closeChat = document.getElementById('close_chat');
    if (closeChat) closeChat.addEventListener('click', function (e) {
      e.preventDefault();
      self.close();
    });
  };

  ResturanyarSupportChat.prototype.saveState = function (state) {
    try { sessionStorage.setItem(STATE_KEY, state); } catch (e) { }
  };

  ResturanyarSupportChat.prototype.restoreState = function () {
    try {
      var state = sessionStorage.getItem(STATE_KEY);
      if (state === 'open') this._wantOpen = true;
      if (state === 'minimized') this._wantOpen = false;
    } catch (e) { }
  };

  ResturanyarSupportChat.prototype.updateStatus = function () {
    if (!this.statusEl) return;
    this.statusEl.classList.remove('is-online');
    var label = this.statusEl.querySelector('.ry-sc-status-label');
    if (label) {
      label.textContent = 'معمولاً ظرف حدود ۵ دقیقه پاسخ می‌دهیم';
    }
  };

  ResturanyarSupportChat.prototype.showLoading = function (message) {
    if (!this.loadingEl) return;
    if (this.loadingTextEl) {
      this.loadingTextEl.textContent = message || 'لطفاً کمی صبر کنید…';
    }
    this.loadingEl.hidden = false;
    if (this.panel) this.panel.classList.add('is-busy');
    if (this.inputEl) this.inputEl.disabled = true;
    var attach = this.root && this.root.querySelector('.ry-sc-attach');
    var send = this.root && this.root.querySelector('.ry-sc-send');
    if (attach) attach.disabled = true;
    if (send) send.disabled = true;
  };

  ResturanyarSupportChat.prototype.hideLoading = function () {
    if (!this.loadingEl) return;
    this.loadingEl.hidden = true;
    if (this.panel) this.panel.classList.remove('is-busy');
    if (this.inputEl) this.inputEl.disabled = false;
    var attach = this.root && this.root.querySelector('.ry-sc-attach');
    var send = this.root && this.root.querySelector('.ry-sc-send');
    if (attach) attach.disabled = false;
    if (send) send.disabled = false;
  };

  ResturanyarSupportChat.prototype.setBadge = function (n) {
    this.unreadCustomer = Math.max(0, n | 0);
    if (!this.badgeEl) return;
    if (this.unreadCustomer > 0 && !this.panel.classList.contains('is-open')) {
      this.badgeEl.style.display = 'inline-block';
      this.badgeEl.textContent = this.unreadCustomer > 99 ? '99+' : String(this.unreadCustomer);
    } else {
      this.badgeEl.style.display = 'none';
    }
  };

  ResturanyarSupportChat.prototype.open = async function () {
    var alreadyOpen = this.panel && this.panel.classList.contains('is-open');
    if (!alreadyOpen) {
      this.panel.classList.add('is-open');
      this.panel.classList.remove('is-minimized');
      if (this.root) this.root.classList.add('is-chat-open');
      document.documentElement.classList.add('ry-sc-noscroll');
      document.body.classList.add('ry-sc-noscroll');
      this.saveState('open');
      this.setBadge(0);
    }
    var needsLoad = !this.connection || !this.conversationId;
    if (needsLoad) this.showLoading('در حال آماده‌سازی گفتگو…');
    try {
      if (!this.connection) await this.connectHub();
      if (!this.conversationId) {
        await this.openConversation();
      }
    } finally {
      if (needsLoad) this.hideLoading();
    }
  };

  ResturanyarSupportChat.prototype.minimize = function () {
    this.panel.classList.remove('is-open');
    this.panel.classList.add('is-minimized');
    if (this.root) this.root.classList.remove('is-chat-open');
    document.documentElement.classList.remove('ry-sc-noscroll');
    document.body.classList.remove('ry-sc-noscroll');
    this.saveState('minimized');
  };

  ResturanyarSupportChat.prototype.close = function () {
    this.panel.classList.remove('is-open');
    this.panel.classList.remove('is-minimized');
    if (this.root) this.root.classList.remove('is-chat-open');
    document.documentElement.classList.remove('ry-sc-noscroll');
    document.body.classList.remove('ry-sc-noscroll');
    this.saveState('closed');

    // Embed (Android WebView): leave the host activity
    if (this.embed) {
      try {
        if (window.ResturanyarApp && typeof window.ResturanyarApp.close === 'function') {
          window.ResturanyarApp.close();
          return;
        }
      } catch (e) { }
      try {
        if (window.history.length > 1) {
          window.history.back();
          return;
        }
      } catch (e2) { }
    }
  };

  ResturanyarSupportChat.prototype.connectHub = async function () {
    if (this.connection) return this.connection;
    if (!window.signalR) {
      await this.loadScript('/lib/microsoft-signalr/signalr.min.js');
    }
    var self = this;
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    this.connection.on('SupportStatusChanged', function () {
      // Presence is tracked server-side for SMS; do not show online/offline to customers.
      self.updateStatus();
    });

    this.connection.on('ConversationOpened', function (payload) {
      if (!payload || !payload.conversation) return;
      var convId = payload.conversation.id;
      var msgs = payload.conversation.messages || [];
      // Ignore duplicate ConversationOpened for same thread (prevents history flash)
      if (self.conversationId === convId && self._historyKey === convId + ':' + msgs.length) {
        self.updateStatus();
        return;
      }
      self.conversationId = convId;
      self.updateStatus();
      self._historyKey = convId + ':' + msgs.length;
      self.renderHistory(msgs);
    });

    this.connection.on('ReceiveMessage', function (msg) {
      self.onReceive(msg);
    });

    this.connection.onreconnected(async function () {
      try {
        var ctx = readBodyContext();
        await self.connection.invoke('JoinCustomer', ctx.restaurantId ? null : getGuestKey(), ctx.restaurantId || null);
        if (self.conversationId) {
          await self.openConversation();
        }
      } catch (e) { }
    });

    await this.connection.start();
    var ctx = readBodyContext();
    await this.connection.invoke('JoinCustomer', ctx.restaurantId ? null : getGuestKey(), ctx.restaurantId || null);
    return this.connection;
  };

  ResturanyarSupportChat.prototype.openConversation = async function () {
    if (!this.connection) return;
    if (this._openConvPromise) return this._openConvPromise;
    var self = this;
    var ctx = readBodyContext();
    var request = {
      guestKey: ctx.restaurantId ? null : getGuestKey(),
      restaurantId: ctx.restaurantId,
      ownerId: ctx.ownerId,
      restaurantName: ctx.restaurantName,
      ownerName: ctx.ownerName,
      ownerPhone: ctx.ownerPhone,
      pageUrl: ctx.pageUrl,
      userAgent: ctx.userAgent
    };
    this._openConvPromise = this.connection.invoke('OpenConversation', request)
      .catch(function (e) { throw e; })
      .finally(function () { self._openConvPromise = null; });
    return this._openConvPromise;
  };

  ResturanyarSupportChat.prototype.renderHistory = function (messages) {
    this.messagesEl.innerHTML = '';
    if (!messages.length) {
      this.messagesEl.innerHTML =
        '<div class="ry-sc-empty">' +
        '<div class="ry-sc-empty-icon"><i class="fas fa-comments"></i></div>' +
        '<strong>سلام، خوش آمدید</strong>' +
        '<p>پیام خود را بنویسید؛ معمولاً ظرف حدود ۵ دقیقه پاسخ می‌دهیم.</p>' +
        '</div>';
      return;
    }
    var self = this;
    messages.forEach(function (m) { self.appendMessage(m, 'sent', false); });
    this.scrollBottom();
  };

  ResturanyarSupportChat.prototype.appendMessage = function (msg, status, scroll) {
    var empty = this.messagesEl.querySelector('.ry-sc-empty');
    if (empty) empty.remove();

    var mine = msg.senderType === 0;
    var el = document.createElement('div');
    el.className = 'ry-sc-bubble ' + (mine ? 'mine' : 'theirs');
    if (msg.clientMessageId) el.setAttribute('data-client-id', msg.clientMessageId);
    if (msg.id) el.setAttribute('data-id', msg.id);
    el.setAttribute('data-sender', String(msg.senderType == null ? 0 : msg.senderType));
    el.setAttribute('data-body', (msg.body || '').slice(0, 200));
    el.setAttribute('data-has-image', msg.imageUrl ? '1' : '0');

    var html = '';
    if (msg.replyToMessageId) {
      var who = this.replyWhoLabel(msg.replyToSenderType);
      var preview = msg.replyToBody
        ? escapeHtml(msg.replyToBody)
        : (msg.replyToHasImage ? 'تصویر' : '');
      html += '<button type="button" class="ry-sc-quote" data-goto="' + msg.replyToMessageId + '">' +
        '<strong>' + escapeHtml(who) + '</strong><span>' + preview + '</span></button>';
    }
    if (msg.imageUrl && isAllowedSupportImageUrl(msg.imageUrl)) {
      var safeUrl = escapeHtml(msg.imageUrl);
      html += '<a href="' + safeUrl + '" target="_blank" rel="noopener"><img src="' + safeUrl + '" alt="تصویر" /></a>';
    }
    if (msg.body) html += '<div>' + escapeHtml(msg.body) + '</div>';
    html += '<div class="ry-sc-meta"><span>' + formatTime(msg.createdAtUtc) + '</span>';
    if (mine) {
      html += '<span class="ry-sc-status-text' + (status === 'failed' ? ' failed' : '') + '"' +
        (msg.clientMessageId ? ' data-client-id="' + msg.clientMessageId + '"' : '') + '>' +
        statusLabel(status) + '</span>';
    }
    if (msg.id) {
      html += '<button type="button" class="ry-sc-reply-btn" title="پاسخ" aria-label="پاسخ"><i class="fas fa-reply"></i></button>';
    }
    html += '</div>';
    el.innerHTML = html;
    this.messagesEl.appendChild(el);
    if (scroll !== false) this.scrollBottom();
    return el;
  };

  ResturanyarSupportChat.prototype.updateLocalStatus = function (clientMessageId, status) {
    var el = this.messagesEl.querySelector('[data-client-id="' + clientMessageId + '"] .ry-sc-status-text');
    if (!el) {
      el = this.messagesEl.querySelector('.ry-sc-bubble[data-client-id="' + clientMessageId + '"] .ry-sc-status-text');
    }
    if (!el) return;
    el.textContent = statusLabel(status);
    el.classList.toggle('failed', status === 'failed');
  };

  ResturanyarSupportChat.prototype.playNotifySound = function () {
    try {
      if (!this._notifyAudio) {
        this._notifyAudio = new Audio('/sounds/support-notify.mp3');
        this._notifyAudio.preload = 'auto';
        this._notifyAudio.volume = 0.55;
      }
      this._notifyAudio.currentTime = 0;
      var playPromise = this._notifyAudio.play();
      if (playPromise && typeof playPromise.catch === 'function') {
        playPromise.catch(function () {
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
  };

  ResturanyarSupportChat.prototype.onReceive = function (msg) {
    if (!msg) return;
    if (msg.clientMessageId && this.pending[msg.clientMessageId]) {
      delete this.pending[msg.clientMessageId];
      this.updateLocalStatus(msg.clientMessageId, 'sent');
      var bubble = this.messagesEl.querySelector('.ry-sc-bubble[data-client-id="' + msg.clientMessageId + '"]');
      if (bubble) {
        if (msg.id) {
          bubble.setAttribute('data-id', msg.id);
          var meta = bubble.querySelector('.ry-sc-meta');
          if (meta && !meta.querySelector('.ry-sc-reply-btn')) {
            meta.insertAdjacentHTML('beforeend',
              '<button type="button" class="ry-sc-reply-btn" title="پاسخ" aria-label="پاسخ"><i class="fas fa-reply"></i></button>');
          }
        }
        return;
      }
    }

    var exists = msg.id && this.messagesEl.querySelector('.ry-sc-bubble[data-id="' + msg.id + '"]');
    if (exists) return;

    var el = this.appendMessage(msg, 'sent', true);
    if (msg.id) el.setAttribute('data-id', msg.id);

    // Support reply → notify only when chat is closed/minimized
    if (msg.senderType === 1) {
      var chatOpen = this.panel && this.panel.classList.contains('is-open');
      if (!chatOpen) {
        this.playNotifySound();
        this.setBadge(this.unreadCustomer + 1);
      }
    }
  };

  ResturanyarSupportChat.prototype.sendText = async function () {
    var text = (this.inputEl.value || '').trim();
    if (!text) return;
    this.inputEl.value = '';
    await this.sendPayload({ body: text });
  };

  ResturanyarSupportChat.prototype.sendImage = async function (file) {
    this.showLoading('در حال آماده‌سازی و ارسال تصویر…');
    try {
      var prepare = window.__ryPrepareSupportImage;
      var ready = prepare ? await prepare(file) : file;
      var form = new FormData();
      // Third arg forces a filename even if ready is a Blob without File.name
      form.append('file', ready, (ready && ready.name) ? ready.name : ('support-' + Date.now() + '.jpg'));
      var res = await fetch(apiBase + '/upload', { method: 'POST', body: form, credentials: 'same-origin' });
      var json = await res.json();
      if (!json.success) throw new Error(json.message || 'upload failed');
      var caption = (this.inputEl.value || '').trim();
      this.inputEl.value = '';
      this.hideLoading();
      await this.sendPayload({ body: caption || null, imageUrl: json.data.imageUrl });
    } catch (e) {
      this.hideLoading();
      alert('ارسال تصویر ناموفق بود.');
    }
  };

  ResturanyarSupportChat.prototype.sendPayload = async function (payload) {
    if (!this.connection) await this.connectHub();
    if (!this.conversationId) await this.openConversation();

    var ctx = readBodyContext();
    var clientMessageId = uuid();
    var replyTo = this.replyTo;
    var optimistic = {
      id: null,
      conversationId: this.conversationId,
      senderType: 0,
      body: payload.body || null,
      imageUrl: payload.imageUrl || null,
      clientMessageId: clientMessageId,
      createdAtUtc: new Date().toISOString(),
      replyToMessageId: replyTo ? replyTo.id : null,
      replyToSenderType: replyTo ? replyTo.senderType : null,
      replyToBody: replyTo ? (replyTo.body || '').slice(0, 120) : null,
      replyToHasImage: !!(replyTo && (replyTo.hasImage || replyTo.imageUrl))
    };
    this.appendMessage(optimistic, 'sending', true);
    this.pending[clientMessageId] = Object.assign({}, payload, {
      clientMessageId: clientMessageId,
      replyToMessageId: replyTo ? replyTo.id : null
    });
    this.clearReplyTarget();

    var request = {
      conversationId: this.conversationId,
      body: payload.body || null,
      imageUrl: payload.imageUrl || null,
      clientMessageId: clientMessageId,
      replyToMessageId: replyTo ? replyTo.id : null,
      guestKey: ctx.restaurantId ? null : getGuestKey(),
      restaurantId: ctx.restaurantId,
      ownerId: ctx.ownerId,
      restaurantName: ctx.restaurantName,
      ownerName: ctx.ownerName,
      ownerPhone: ctx.ownerPhone,
      pageUrl: ctx.pageUrl,
      userAgent: ctx.userAgent
    };

    try {
      await this.connection.invoke('SendCustomerMessage', request);
      this.updateLocalStatus(clientMessageId, 'sent');
    } catch (e) {
      this.updateLocalStatus(clientMessageId, 'failed');
    }
  };

  ResturanyarSupportChat.prototype.retry = async function (clientMessageId) {
    var payload = this.pending[clientMessageId];
    if (!payload) return;
    this.updateLocalStatus(clientMessageId, 'sending');
    var ctx = readBodyContext();
    try {
      await this.connection.invoke('SendCustomerMessage', {
        conversationId: this.conversationId,
        body: payload.body || null,
        imageUrl: payload.imageUrl || null,
        clientMessageId: clientMessageId,
        replyToMessageId: payload.replyToMessageId || null,
        guestKey: ctx.restaurantId ? null : getGuestKey(),
        restaurantId: ctx.restaurantId,
        ownerId: ctx.ownerId,
        restaurantName: ctx.restaurantName,
        ownerName: ctx.ownerName,
        ownerPhone: ctx.ownerPhone,
        pageUrl: ctx.pageUrl,
        userAgent: ctx.userAgent
      });
      this.updateLocalStatus(clientMessageId, 'sent');
      delete this.pending[clientMessageId];
    } catch (e) {
      this.updateLocalStatus(clientMessageId, 'failed');
    }
  };

  ResturanyarSupportChat.prototype.scrollBottom = function () {
    this.messagesEl.scrollTop = this.messagesEl.scrollHeight;
  };

  ResturanyarSupportChat.prototype.loadScript = function (src) {
    return new Promise(function (resolve, reject) {
      var s = document.createElement('script');
      s.src = src;
      s.async = true;
      s.onload = resolve;
      s.onerror = reject;
      document.head.appendChild(s);
    });
  };

  function escapeHtml(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');
  }

  /** Same-origin support uploads only — blocks XSS via crafted imageUrl. */
  function isAllowedSupportImageUrl(url) {
    if (!url || typeof url !== 'string') return false;
    var u = url.trim();
    if (!u || u.length > 500) return false;
    if (u.indexOf('..') >= 0) return false;
    if (/[\s"'<>\\]/.test(u)) return false;
    if (u.indexOf(':') >= 0) return false;
    return /^\/uploads\/support\//i.test(u);
  }

  function ensureCss() {
    if (document.querySelector('link[data-ry-support-css]')) return;
    var link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = '/css/support-chat.css';
    link.setAttribute('data-ry-support-css', '1');
    document.head.appendChild(link);
    if (!document.querySelector('link[href*="font-awesome"], link[href*="fontawesome"]')) {
      var fa = document.createElement('link');
      fa.rel = 'stylesheet';
      fa.href = 'https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.0/css/all.min.css';
      document.head.appendChild(fa);
    }
  }

  window.ResturanyarSupportChat = ResturanyarSupportChat;
  window.__ryStartSupportChat = function (opts) {
    if (window.__rySupportChatInstance) {
      return window.__rySupportChatInstance;
    }
    ensureCss();
    var chat = new ResturanyarSupportChat(opts || {});
    window.__rySupportChatInstance = chat;
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', function () { chat.init(); });
    } else {
      chat.init();
    }
    return chat;
  };
})(window, document);
