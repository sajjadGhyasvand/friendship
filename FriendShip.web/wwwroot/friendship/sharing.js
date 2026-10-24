(() => {
    "use strict";
    const status = document.querySelector("[data-share-status]");
    function report(message) { if (status) status.textContent = message; }
    async function copy(id) {
        const input = document.getElementById(id);
        try { await navigator.clipboard.writeText(input.value); report("لینک کپی شد؛ بفرستش برای دوستات!"); }
        catch { input.focus(); input.select(); report("لینک انتخاب شد؛ با گزینه کپی مرورگر بردارش."); }
    }
    document.querySelectorAll("[data-copy-link]").forEach(button => button.addEventListener("click", () => copy(button.dataset.copyLink)));
    document.querySelectorAll("[data-share-link]").forEach(button => button.addEventListener("click", async () => {
        const input = document.getElementById(button.dataset.shareLink);
        if (!navigator.share) { await copy(button.dataset.shareLink); return; }
        try { await navigator.share({ title: "چقدر منو می‌شناسی؟", text: "چند تا حدس بزن؛ شاید غافلگیر بشیم!", url: input.value }); }
        catch (error) { if (error.name !== "AbortError") report("اشتراک باز نشد؛ لینک رو کپی کن."); }
    }));
    const drafts = document.querySelector("[data-clear-drafts]");
    if (drafts) {
        try { ["first", "new-round"].forEach(type => sessionStorage.removeItem(`friendship:profile-${type}-${drafts.dataset.clearDrafts}`)); } catch { }
    }
})();
