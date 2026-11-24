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
        let total = p.basePrice + (pages - 1) * p.extraPage + cfg.photoCount * p.photo + (cfg.documentCount || 0) * p.document;
        if (field("PostcardCategory").value) total += p.postcard;
        if (field("Paper").value !== "0") total += p.coloredPaper;
        if (field("Envelope").value !== "0") total += p.coloredEnvelope;
        if (field("Scent").value !== "0") total += p.scent;
        return total;
    }

    // Asker: birlik bilgisi. Cezaevi: baba adı + koğuş (kurum adı gizli alanda, seçilen cezaevinden gelir).
    function updateRecipientDetail() {
        const type = field("RecipientType").value;
        const soldier = type === "1", inmate = type === "2";
        $("recipientDetailGroup").style.display = soldier ? "" : "none";
        field("RecipientDetail").required = soldier;
        // Baba adı ve koğuş isteğe bağlıdır (tavsiye edilir), bu yüzden required yapılmaz.
        $("inmateGroup").style.display = inmate ? "" : "none";
    }

    // ---------------- Önceki mektuptan doldurma ----------------
    // Yeni mektupta cezaevi / asker seçilince o türdeki son mektubun alıcı ve gönderen bilgileri yazılır.
    // Kullanıcı alıcı adını kendisi değiştirdiyse dokunulmaz.
    let autoName = cfg.isNew && cfg.prefills[field("RecipientType").value] ? field("RecipientName").value : null;

    async function applyPrefill() {
        if (!cfg.isNew) return;
        const name = field("RecipientName").value;
        if (name && name !== autoName) return;
        const p = cfg.prefills[field("RecipientType").value];
        const set = (n, v) => { if (field(n)) field(n).value = v ?? ""; };
        const picker = n => field(n + ".City")?.closest("[data-address-picker]");
        const empty = { city: "", district: "", line: "", postalCode: "" };

        set("RecipientName", p?.recipientName);
        set("RecipientDetail", p?.recipientDetail);
        set("RecipientFatherName", p?.recipientFatherName);
        set("RecipientWard", p?.recipientWard);
        autoName = p ? p.recipientName : null;
        await window.addressPicker?.set(picker("RecipientAddress"), p ? p.recipientAddress : empty);
        if (p) {
            set("SenderName", p.senderName);
            await window.addressPicker?.set(picker("SenderAddress"), p.senderAddress);
        }
        render();
    }

    field("RecipientType").addEventListener("change", applyPrefill);

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
