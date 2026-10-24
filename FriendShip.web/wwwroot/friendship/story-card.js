(() => {
    "use strict";
    const buttons = document.querySelectorAll("[data-story-card]");
    const status = document.querySelector("[data-card-status]");
    const text = message => { if (status) status.textContent = message; };
    const loadImage = src => new Promise((resolve, reject) => {
        const image = new Image(); image.onload = () => resolve(image); image.onerror = reject; image.src = src;
    });
    function wrap(ctx, content, x, y, maxWidth, lineHeight) {
        let line = "";
        for (const word of content.split(/\s+/)) {
            const next = line ? `${line} ${word}` : word;
            if (ctx.measureText(next).width > maxWidth && line) { ctx.fillText(line, x, y, maxWidth); y += lineHeight; line = word; }
            else line = next;
        }
        if (line) ctx.fillText(line, x, y, maxWidth);
        return y;
    }
    function fitText(ctx, content, y, maxWidth, maxHeight, startSize, weight) {
        let size = startSize, lineHeight = size * 1.6;
        for (; size >= 24; size -= 2) {
            ctx.font = `${weight} ${size}px "Vazirmatn"`;
            let line = "", count = 1;
            for (const word of content.split(/\s+/)) {
                const next = line ? `${line} ${word}` : word;
                if (ctx.measureText(next).width > maxWidth && line) { count++; line = word; }
                else line = next;
            }
            lineHeight = size * 1.6;
            if (count * lineHeight <= maxHeight) break;
        }
        wrap(ctx, content, 540, y, maxWidth, lineHeight);
    }
    async function render(button) {
        const fact = button.dataset.cardKind === "fact";
        const percent = Number(button.dataset.cardPercent);
        if (!fact && (!Number.isFinite(percent) || percent < 0 || percent > 100)) throw new Error("Invalid result");
        const faces = await document.fonts.load('800 80px "Vazirmatn"');
        if (!faces.length) throw new Error("Persian font unavailable");
        await document.fonts.load('500 46px "Vazirmatn"');
        await document.fonts.ready;
        const mascot = await loadImage(button.dataset.cardMascot);
        const canvas = document.createElement("canvas"); canvas.width = 1080; canvas.height = 1920;
        const ctx = canvas.getContext("2d");
        const gradient = ctx.createLinearGradient(0, 0, 1080, 1920);
        gradient.addColorStop(0, "#10143A"); gradient.addColorStop(0.7, "#24145B"); gradient.addColorStop(1, "#10143A");
        ctx.fillStyle = gradient; ctx.fillRect(0, 0, 1080, 1920);
        ctx.fillStyle = "#35F58A26"; ctx.beginPath(); ctx.arc(60, 70, 220, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#B94BFF30"; ctx.beginPath(); ctx.arc(1030, 1120, 230, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#F8F7FF"; ctx.textAlign = "center"; ctx.direction = "rtl";
        ctx.font = '800 82px "Vazirmatn"'; ctx.fillText("چقدر منو می‌شناسی؟", 540, 300);
        ctx.font = '600 54px "Vazirmatn"';
        wrap(ctx, button.dataset.cardOwner || "", 540, 410, 850, 70);
        if (fact) {
            ctx.fillStyle = "#FFD85A";
            fitText(ctx, button.dataset.cardTitle || "", 560, 860, 190, 62, 800);
            ctx.fillStyle = "#F8F7FF";
            fitText(ctx, button.dataset.cardMessage || "", 790, 830, 340, 46, 500);
            const height = 470, width = height * mascot.width / mascot.height;
            ctx.drawImage(mascot, (1080 - width) / 2, 1140, width, height);
        } else {
            ctx.fillStyle = "#FFD85A"; ctx.font = '900 184px "Vazirmatn"';
            ctx.fillText(new Intl.NumberFormat("fa-IR", { maximumFractionDigits: 1 }).format(percent) + "٪", 540, 750);
            ctx.fillStyle = "#6EF5D2"; ctx.font = '700 50px "Vazirmatn"';
            wrap(ctx, `${button.dataset.cardName || ""} · حدس درست`, 540, 850, 860, 65);
            const height = 590, width = height * mascot.width / mascot.height;
            ctx.drawImage(mascot, (1080 - width) / 2, 925, width, height);
        }
        ctx.fillStyle = "#F8F7FF"; ctx.font = '500 44px "Vazirmatn"';
        wrap(ctx, fact ? `${button.dataset.cardSource} · ${new Intl.NumberFormat("fa-IR").format(Number(button.dataset.cardSample))} پاسخ پشتیبان` : button.dataset.cardMessage || "", 540, 1650, 860, 64);
        const publicUrl = new URL(button.dataset.cardUrl, location.href);
        ctx.fillStyle = "#6EF5D2"; ctx.font = '600 38px "Vazirmatn"'; ctx.direction = "ltr";
        wrap(ctx, publicUrl.host + publicUrl.pathname + publicUrl.search, 540, 1780, 920, 48);
        ctx.font = '500 30px "Vazirmatn"'; ctx.direction = "rtl"; ctx.fillStyle = "#D5D1EB";
        ctx.fillText("رفاقت بیشتر از یک امتیازه ✦", 540, 1860);
        return canvas;
    }
    buttons.forEach(button => button.addEventListener("click", async () => {
        button.disabled = true; text("در حال آماده کردن کارت…");
        try {
            const canvas = await render(button);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/png"));
            if (!blob) throw new Error("Export failed");
            const url = URL.createObjectURL(blob);
            const link = document.createElement("a"); link.href = url; link.download = "friendship-story.png";
            document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 60000);
            text("کارت ۱۰۸۰ × ۱۹۲۰ آماده شد؛ می‌تونی توی استوری بذاریش.");
        } catch { text("کارت آماده نشد. اتصال و بارگذاری فونت یا تصویر را بررسی کن و دوباره بزن."); }
        finally { button.disabled = false; }
    }));

    // A small opt-in registry connects future illustrations to stable server visual keys.
    // Empty or unavailable registry means emoji, never a broken image placeholder.
    const visualElements = [...document.querySelectorAll("[data-visual-key]")];
    const narrativeButtons = [...document.querySelectorAll("[data-narrative-story]")];
    const registry = visualElements.length || narrativeButtons.length ? fetch(new URL("visual-assets.json", document.currentScript.src))
        .then(r => r.ok ? r.json() : {}).catch(() => ({})) : Promise.resolve({});
    async function visual(key) {
        const assets = await registry;
        const value = assets && typeof assets === "object" && !Array.isArray(assets) ? assets[key] : null;
        if (typeof value !== "string") return null;
        try {
            const url = new URL(value, location.origin);
            if (url.origin !== location.origin || !url.pathname.startsWith("/images/")) return null;
            return await loadImage(url.href);
        } catch { return null; }
    }
    visualElements.forEach(async element => {
        const image = await visual(element.dataset.visualKey);
        if (image) { image.alt = ""; element.replaceChildren(image); }
    });
    async function renderNarrative(button) {
        const story = JSON.parse(document.getElementById(button.dataset.narrativeStory).textContent);
        if (!Array.isArray(story.Facts) || story.Facts.length < 1 || story.Facts.length > 4) throw new Error("Invalid story");
        const faces = await document.fonts.load('800 64px "Vazirmatn"');
        if (!faces.length) throw new Error("Font unavailable");
        await document.fonts.load('500 40px "Vazirmatn"'); await document.fonts.ready;
        const visuals = await Promise.all([visual(story.VisualKey), ...story.Facts.map(f => visual(f.VisualKey))]);
        const canvas = document.createElement("canvas"); canvas.width = 1080; canvas.height = 1920;
        const ctx = canvas.getContext("2d");
        const gradient = ctx.createLinearGradient(0, 0, 1080, 1920);
        gradient.addColorStop(0, "#162d3f"); gradient.addColorStop(.45, "#201943"); gradient.addColorStop(1, "#40225e");
        ctx.fillStyle = gradient; ctx.fillRect(0, 0, 1080, 1920);
        ctx.fillStyle = "#76f5d817"; ctx.beginPath(); ctx.arc(1040, 250, 310, 0, Math.PI * 2); ctx.fill();
        ctx.fillStyle = "#f986e81a"; ctx.beginPath(); ctx.arc(0, 1500, 350, 0, Math.PI * 2); ctx.fill();
        ctx.direction = "rtl"; ctx.textAlign = "center";
        function box(x, y, w, h, fill, stroke) {
            ctx.beginPath(); ctx.roundRect(x, y, w, h, 34); ctx.fillStyle = fill; ctx.fill();
            if (stroke) { ctx.strokeStyle = stroke; ctx.lineWidth = 2; ctx.stroke(); }
        }
        function icon(image, emoji, x, y, size) {
            if (image) {
                const scale = size / Math.max(image.width, image.height);
                const w = image.width * scale, h = image.height * scale;
                ctx.drawImage(image, x - w / 2, y - h / 2, w, h);
            } else {
                ctx.font = `${size * .8}px "Segoe UI Emoji", "Apple Color Emoji", sans-serif`;
                ctx.fillStyle = "#fff"; ctx.textBaseline = "middle"; ctx.fillText(emoji || "✨", x, y); ctx.textBaseline = "alphabetic";
            }
        }
        ctx.fillStyle = "#f8f7ff"; ctx.font = '900 52px "Vazirmatn"'; ctx.fillText("فرندشیپ ✦", 540, 145);
        ctx.fillStyle = "#d7cbe9"; ctx.font = '500 32px "Vazirmatn"';
        ctx.fillText(story.Kind === "friends" ? "دوستام چقدر منو می‌شناسن؟" : "نسخهٔ غیررسمی من، از انتخاب‌های خودم", 540, 208, 900);
        ctx.fillStyle = "#c3bad6"; fitText(ctx, story.OwnerName, 280, 860, 65, 36, 600);
        icon(visuals[0], story.Emoji, 540, 399, 146);
        ctx.fillStyle = "#ffda71"; fitText(ctx, story.Title, 555, 900, 190, 76, 900);
        ctx.fillStyle = "#ece3f7"; fitText(ctx, story.Description, 737, 880, 170, 40, 500);
        const count = story.Facts.length, rowHeight = 136, gap = 20;
        // Fixed bands keep all four facts above the footer. Fewer facts stay centred in this band.
        const start = 948 + ((4 - count) * (rowHeight + gap)) / 2;
        for (let i = 0; i < count; i++) {
            const y = start + i * (rowHeight + gap), fact = story.Facts[i];
            box(76, y, 928, rowHeight, "#ffffff0d", "#ffffff22");
            icon(visuals[i + 1], fact.Emoji, 924, y + rowHeight / 2, 72);
            ctx.fillStyle = "#faf8ff";
            ctx.save(); ctx.translate(-40, 0);
            fitText(ctx, fact.Text, y + 48, 720, 96, 38, 650); ctx.restore();
        }
        ctx.fillStyle = "#76f5d8"; fitText(ctx, story.PositiveLine, 1645, 900, 100, 38, 700);
        ctx.fillStyle = "#ded1ed"; fitText(ctx, story.Invitation, 1760, 880, 85, 32, 500);
        const publicUrl = new URL(button.dataset.cardUrl, location.href);
        ctx.direction = "ltr"; ctx.font = '500 28px "Vazirmatn"'; ctx.fillStyle = "#bfb0d5";
        ctx.fillText(publicUrl.host + publicUrl.pathname, 540, 1860, 920);
        return canvas;
    }
    narrativeButtons.forEach(button => button.addEventListener("click", async () => {
        narrativeButtons.forEach(b => b.disabled = true); text("داریم استوری رو آماده می‌کنیم…");
        try {
            const canvas = await renderNarrative(button);
            const blob = await new Promise(resolve => canvas.toBlob(resolve, "image/png"));
            if (!blob) throw new Error("Export failed");
            const file = new File([blob], "friendship-story.png", { type: "image/png" });
            if (button.hasAttribute("data-share-image") && navigator.canShare?.({ files: [file] })) {
                await navigator.share({ files: [file], title: "استوری من در فرندشیپ" }); text("تصویر برای اشتراک آماده شد.");
            } else {
                const url = URL.createObjectURL(blob), link = document.createElement("a");
                link.href = url; link.download = file.name; document.body.append(link); link.click(); link.remove();
                setTimeout(() => URL.revokeObjectURL(url), 60000);
                text("تصویر دانلود شد؛ حالا می‌تونی توی استوری بذاریش.");
            }
        } catch (error) { if (error.name !== "AbortError") text("تصویر آماده نشد؛ بارگذاری فونت و اتصال را بررسی کن و دوباره بزن."); }
        finally { narrativeButtons.forEach(b => b.disabled = false); }
    }));
})();
