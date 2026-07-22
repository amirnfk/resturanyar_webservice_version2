(function () {
    'use strict';

    var FULL_RELOAD_PATHS = ['/home/dashboard', '/home/Dashboard'];
    var FULL_PAGE_LEAVE_PATHS = ['/home/upgrade', '/home/prepareupgrade'];
    var currentAbortController = null;

    function isFullReloadUrl(url) {
        try {
            var path = new URL(url, window.location.origin).pathname.toLowerCase();
            return FULL_RELOAD_PATHS.some(function (p) { return path === p.toLowerCase(); });
        } catch (e) {
            return false;
        }
    }

    function isFullPageLeaveUrl(url) {
        try {
            var path = new URL(url, window.location.origin).pathname.toLowerCase();
            return FULL_PAGE_LEAVE_PATHS.some(function (p) { return path === p || path.endsWith(p); });
        } catch (e) {
            return false;
        }
    }

    function shouldHandleLink(link, event) {
        if (!link || !link.href) return false;
        if (link.target === '_blank') return false;
        if (event && (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)) {
            return false;
        }

        try {
            var url = new URL(link.href, window.location.origin);
            if (url.origin !== window.location.origin) return false;
            if (isFullReloadUrl(url.href)) return false;
            return link.closest('.sidebar-nav') !== null;
        } catch (e) {
            return false;
        }
    }

    function getContentCard() {
        return document.getElementById('contentCard');
    }

    function setNavigating(isNavigating) {
        var card = getContentCard();
        if (card) {
            card.classList.toggle('is-navigating', isNavigating);
        }
    }

    function updateActiveNav(url) {
        var path;
        try {
            path = new URL(url, window.location.origin).pathname.toLowerCase();
        } catch (e) {
            return;
        }

        document.querySelectorAll('.sidebar-nav a.nav-item').forEach(function (link) {
            var linkPath = new URL(link.href, window.location.origin).pathname.toLowerCase();
            link.classList.toggle('active', linkPath === path);
        });
    }

    function appendRestaurantName(title) {
        if (!title) return '';
        var name = document.body?.dataset?.restaurantName;
        if (!name) return title;
        if (title.indexOf(name) !== -1) return title;
        return title + ' - ' + name;
    }

    function updatePageTitle(title) {
        if (!title) return;
        var cleaned = title.replace(/\s*-\s*رستورانیار.*$/, '').trim();
        var displayTitle = appendRestaurantName(cleaned);
        var titleEl = document.querySelector('.page-title span');
        if (titleEl) titleEl.textContent = displayTitle;
        document.title = displayTitle + ' - رستورانیار دلاویتا';
    }

    function stylesheetExists(href) {
        if (!href) return false;
        try {
            var absolute = new URL(href, window.location.origin).href;
            return Array.from(document.querySelectorAll('link[rel="stylesheet"]')).some(function (link) {
                try {
                    return new URL(link.href, window.location.origin).href === absolute;
                } catch (e) {
                    return link.getAttribute('href') === href;
                }
            });
        } catch (e) {
            return false;
        }
    }

    function injectStyles(doc) {
        doc.querySelectorAll('link[rel="stylesheet"]').forEach(function (link) {
            var href = link.getAttribute('href');
            if (!href || stylesheetExists(href)) return;
            var el = document.createElement('link');
            el.rel = 'stylesheet';
            el.href = href;
            document.head.appendChild(el);
        });
    }

    function stripStylesheetLinks(root) {
        root.querySelectorAll('link[rel="stylesheet"]').forEach(function (link) {
            link.remove();
        });
    }

    function cleanupCurrentPage() {
        var destroyFns = [
            'destroyCustomersListPage',
            'destroyRestaurantSettingsPage'
        ];
        destroyFns.forEach(function (name) {
            if (typeof window[name] === 'function') {
                window[name]();
            }
        });
    }

    function cleanupPageScripts() {
        cleanupCurrentPage();
        document.querySelectorAll('[data-sidebar-page-script]').forEach(function (script) {
            script.remove();
        });
    }

    function externalScriptLoaded(src) {
        if (!src) return false;
        try {
            var absolute = new URL(src, window.location.origin).href;
            return Array.from(document.querySelectorAll('script[src]')).some(function (script) {
                try {
                    return new URL(script.src, window.location.origin).href === absolute;
                } catch (e) {
                    return script.getAttribute('src') === src;
                }
            });
        } catch (e) {
            return false;
        }
    }

    function shouldSkipModuleScript(code) {
        if (!code || !window.fetchWithAuth) {
            return false;
        }
        return /fetchWithAuth/.test(code) && /import\s+/.test(code);
    }

    function loadExternalScript(oldScript) {
        var src = oldScript.getAttribute('src');
        if (!src || externalScriptLoaded(src)) {
            return Promise.resolve();
        }

        return new Promise(function (resolve, reject) {
            var external = document.createElement('script');
            Array.from(oldScript.attributes).forEach(function (attr) {
                external.setAttribute(attr.name, attr.value);
            });
            external.setAttribute('data-sidebar-page-script', 'true');
            external.onload = function () { resolve(); };
            external.onerror = function () { reject(new Error('Failed to load ' + src)); };
            document.body.appendChild(external);
        });
    }

    async function executeScripts(container) {
        var scripts = Array.from(container.querySelectorAll('script'));

        for (var i = 0; i < scripts.length; i++) {
            var oldScript = scripts[i];
            var code = oldScript.textContent || '';
            var isModule = oldScript.type === 'module';
            var src = oldScript.getAttribute('src');
            oldScript.remove();

            if (src) {
                try {
                    await loadExternalScript(oldScript);
                } catch (e) {
                    console.error(e);
                }
                continue;
            }

            if (!code.trim()) {
                continue;
            }

            if (isModule && shouldSkipModuleScript(code)) {
                continue;
            }

            var script = document.createElement('script');
            script.setAttribute('data-sidebar-page-script', 'true');
            if (isModule) {
                script.type = 'module';
            }
            script.textContent = code;
            document.body.appendChild(script);
        }
    }

    function extractPageContent(html) {
        var parser = new DOMParser();
        var doc = parser.parseFromString(html, 'text/html');
        var root = doc.querySelector('.ajax-page-root');
        if (root) {
            return {
                title: root.getAttribute('data-page-title') || doc.querySelector('title')?.textContent || '',
                html: root.innerHTML,
                doc: doc,
                root: root
            };
        }

        var contentCard = doc.querySelector('#contentCard') || doc.querySelector('.content-card');
        if (contentCard) {
            return {
                title: doc.querySelector('.page-title span')?.textContent || doc.querySelector('title')?.textContent || '',
                html: contentCard.innerHTML,
                doc: doc
            };
        }

        return null;
    }

    var PAGE_INIT_MAP = [
        ['/home/foodlist', 'initFoodListPage'],
        ['/home/addorder', 'initAddOrderPage'],
        ['/home/categorylist', 'initCategoryListPage'],
        ['/home/tablelist', 'initTableListPage'],
        ['/home/managerorderlist', 'initManagerOrderListPage'],
        ['/home/managerreports', 'initManagerReportsPage'],
        ['/home/restaurantsubscription', 'initRestaurantSubscriptionPage'],
        ['/menu/publicmenuqrcode', 'initRestaurantSettingsPage'],
        ['/menu/restaurantmenu', 'initRestaurantSettingsPage'],
        ['/home/customerslist', 'initCustomersListPage'],
        ['/home/managestaff', 'initManageStaffPage'],
        ['/home/messages', 'initMessagesPage'],
        ['/home/menusettings', 'initRestaurantSettingsPage'],
        ['/home/settings', 'initRestaurantSettingsPage'],
        ['/home/restaurantsetting', 'initRestaurantGeneralSettingsPage'],
        ['/home/support', 'initSupportPage']
    ];

    function resolvePageInitName(url) {
        try {
            var path = new URL(url, window.location.origin).pathname.toLowerCase();
            for (var i = 0; i < PAGE_INIT_MAP.length; i++) {
                var route = PAGE_INIT_MAP[i][0];
                if (path === route || path.endsWith(route)) {
                    return PAGE_INIT_MAP[i][1];
                }
            }
        } catch (e) { /* ignore */ }
        return null;
    }

    function runPageInit(forUrl) {
        var initName = resolvePageInitName(forUrl || window.location.href);
        if (initName && typeof window[initName] === 'function') {
            window[initName]();
        }
    }

    window.runSidebarPageInit = runPageInit;

    async function navigateTo(url, pushState) {
        if (currentAbortController) {
            currentAbortController.abort();
        }
        currentAbortController = new AbortController();

        var card = getContentCard();
        if (!card) {
            window.location.href = url;
            return;
        }

        setNavigating(true);

        try {
            var response = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                signal: currentAbortController.signal,
                credentials: 'same-origin'
            });

            if (response.redirected) {
                try {
                    var requestedUrl = new URL(url, window.location.origin).href;
                    if (response.url !== requestedUrl) {
                        window.location.href = response.url;
                        return;
                    }
                } catch (e) {
                    window.location.href = response.url;
                    return;
                }
            }

            if (response.status === 401 || response.status === 403) {
                window.location.href = url;
                return;
            }

            if (!response.ok) {
                window.location.href = url;
                return;
            }

            var html = await response.text();
            var extracted = extractPageContent(html);
            if (!extracted) {
                window.location.href = url;
                return;
            }

            cleanupPageScripts();
            injectStyles(extracted.doc);
            if (extracted.root) {
                stripStylesheetLinks(extracted.root);
            }

            card.innerHTML = extracted.root ? extracted.root.innerHTML : extracted.html;
            await executeScripts(card);
            updatePageTitle(extracted.title.replace(/\s*-\s*رستورانیار.*$/, '').trim());
            updateActiveNav(url);

            if (pushState !== false) {
                history.pushState({ sidebarNav: true, url: url }, extracted.title, url);
            }

            window.scrollTo({ top: 0, behavior: 'auto' });
            document.getElementById('sidebar')?.classList.remove('open');
            runPageInit(url);
        } catch (error) {
            if (error.name !== 'AbortError') {
                window.location.href = url;
            }
        } finally {
            setNavigating(false);
            currentAbortController = null;
        }
    }

    function onDocumentClick(event) {
        var link = event.target.closest('.sidebar-nav a.nav-item');
        if (!shouldHandleLink(link, event)) return;

        event.preventDefault();
        navigateTo(link.href, true);
    }

    function onPopState(event) {
        if (event.state && event.state.sidebarNav && event.state.url) {
            window.location.assign(event.state.url);
            return;
        }

        window.location.reload();
    }

    function onLeaveSpaClick(event) {
        var link = event.target.closest('a[href]');
        if (!link || !getContentCard()) return;
        if (!isFullPageLeaveUrl(link.href)) return;

        try {
            history.replaceState({
                sidebarNav: true,
                url: window.location.href
            }, document.title, window.location.href);
        } catch (e) { /* ignore */ }
    }

    document.addEventListener('click', onDocumentClick);
    document.addEventListener('click', onLeaveSpaClick, true);
    window.addEventListener('popstate', onPopState);
    window.addEventListener('pageshow', function () {
        if (typeof window.runSidebarPageInit === 'function') {
            window.runSidebarPageInit();
        }
    });

    history.replaceState({ sidebarNav: true, url: window.location.href }, document.title, window.location.href);
})();
