(function () {
  var btn = document.getElementById("near-me");
  var form = document.getElementById("dir-bar") || document.getElementById("dir-filter-form");
  if (btn) {
    btn.addEventListener("click", function () {
      if (!navigator.geolocation) {
        btn.textContent = "Location off";
        return;
      }
      btn.disabled = true;
      btn.textContent = "Locating…";
      navigator.geolocation.getCurrentPosition(function (pos) {
        document.querySelectorAll("#dir-lat, input[name=lat]").forEach(function (el) {
          el.value = pos.coords.latitude.toFixed(5);
        });
        document.querySelectorAll("#dir-lng, input[name=lng]").forEach(function (el) {
          el.value = pos.coords.longitude.toFixed(5);
        });
        var sort = document.querySelector("select[name=sort]");
        if (sort) sort.value = "distance";
        var target = document.getElementById("dir-filter-form") || document.getElementById("dir-bar");
        if (target) target.submit();
      }, function () {
        btn.disabled = false;
        btn.textContent = "Use my location";
      }, { enableHighAccuracy: false, timeout: 8000, maximumAge: 60000 });
    });
  }

  var sheet = document.getElementById("dir-filters");
  var openers = document.querySelectorAll("[data-filters-open]");
  var closers = document.querySelectorAll("[data-filters-close]");
  function showSheet(on) {
    if (!sheet) return;
    if (window.matchMedia("(min-width: 1100px)").matches) {
      sheet.hidden = false;
      return;
    }
    sheet.hidden = !on;
  }
  openers.forEach(function (el) { el.addEventListener("click", function () { showSheet(true); }); });
  closers.forEach(function (el) { el.addEventListener("click", function () { showSheet(false); }); });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") showSheet(false);
  });
  showSheet(false);
  if (window.matchMedia("(min-width: 1100px)").matches && sheet) sheet.hidden = false;

  var filterForm = document.getElementById("dir-filter-form");
  if (filterForm && window.matchMedia("(min-width: 1100px)").matches) {
    filterForm.querySelectorAll("input, select").forEach(function (el) {
      el.addEventListener("change", function () { filterForm.submit(); });
    });
  }

  var locInput = document.getElementById("dir-location");
  var locClear = document.getElementById("dir-loc-clear");
  function syncLocClear() {
    if (!locClear || !locInput) return;
    locClear.hidden = !locInput.value;
  }
  if (locClear && locInput) {
    syncLocClear();
    locInput.addEventListener("input", syncLocClear);
    locClear.addEventListener("click", function () {
      locInput.value = "";
      document.querySelectorAll("#dir-lat, #dir-lng, input[name=lat], input[name=lng]").forEach(function (el) {
        if (el.closest("#dir-bar") || el.id === "dir-lat" || el.id === "dir-lng")
          el.value = "";
      });
      syncLocClear();
      locInput.focus();
    });
  }
})();
