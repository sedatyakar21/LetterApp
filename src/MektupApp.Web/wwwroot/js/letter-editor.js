// "Mektup Yaz" ekranı: canlı önizleme, sayfa sayısı ve tahmini fiyat.
// Kesin fiyat sunucuda domain'deki LetterPricingPolicy ile hesaplanır; buradaki sadece tahmindir.
(function () {
    const cfg = window.letterEditor;
    const $ = (id) => document.getElementById(id);
    const form = $("letterForm");
    const field = (name) => form.elements[name];

    const enumNames = {
        Paper: ["White", "Cream", "Pink", "Blue", "Green"],
        Envelope: ["White", "Kraft", "Red", "Navy", "Pink"],
        Font: ["Serif", "Sans", "Handwriting"],
        Ink: ["Black", "Navy", "Burgundy"]
    };

    function swapClass(el, prefix, value) {
        [...el.classList].filter(c => c.startsWith(prefix)).forEach(c => el.classList.remove(c));
        el.classList.add(prefix + value);
    }

    // Domain'deki LetterPages.Count ile aynı kural.
    function pageCount(text) {
        if (!text) return 1;
        const breaks = (text.match(/\n/g) || []).length;
        return Math.max(1, Math.ceil((text.length + breaks * 60) / cfg.charsPerPage));
    }

    const money = (v) => v.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " ₺";

    function estimate(pages) {
        const p = cfg.prices;
        let total = p.basePrice + (pages - 1) * p.extraPage + cfg.photoCount * p.photo;
        if (field("PostcardCategory").value) total += p.postcard;
        if (field("Paper").value !== "0") total += p.coloredPaper;
        if (field("Envelope").value !== "0") total += p.coloredEnvelope;
        if (field("Scent").value !== "0") total += p.scent;
        return total;
    }

    function updateRecipientDetail() {
        const type = field("RecipientType").value;
        const group = $("recipientDetailGroup");
        const label = $("recipientDetailLabel");
        const input = field("RecipientDetail");
        group.style.display = type === "0" ? "none" : "";
        input.required = type !== "0";
        label.textContent = type === "1" ? "Birlik bilgisi (rütbe, birlik adı)" : "Kurum ve koğuş bilgisi";
    }

    function updatePostcard() {
        const cat = field("PostcardCategory").value;
        const designs = $("postcardDesigns");
        designs.style.display = cat ? "" : "none";
        designs.querySelectorAll(".postcard-icon").forEach(i => i.textContent = cfg.postcardIcons[cat] || "");
    }

    function render() {
        const content = field("Content").value;
        const pages = pageCount(content);
        $("pvContent").textContent = content || "Mektubunuz burada görünecek…";
        $("pvSignature").textContent = field("SenderName").value;
        $("pvSender").textContent = field("SenderName").value;
        $("pvRecipient").textContent = field("RecipientName").value || "Alıcı";
        $("charCount").textContent = content.length.toLocaleString("tr-TR");
        $("pageCount").textContent = pages;
        $("estimate").textContent = money(estimate(pages));

        const paper = $("paperPreview");
        swapClass(paper, "paper-", enumNames.Paper[field("Paper").value]);
        swapClass(paper, "font-", enumNames.Font[field("Font").value]);
        swapClass(paper, "ink-", enumNames.Ink[field("Ink").value]);
        swapClass($("envelopePreview"), "envelope-", enumNames.Envelope[field("Envelope").value]);

        updateRecipientDetail();
        updatePostcard();
    }

    form.addEventListener("input", render);
    form.addEventListener("change", render);

    // Kaydedilmemiş değişiklik uyarısı
    let dirty = false;
    form.addEventListener("input", () => dirty = true);
    form.addEventListener("submit", () => dirty = false);
    window.addEventListener("beforeunload", (e) => { if (dirty) e.preventDefault(); });

    render();
})();
