(function () {
  var nav = document.getElementById("admin-nav");
  var backdrop = document.querySelector("[data-admin-nav-close]");
  document.querySelector("[data-admin-nav-open]")?.addEventListener("click", function () {
    nav?.classList.add("is-open");
    if (backdrop) backdrop.hidden = false;
  });
  backdrop?.addEventListener("click", function () {
    nav?.classList.remove("is-open");
    if (backdrop) backdrop.hidden = true;
  });

  var inspector = document.querySelector("[data-testid=shop-inspector], [data-testid=mfr-inspector]");
  var lastRow = null;
  document.querySelectorAll("tr[data-inspect]").forEach(function (row) {
    row.setAttribute("tabindex", "0");
    row.addEventListener("click", function (e) {
      if (e.target.closest("a,button,form,input,select,label")) return;
      var url = new URL(location.href);
      url.searchParams.set("inspect", row.getAttribute("data-inspect"));
      location.href = url.toString();
    });
    row.addEventListener("keydown", function (e) {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        row.click();
      }
    });
  });
  function closeInspector() {
    if (!inspector || inspector.hidden) return;
    inspector.hidden = true;
    lastRow = document.querySelector("tr.is-selected");
    lastRow?.focus();
    var url = new URL(location.href);
    url.searchParams.delete("inspect");
    history.replaceState({}, "", url.toString());
  }
  inspector?.querySelector("[data-admin-inspect-close]")?.addEventListener("click", closeInspector);
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") {
      closeInspector();
      nav?.classList.remove("is-open");
      if (backdrop) backdrop.hidden = true;
    }
  });
})();
