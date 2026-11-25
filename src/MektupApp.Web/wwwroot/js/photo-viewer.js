// Fotoğraf büyütme: [data-gallery] içindeki görsele tıklanınca büyük görünüm açılır.
// Galerideki diğer fotoğraflara önceki / sonraki düğmeleri, ← → tuşları ve dokunmatik kaydırmayla geçilir.
(function () {
    "use strict";
    const el = document.getElementById("photoViewer");
    if (!el || !window.bootstrap) return;
    const modal = bootstrap.Modal.getOrCreateInstance(el);
    const img = document.getElementById("photoViewerImg");
    const title = document.getElementById("photoViewerTitle");
    const count = document.getElementById("photoViewerCount");
    const prev = document.getElementById("photoViewerPrev");
    const next = document.getElementById("photoViewerNext");
    let items = [];
    let index = 0;

    function show(i) {
        index = (i + items.length) % items.length;
        const source = items[index];
        img.src = source.currentSrc || source.src;
        img.alt = source.alt || "";
        title.textContent = source.alt || "";
        count.textContent = items.length > 1 ? `${index + 1} / ${items.length}` : "";
        prev.hidden = next.hidden = items.length < 2;
    }

    document.addEventListener("click", (e) => {
        const target = e.target.closest("[data-gallery] img");
        // Kaldır gibi düğmelere tıklanınca açılmaz; görseli saran bağlantı (yeni sekmede aç) yerine büyük görünüm açılır.
        if (!target || e.target.closest("button")) return;
        const gallery = target.closest("[data-gallery]");
        items = [...gallery.querySelectorAll("img")];
        e.preventDefault();
        show(items.indexOf(target));
        modal.show();
    });

    // Klavyeyle de açılabilsin (görseller odaklanabilir yapılır).
    document.addEventListener("keydown", (e) => {
        if ((e.key === "Enter" || e.key === " ") && e.target.matches?.("[data-gallery] img")) {
            e.preventDefault();
            e.target.click();
        }
    });

    prev.addEventListener("click", () => show(index - 1));
    next.addEventListener("click", () => show(index + 1));
    el.addEventListener("keydown", (e) => {
        if (e.key === "ArrowLeft") show(index - 1);
        if (e.key === "ArrowRight") show(index + 1);
    });

    let touchX = null;
    img.addEventListener("touchstart", (e) => { touchX = e.touches[0].clientX; }, { passive: true });
    img.addEventListener("touchend", (e) => {
        if (touchX === null || items.length < 2) return;
        const dx = e.changedTouches[0].clientX - touchX;
        if (Math.abs(dx) > 40) show(index + (dx < 0 ? 1 : -1));
        touchX = null;
    });
    el.addEventListener("hidden.bs.modal", () => { img.removeAttribute("src"); });

    // Galerideki görsellere odak ve "büyüt" ipucu (sonradan eklenenler için tıklamada da çalışır).
    const mark = () => document.querySelectorAll("[data-gallery] img:not([tabindex])").forEach(i => {
        i.tabIndex = 0;
        i.setAttribute("role", "button");
        i.title = "Büyütmek için tıklayın";
    });
    mark();
    new MutationObserver(mark).observe(document.body, { childList: true, subtree: true });
})();
