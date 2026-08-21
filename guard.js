/* Invite gate: without an accepted invite code (see the teaser page),
   every site page returns to the teaser. Curiosity gate, not security. */
(function () {
  try {
    if (!localStorage.getItem('ntca-access')) location.replace('./');
  } catch (e) {}
})();
