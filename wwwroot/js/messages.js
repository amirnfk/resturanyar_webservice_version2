(function () {
    'use strict';

    var API_BASE = '/api/v2/UserApi/messages';
    var messagesPageId = 0;
    var currentFilter = 'all';
    var restaurantId = 0;
    var allMessages = [];
    var selectedMessage = null;

    function isPageCurrent(id) {
        return id === messagesPageId;
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text ?? '';
        return div.innerHTML;
    }

    function formatDate(dateStr) {
        try {
            return new Date(dateStr).toLocaleString('fa-IR', { dateStyle: 'short', timeStyle: 'short' });
        } catch (e) {
            return dateStr;
        }
    }

    function getTypeLabel(messageType) {
        return messageType === 1 ? 'اختصاصی' : 'عمومی';
    }

    function getTypeClass(messageType) {
        return messageType === 1 ? 'is-private' : 'is-public';
    }

    async function fetchMessages(unreadOnly) {
        var url = unreadOnly
            ? API_BASE + '/unread?restaurantId=' + restaurantId
            : API_BASE + '?restaurantId=' + restaurantId;
        var res = await window.fetchWithAuth(url);
        if (!res.ok) return [];
        var data = await res.json();
        return data.success ? (data.messages ?? []) : [];
    }

    async function markRead(messageId) {
        var res = await window.fetchWithAuth(API_BASE + '/mark-read', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ restaurantId: restaurantId, messageId: messageId })
        });
        return res.ok;
    }

    async function refreshBadges() {
        if (typeof window.refreshOwnerMessageBadge === 'function') {
            await window.refreshOwnerMessageBadge(restaurantId);
        }
    }

    function getFilteredMessages() {
        if (currentFilter === 'unread') {
            return allMessages.filter(function (m) { return !m.isRead; });
        }
        return allMessages;
    }

    function updateHeaderStats() {
        var unreadCount = allMessages.filter(function (m) { return !m.isRead; }).length;
        var statEl = document.getElementById('messagesUnreadStat');
        var countEl = document.getElementById('messagesUnreadCount');
        var filterBadge = document.getElementById('messagesFilterBadge');
        var markAllBtn = document.getElementById('btnMarkAllRead');

        if (countEl) countEl.textContent = unreadCount;

        if (statEl) statEl.classList.toggle('d-none', unreadCount === 0);
        if (markAllBtn) markAllBtn.classList.toggle('d-none', unreadCount === 0);

        if (filterBadge) {
            if (unreadCount > 0) {
                filterBadge.textContent = unreadCount > 99 ? '99+' : unreadCount;
                filterBadge.classList.remove('d-none');
            } else {
                filterBadge.classList.add('d-none');
            }
        }
    }

    function showLoading(show) {
        document.getElementById('messagesLoading')?.classList.toggle('d-none', !show);
    }

    function showEmpty(show, title, desc) {
        var emptyEl = document.getElementById('messagesEmpty');
        if (!emptyEl) return;
        emptyEl.classList.toggle('d-none', !show);
        if (title) document.getElementById('messagesEmptyTitle').textContent = title;
        if (desc) document.getElementById('messagesEmptyDesc').textContent = desc;
    }

    function renderMessageList(pageId) {
        var listEl = document.getElementById('messagesList');
        if (!listEl || !isPageCurrent(pageId)) return;

        var messages = getFilteredMessages();
        updateHeaderStats();

        if (!messages.length) {
            listEl.classList.add('d-none');
            var emptyTitle = currentFilter === 'unread' ? 'پیام خوانده‌نشده‌ای ندارید' : 'پیامی وجود ندارد';
            var emptyDesc = currentFilter === 'unread'
                ? 'همه پیام‌های شما خوانده شده‌اند.'
                : 'وقتی پیام جدیدی دریافت کنید، اینجا نمایش داده می‌شود.';
            showEmpty(true, emptyTitle, emptyDesc);
            return;
        }

        showEmpty(false);
        listEl.classList.remove('d-none');

        listEl.innerHTML = messages.map(function (m) {
            return (
                '<article class="message-card' + (m.isRead ? '' : ' is-unread') + '" data-id="' + m.id + '">' +
                    '<div class="message-card__header">' +
                        '<div class="message-card__title-wrap">' +
                            (!m.isRead ? '<span class="message-card__unread-dot"></span>' : '') +
                            '<h5 class="message-card__title">' + escapeHtml(m.title) + '</h5>' +
                        '</div>' +
                        '<span class="message-card__date">' + formatDate(m.createdAt) + '</span>' +
                    '</div>' +
                    '<p class="message-card__preview">' + escapeHtml(m.body) + '</p>' +
                    '<div class="message-card__footer">' +
                        '<span class="message-type-chip ' + getTypeClass(m.messageType) + '">' + getTypeLabel(m.messageType) + '</span>' +
                        (!m.isRead
                            ? '<button type="button" class="message-card__read-btn" data-id="' + m.id + '">علامت به عنوان خوانده‌شده</button>'
                            : '') +
                    '</div>' +
                '</article>'
            );
        }).join('');

        listEl.querySelectorAll('.message-card').forEach(function (card) {
            card.addEventListener('click', function (e) {
                if (e.target.closest('.message-card__read-btn')) return;
                var id = parseInt(card.dataset.id, 10);
                var msg = allMessages.find(function (m) { return m.id === id; });
                if (msg) openDetailModal(msg);
            });
        });

        listEl.querySelectorAll('.message-card__read-btn').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                var id = parseInt(btn.dataset.id, 10);
                handleMarkRead(id, pageId);
            });
        });
    }

    function openDetailModal(message) {
        selectedMessage = message;
        var modal = document.getElementById('messageDetailModal');
        if (!modal) return;

        document.getElementById('messageDetailTitle').textContent = message.title;
        document.getElementById('messageDetailBody').textContent = message.body;
        document.getElementById('messageDetailDate').textContent = formatDate(message.createdAt);

        var typeEl = document.getElementById('messageDetailType');
        typeEl.textContent = getTypeLabel(message.messageType);
        typeEl.className = 'message-type-chip ' + getTypeClass(message.messageType);

        var markBtn = document.getElementById('messageDetailMarkRead');
        markBtn.classList.toggle('d-none', message.isRead);

        modal.classList.add('active');
    }

    function closeDetailModal() {
        document.getElementById('messageDetailModal')?.classList.remove('active');
        selectedMessage = null;
    }

    async function handleMarkRead(messageId, pageId) {
        var ok = await markRead(messageId);
        if (!ok || !isPageCurrent(pageId)) return;

        allMessages = allMessages.map(function (m) {
            if (m.id === messageId) return Object.assign({}, m, { isRead: true });
            return m;
        });

        if (selectedMessage && selectedMessage.id === messageId) {
            selectedMessage.isRead = true;
            document.getElementById('messageDetailMarkRead')?.classList.add('d-none');
        }

        await refreshBadges();
        renderMessageList(pageId);

        if (typeof showToast === 'function') {
            showToast('پیام به عنوان خوانده‌شده علامت‌گذاری شد', 'success');
        }
    }

    async function handleMarkAllRead(pageId) {
        var unread = allMessages.filter(function (m) { return !m.isRead; });
        if (!unread.length) return;

        for (var i = 0; i < unread.length; i++) {
            if (!isPageCurrent(pageId)) return;
            await markRead(unread[i].id);
        }

        allMessages = allMessages.map(function (m) {
            return Object.assign({}, m, { isRead: true });
        });

        await refreshBadges();
        renderMessageList(pageId);

        if (typeof showToast === 'function') {
            showToast('همه پیام‌ها خوانده شدند', 'success');
        }
    }

    async function loadMessages() {
        var pageId = messagesPageId;
        showLoading(true);
        showEmpty(false);
        document.getElementById('messagesList')?.classList.add('d-none');

        try {
            allMessages = await fetchMessages(false);
            if (!isPageCurrent(pageId)) return;
            renderMessageList(pageId);
        } catch (e) {
            if (isPageCurrent(pageId) && typeof showToast === 'function') {
                showToast('خطا در دریافت پیام‌ها', 'error');
            }
        } finally {
            if (isPageCurrent(pageId)) showLoading(false);
        }
    }

    function bindEvents() {
        document.querySelectorAll('.messages-filter-tab').forEach(function (tab) {
            tab.onclick = function () {
                document.querySelectorAll('.messages-filter-tab').forEach(function (t) {
                    t.classList.remove('active');
                });
                tab.classList.add('active');
                currentFilter = tab.dataset.filter || 'all';
                renderMessageList(messagesPageId);
            };
        });

        var markAllBtn = document.getElementById('btnMarkAllRead');
        if (markAllBtn) {
            markAllBtn.onclick = function () {
                handleMarkAllRead(messagesPageId);
            };
        }

        var detailClose = document.getElementById('messageDetailClose');
        var detailCloseBtn = document.getElementById('messageDetailCloseBtn');
        var detailModal = document.getElementById('messageDetailModal');
        var detailMarkRead = document.getElementById('messageDetailMarkRead');

        if (detailClose) detailClose.onclick = closeDetailModal;
        if (detailCloseBtn) detailCloseBtn.onclick = closeDetailModal;

        if (detailMarkRead) {
            detailMarkRead.onclick = function () {
                if (selectedMessage) {
                    handleMarkRead(selectedMessage.id, messagesPageId);
                }
            };
        }

        if (detailModal) {
            detailModal.onclick = function (e) {
                if (e.target === detailModal) closeDetailModal();
            };
        }
    }

    function initMessagesPage() {
        var pageEl = document.querySelector('.messages-page');
        if (!pageEl) return;

        restaurantId = parseInt(pageEl.dataset.restaurantId, 10) || 0;
        if (!restaurantId) return;

        messagesPageId++;
        currentFilter = 'all';
        allMessages = [];
        selectedMessage = null;

        document.querySelectorAll('.messages-filter-tab').forEach(function (tab) {
            tab.classList.toggle('active', tab.dataset.filter === 'all');
        });

        bindEvents();
        loadMessages();
    }

    window.initMessagesPage = initMessagesPage;
})();
