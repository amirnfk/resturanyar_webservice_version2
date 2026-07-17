
let isRefreshing = false;
let failedQueue = [];

const processQueue = (error, token = null) => {
    failedQueue.forEach(prom => {
        if (error) prom.reject(error);
        else prom.resolve(token);
    });
    failedQueue = [];
};

export const refreshAccessToken = async () => {
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
        localStorage.setItem('accessToken', data.token);
        localStorage.setItem('refreshToken', data.refreshToken);
        return data.token;
    } else {
        throw new Error('Refresh invalid');
    }
};

const logoutAndRedirect = () => {
    const refreshToken = localStorage.getItem('refreshToken');
    const accessToken = localStorage.getItem('accessToken');

    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('phone');

    if (refreshToken) {
        const headers = { 'Content-Type': 'application/json' };
        if (accessToken) {
            headers['Authorization'] = `Bearer ${accessToken}`;
        }

        fetch('/api/v2/UserApi/logout', {
            method: 'POST',
            headers,
            body: JSON.stringify(refreshToken)
        }).catch(() => { });
    }

    window.location.href = '/Home/Logout';
};

export const fetchWithAuth = async (url, options = {}) => {
    const accessToken = localStorage.getItem('accessToken');
    if (!accessToken) {
        logoutAndRedirect();
        return Promise.reject(new Error('No access token'));
    }

    const headers = {
        ...options.headers,
        'Authorization': `Bearer ${accessToken}`
    };

    if (options.body && options.body instanceof FormData) {
        delete headers['Content-Type'];
    }

    const requestOptions = { ...options, headers };

    try {
        let response = await fetch(url, requestOptions);

        if (response.status === 401) {
            if (isRefreshing) {
                return new Promise((resolve, reject) => {
                    failedQueue.push({ resolve, reject });
                })
                    .then((newToken) => {
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
                processQueue(null, newToken);

                const retryHeaders = { ...headers, 'Authorization': `Bearer ${newToken}` };
                return fetch(url, { ...options, headers: retryHeaders });
            } catch (refreshError) {
                processQueue(refreshError, null);
                logoutAndRedirect();
                return Promise.reject(refreshError);
            } finally {
                isRefreshing = false;
            }
        }

        return response;
    } catch (error) {
        return Promise.reject(error);
    }
};
