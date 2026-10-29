/*
 * Resturanyar shared theme module.
 * Load this synchronously in <head> (before stylesheets) so the theme is applied before first paint.
 *
 * Storage key: 'ry-theme' (legacy 'theme' key is migrated once and removed).
 * Public API: window.RYTheme = { get, set, toggle, syncToggleIcons }
 * Event:      document dispatches 'ry-theme-change' CustomEvent with detail { theme }
 * Toggles:    any [data-theme-toggle] element or #themeToggle is wired automatically.
 */
(function () {
    'use strict';

    var STORAGE_KEY = 'ry-theme';
    var LEGACY_KEY = 'theme';
    var THEME_COLOR_DARK = '#0a0c12';
    var THEME_COLOR_LIGHT = '#FF7A00';
    var root = document.documentElement;

    function normalize(value) {
        return value === 'dark' ? 'dark' : (value === 'light' ? 'light' : null);
    }

    function readStored() {
        try {
            var saved = normalize(localStorage.getItem(STORAGE_KEY));
            if (saved) return saved;

            // One-time migration from the old key used by older layouts.
            var legacy = normalize(localStorage.getItem(LEGACY_KEY));
            if (legacy) {
                localStorage.setItem(STORAGE_KEY, legacy);
            }
            if (localStorage.getItem(LEGACY_KEY) !== null) {
                localStorage.removeItem(LEGACY_KEY);
            }
            return legacy;
        } catch (e) {
            return null;
        }
    }

    function systemPreference() {
        try {
            return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        } catch (e) {
            return 'light';
        }
    }

    function resolveInitial() {
        return readStored() || systemPreference();
    }

    function updateMetaThemeColor(theme) {
        var meta = document.querySelector('meta[name="theme-color"]');
        if (!meta) return;
        // Remember the layout's own light color the first time we see it.
        if (!meta.hasAttribute('data-ry-light-color')) {
            var current = meta.getAttribute('content');
            meta.setAttribute('data-ry-light-color', current && current.toLowerCase() !== THEME_COLOR_DARK ? current : THEME_COLOR_LIGHT);
        }
        meta.setAttribute('content', theme === 'dark' ? THEME_COLOR_DARK : meta.getAttribute('data-ry-light-color'));
    }

    function setIcon(icon, theme) {
        if (!icon) return;
        var isDark = theme === 'dark';
        var cls = icon.className || '';
        var hasSolidPrefix = /\bfa-solid\b/.test(cls);
        var hasFasPrefix = /\bfas\b/.test(cls);
        // Keep whatever prefix the markup already uses; default to "fas".
        var prefix = hasSolidPrefix ? 'fa-solid' : (hasFasPrefix ? 'fas' : 'fas');
        var preserved = cls
            .split(/\s+/)
            .filter(function (c) {
                return c && c !== 'fa-moon' && c !== 'fa-sun' && c !== 'fas' && c !== 'fa-solid';
            });
        preserved.unshift(prefix, isDark ? 'fa-sun' : 'fa-moon');
        icon.className = preserved.join(' ');
    }

    function syncToggleIcons(theme) {
        theme = theme || get();
        var toggles = document.querySelectorAll('[data-theme-toggle], #themeToggle');
        toggles.forEach(function (btn) {
            var icon = btn.querySelector('i');
            if (!icon) {
                icon = document.createElement('i');
                btn.appendChild(icon);
            }
            setIcon(icon, theme);
            btn.setAttribute('aria-pressed', theme === 'dark' ? 'true' : 'false');
            var label = theme === 'dark' ? 'تغییر به حالت روشن' : 'تغییر به حالت تاریک';
            btn.setAttribute('title', label);
            btn.setAttribute('aria-label', label);
        });
    }

    function apply(theme, persist) {
        theme = normalize(theme) || 'light';
        root.setAttribute('data-theme', theme);
        root.style.colorScheme = theme;
        updateMetaThemeColor(theme);

        if (persist !== false) {
            try { localStorage.setItem(STORAGE_KEY, theme); } catch (e) { /* ignore */ }
        }

        if (document.body) {
            syncToggleIcons(theme);
        }

        try {
            document.dispatchEvent(new CustomEvent('ry-theme-change', { detail: { theme: theme } }));
        } catch (e) { /* ignore */ }

        return theme;
    }

    function get() {
        return root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function set(theme) {
        return apply(theme, true);
    }

    function toggle() {
        return set(get() === 'dark' ? 'light' : 'dark');
    }

    // Apply immediately (before paint). Do not persist an implicit system choice.
    apply(resolveInitial(), false);

    // Wire toggle buttons (event delegation so AJAX-injected content works too).
    document.addEventListener('click', function (event) {
        var btn = event.target && event.target.closest
            ? event.target.closest('[data-theme-toggle], #themeToggle')
            : null;
        if (!btn) return;
        event.preventDefault();
        toggle();
    });

    // Follow OS changes only while the user has not chosen explicitly.
    try {
        var mql = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)');
        if (mql && mql.addEventListener) {
            mql.addEventListener('change', function (e) {
                if (!readStored()) {
                    apply(e.matches ? 'dark' : 'light', false);
                }
            });
        }
    } catch (e) { /* ignore */ }

    function onReady() {
        // <meta name="theme-color"> and toggle buttons are usually parsed after this script runs.
        updateMetaThemeColor(get());
        syncToggleIcons(get());
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', onReady);
    } else {
        onReady();
    }

    window.RYTheme = {
        get: get,
        set: set,
        toggle: toggle,
        syncToggleIcons: syncToggleIcons
    };
})();
