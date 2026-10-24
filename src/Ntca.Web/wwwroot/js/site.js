(function () {
  var SAVE_KEY = "ntca.savedShops";

  function savedList() {
    try { return JSON.parse(localStorage.getItem(SAVE_KEY) || "[]"); }
    catch (e) { return []; }
  }
  function writeSaved(items) {
    localStorage.setItem(SAVE_KEY, JSON.stringify(items));
  }
  function isSaved(id) {
    return savedList().some(function (s) { return String(s.id) === String(id); });
  }
  function toggleSave(btn) {
    var card = btn.closest("[data-shop]");
    var id = btn.getAttribute("data-shop") || (card && card.getAttribute("data-shop"));
    if (!id) return;
    var items = savedList();
    var next;
    if (isSaved(id)) {
      next = items.filter(function (s) { return String(s.id) !== String(id); });
      btn.setAttribute("aria-pressed", "false");
    } else {
      next = items.concat([{
        id: id,
        name: btn.getAttribute("data-name") || (card && card.getAttribute("data-name")) || "Shop",
        loc: btn.getAttribute("data-loc") || (card && card.getAttribute("data-loc")) || "/shops",
        where: btn.getAttribute("data-where") || (card && card.getAttribute("data-where")) || ""
      }]);
      btn.setAttribute("aria-pressed", "true");
    }
    writeSaved(next);
  }
  function syncSaves() {
    document.querySelectorAll("[data-save-shop]").forEach(function (btn) {
      var card = btn.closest("[data-shop]");
      var id = btn.getAttribute("data-shop") || (card && card.getAttribute("data-shop"));
      if (id) btn.setAttribute("aria-pressed", isSaved(id) ? "true" : "false");
    });
  }

  document.addEventListener("click", function (e) {
    var save = e.target.closest("[data-save-shop]");
    if (save) {
      e.preventDefault();
      toggleSave(save);
      return;
    }
    var share = e.target.closest("[data-share-shop]");
    if (share) {
      e.preventDefault();
      var url = new URL(share.getAttribute("data-url") || window.location.href, window.location.origin).toString();
      var title = share.getAttribute("data-title") || document.title;
      if (navigator.share) {
        navigator.share({ title: title, url: url }).catch(function () {});
      } else if (navigator.clipboard) {
        navigator.clipboard.writeText(url).then(function () {
          share.textContent = "Link copied";
        });
      }
    }
  });
  syncSaves();

  var openBtn = document.querySelector("[data-menu-open]");
  var menu = document.getElementById("mobile-menu");
  var closeBtn = document.querySelector("[data-menu-close]");
  function setMenu(open) {
    if (!menu || !openBtn) return;
    menu.hidden = !open;
    openBtn.setAttribute("aria-expanded", open ? "true" : "false");
    document.body.style.overflow = open ? "hidden" : "";
    if (open) (closeBtn || menu).focus();
    else openBtn.focus();
  }
  openBtn && openBtn.addEventListener("click", function () { setMenu(true); });
  closeBtn && closeBtn.addEventListener("click", function () { setMenu(false); });
  menu && menu.addEventListener("click", function (e) {
    if (e.target === menu) setMenu(false);
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && menu && !menu.hidden) setMenu(false);
  });

  var box = document.getElementById("lightbox");
  function openLightbox(src, caption, alt) {
    if (!box) return;
    var img = box.querySelector("img");
    var cap = box.querySelector(".lightbox-caption");
    if (img) {
      img.src = src;
      img.alt = alt || caption || "";
    }
    if (cap) cap.textContent = caption || "";
    if (typeof box.showModal === "function") box.showModal();
    else box.setAttribute("open", "");
  }
  function closeLightbox() {
    if (!box) return;
    if (typeof box.close === "function") box.close();
    else box.removeAttribute("open");
  }
  document.addEventListener("click", function (e) {
    var open = e.target.closest("[data-lightbox]");
    if (open) {
      e.preventDefault();
      var fig = open.closest(".gallery-item");
      openLightbox(
        (fig && fig.getAttribute("data-full")) || open.querySelector("img")?.src,
        fig && fig.getAttribute("data-caption"),
        open.querySelector("img")?.alt
      );
      return;
    }
    if (e.target.closest("[data-lightbox-close]") || e.target === box) closeLightbox();
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") closeLightbox();
  });

  document.querySelectorAll("[data-review-clamp]").forEach(function (el) {
    var btn = el.parentElement && el.parentElement.querySelector("[data-review-more]");
    if (!btn) return;
    if (el.scrollHeight > el.clientHeight + 2) btn.hidden = false;
    btn.addEventListener("click", function () {
      var open = el.classList.toggle("is-open");
      btn.textContent = open ? "Read less" : "Read more";
    });
  });

  document.addEventListener("keydown", function (e) {
    if (e.key !== "Enter" && e.key !== " ") return;
    var card = e.target.closest(".shop-card[data-loc]");
    if (!card || e.target !== card) return;
    e.preventDefault();
    window.location.href = card.getAttribute("data-loc");
  });
})();
