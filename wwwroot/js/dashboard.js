 


      const html = document.documentElement;
    const key = "ry-theme";
    try{
        const saved = localStorage.getItem(key);
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (saved) html.setAttribute('data-theme', saved);
    else if (prefersDark) html.setAttribute('data-theme', 'dark');
        } catch (e) { }



    const btn = document.getElementById('themeToggle');
      const setIcon = () => {
        if (!btn) return;
    const dark = html.getAttribute('data-theme') === 'dark';
    btn.innerHTML = dark ? '<i class="fa-solid fa-sun"></i>' : '<i class="fa-solid fa-moon"></i>';
      };
    setIcon();
      btn?.addEventListener('click', ()=>{
        const current = html.getAttribute('data-theme') || 'light';
    const next = current === 'dark' ? 'light' : 'dark';
    html.setAttribute('data-theme', next);
    try{localStorage.setItem(key, next); }catch(e){ }
    setIcon();
      });









  
    (function clock(){
      const el = document.getElementById('dashClock');
    if (!el) return;
      const tick = ()=>{
        const d = new Date();
    const df = new Intl.DateTimeFormat('fa-IR', {weekday:'long', hour:'2-digit', minute:'2-digit' });
    el.textContent = df.format(d);
      };
    tick();
    setInterval(tick, 30000);
    })();

    // Reveal on scroll
    (function(){
      const els = document.querySelectorAll('.fade-up');
    if (!('IntersectionObserver' in window)) {
        els.forEach(e => e.classList.add('revealed'));
    return;
      }
      const io = new IntersectionObserver((entries)=>{
        entries.forEach(e => {
            if (e.isIntersecting) { e.target.classList.add('revealed'); io.unobserve(e.target); }
        });
      }, {threshold: 0.12 });
      els.forEach(el => io.observe(el));
    })();

    // Counters
    (function () {
      const counters = document.querySelectorAll('.num[data-count]');
    if (!counters.length) return;
      const easeOutCubic = t => 1 - Math.pow(1 - t, 3);
    function animate(el){
        const end = +el.dataset.count || 0;
    const dur = +el.dataset.duration || 1200;
    const start = performance.now();
    function step(ts){
          const p = Math.min(1, (ts - start) / dur);
    const eased = easeOutCubic(p);
    const val = Math.round(end * (0.1 + 0.9 * eased));
    el.textContent = new Intl.NumberFormat('fa-IR').format(val);
    if (p < 1) requestAnimationFrame(step);
        }
    requestAnimationFrame(step);
      }
      const io = new IntersectionObserver((entries, obs)=>{
        entries.forEach(e => {
            if (e.isIntersecting) { animate(e.target); obs.unobserve(e.target); }
        });
      }, {threshold: 0.4 });
      counters.forEach(el => io.observe(el));
    })();

    // Keyboard support + ripple for tiles
    (function(){
      const tiles = document.querySelectorAll('.tile');
      tiles.forEach(tile=>{
        tile.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); tile.click(); }
        });
        tile.addEventListener('click', (e)=>{
          const r = tile.querySelector('.ripple');
    if (!r) return;
    const rect = tile.getBoundingClientRect();
    const size = Math.max(rect.width, rect.height);
    r.style.width = r.style.height = size + 'px';
    r.style.left = (e.clientX - rect.left - size/2) + 'px';
    r.style.top  = (e.clientY - rect.top  - size/2) + 'px';
    r.classList.remove('show');
    // restart animation
    void r.offsetWidth;
    r.classList.add('show');
        });
      });
    })();


    (function(){
      const enableOnTouch = false;
    const isCoarse = window.matchMedia('(pointer: coarse)').matches;
    const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if ((!enableOnTouch && isCoarse) || reduce) return;

    const cards = document.querySelectorAll('.tile.tilt');
      cards.forEach(card=>{
        let rafId;
    function move(e){
          const rect = card.getBoundingClientRect();
    const px = (e.clientX - rect.left) / rect.width - 0.5;
    const py = (e.clientY - rect.top)  / rect.height - 0.5;
    const rx = (-py) * 6, ry = (px) * 8;
    if (rafId) cancelAnimationFrame(rafId);
          rafId = requestAnimationFrame(()=>{
        card.style.transform = `perspective(800px) rotateX(${rx}deg) rotateY(${ry}deg) translateY(-6px)`;
          });
        }
    function leave(){
        card.style.transition = 'transform 160ms ease';
    card.style.transform = '';
          setTimeout(()=> card.style.transition = '', 180);
        }
    card.addEventListener('mousemove', move);
    card.addEventListener('mouseleave', leave);
      });
    })();
 