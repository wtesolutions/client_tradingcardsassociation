(function () {
  var btn = document.getElementById("near-me");
  if (!btn) return;
  btn.addEventListener("click", function () {
    if (!navigator.geolocation) {
      btn.textContent = "Location off";
      return;
    }
    btn.disabled = true;
    btn.textContent = "Locating…";
    navigator.geolocation.getCurrentPosition(function (pos) {
      var lat = document.getElementById("dir-lat");
      var lng = document.getElementById("dir-lng");
      if (lat) lat.value = pos.coords.latitude.toFixed(5);
      if (lng) lng.value = pos.coords.longitude.toFixed(5);
      var sort = document.querySelector("#dir-bar select[name=sort]");
      if (sort) sort.value = "distance";
      document.getElementById("dir-bar").submit();
    }, function () {
      btn.disabled = false;
      btn.textContent = "Near me";
    }, { enableHighAccuracy: false, timeout: 8000, maximumAge: 60000 });
  });
})();
