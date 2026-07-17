import { fetchWithAuth } from '/js/authentication/apiinterceptor.js';

const API_BASE = '/api/v2/UserApi/messages';

function getRestaurantId() {
    const fromBody = document.body?.dataset?.restaurantId;
    const fromFrame = document.querySelector('.dashboard-frame')?.dataset?.restaurantId;
    const fromPage = document.querySelector('.messages-page')?.dataset?.restaurantId;
    const id = fromBody || fromFrame || fromPage;
    return id ? parseInt(id, 10) : 0;
}

function updateBadge(count) {
    const badges = [
        document.getElementById('ownerMessageBadge'),
        document.getElementById('navMessagesBadge')
    ];

    badges.forEach(badge => {
        if (!badge) return;
        if (count > 0) {
            const label = count > 99 ? '99+' : String(count);
            badge.textContent = label;
            badge.classList.toggle('is-single-digit', count < 10);
            badge.hidden = false;
            badge.style.display = '';
        } else {
            badge.hidden = true;
            badge.style.display = 'none';
            badge.classList.remove('is-single-digit');
        }
    });
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

function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text ?? '';
    return div.innerHTML;
}

async function refreshBadge(restaurantId) {
    const id = restaurantId || getRestaurantId();
    if (!id) return 0;
    const count = await fetchUnreadCount(id);
    updateBadge(count);
    return count;
}

function navigateToMessagesPage() {
    const url = '/Home/Messages';
    const navItem = document.querySelector('.sidebar-nav a[href*="Messages"]');
    if (navItem) {
        navItem.click();
        return;
    }
    window.location.href = url;
}

function ensureUnreadModal() {
    let modal = document.getElementById('ownerUnreadModal');
    if (modal) return modal;

    modal = document.createElement('div');
    modal.id = 'ownerUnreadModal';
    modal.className = 'custom-modal-overlay owner-unread-modal-overlay';
    modal.innerHTML = `
        <div class="custom-modal-card owner-unread-modal">
            <div class="owner-unread-modal__header">
                <div>
                    <h3 class="owner-unread-modal__title">پیام‌های خوانده‌نشده</h3>
                    <p class="owner-unread-modal__subtitle">پیام‌های جدید دریافتی از پشتیبانی</p>
                </div>
                <button type="button" id="ownerUnreadModalClose" class="owner-unread-modal__close" aria-label="بستن">&times;</button>
            </div>
            <div id="ownerUnreadModalList" class="owner-unread-modal__list"></div>
            <div class="owner-unread-modal__footer">
                <button type="button" id="ownerUnreadModalViewAll" class="btn btn-primary btn-sm">
                    <i class="fas fa-envelope me-1"></i>
                    مشاهده همه پیام‌ها
                </button>
            </div>
        </div>
    `;
    document.body.appendChild(modal);

    document.getElementById('ownerUnreadModalClose').addEventListener('click', () => {
        modal.classList.remove('active');
    });

    modal.addEventListener('click', (e) => {
        if (e.target === modal) modal.classList.remove('active');
    });

    document.getElementById('ownerUnreadModalViewAll').addEventListener('click', () => {
        modal.classList.remove('active');
        navigateToMessagesPage();
    });

    return modal;
}

async function renderUnreadModal(restaurantId) {
    const modal = ensureUnreadModal();
    const list = document.getElementById('ownerUnreadModalList');
    list.innerHTML = '<p class="owner-unread-modal__loading">در حال بارگذاری...</p>';
    modal.classList.add('active');

    const messages = await fetchMessages(restaurantId, true);
    if (!messages.length) {
        modal.classList.remove('active');
        navigateToMessagesPage();
        return;
    }

    list.innerHTML = messages.map(m => `
        <div class="owner-unread-item" data-id="${m.id}">
            <div class="owner-unread-item__header">
                <strong class="owner-unread-item__title">${escapeHtml(m.title)}</strong>
                <span class="owner-unread-item__date">${formatDate(m.createdAt)}</span>
            </div>
            <p class="owner-unread-item__body">${escapeHtml(m.body)}</p>
            <button type="button" class="owner-unread-item__read-btn" data-id="${m.id}">
                <i class="fas fa-check me-1"></i>خوانده شد
            </button>
        </div>
    `).join('');

    list.querySelectorAll('.owner-unread-item__read-btn').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const messageId = parseInt(btn.dataset.id, 10);
            btn.disabled = true;
            await markRead(restaurantId, messageId);
            await refreshBadge(restaurantId);

            const item = btn.closest('.owner-unread-item');
            item?.remove();

            const remaining = list.querySelectorAll('.owner-unread-item').length;
            if (remaining === 0) {
                modal.classList.remove('active');
                list.innerHTML = '<p class="owner-unread-modal__empty">همه پیام‌ها خوانده شدند.</p>';
            }
        });
    });
}

async function handleBadgeClick(restaurantId) {
    const count = await refreshBadge(restaurantId);
    if (count > 0) {
        await renderUnreadModal(restaurantId);
    } else {
        navigateToMessagesPage();
    }
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

    document.getElementById('ownerMessagePopupAll').onclick = () => {
        closePopup();
        navigateToMessagesPage();
    };

    modal.classList.add('active');
}

function wireBadgeClickHandlers(restaurantId) {
    const headerBadge = document.getElementById('ownerMessageBadge');
    if (headerBadge) {
        headerBadge.style.cursor = 'pointer';
        headerBadge.addEventListener('click', (e) => {
            e.stopPropagation();
            handleBadgeClick(restaurantId);
        });
    }

    const sidebarBadge = document.getElementById('navMessagesBadge');
    if (sidebarBadge) {
        sidebarBadge.style.cursor = 'pointer';
        sidebarBadge.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            handleBadgeClick(restaurantId);
        });
    }

    const btn = document.getElementById('ownerMessageBtn');
    if (btn) {
        btn.addEventListener('click', (e) => {
            if (e.target.closest('#ownerMessageBadge')) return;
            navigateToMessagesPage();
        });
    }
}

document.addEventListener('DOMContentLoaded', async () => {
    const restaurantId = getRestaurantId();
    if (!restaurantId) return;

    await refreshBadge(restaurantId);
    wireBadgeClickHandlers(restaurantId);
    await showUnreadPopupIfNeeded(restaurantId);
});

window.refreshOwnerMessageBadge = refreshBadge;

export { refreshBadge, showUnreadPopupIfNeeded, navigateToMessagesPage };
