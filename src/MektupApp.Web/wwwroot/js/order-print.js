// Yönetim paneli baskı sayfası.
// - Kutucuklarla hangi bölümlerin (adres etiketi, mektup, fotoğraf, belge) basılacağı seçilir.
// - PDF belgelerin her sayfası pdf.js ile görüntüye çevrilip baskıya eklenir.
// - Tüm görseller ve PDF'ler hazır olunca "Yazdır" düğmesi açılır.
(function () {
    "use strict";

    const status = document.getElementById("printStatus");
    const button = document.getElementById("printButton");
    const parts = ["label", "letter", "photos", "documents"];
    const storageKey = "orderPrint.parts";
    let pdfErrors = 0;

    // ---------------- Bölüm seçimi ----------------

    function applyParts() {
        const hidden = [];
        document.querySelectorAll("[data-part]").forEach(cb => {
            document.body.classList.toggle("hide-" + cb.dataset.part, !cb.checked);
            if (!cb.checked) hidden.push(cb.dataset.part);
        });
        try { localStorage.setItem(storageKey, JSON.stringify(hidden)); } catch { /* tarayıcı izin vermiyorsa önemsiz */ }
        markFirstVisible();
    }

    // İlk görünen bölümden önce sayfa sonu olmasın (baskıda boş ilk sayfa çıkmasın).
    function markFirstVisible() {
        const sheets = [...document.querySelectorAll(".print-pages .sheet")];
        sheets.forEach(s => s.classList.remove("first-visible"));
        const first = sheets.find(s => s.offsetParent !== null);
        if (first) first.classList.add("first-visible");
    }

    try {
        const hidden = JSON.parse(localStorage.getItem(storageKey) || "[]");
        document.querySelectorAll("[data-part]").forEach(cb => { if (hidden.includes(cb.dataset.part)) cb.checked = false; });
    } catch { /* yok say */ }
    document.querySelectorAll("[data-part]").forEach(cb => cb.addEventListener("change", applyParts));
    applyParts();

    // ---------------- Uzun mektubu sayfalara bölme ----------------
    // Her mektup sayfası sabit A4 yüksekliğindedir; sığmayan metin yeni bir sayfaya taşınır.
    // Böylece "[gönderen → alıcı]" altbilgisi her sayfanın altında basılır (başlık yalnızca ilk sayfada).

    function fits(box) { return box.scrollHeight <= box.clientHeight + 1; }

    // Altbilgi: "[gönderen → alıcı] Sayfa[n]" (her mektubun sayfaları 1'den başlar).
    function numberPages(pages) {
        pages.forEach((p, i) => {
            const foot = p.querySelector(".letter-foot");
            foot.dataset.base ??= foot.textContent.trim();
            foot.textContent = `${foot.dataset.base} Sayfa[${i + 1}]`;
        });
    }

    function paginateLetter(first) {
        const template = first.cloneNode(true);
        template.querySelector(".letter-head")?.remove();
        template.classList.remove("first-visible");

        const pages = [first];
        let page = first;
        let text = page.querySelector(".paper-content").textContent;
        for (let guard = 0; guard < 200; guard++) {
            const content = page.querySelector(".paper-content");
            const box = page.querySelector(".letter-body");
            content.textContent = text;
            if (fits(box)) break;

            // Sığan en uzun başlangıcı ikili aramayla bul.
            let lo = 1, hi = text.length;
            while (lo < hi) {
                const mid = Math.ceil((lo + hi) / 2);
                content.textContent = text.slice(0, mid);
                if (fits(box)) lo = mid; else hi = mid - 1;
            }
            // Kelimenin ortasından bölme: son boşluk / satır sonundan kes (çok gerideyse olduğu gibi bırak).
            let cut = lo;
            const ws = Math.max(text.lastIndexOf(" ", lo), text.lastIndexOf("\n", lo));
            if (ws > lo * 0.6) cut = ws;
            content.textContent = text.slice(0, cut).replace(/\s+$/, "");
            text = text.slice(cut).replace(/^\s+/, "");
            if (!text) break;

            const next = template.cloneNode(true);
            page.after(next);
            page = next;
            pages.push(page);
        }
        numberPages(pages);
    }

    async function paginateLetters() {
        // Ölçüm için bölüm görünür olmalı (kutucuk kapalı olsa bile geçici olarak aç).
        const hidden = document.body.classList.contains("hide-letter");
        document.body.classList.remove("hide-letter");
        try { await document.fonts.ready; } catch { /* yazı tipi yüklenemezse yedek yazı tipiyle devam */ }
        document.querySelectorAll(".print-pages .sheet.part-letter").forEach(paginateLetter);
        document.body.classList.toggle("hide-letter", hidden);
        markFirstVisible();
    }

    // ---------------- PDF → görüntü ----------------

    const pdfjs = window.pdfjsLib;
    if (pdfjs) pdfjs.GlobalWorkerOptions.workerSrc = window.pdfWorkerSrc;

    async function renderPdf(placeholder) {
        const name = placeholder.dataset.name;
        // isEvalSupported: false → kötü niyetli PDF yazı tiplerinin kod çalıştırmasını engeller (CVE-2024-4367).
        const doc = await pdfjs.getDocument({ url: placeholder.dataset.pdf, isEvalSupported: false, withCredentials: true }).promise;
        const pages = [];
        for (let n = 1; n <= doc.numPages; n++) {
            const page = await doc.getPage(n);
            const viewport = page.getViewport({ scale: 2.5 }); // ~180 dpi: baskıda net
            const canvas = document.createElement("canvas");
            canvas.width = Math.floor(viewport.width);
            canvas.height = Math.floor(viewport.height);
            const ctx = canvas.getContext("2d");
            ctx.fillStyle = "#fff";
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            await page.render({ canvasContext: ctx, viewport }).promise;

            const img = new Image();
            img.alt = `${name} — sayfa ${n}/${doc.numPages}`;
            img.src = await new Promise(resolve => canvas.toBlob(b => resolve(URL.createObjectURL(b)), "image/png"));
            await img.decode().catch(() => { });

            const sheet = document.createElement("section");
            sheet.className = "sheet part-documents sheet-image";
            sheet.appendChild(img);
            pages.push(sheet);
            page.cleanup();
        }
        await doc.destroy();
        placeholder.replaceWith(...pages);
    }

    function showPdfError(placeholder, error) {
        pdfErrors++;
        placeholder.classList.remove("pdf-placeholder");
        placeholder.classList.add("pdf-error", "no-print");
        placeholder.innerHTML = "";
        const msg = document.createElement("div");
        msg.innerHTML = "<strong>Bu PDF otomatik hazırlanamadı; lütfen ayrıca yazdırın:</strong> ";
        const link = document.createElement("a");
        link.href = placeholder.dataset.pdf;
        link.target = "_blank";
        link.rel = "noopener";
        link.textContent = placeholder.dataset.name; // dosya adı metin olarak eklenir (HTML değil)
        msg.appendChild(link);
        placeholder.appendChild(msg);
        console.error("PDF hazırlanamadı:", placeholder.dataset.name, error);
    }

    // ---------------- Hazırlık ----------------

    function waitForImages() {
        return Promise.all([...document.querySelectorAll("img[data-wait]")].map(img =>
            img.complete ? Promise.resolve() : new Promise(r => { img.onload = img.onerror = r; })));
    }

    async function prepare() {
        status.textContent = "Mektuplar sayfalara bölünüyor…";
        await paginateLetters();

        const placeholders = [...document.querySelectorAll(".pdf-placeholder")];
        let done = 0;
        const update = () => {
            if (placeholders.length) status.textContent = `PDF belgeler hazırlanıyor (${done}/${placeholders.length})…`;
            else status.textContent = "Görseller yükleniyor…";
        };
        update();

        for (const p of placeholders) {
            try {
                if (!pdfjs) throw new Error("pdf.js yüklenemedi");
                await renderPdf(p);
            } catch (e) {
                showPdfError(p, e);
            }
            done++;
            update();
        }
        await waitForImages();
        markFirstVisible();

        button.disabled = false;
        status.textContent = pdfErrors > 0
            ? `⚠ ${pdfErrors} PDF hazırlanamadı; bağlantılarından ayrıca yazdırın.`
            : "Baskıya hazır.";
        status.classList.toggle("text-danger", pdfErrors > 0);
        status.classList.toggle("text-success", pdfErrors === 0);
    }

    button.addEventListener("click", () => window.print());
    window.addEventListener("beforeprint", markFirstVisible);
    prepare();
})();
