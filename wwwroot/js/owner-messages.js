import { fetchWithAuth } from '/js/authentication/apiinterceptor.js';

const API_BASE = '/api/v2/UserApi/messages';

function getRestaurantId() {
    const fromBody = document.body?.dataset?.restaurantId;
    const fromFrame = document.querySelector('.dashboard-frame')?.dataset?.restaurantId;
    const id = fromBody || fromFrame;
    return id ? parseInt(id, 10) : 0;
}

function updateBadge(count) {
    const badge = document.getElementById('ownerMessageBadge');
    if (!badge) return;
    if (count > 0) {
        badge.textContent = count > 99 ? '99+' : count;
        badge.hidden = false;
        badge.style.display = '';
    } else {
        badge.hidden = true;
        badge.style.display = 'none';
    }
}

async function fetchUnreadCount(restaurantId) {
    const res = await fetchWithAuth(`${API_BASE}/unread-count?restaurantId=${restaurantId}`);
    if (!res.ok) return 0;
    const data = await res.json();
    return data.success ? (data.count ?? 0) : 0;
}

async function fetchMessages(restaurantId, unreadOnly = false) {
    const url = unreadOnly
        ? `${API_BASE}/unread?restaurantId=${restaurantId}`
        : `${API_BASE}?restaurantId=${restaurantId}`;
    const res = await fetchWithAuth(url);
    if (!res.ok) return [];
    const data = await res.json();
    return data.success ? (data.messages ?? []) : [];
}

async function markRead(restaurantId, messageId) {
    const res = await fetchWithAuth(`${API_BASE}/mark-read`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ restaurantId, messageId })
    });
    return res.ok;
}

function formatDate(dateStr) {
    try {
        return new Date(dateStr).toLocaleString('fa-IR', { dateStyle: 'short', timeStyle: 'short' });
    } catch {
        return dateStr;
    }
}

function ensurePanel() {
    let panel = document.getElementById('ownerMessagePanel');
    if (panel) return panel;

    panel = document.createElement('div');
    panel.id = 'ownerMessagePanel';
    panel.className = 'owner-msg-panel';
    panel.innerHTML = `
        <div class="owner-msg-panel__header">
            <strong>پیام‌ها</strong>
            <button type="button" id="ownerMessagePanelClose" class="owner-msg-panel__close">&times;</button>
        </div>
        <div id="ownerMessagePanelList" class="owner-msg-panel__list"></div>
    `;
    document.body.appendChild(panel);

    document.getElementById('ownerMessagePanelClose').addEventListener('click', () => {
        panel.classList.remove('active');
    });

    document.addEventListener('click', (e) => {
        if (!panel.classList.contains('active')) return;
        if (!panel.contains(e.target) && !document.getElementById('ownerMessageBtn')?.contains(e.target)) {
            panel.classList.remove('active');
        }
    });

    return panel;
}

async function renderMessageList(restaurantId) {
    const panel = ensurePanel();
    const list = document.getElementById('ownerMessagePanelList');
    list.innerHTML = '<p class="owner-msg-panel__loading">در حال بارگذاری...</p>';

    const messages = await fetchMessages(restaurantId);
    if (!messages.length) {
        list.innerHTML = '<p class="owner-msg-panel__empty">پیامی وجود ندارد.</p>';
        panel.classList.add('active');
        return;
    }

    list.innerHTML = messages.map(m => `
        <div class="owner-msg-item ${m.isRead ? 'is-read' : 'is-unread'}" data-id="${m.id}">
            <div class="owner-msg-item__title">${escapeHtml(m.title)}</div>
            <div class="owner-msg-item__date">${formatDate(m.createdAt)}</div>
            <div class="owner-msg-item__body">${escapeHtml(m.body)}</div>
            ${!m.isRead ? `<button type="button" class="owner-msg-item__read-btn" data-id="${m.id}">علامت به عنوان خوانده‌شده</button>` : ''}
        </div>
    `).join('');

    list.querySelectorAll('.owner-msg-item__read-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const messageId = parseInt(btn.dataset.id, 10);
            await markRead(restaurantId, messageId);
            await refreshBadge(restaurantId);
            await renderMessageList(restaurantId);
        });
    });

    panel.classList.add('active');
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text ?? '';
    return div.innerHTML;
}

async function refreshBadge(restaurantId) {
    const count = await fetchUnreadCount(restaurantId);
    updateBadge(count);
    return count;
}

function ensurePopupModal() {
    let modal = document.getElementById('ownerMessagePopup');
    if (modal) return modal;

    modal = document.createElement('div');
    modal.id = 'ownerMessagePopup';
    modal.className = 'custom-modal-overlay';
    modal.innerHTML = `
        <div class="custom-modal-card owner-msg-popup-card">
            <div class="custom-modal-icon"><i class="fa-solid fa-envelope"></i></div>
            <h3 id="ownerMessagePopupTitle" class="custom-modal-title"></h3>
            <p id="ownerMessagePopupBody" class="custom-modal-desc owner-msg-popup-body"></p>
            <div class="custom-modal-actions">
                <button type="button" id="ownerMessagePopupAll" class="custom-modal-btn btn-cancel">مشاهده همه</button>
                <button type="button" id="ownerMessagePopupOk" class="custom-modal-btn btn-exit">متوجه شدم</button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
    return modal;
}

async function showUnreadPopupIfNeeded(restaurantId) {
    if (!document.querySelector('.dashboard-frame')) return;

    const unread = await fetchMessages(restaurantId, true);
    if (!unread.length) return;

    const latest = unread[0];
    const modal = ensurePopupModal();
    document.getElementById('ownerMessagePopupTitle').textContent = latest.title;
    document.getElementById('ownerMessagePopupBody').textContent = latest.body;

    const closePopup = () => modal.classList.remove('active');

    document.getElementById('ownerMessagePopupOk').onclick = async () => {
        await markRead(restaurantId, latest.id);
        await refreshBadge(restaurantId);
        closePopup();
    };

    document.getElementById('ownerMessagePopupAll').onclick = async () => {
        closePopup();
        await renderMessageList(restaurantId);
    };

    modal.classList.add('active');
}

document.addEventListener('DOMContentLoaded', async () => {
    const restaurantId = getRestaurantId();
    if (!restaurantId) return;

    await refreshBadge(restaurantId);

    const btn = document.getElementById('ownerMessageBtn');
    if (btn) {
        btn.addEventListener('click', () => renderMessageList(restaurantId));
    }

    await showUnreadPopupIfNeeded(restaurantId);
});

export { refreshBadge, renderMessageList, showUnreadPopupIfNeeded };
