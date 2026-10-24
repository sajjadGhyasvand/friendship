(() => {
    "use strict";
    document.querySelectorAll("form[data-fact-feedback]").forEach(form => form.addEventListener("submit", async event => {
        if (form.dataset.busy) { event.preventDefault(); return; }
        if (!event.submitter) return;
        event.preventDefault();
        const data = new FormData(form), choice = event.submitter.value;
        data.set("state", choice);
        const buttons = [...form.querySelectorAll("button[name=state]")];
        const status = form.querySelector("[data-feedback-status]");
        form.dataset.busy = "true"; buttons.forEach(b => b.disabled = true);
        status.textContent = "داریم نظرت رو ثبت می‌کنیم…";
        try {
            const response = await fetch(form.action, { method: "POST", body: data, headers: { Accept: "application/json" }, credentials: "same-origin" });
            const result = await response.json();
            if (!response.ok || !result.succeeded) throw new Error(result.error || "ثبت نشد؛ دوباره امتحان کن.");
            buttons.forEach(b => b.setAttribute("aria-pressed", String(b.value === result.state)));
            form.closest("[data-fact-row]").classList.toggle("is-rejected", result.state === "Rejected");
            if (result.portrait) {
                const entries = { title: "nickname", description: "description", emoji: "emoji", humor: "humorLine", coverage: "coverage" };
                Object.entries(entries).forEach(([key, property]) => {
                    const element = document.querySelector(`[data-portrait-${key}]`);
                    if (element) element.textContent = key === "humor" && result.portrait.humorLine === result.portrait.description ? "" :
                        (key === "coverage" ? "✦ " : "") + (result.portrait[property] || "");
                    if (key === "emoji" && element) element.dataset.visualKey = result.portrait.visualKey;
                });
                const strengths = document.querySelector("[data-portrait-strengths]");
                if (strengths) strengths.replaceChildren(...(result.portrait.strengths || []).slice(0, 2).map(value => {
                    const chip = document.createElement("span"); chip.textContent = "✦ " + value; return chip;
                }));
                const interests = document.querySelector("[data-portrait-interests]");
                if (interests) interests.textContent = (result.portrait.signatureInterests || []).join(" · ");
            }
            status.textContent = result.state === "Confirmed" ? "با تأیید تو؛ می‌تونی برای استوری انتخابش کنی." : result.state === "Partial" ? "تا حدی قبولش داری؛ برای لقب و استوری تأیید کامل لازم داریم." : "قبولش نداری؛ وارد استوری نمی‌شه. می‌تونی نظرت رو عوض کنی.";
        } catch (error) { status.textContent = error.message === "Failed to fetch" ? "اتصال قطع شد؛ نظرت هنوز ثبت نشده. دوباره بزن." : "ثبت نشد؛ صفحه را تازه کن و دوباره امتحان کن."; }
        finally { delete form.dataset.busy; buttons.forEach(b => b.disabled = false); }
    }));
    document.querySelectorAll("[data-discovery-share]").forEach(button => button.addEventListener("click", async () => {
        const status = button.parentElement.querySelector("[data-share-status]");
        try {
            const url = new URL(button.dataset.discoveryShare, location.href).href;
            if (navigator.share) await navigator.share({ title: "کشف من در فرندشیپ", url });
            else await navigator.clipboard.writeText(url);
            if (status) status.textContent = navigator.share ? "لینک آمادهٔ ارسال شد." : "لینک کپی شد.";
        } catch (error) {
            if (status && error.name !== "AbortError") status.textContent = "ارسال خودکار ممکن نشد؛ لینک را از نوار آدرس یا کادر لینک کپی کن.";
        }
    }));
})();
