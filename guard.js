/* Invite gate: pages load codes.js before this file. A valid ?code= in
   the URL (from an invitation link or the teaser's invite form) unlocks
   this device; otherwise visitors without access return to the teaser.
   Deliberately event-free: some accessibility browser extensions break
   addEventListener on the page, so the gate must not depend on events. */
(function () {
  try {
    var codes = window.NTCA_CODES || [];
    var m = /[?&]code=([^&#]+)/.exec(location.search);
    if (m) {
      var code = decodeURIComponent(m[1]).trim().toUpperCase();
      if (codes.indexOf(code) !== -1) {
        localStorage.setItem('ntca-access', code);
        /* tidy the address bar */
        try {
          history.replaceState(null, '', location.pathname + location.hash);
        } catch (e2) {}
      }
    }
    if (!localStorage.getItem('ntca-access')) location.replace('./');
  } catch (e) {}
})();
