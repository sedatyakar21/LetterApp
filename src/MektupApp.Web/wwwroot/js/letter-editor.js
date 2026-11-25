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

    // Ödeme adımındaki özet (alıcı, gönderen, seçimler ve fiyat kalemleri).
    function renderSummary(pages) {
        const address = (prefix) => [field(prefix + ".Line")?.value, [field(prefix + ".District")?.value, field(prefix + ".City")?.value].filter(Boolean).join(" / ")]
            .filter(Boolean).join(", ");
        const person = (name, prefix) => [field(name).value.trim(), address(prefix)].filter(Boolean).join(" — ") || "—";
        $("sumRecipient").textContent = person("RecipientName", "RecipientAddress");
        $("sumSender").textContent = person("SenderName", "SenderAddress");
        const cards = selectedPostcards().map(c => c.dataset.title);
        $("sumChoices").textContent = [
            `${selectedText("Paper")} kâğıt`, `${selectedText("Envelope")} zarf`, `${selectedText("Font")} / ${selectedText("Ink")}`,
            field("ScentId").value ? `${selectedText("ScentId")} kokusu` : "Kokusuz",
            cards.length ? `Kartpostal: ${cards.join(", ")}` : null,
            `${pages} sayfa`
        ].filter(Boolean).join(" · ");
        const body = $("sumLines");
        body.replaceChildren(...priceLines(pages).map(([label, amount]) => {
            const tr = document.createElement("tr");
            const a = document.createElement("td"); a.textContent = label;
            const b = document.createElement("td"); b.className = "text-end"; b.textContent = money(amount);
            tr.append(a, b);
            return tr;
        }));
        $("sumTotal").textContent = money(estimate(pages));
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

        // Editörün kendisi de seçilen kâğıt rengi, yazı stili ve mürekkeple görünür.
        const editor = field("Content");
        swapClass(editor, "paper-", enumNames.Paper[field("Paper").value]);
        swapClass(editor, "font-", enumNames.Font[field("Font").value]);
        swapClass(editor, "ink-", enumNames.Ink[field("Ink").value]);
        swapClass($("inkDot"), "ink-dot-", enumNames.Ink[field("Ink").value]);

        updateRecipientDetail();
        updatePostcards();
        renderSummary(pages);
    }

    // ---------------- Adımlar (sekmeler) ----------------
    // 1 Mektup → 2 Ekstralar → 3 İletişim bilgileri → 4 Ödeme. "Devam et" o adımı doğrular; geri her zaman dönülebilir.
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
        // Mektup sekmesinde sağdaki zarf / kâğıt önizlemesi gizlenir ve editör genişler (editörün kendisi önizlemedir).
        const writing = n === 1;
        $("letterPreview").classList.toggle("d-none", writing);
        // Mektup sekmesinde "Taslak Kaydet" yok: taslak "Devam et" ile kaydedilir.
        $("saveDraftBox").classList.toggle("d-none", writing);
        $("wizardMain").classList.toggle("col-lg-8", writing);
        $("wizardMain").classList.toggle("col-lg-6", !writing);
        $("wizardSide").classList.toggle("col-lg-4", writing);
        $("wizardSide").classList.toggle("col-lg-6", !writing);
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
        if (n <= current) return showStep(n);
        const leavingLetter = current === 1;
        for (let i = current; i < n; i++) {
            if (i !== current) showStep(i, false);
            if (!validateStep(i)) return;
        }
        if (leavingLetter) await saveDraft();
        showStep(n);
    }

    tabEls.forEach(t => t.addEventListener("click", () => goTo(Number(t.dataset.stepTab))));
    form.querySelectorAll("[data-step-next]").forEach(b => b.addEventListener("click", () => goTo(current + 1)));
    form.querySelectorAll("[data-step-prev]").forEach(b => b.addEventListener("click", () => showStep(current - 1)));
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
    form.addEventListener("input", () => dirty = true);
    form.addEventListener("submit", () => dirty = false);
    // Tarayıcı kapatılır / yenilenirse tarayıcının kendi uyarısı çıkar (özel metin gösterilemez).
    window.addEventListener("beforeunload", (e) => { if (dirty) e.preventDefault(); });

    // Sitedeki bir bağlantıyla ayrılırken: "Yaptığınız değişiklikler kaydedilsin mi?" (Kaydet / Kaydetme / Vazgeç).
    const leaveModalEl = $("leaveModal");
    const leaveModal = window.bootstrap && leaveModalEl ? bootstrap.Modal.getOrCreateInstance(leaveModalEl) : null;
    let leaveTo = null;

    document.addEventListener("click", (e) => {
        if (!dirty || !leaveModal || e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
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
})();
