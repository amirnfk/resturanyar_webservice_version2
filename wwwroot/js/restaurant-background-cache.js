(function (window, document) {
    'use strict';

    var STORAGE_KEY = 'ry:lastRestaurantTheme:v1';
    var DEFAULT_BACKGROUND = '/images/backgrounds/default.jpg';
    var ALLOWED_BACKGROUNDS = [
        DEFAULT_BACKGROUND,
        '/images/backgrounds/fastfoodbackground.jpg',
        '/images/backgrounds/caferestaurantbackground.jpg',
        '/images/backgrounds/kababibackground.jpg',
        '/images/backgrounds/sonatibackground.jpg',
        '/images/backgrounds/modernbackground.jpg',
        '/images/backgrounds/organicbackground.jpg'
    ];

    function normalizeBackgroundUrl(value) {
        if (typeof value !== 'string') return DEFAULT_BACKGROUND;

        var trimmed = value.trim();
        try {
            var parsed = new URL(trimmed, window.location.origin);
            if (parsed.origin !== window.location.origin) return DEFAULT_BACKGROUND;
            trimmed = parsed.pathname;
        } catch (_) {
            return DEFAULT_BACKGROUND;
        }

        return ALLOWED_BACKGROUNDS.indexOf(trimmed) >= 0
            ? trimmed
            : DEFAULT_BACKGROUND;
    }

    function normalizeRestaurantId(value) {
        var id = Number.parseInt(value, 10);
        return Number.isInteger(id) && id > 0 ? id : null;
    }

    function get() {
        try {
            var stored = JSON.parse(window.localStorage.getItem(STORAGE_KEY) || 'null');
            if (!stored || typeof stored !== 'object') return null;

            return {
                restaurantId: normalizeRestaurantId(stored.restaurantId),
                backgroundUrl: normalizeBackgroundUrl(stored.backgroundUrl)
            };
        } catch (_) {
            return null;
        }
    }

    function remember(restaurantId, backgroundUrl) {
        var normalizedId = normalizeRestaurantId(restaurantId);
        if (!normalizedId) return false;

        var preference = {
            restaurantId: normalizedId,
            backgroundUrl: normalizeBackgroundUrl(backgroundUrl)
        };

        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(preference));
            return true;
        } catch (_) {
            return false;
        }
    }

    function apply(backgroundUrl) {
        var normalizedUrl = normalizeBackgroundUrl(backgroundUrl);
        document.documentElement.style.setProperty(
            '--page-background-image',
            'url("' + normalizedUrl + '")'
        );
        return normalizedUrl;
    }

    function applyLastUsed() {
        var preference = get();
        return apply(preference ? preference.backgroundUrl : DEFAULT_BACKGROUND);
    }

    window.RestaurantBackgroundCache = {
        defaultBackground: DEFAULT_BACKGROUND,
        normalize: normalizeBackgroundUrl,
        get: get,
        remember: remember,
        apply: apply,
        applyLastUsed: applyLastUsed
    };
})(window, document);
