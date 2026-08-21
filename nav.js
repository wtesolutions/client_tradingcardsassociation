/* Mobile navigation toggle — app-bar + drawer pattern.
   The .nav-toggle button collapses/expands the nav links below the
   nav breakpoint (see site.css); on desktop it is display:none and
   this script is inert. */
(() => {
  const nav = document.querySelector('.nav');
  const btn = document.querySelector('.nav-toggle');
  if (!nav || !btn) return;

  const setOpen = (open) => {
    nav.classList.toggle('nav-open', open);
    btn.setAttribute('aria-expanded', open ? 'true' : 'false');
    btn.setAttribute('aria-label', open ? 'Close menu' : 'Open menu');
  };

  btn.addEventListener('click', () => setOpen(!nav.classList.contains('nav-open')));

  // Choosing a link closes the drawer (matters for same-page anchors).
  nav.addEventListener('click', (e) => {
    if (e.target.closest('a') && nav.classList.contains('nav-open')) setOpen(false);
  });

  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape' && nav.classList.contains('nav-open')) setOpen(false);
  });

  // Growing past the breakpoint resets to the inline desktop nav.
  const mq = window.matchMedia('(min-width: 1100px)');
  const onChange = (m) => { if (m.matches) setOpen(false); };
  if (mq.addEventListener) mq.addEventListener('change', onChange);
  else mq.addListener(onChange);
})();
