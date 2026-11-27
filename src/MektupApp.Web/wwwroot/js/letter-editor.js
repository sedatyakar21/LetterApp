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
        Ink: ["Black", "Navy", "Burgundy"],
        Size: ["Small", "Medium", "Large"]
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

    const selectedPostcards = () => [...form.querySelectorAll('input[type="checkbox"][name="PostcardIds"]:checked')];

    // Çoklu kartpostal: seçilen sayıyı göster; sınıra ulaşınca seçilmemiş olanlar kapanır.
    function updatePostcards() {
        const picker = $("postcardPicker");
        if (!picker) return;
        const max = Number(picker.dataset.max) || 5;
        const count = selectedPostcards().length;
        picker.querySelectorAll('input[name="PostcardIds"]').forEach(i => i.disabled = !i.checked && count >= max);
        $("postcardCount").textContent = count === 0 ? "" : `${count} kartpostal seçildi${count >= max ? " (sınır)" : ""}.`;
    }

    const money = (v) => v.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + " ₺";

    // Tahmini fiyat kalemleri (domain'deki LetterPricingPolicy ile aynı sıra ve kurallar).
    function priceLines(pages) {
        const p = cfg.prices;
        const lines = [["Mektup (1 sayfa, zarf ve kayıtlı posta)", p.basePrice]];
        if (pages > 1) lines.push([`Ek sayfa × ${pages - 1}`, (pages - 1) * p.extraPage]);
        if (cfg.photoCount > 0) lines.push([`Fotoğraf × ${cfg.photoCount}`, cfg.photoCount * p.photo]);
        if (cfg.documentCount > 0) lines.push([`Belge × ${cfg.documentCount}`, cfg.documentCount * p.document]);
        const cards = selectedPostcards().length;
        if (cards > 0) lines.push([cards === 1 ? "Kartpostal" : `Kartpostal × ${cards}`, cards * p.postcard]);
        if (field("Paper").value !== "0") lines.push(["Renkli kâğıt", p.coloredPaper]);
        if (field("Envelope").value !== "0") lines.push(["Renkli zarf", p.coloredEnvelope]);
        if (field("ScentId").value) lines.push(["Koku", p.scent]);
        return lines;
    }

    const estimate = (pages) => priceLines(pages).reduce((sum, l) => sum + l[1], 0);

    const selectedText = (name) => { const s = field(name); return s?.selectedOptions[0]?.text ?? ""; };

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

    function render() {
        const content = field("Content").value;
        const pages = pageCount(content);
        $("pvContent").textContent = content || "Mektubunuz burada görünecek…";
        $("charCount").textContent = content.length.toLocaleString("tr-TR");
        $("pageCount").textContent = pages;
        $("estimate").textContent = money(estimate(pages));

        const paper = $("paperPreview");
        swapClass(paper, "paper-", enumNames.Paper[field("Paper").value]);
        swapClass(paper, "font-", enumNames.Font[field("Font").value]);
        swapClass(paper, "ink-", enumNames.Ink[field("Ink").value]);
        swapClass(paper, "tsize-", enumNames.Size[field("Size").value]);
        swapClass($("envelopePreview"), "env-color-", enumNames.Envelope[field("Envelope").value]);

        // Editörün kendisi de seçilen kâğıt rengi, yazı stili ve mürekkeple görünür.
        const editor = field("Content");
        swapClass(editor, "paper-", enumNames.Paper[field("Paper").value]);
        swapClass(editor, "font-", enumNames.Font[field("Font").value]);
        swapClass(editor, "ink-", enumNames.Ink[field("Ink").value]);
        swapClass(editor, "tsize-", enumNames.Size[field("Size").value]);
        swapClass($("inkDot"), "ink-dot-", enumNames.Ink[field("Ink").value]);

        updateRecipientDetail();
        updatePostcards();
        renderReview();
    }

    // ---------------- Kontrol sekmesi: zarf adresleri ve ekstralar ----------------
    // Aynı adlı birden fazla alan olabilir (ör. cezaevi seçilince kilitlenen ilçe ve onun gizli kopyası): dolu olan alınır.
    function valueOf(name) {
        return [...form.querySelectorAll(`[name="${name}"]`)].map(el => el.value?.trim()).find(Boolean) ?? "";
    }

    function addressText(prefix) {
        const place = [valueOf(prefix + ".District"), valueOf(prefix + ".City")].filter(Boolean).join(" / ");
        return [valueOf(prefix + ".Line"), place].filter(Boolean).join(", ");
    }

    // Tahmini ağırlık (domain'deki LetterWeightPolicy ile aynı): zarf + mektup sayfaları + fotoğraflar + kartpostallar
    // + belge yaprakları (PDF'in sayfa sayısı, görsel 1); yukarı yuvarlanır.
    function estimateWeight() {
        const w = cfg.weights;
        const docSheets = [...document.querySelectorAll("#documentList li")].reduce((sum, li) => sum + (Number(li.dataset.pages) || 1), 0);
        const grams = w.envelope + pageCount(field("Content").value) * w.letterSheet + cfg.photoCount * w.photo
            + selectedPostcards().length * w.postcard + docSheets * w.documentSheet;
        return Math.ceil(grams - 1e-9);
    }

    const dash = () => { const s = document.createElement("span"); s.className = "text-muted"; s.textContent = "Yok"; return s; };

    function renderReview() {
        if (!$("rvScent")) return;
        // Zarf: yönetim panelindeki zarf çıktısıyla aynı satırlar.
        const place = (prefix) => [[valueOf(prefix + ".District"), valueOf(prefix + ".City")].filter(Boolean).join(" / "), valueOf(prefix + ".PostalCode")]
            .filter(Boolean).join(" ");
        $("pvSender").textContent = field("SenderName").value.trim() || "Gönderen adı";
        $("pvSenderLine").textContent = valueOf("SenderAddress.Line");
        $("pvSenderPlace").textContent = place("SenderAddress");
        $("pvRecipient").textContent = field("RecipientName").value.trim() || "Alıcı adı";
        const type = field("RecipientType").value; // 0 standart, 1 asker, 2 cezaevi
        const note = type === "2"
            ? [valueOf("RecipientFatherName") && `Baba adı: ${valueOf("RecipientFatherName")}`, valueOf("RecipientWard") && `Koğuş: ${valueOf("RecipientWard")}`].filter(Boolean).join(" · ")
            : "";
        $("pvRecipientNote").textContent = note;
        $("pvRecipientDetail").textContent = type === "0" ? "" : valueOf("RecipientDetail");
        $("pvRecipientLine").textContent = valueOf("RecipientAddress.Line");
        $("pvRecipientPlace").textContent = place("RecipientAddress");
        $("pvWeight").textContent = cfg.weights ? `${estimateWeight()} g` : "—";

        $("rvScent").textContent = field("ScentId").value ? selectedText("ScentId") : "Kokusuz";

        const cards = selectedPostcards().map(input => {
            const tile = input.closest(".postcard-option");
            const box = document.createElement("div");
            box.className = "review-postcard";
            const img = document.createElement("img");
            img.src = tile?.querySelector("img")?.src ?? ""; img.alt = "";
            const name = document.createElement("div");
            name.className = "text-truncate"; name.textContent = input.dataset.title ?? "";
            box.append(img, name);
            return box;
        });
        $("rvPostcards").replaceChildren(...(cards.length ? cards : [dash()]));

        // Fotoğraflar Ekstralar'daki listeden alınır; burada da tıklayınca büyür (data-gallery).
        const photos = [...document.querySelectorAll("#photoList .attach-photo img")].map(src => {
            const img = document.createElement("img");
            img.src = src.src; img.alt = src.alt; img.loading = "lazy";
            return img;
        });
        $("rvPhotos").replaceChildren(...(photos.length ? photos : [dash()]));

        const docs = [...document.querySelectorAll("#documentList li a")].map(a => {
            const li = document.createElement("li");
            const link = document.createElement("a");
            link.href = a.href; link.target = "_blank"; link.rel = "noopener"; link.textContent = `📄 ${a.textContent}`;
            li.append(link);
            return li;
        });
        $("rvDocuments").replaceChildren(...(docs.length ? docs : [Object.assign(document.createElement("li"), { textContent: "Yok", className: "text-muted" })]));
    }

    // ---------------- Adımlar (sekmeler) ----------------
    // 1 Mektup → 2 Ekstralar → 3 İletişim bilgileri → 4 Kontrol → 5 Ödeme. "Devam et" o adımı doğrular; geri her zaman dönülebilir.
    const stepEls = [...form.querySelectorAll(".wizard-step")];
    const tabEls = [...form.querySelectorAll("[data-step-tab]")];
    let current = 1;

    // Adımdaki görünür alanlar: önce tarayıcının "required" kontrolü, sonra jQuery doğrulama (data-val) kuralları.
    function validateStep(n) {
        if (n === 1) {
            const empty = !field("Content").value.trim();
            $("contentError").hidden = !empty;
            if (empty) field("Content").focus();
            return !empty;
        }
        const fields = [...stepEls[n - 1].querySelectorAll("input, select, textarea")]
            .filter(el => el.type !== "hidden" && !el.disabled && el.offsetParent !== null);
        // Alanı olmayan adım (Kontrol) her zaman geçerlidir; jQuery doğrulama boş listede hata verir.
        if (fields.length === 0) return true;
        const invalid = fields.find(el => !el.checkValidity());
        if (invalid) { invalid.reportValidity(); return false; }
        if (window.jQuery?.fn.valid && !window.jQuery(fields).valid()) {
            form.querySelector(".input-validation-error")?.focus();
            return false;
        }
        return true;
    }

    function showStep(n, focusTop = true) {
        current = n;
        stepEls.forEach(s => s.classList.toggle("d-none", Number(s.dataset.step) !== n));
        tabEls.forEach(t => {
            const i = Number(t.dataset.stepTab);
            t.classList.toggle("active", i === n);
            t.classList.toggle("done", i < n);
            t.setAttribute("aria-selected", i === n ? "true" : "false");
        });
        // Mektup sekmesinde "Taslak Kaydet" yok (taslak "Devam et" ile kaydedilir); Kontrol sekmesinde de gereksiz.
        $("saveDraftBox").classList.toggle("d-none", n === 1 || n === 4);
        // Ödeme sekmesi iki sütunlu (kalemler + kart formu): sağdaki tahmini tutar kartı gizlenir, alan genişler.
        const paying = n === 5;
        $("wizardSide").classList.toggle("d-none", paying);
        $("wizardMain").classList.toggle("col-lg-8", !paying);
        $("wizardMain").classList.toggle("col-lg-12", paying);
        render();
        if (focusTop) form.querySelector(".wizard-tabs").scrollIntoView({ behavior: "smooth", block: "start" });
    }

    // ---------------- Mektup sekmesinden çıkarken taslak kaydı ----------------
    // Sayfadan ayrılmadan kaydedilir (alıcı / gönderen henüz boş olabilir). Yeni mektupta dönen kimlik forma yazılır,
    // sonraki kayıtlar aynı taslağı günceller; adres çubuğu düzenleme sayfasına çevrilir (yenilemede kaybolmasın).
    const draftStatus = $("draftStatus");
    let saving = null;

    async function saveDraft() {
        if (saving) return saving;
        draftStatus.className = "small text-muted";
        draftStatus.textContent = "Taslak kaydediliyor…";
        saving = (async () => {
            try {
                const res = await fetch(form.dataset.draftUrl, { method: "POST", body: new FormData(form), headers: { "Accept": "application/json" } });
                const data = await res.json().catch(() => ({}));
                if (!res.ok) throw new Error(data.message || "Taslak kaydedilemedi.");
                field("Id").value = data.id;
                if (data.editUrl && location.pathname !== data.editUrl) history.replaceState(null, "", data.editUrl);
                dirty = false;
                draftStatus.className = "small text-success";
                draftStatus.textContent = "✓ Taslak kaydedildi";
                return true;
            } catch (e) {
                draftStatus.className = "small text-danger";
                draftStatus.textContent = `Taslak kaydedilemedi: ${e.message} Sonra "Taslak Kaydet" ile tekrar deneyebilirsiniz.`;
                return false;
            } finally {
                saving = null;
            }
        })();
        return saving;
    }

    // ---------------- Fotoğraf ve belge (Ekstralar sekmesi, sayfa içinde) ----------------
    // Yükleme / silme sunucudaki uçlara JSON isteğiyle yapılır; dönen güncel liste ekrana çizilir, tutar güncellenir.
    const attach = $("attachments");
    const attachStatus = $("attachStatus");
    const token = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";

    function setAttachStatus(text, kind = "muted") {
        attachStatus.className = `small mt-2 text-${kind}`;
        attachStatus.textContent = text;
    }

    // Yükleme için mektubun kaydedilmiş olması gerekir (yeni mektupta "Devam et" bunu zaten yapar).
    async function ensureLetterId() {
        if (field("Id").value) return field("Id").value;
        return (await saveDraft()) ? field("Id").value : null;
    }

    // Kaldırma onayı (Bootstrap penceresi; yoksa tarayıcının onay kutusu). Kaldır'a basılırsa true.
    function confirmRemove(title, name) {
        const el = $("removeModal");
        if (!window.bootstrap || !el) return Promise.resolve(window.confirm(`${title}\n${name ?? ""}`));
        $("removeModalTitle").textContent = title;
        $("removeModalName").textContent = name ?? "";
        const modal = bootstrap.Modal.getOrCreateInstance(el);
        return new Promise(resolve => {
            let ok = false;
            const onOk = () => { ok = true; modal.hide(); };
            $("removeModalOk").addEventListener("click", onOk, { once: true });
            el.addEventListener("hidden.bs.modal", () => {
                $("removeModalOk").removeEventListener("click", onOk);
                resolve(ok);
            }, { once: true });
            modal.show();
        });
    }

    async function postAttachment(url, body) {
        const data = body ?? new FormData();
        data.append("__RequestVerificationToken", token());
        const res = await fetch(url, { method: "POST", body: data, headers: { "Accept": "application/json" } });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.message || "İşlem tamamlanamadı.");
        return json;
    }

    function renderAttachments({ photos, documents }) {
        const photoList = $("photoList"), addPhoto = $("photoAdd");
        photoList.querySelectorAll(".attach-photo").forEach(el => el.remove());
        photos.forEach(p => {
            const box = document.createElement("div");
            box.className = "attach-photo";
            box.dataset.id = p.id;
            const img = document.createElement("img");
            img.src = p.url; img.alt = p.name; img.loading = "lazy";
            const remove = document.createElement("button");
            remove.type = "button"; remove.className = "attach-remove"; remove.textContent = "×";
            remove.dataset.removePhoto = p.id;
            remove.setAttribute("aria-label", `${p.name} fotoğrafını kaldır`);
            box.append(img, remove);
            photoList.insertBefore(box, addPhoto);
        });
        const docList = $("documentList");
        docList.replaceChildren(...documents.map(d => {
            const li = document.createElement("li");
            li.className = "list-group-item d-flex align-items-center gap-2 px-0";
            li.dataset.id = d.id;
            li.dataset.pages = d.pageCount || 1;
            const icon = document.createElement("span"); icon.textContent = d.isPdf ? "📄" : "🖼️"; icon.setAttribute("aria-hidden", "true");
            const link = document.createElement("a");
            link.href = d.url; link.target = "_blank"; link.rel = "noopener"; link.className = "text-truncate"; link.textContent = d.name;
            li.append(icon, link);
            if (d.isPdf) { const pages = document.createElement("span"); pages.className = "text-muted small flex-shrink-0"; pages.textContent = `${d.pageCount} sayfa`; li.append(pages); }
            const remove = document.createElement("button");
            remove.type = "button"; remove.className = "btn btn-sm btn-link text-danger ms-auto"; remove.textContent = "Kaldır";
            remove.dataset.removeDocument = d.id;
            li.append(remove);
            return li;
        }));
        cfg.photoCount = photos.length;
        cfg.documentCount = documents.length;
        $("photoCountText").textContent = photos.length;
        $("documentCountText").textContent = documents.length;
        addPhoto.classList.toggle("disabled", photos.length >= Number(attach.dataset.maxPhotos));
        $("documentAdd").classList.toggle("disabled", documents.length >= Number(attach.dataset.maxDocuments));
        render();
    }

    // Dosyalar sırayla yüklenir; sınır, tür ve boyut önce tarayıcıda denetlenir (asıl denetim sunucuda).
    async function upload(files, kind) {
        const isPhoto = kind === "photo";
        const max = Number(isPhoto ? attach.dataset.maxPhotos : attach.dataset.maxDocuments);
        const maxBytes = Number(isPhoto ? attach.dataset.maxPhotoBytes : attach.dataset.maxDocumentBytes);
        const allowed = isPhoto ? /\.(jpe?g|png|webp)$/i : /\.(pdf|jpe?g|png)$/i;
        const id = await ensureLetterId();
        if (!id) { setAttachStatus("Önce taslak kaydedilmeli; lütfen tekrar deneyin.", "danger"); return; }

        let added = 0;
        for (const file of files) {
            const count = isPhoto ? cfg.photoCount : cfg.documentCount;
            if (count >= max) { setAttachStatus(`En fazla ${max} ${isPhoto ? "fotoğraf" : "belge"} eklenebilir.`, "danger"); break; }
            if (!allowed.test(file.name)) { setAttachStatus(`${file.name}: bu dosya türü eklenemez.`, "danger"); continue; }
            if (file.size > maxBytes) { setAttachStatus(`${file.name}: dosya en fazla ${Math.round(maxBytes / 1048576)} MB olabilir.`, "danger"); continue; }
            setAttachStatus(`${file.name} yükleniyor…`);
            const body = new FormData();
            body.append(isPhoto ? "photo" : "document", file);
            try {
                renderAttachments(await postAttachment(`/mektup/${id}/${isPhoto ? "fotograf" : "belge"}`, body));
                added++;
            } catch (e) {
                setAttachStatus(`${file.name}: ${e.message}`, "danger");
            }
        }
        if (added > 0) setAttachStatus(`✓ ${added} ${isPhoto ? "fotoğraf" : "belge"} eklendi.`, "success");
    }

    if (attach) {
        $("photoInput").addEventListener("change", async (e) => { const files = [...e.target.files]; e.target.value = ""; await upload(files, "photo"); });
        $("documentInput").addEventListener("change", async (e) => { const files = [...e.target.files]; e.target.value = ""; await upload(files, "document"); });
        attach.addEventListener("click", async (e) => {
            const photo = e.target.closest("[data-remove-photo]");
            const doc = e.target.closest("[data-remove-document]");
            if (!photo && !doc) return;
            // Önce onay: fotoğrafın / belgenin adı gösterilir.
            const name = photo ? photo.closest(".attach-photo")?.querySelector("img")?.alt : doc.closest("li")?.querySelector("a")?.textContent;
            if (!await confirmRemove(photo ? "Fotoğraf kaldırılsın mı?" : "Belge kaldırılsın mı?", name)) return;
            const id = field("Id").value;
            const url = photo ? `/mektup/${id}/fotograf/${photo.dataset.removePhoto}/sil` : `/mektup/${id}/belge/${doc.dataset.removeDocument}/sil`;
            (photo ?? doc).disabled = true;
            try {
                renderAttachments(await postAttachment(url));
                setAttachStatus(photo ? "Fotoğraf kaldırıldı." : "Belge kaldırıldı.");
            } catch (err) {
                (photo ?? doc).disabled = false;
                setAttachStatus(err.message, "danger");
            }
        });
        // Sınırdaysa ekle düğmeleri baştan kapalı.
        $("photoAdd").classList.toggle("disabled", cfg.photoCount >= Number(attach.dataset.maxPhotos));
        $("documentAdd").classList.toggle("disabled", cfg.documentCount >= Number(attach.dataset.maxDocuments));
    }

    // İleri bir sekmeye atlarken aradaki adımların hepsi doğrulanır; ilk eksik adımda durulur.
    // Mektup sekmesinden ileri geçilirken taslak kaydedilir (kayıt başarısız olsa da devam edilir, uyarı gösterilir).
    async function goTo(n) {
        // Ödeme sekmesinden geri dönülürken başlamış ödeme denemesi kapatılır (mektup yeniden düzenlenebilir).
        if (current === 5 && n < 5 && !(await leavePayment())) return;
        if (n <= current) return showStep(n);
        const leavingLetter = current === 1;
        for (let i = current; i < n; i++) {
            if (i !== current) showStep(i, false);
            if (!validateStep(i)) return;
        }
        if (leavingLetter || n === 5) await saveDraft();
        showStep(n);
        if (n === 5) await startCheckout();
    }

    tabEls.forEach(t => t.addEventListener("click", () => goTo(Number(t.dataset.stepTab))));
    form.querySelectorAll("[data-step-next]").forEach(b => b.addEventListener("click", () => goTo(current + 1)));
    form.querySelectorAll("[data-step-prev]").forEach(b => b.addEventListener("click", () => goTo(current - 1)));
    // Kontrol sekmesindeki "Düzenle" bağlantıları ilgili sekmeye döner.
    form.querySelectorAll("[data-goto-step]").forEach(b => b.addEventListener("click", () => goTo(Number(b.dataset.gotoStep))));

    // ---------------- Ödeme sekmesi ----------------
    // Solda sunucudan gelen kesin döküm (kampanya, kupon, MektupPuan); sağda PayTR'nin güvenli kart formu (çerçeve).
    // Ödeme başlatılınca mektup kilitlenir; kupon / puan / yöntem değişirse deneme kapatılıp yenisi başlatılır.
    const pay = { method: "Card", usePoints: false, coupon: null, oid: null, quote: null, busy: false };
    const letterId = () => field("Id").value;
    const jsonHeaders = { "Accept": "application/json" };

    function payStatus(text, kind = "muted") {
        $("payStatus").className = `small text-${kind}`;
        $("payStatus").textContent = text;
        $("payStatus").hidden = !text;
    }

    async function fetchQuote() {
        const qs = new URLSearchParams({ method: pay.method, usePoints: pay.usePoints });
        if (pay.coupon) qs.set("kupon", pay.coupon);
        const res = await fetch(`/mektup/${letterId()}/odeme/teklif?${qs}`, { headers: jsonHeaders });
        const data = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(data.message || "Tutar hesaplanamadı.");
        return data;
    }

    function renderQuote(q) {
        pay.quote = q;
        pay.method = q.method;
        $("payRecipient").textContent = [field("RecipientName").value.trim(), addressText("RecipientAddress")].filter(Boolean).join(" — ");
        $("payMethods").classList.toggle("d-none", !q.mobilePaymentAvailable && !q.bankTransferAvailable);
        $("payMethods").querySelectorAll("[data-method]").forEach(b => {
            const m = b.dataset.method;
            b.classList.toggle("d-none", (m === "Mobile" && !q.mobilePaymentAvailable) || (m === "BankTransfer" && !q.bankTransferAvailable));
            b.classList.toggle("btn-primary", m === q.method);
            b.classList.toggle("btn-outline-primary", m !== q.method);
        });
        const bankBtn = $("payMethods").querySelector("[data-method=BankTransfer]");
        bankBtn.textContent = q.bankTransferPercent > 0
            ? `🏦 Havale / EFT (%${q.bankTransferPercent.toLocaleString("tr-TR")} indirim)` : "🏦 Havale / EFT";
        const bank = q.method === "BankTransfer";
        const row = (label, amount, cls = "") => {
            const tr = document.createElement("tr");
            if (cls) tr.className = cls;
            const a = document.createElement("td"); a.textContent = label;
            const b = document.createElement("td"); b.className = "text-end"; b.textContent = amount;
            tr.append(a, b);
            return tr;
        };
        const rows = q.lines.map(l => row(l.description, money(l.amount)));
        if (q.campaign) rows.push(row(`🎉 Kampanya: ${q.campaign.title}${q.couponApplied ? ` (kupon ${q.couponCode})` : ""}`, `−${money(q.campaign.discount)}`, "text-success"));
        if (bank && q.bankTransferDiscount > 0)
            rows.push(row(`🏦 Havale / EFT indirimi (%${q.bankTransferPercent.toLocaleString("tr-TR")})`, `−${money(q.bankTransferDiscount)}`, "text-success"));
        if (q.pointsUsed > 0) rows.push(row(`MektupPuan (${q.pointsUsed.toLocaleString("tr-TR")} puan)`, `−${money(q.pointsDiscount)}`, "text-success"));
        $("payLines").replaceChildren(...rows);
        $("paySubtotalRow").classList.toggle("d-none", !(q.campaign || q.pointsUsed > 0 || (bank && q.bankTransferDiscount > 0)));
        $("paySubtotal").textContent = money(q.total);
        $("payTotal").textContent = money(q.amountToPay);

        const message = $("payCouponMessage");
        $("payCoupon").classList.toggle("is-valid", q.couponApplied);
        $("payCoupon").classList.toggle("is-invalid", !!q.couponMessage);
        $("payCouponRemove").classList.toggle("d-none", !q.couponCode);
        message.className = `small mt-1 ${q.couponMessage ? "text-danger" : q.couponApplied ? "text-success" : ""}`;
        message.textContent = q.couponMessage ?? (q.couponApplied ? "Kupon uygulandı." : "Kodsuz kampanyalar otomatik uygulanır.");

        $("payPointsBox").classList.toggle("d-none", !(q.pointsBalance > 0));
        $("payUsePoints").checked = q.usePoints;
        $("payPointsLabel").textContent = `MektupPuan kullan — bakiye ${q.pointsBalance.toLocaleString("tr-TR")} puan (= ${money(q.balanceValue)})`;
        $("payEarn").classList.toggle("d-none", !(q.pointsToEarn > 0));
        $("payEarn").textContent = `🎁 Bu mektup postaya verildiğinde ${q.pointsToEarn.toLocaleString("tr-TR")} MektupPuan (= ${money(q.pointsToEarnValue)}) kazanacaksınız.`;
        $("paySimulation").classList.toggle("d-none", bank || !q.providerIsSimulation);
        $("payNote").classList.toggle("d-none", bank || q.providerIsSimulation);
        $("payPanelTitle").textContent = bank ? "🏦 Havale / EFT bilgileri" : "🔒 Kart bilgileri";
    }

    // Havale / EFT: kart formu açılmaz; banka bilgileri ve "Siparişi Oluştur" gösterilir.
    function showBankTransfer(q) {
        const b = q.bankTransfer ?? {};
        $("payBankName").textContent = b.bankName ?? "";
        $("payBankHolder").textContent = b.accountHolder ?? "";
        $("payBankIban").textContent = b.iban ?? "";
        $("payBankAmount").textContent = money(q.amountToPay);
        $("payBankNote").textContent = b.note ?? "";
        $("payBankNote").classList.toggle("d-none", !b.note);
        $("payBankDays").textContent = `Havale ${b.validDays ?? ""} gün içinde hesabımıza geçmezse sipariş iptal olur ve mektubunuz taslaklarınıza döner.`;
        $("payBankBox").classList.remove("d-none");
    }

    async function postPayment(url, body) {
        const data = body ?? new FormData();
        data.append("__RequestVerificationToken", token());
        const res = await fetch(url, { method: "POST", body: data, headers: jsonHeaders });
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json.message || "Ödeme başlatılamadı.");
        return json;
    }

    // PayTR formu çerçeve yüksekliğini kendisi bildirir (iframeResizer); test modunda gerekmez.
    let resizerLoaded = null;
    function loadResizer() {
        resizerLoaded ??= new Promise(resolve => {
            const script = document.createElement("script");
            script.src = "https://www.paytr.com/js/iframeResizer.min.js";
            script.onload = resolve; script.onerror = resolve;
            document.head.append(script);
        });
        return resizerLoaded;
    }

    function resetPaymentPanel() {
        $("payFrameBox").classList.add("d-none");
        $("payFreeBox").classList.add("d-none");
        $("payBankBox").classList.add("d-none");
        $("payFrame").removeAttribute("src");
    }

    async function abandonPayment() {
        if (!pay.oid) return true;
        try {
            await postPayment(`/mektup/${letterId()}/odeme/vazgec`);
            pay.oid = null;
            return true;
        } catch {
            payStatus("Önceki ödeme denemesi kapatılamadı; lütfen sayfayı yenileyin.", "danger");
            return false;
        }
    }

    // Tutarı alır ve ödemeyi başlatır: tutar varsa kart formu açılır, yoksa (puan / kampanya) "Siparişi Tamamla".
    async function startCheckout() {
        if (pay.busy) return;
        pay.busy = true;
        resetPaymentPanel();
        payStatus("Ödeme formu hazırlanıyor…");
        try {
            if (!(await abandonPayment())) return;
            const q = await fetchQuote();
            renderQuote(q);
            if (q.amountToPay <= 0) {
                payStatus("");
                $("payFreeText").textContent = q.pointsUsed > 0
                    ? "Tutarın tamamı MektupPuan ile karşılanıyor; kart bilgisi gerekmez."
                    : "Kampanya indirimiyle ödenecek tutar yok; kart bilgisi gerekmez.";
                $("payFreeButton").textContent = q.pointsUsed > 0 ? "Puanla Öde ve Gönder" : "Ücretsiz Gönder";
                $("payFreeBox").classList.remove("d-none");
                return;
            }
            if (q.method === "BankTransfer") { payStatus(""); showBankTransfer(q); return; }
            const body = new FormData();
            body.append("method", pay.method);
            body.append("usePoints", pay.usePoints);
            if (q.couponApplied && q.couponCode) body.append("coupon", q.couponCode);
            const started = await postPayment(`/mektup/${letterId()}/odeme`, body);
            if (started.completed) { dirty = false; location.href = started.redirectUrl; return; }
            pay.oid = started.merchantOid;
            const frame = $("payFrame");
            if (!started.formUrl.startsWith("/")) {
                await loadResizer();
                frame.addEventListener("load", () => window.iFrameResize?.({}, "#payFrame"), { once: true });
            }
            frame.src = started.formUrl;
            $("payFrameBox").classList.remove("d-none");
            payStatus("");
        } catch (e) {
            payStatus(e.message, "danger");
        } finally {
            pay.busy = false;
        }
    }

    // Geri dönerken: kart bilgileri girilmiş olabilir; ödeme tamamlandıysa vazgeçilmemeli.
    async function leavePayment() {
        if (!pay.oid) return true;
        if (!window.confirm("Ödeme formu kapatılacak ve mektubunuz yeniden düzenlenebilir olacak.\nÖdemeyi tamamladıysanız geri dönmeyin; sonuç birkaç saniye içinde gelir.")) return false;
        const ok = await abandonPayment();
        if (ok) resetPaymentPanel();
        return ok;
    }

    $("payMethods").addEventListener("click", (e) => {
        const b = e.target.closest("[data-method]");
        if (!b || b.dataset.method === pay.method || pay.busy) return;
        pay.method = b.dataset.method;
        startCheckout();
    });
    $("payUsePoints").addEventListener("change", (e) => { pay.usePoints = e.target.checked; startCheckout(); });
    const applyCoupon = () => { pay.coupon = $("payCoupon").value.trim().toUpperCase() || null; startCheckout(); };
    $("payCouponApply").addEventListener("click", applyCoupon);
    $("payCoupon").addEventListener("keydown", (e) => { if (e.key === "Enter") { e.preventDefault(); applyCoupon(); } });
    $("payCouponRemove").addEventListener("click", () => { pay.coupon = null; $("payCoupon").value = ""; startCheckout(); });
    // Ücretsiz / puanla tamamlama ve havale ile sipariş: ikisi de ödemeyi tek istekle başlatıp yönlendirir.
    const submitOrder = (button) => async () => {
        button.disabled = true;
        try {
            const body = new FormData();
            body.append("method", pay.method);
            body.append("usePoints", pay.usePoints);
            if (pay.quote?.couponApplied && pay.quote.couponCode) body.append("coupon", pay.quote.couponCode);
            const done = await postPayment(`/mektup/${letterId()}/odeme`, body);
            dirty = false;
            location.href = done.redirectUrl ?? `/mektup/${letterId()}`;
        } catch (e) {
            button.disabled = false;
            payStatus(e.message, "danger");
        }
    };
    $("payBankButton").addEventListener("click", submitOrder($("payBankButton")));
    $("payFreeButton").addEventListener("click", submitOrder($("payFreeButton")));
    field("Content").addEventListener("input", () => { if (field("Content").value.trim()) $("contentError").hidden = true; });

    // Sunucu doğrulama hatasıyla dönülürse hatalı alanın bulunduğu adım açılır.
    const firstError = form.querySelector(".input-validation-error, .field-validation-error");
    // Alan bazlı hata yoksa (ör. adres alanları) ama özet hata gösteriyorsa iletişim bilgileri adımı açılır.
    const summaryHasErrors = form.querySelector(".validation-summary-errors li");
    const errorStep = firstError?.closest(".wizard-step") ?? (summaryHasErrors ? $("wizardStep3") : null);

    form.addEventListener("input", render);
    form.addEventListener("change", render);

    // Kaydedilmemiş değişiklik uyarısı
    let dirty = false;
    // Ödeme sekmesindeki alanlar (kupon, puan, yöntem) mektuba kaydedilecek veri değildir.
    form.addEventListener("input", (e) => { if (!e.target.closest("#wizardStep5")) dirty = true; });
    form.addEventListener("submit", () => dirty = false);
    // Tarayıcı kapatılır / yenilenirse tarayıcının kendi uyarısı çıkar (özel metin gösterilemez).
    // Ödeme sekmesinde uyarı yok: taslak bu sekmeye girerken kaydedilir, burada kaydedilecek veri olmaz.
    window.addEventListener("beforeunload", (e) => { if (dirty && current !== 5) e.preventDefault(); });

    // Sitedeki bir bağlantıyla ayrılırken: "Yaptığınız değişiklikler kaydedilsin mi?" (Kaydet / Kaydetme / Vazgeç).
    const leaveModalEl = $("leaveModal");
    const leaveModal = window.bootstrap && leaveModalEl ? bootstrap.Modal.getOrCreateInstance(leaveModalEl) : null;
    let leaveTo = null;

    document.addEventListener("click", (e) => {
        if (!dirty || current === 5 || !leaveModal || e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        const a = e.target.closest("a[href]");
        if (!a || a.target === "_blank" || a.hasAttribute("download") || a.closest("#leaveModal")) return;
        const url = new URL(a.href, location.href);
        if (url.origin !== location.origin || (url.pathname === location.pathname && url.hash)) return;
        e.preventDefault();
        leaveTo = url.href;
        $("leaveError").hidden = true;
        leaveModal.show();
    });

    $("leaveDiscard")?.addEventListener("click", () => {
        dirty = false;
        location.href = leaveTo;
    });

    $("leaveSave")?.addEventListener("click", async () => {
        const button = $("leaveSave");
        button.disabled = true;
        const ok = await saveDraft();
        button.disabled = false;
        if (ok) {
            location.href = leaveTo;
        } else {
            $("leaveError").textContent = draftStatus.textContent;
            $("leaveError").hidden = false;
        }
    });

    if (errorStep) showStep(Number(errorStep.dataset.step), false);
    else render();
    // "#odeme" ile açılınca (mektup sayfasındaki "Ödemeye Geç", başarısız ödemede "Tekrar Dene") doğrudan Ödeme sekmesi;
    // aradaki adımlar yine doğrulanır, eksik varsa o adımda kalınır.
    if (!errorStep && location.hash === "#odeme" && field("Id").value) goTo(5);
})();
