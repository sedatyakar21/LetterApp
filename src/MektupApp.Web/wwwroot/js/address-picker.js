// Adres formları (_AddressFields.cshtml):
//  - İl seçilince ilçe listesini yeniler (ilk açılışta listeler sunucuda doldurulur).
//  - Alıcı türü "Cezaevi" ise il etiketi "Alıcı Cezaevi" olur, ilin cezaevleri ayrı listede gösterilir;
//    kurum seçilince ilçe, adres, posta kodu ve (boşsa) "Kurum ve koğuş bilgisi" otomatik dolar.
(function () {
    const pickers = document.querySelectorAll("[data-address-picker]");
    if (!pickers.length) return;

    let geo = null;
    function loadGeo() {
        geo ??= fetch("/data/il-ilce.json")
            .then(r => r.ok ? r.json() : [])
            .then(list => new Map(list.map(p => [p.name, p.districts.slice().sort((a, b) => a.localeCompare(b, "tr"))])))
            .catch(() => new Map());
        return geo;
    }

    const prisonCache = new Map();
    function loadPrisons(city) {
        if (!city) return Promise.resolve([]);
        if (!prisonCache.has(city)) {
            prisonCache.set(city, fetch("/api/mobile/prisons?city=" + encodeURIComponent(city))
                .then(r => r.ok ? r.json() : [])
                .catch(() => []));
        }
        return prisonCache.get(city);
    }

    // Türkçe karakter ve büyük/küçük harf farkını yok sayan karşılaştırma anahtarı.
    const fold = s => (s || "").trim().replace(/I/g, "ı").replace(/İ/g, "i").toLocaleLowerCase("tr")
        .replace(/ç/g, "c").replace(/ğ/g, "g").replace(/ı/g, "i").replace(/ö/g, "o").replace(/ş/g, "s").replace(/ü/g, "u")
        .replace(/\s+/g, " ");

    const fire = el => el && el.dispatchEvent(new Event("change", { bubbles: true }));

    pickers.forEach(picker => {
        const city = picker.querySelector("[data-address-city]");
        const district = picker.querySelector("[data-address-district]");
        const line = picker.querySelector("[data-address-line]");
        const postal = picker.querySelector("[data-address-postal]");
        if (!city || !district) return;

        async function fillDistricts() {
            const map = await loadGeo();
            const items = map.get(city.value) || [];
            district.replaceChildren(new Option(city.value ? "İlçe seçin" : "Önce il seçin", ""));
            items.forEach(d => district.add(new Option(d, d)));
            district.value = "";
            fire(district);
        }

        // ---------------- Cezaevi seçimi ----------------
        const prisonRow = picker.querySelector("[data-prison-row]");
        const prisonSelect = picker.querySelector("[data-prison-select]");
        const cityLabel = picker.querySelector("[data-city-label]");
        const typeSelect = picker.dataset.typeSelect ? document.querySelector(picker.dataset.typeSelect) : null;
        const detail = picker.dataset.detailInput ? document.querySelector(picker.dataset.detailInput) : null;
        let prisons = [];

        const isInmate = () => typeSelect ? typeSelect.value === picker.dataset.inmateValue : picker.dataset.inmate === "true";

        async function fillPrisons(preselect) {
            if (!prisonSelect) return;
            prisons = city.value ? await loadPrisons(city.value) : [];
            prisonSelect.replaceChildren(new Option(!city.value ? "Önce il seçin" : prisons.length ? "Cezaevi seçin" : "Bu ilde kayıtlı cezaevi yok", ""));
            prisons.forEach(p => prisonSelect.add(new Option(p.name, p.id)));
            prisonSelect.add(new Option("Listede yok, adresi elle gireceğim", "manual"));
            if (preselect) {
                // Kayıtlı adres bir kuruma aitse onu seçili göster (ve alanları kilitle).
                const hit = prisons.find(p => p.addressLine && fold(p.addressLine) === fold(line?.value) && p.district === district.value);
                prisonSelect.value = hit ? hit.id : (line?.value ? "manual" : "");
                lock(isInmate() ? hit || null : null);
            }
        }

        // Kurum seçiliyken ilçe ve adres düzenlenemez. Devre dışı bir select form ile gönderilmediği için
        // değeri aynı adlı gizli bir alanla taşınır; adres alanı salt okunur yapılır.
        const districtMirror = document.createElement("input");
        districtMirror.type = "hidden";
        district.after(districtMirror);

        function lock(p) {
            const lockDistrict = !!(p && p.district);
            const lockLine = !!(p && p.addressLine);
            district.disabled = lockDistrict;
            districtMirror.name = lockDistrict ? district.name : "";
            districtMirror.value = lockDistrict ? district.value : "";
            if (line) {
                line.readOnly = lockLine;
                line.classList.toggle("bg-body-secondary", lockLine);
            }
            district.title = lockDistrict ? "Cezaevi seçildiği için değiştirilemez" : "";
            if (line) line.title = lockLine ? "Cezaevi seçildiği için değiştirilemez" : "";
        }

        function applyPrison() {
            const p = prisons.find(x => x.id === prisonSelect.value);
            if (!p) { lock(null); return; }
            if (p.district) {
                if (![...district.options].some(o => o.value === p.district)) district.add(new Option(p.district, p.district));
                district.value = p.district;
                fire(district);
            }
            if (line && p.addressLine) { line.value = p.addressLine; fire(line); }
            if (postal) postal.value = p.postalCode || "";
            // Kurum adı gizli ek bilgi alanında saklanır (zarfa ve PTT kaydına basılır).
            if (detail) {
                detail.value = p.name;
                detail.dispatchEvent(new Event("input", { bubbles: true }));
            }
            lock(p);
        }

        function updateMode() {
            if (!prisonRow) return;
            const on = isInmate();
            prisonRow.hidden = !on;
            // Cezaevi dışı alıcıda adres serbestçe yazılır.
            if (!on) lock(null);
            else lock(prisons.find(x => x.id === prisonSelect.value) || null);
            if (cityLabel) cityLabel.textContent = on ? cityLabel.dataset.prisonText : cityLabel.dataset.defaultText;
        }

        city.addEventListener("change", async () => {
            if (prisonSelect) lock(null);
            await fillDistricts();
            if (line && prisonSelect && isInmate() && prisonSelect.value && prisonSelect.value !== "manual") line.value = "";
            await fillPrisons(false);
        });

        if (prisonSelect) {
            prisonSelect.addEventListener("change", applyPrison);
            typeSelect?.addEventListener("change", updateMode);
            updateMode();
            fillPrisons(true);
        }

        // Adresi dışarıdan yazar (ör. önceki mektuptan doldurma): il, ilçe listesi, adres, posta kodu ve cezaevi kilidi.
        picker._setAddress = async (a) => {
            if (prisonSelect) lock(null);
            if (a.city && ![...city.options].some(o => o.value === a.city)) city.add(new Option(a.city, a.city));
            city.value = a.city || "";
            await fillDistricts();
            if (a.district && ![...district.options].some(o => o.value === a.district)) district.add(new Option(a.district, a.district));
            district.value = a.district || "";
            if (line) line.value = a.line || "";
            if (postal) postal.value = a.postalCode || "";
            if (prisonSelect) { await fillPrisons(true); updateMode(); }
            fire(district);
        };
    });

    window.addressPicker = {
        set: (picker, address) => picker?._setAddress ? picker._setAddress(address) : Promise.resolve()
    };
})();
