
let isRefreshing = false;
let failedQueue = [];

const processQueue = (error, token = null) => {
    failedQueue.forEach(prom => {
        if (error) prom.reject(error);
        else prom.resolve(token);
    });
    failedQueue = [];
};

const refreshAccessToken = async () => {
    const refreshToken = localStorage.getItem('refreshToken');
    const phone = localStorage.getItem('phone');
    if (!refreshToken || !phone) throw new Error('No refresh token');

    const response = await fetch('/api/v2/UserApi/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ phoneNumber: phone, refreshToken })
    });

    if (!response.ok) throw new Error('Refresh failed');
    const data = await response.json();

    if (data.success) {
        // ✅ Update both tokens in localStorage
        localStorage.setItem('accessToken', data.token);
        localStorage.setItem('refreshToken', data.refreshToken);
        return data.token;
    } else {
        throw new Error('Refresh invalid');
    }
};

const logoutAndRedirect = () => {
    // Clear storage
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('phone');

    // Optional: invalidate server-side session
    fetch('/api/v2/UserApi/logout', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(localStorage.getItem('refreshToken')) // pass the token before clearing
    }).catch(() => { });

    window.location.href = '/Home/ManagerLogin';
};

export const fetchWithAuth = async (url, options = {}) => {
    // 1. Inject the Authorization header
    const accessToken = localStorage.getItem('accessToken');
    const headers = {
        ...options.headers,
        'Authorization': `Bearer ${accessToken}`
    };

    // Remove Content-Type if FormData is used (let browser set it)
    if (options.body && options.body instanceof FormData) {
        delete headers['Content-Type'];
    }

    const requestOptions = { ...options, headers };

    try {
        let response = await fetch(url, requestOptions);

        // If 401, try to refresh
        if (response.status === 401) {
            if (isRefreshing) {
                // Queue this request
                return new Promise((resolve, reject) => {
                    failedQueue.push({ resolve, reject });
                })
                    .then((newToken) => {
                        // Retry with the new token
                        const newHeaders = { ...headers, 'Authorization': `Bearer ${newToken}` };
                        return fetch(url, { ...options, headers: newHeaders });
                    })
                    .catch((err) => {
                        logoutAndRedirect();
                        return Promise.reject(err);
                    });
            }

            isRefreshing = true;
            try {
                const newToken = await refreshAccessToken();
                isRefreshing = false;
                processQueue(null, newToken);

                // Retry the original request with the new token
                const retryHeaders = { ...headers, 'Authorization': `Bearer ${newToken}` };
                return fetch(url, { ...options, headers: retryHeaders });
            } catch (refreshError) {
                isRefreshing = false;
                processQueue(refreshError, null);
                logoutAndRedirect();
                return Promise.reject(refreshError);
            }
        }

        return response;
    } catch (error) {
        // Network errors
        return Promise.reject(error);
    }
};