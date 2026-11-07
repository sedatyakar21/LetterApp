// BiMektup — kaydırma efektleri
//  - [data-reveal]        : ekrana girerken yumuşakça belirir (fade-up). Değer "left" / "right" / "zoom" olabilir.
//  - [data-reveal-group]  : içindeki [data-reveal] öğeleri sırayla (kademeli) belirir.
//  - [data-parallax="0.15"]: kaydırırken hafifçe yavaş hareket eder.
//  - Menü kaydırınca incelir; sayfanın aşağısında "başa dön" butonu çıkar.
// "Hareketi azalt" tercihinde ve JavaScript yokken içerik doğrudan görünür.
(function () {
    "use strict";

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const root = document.documentElement;

    // ---------- Menü: kaydırınca incel ----------
    const header = document.querySelector(".site-header");
    const toTop = document.getElementById("toTop");
    let ticking = false;

    function onScroll() {
        const y = window.scrollY;
        header?.classList.toggle("scrolled", y > 12);
        toTop?.classList.toggle("show", y > 600);
        if (!reduceMotion) parallax(y);
        ticking = false;
    }

    window.addEventListener("scroll", () => {
        if (!ticking) { ticking = true; requestAnimationFrame(onScroll); }
    }, { passive: true });

    toTop?.addEventListener("click", () =>
        window.scrollTo({ top: 0, behavior: reduceMotion ? "auto" : "smooth" }));

    // ---------- Parallax ----------
    const parallaxItems = [...document.querySelectorAll("[data-parallax]")];
    function parallax(y) {
        for (const el of parallaxItems) {
            const speed = parseFloat(el.dataset.parallax) || 0.15;
            const rect = el.getBoundingClientRect();
            if (rect.bottom < -200 || rect.top > window.innerHeight + 200) continue;
            el.style.transform = `translate3d(0, ${(y * speed).toFixed(1)}px, 0)`;
        }
    }

    onScroll();

    // ---------- WhatsApp yardım menüsü ----------
    const wa = document.getElementById("waHelp");
    if (wa) {
        const panel = document.getElementById("waPanel");
        const toggle = document.getElementById("waToggle");
        const setOpen = open => {
            panel.hidden = !open;
            wa.classList.toggle("open", open);
            toggle.setAttribute("aria-expanded", String(open));
            toggle.setAttribute("aria-label", open ? "WhatsApp yardım menüsünü kapat" : "WhatsApp yardım menüsünü aç");
            if (open) panel.querySelector(".wa-topics a")?.focus({ preventScroll: true });
        };
        toggle.addEventListener("click", () => setOpen(panel.hidden));
        document.getElementById("waClose")?.addEventListener("click", () => { setOpen(false); toggle.focus(); });
        document.addEventListener("keydown", e => {
            if (e.key === "Escape" && !panel.hidden) { setOpen(false); toggle.focus(); }
        });
        document.addEventListener("click", e => {
            if (!panel.hidden && !wa.contains(e.target)) setOpen(false);
        });
        // Bir konu seçilince WhatsApp yeni sekmede açılır; menüyü kapat.
        panel.querySelectorAll(".wa-topics a").forEach(a => a.addEventListener("click", () => setOpen(false)));
    }

    // ---------- Görünürken belirme ----------
    const items = document.querySelectorAll("[data-reveal]");
    if (reduceMotion || !("IntersectionObserver" in window) || items.length === 0) return;

    // Kademeli gecikme: grup içindeki sıraya göre
    document.querySelectorAll("[data-reveal-group]").forEach(group => {
        const step = parseInt(group.dataset.revealGroup, 10) || 90;
        group.querySelectorAll("[data-reveal]").forEach((el, i) =>
            el.style.setProperty("--reveal-delay", `${i * step}ms`));
    });

    root.classList.add("reveal-ready"); // gizleme sadece JS çalışıyorsa uygulanır

    const observer = new IntersectionObserver(entries => {
        for (const entry of entries) {
            if (!entry.isIntersecting) continue;
            entry.target.classList.add("revealed");
            observer.unobserve(entry.target); // bir kez oynat
        }
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.12 });

    items.forEach(el => observer.observe(el));
})();
