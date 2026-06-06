 

    (function () {
          const html = document.documentElement;
    const key = "ry-theme";
    const saved = localStorage.getItem(key);
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (saved) html.setAttribute('data-theme', saved);
    else if (prefersDark) html.setAttribute('data-theme', 'dark');
    const btn = document.getElementById('themeToggle');
          const setIcon = () => {
            const dark = html.getAttribute('data-theme') === 'dark';
    btn.innerHTML = dark ? '<i class="fa-solid fa-sun"></i>' : '<i class="fa-solid fa-moon"></i>';
          };
    setIcon();
          btn?.addEventListener('click', () => {
            const newTheme = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    html.setAttribute('data-theme', newTheme);
    localStorage.setItem(key, newTheme);
    setIcon();
          });
        })();

    // Reveal on scroll
    const revealEls = document.querySelectorAll('.fade-up');
        const io = new IntersectionObserver((entries) => {
        entries.forEach(e => { if (e.isIntersecting) { e.target.classList.add('revealed'); io.unobserve(e.target); } });
        }, {threshold: 0.12 });
        revealEls.forEach(el => io.observe(el));

    // Counters
    const counters = document.querySelectorAll('.num[data-count]');
        const io2 = new IntersectionObserver((entries) => {
        entries.forEach(e => {
            if (e.isIntersecting) {
                const el = e.target;
                const end = +el.dataset.count;
                const dur = 1200;
                const start = performance.now();
                const step = (t) => {
                    const p = Math.min(1, (t - start) / dur);
                    el.textContent = new Intl.NumberFormat('fa-IR').format(Math.round(end * (0.1 + 0.9 * p)));
                    if (p < 1) requestAnimationFrame(step);
                };
                requestAnimationFrame(step);
                io2.unobserve(el);
            }
        });
        }, {threshold: 0.4 });
        counters.forEach(c => io2.observe(c));

    // Back to top
    const back = document.getElementById('backTop');
        const onScroll = () => {
          if (window.scrollY > 400) back.classList.add('show'); else back.classList.remove('show');
        };
    document.addEventListener('scroll', onScroll, {passive: true });
        back.addEventListener('click', () => window.scrollTo({top: 0, behavior: 'smooth' }));

        // Bootstrap scrollspy refresh (after bundle loaded)
        window.addEventListener('load', () => {
          if (typeof bootstrap !== 'undefined') {
            const spy = bootstrap.ScrollSpy.getOrCreateInstance(document.body);
            window.addEventListener('resize', () => spy.refresh());
          }
        });
 