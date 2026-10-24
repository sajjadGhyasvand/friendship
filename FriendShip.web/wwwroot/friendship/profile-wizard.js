(() => {
    "use strict";
    const form = document.querySelector("[data-profile-wizard]");
    if (!form) return;
    const questions = [...form.querySelectorAll("[data-wizard-step]")];
    const review = form.querySelector("[data-wizard-review]");
    const steps = [...questions, review];
    const key = `friendship:${form.dataset.storageKey}`;
    let current = 0;
    const formatter = new Intl.NumberFormat("fa-IR");
    try {
        const saved = JSON.parse(sessionStorage.getItem(key) || "null");
        if (saved && Date.now() - saved.time < 86400000) {
            form.querySelectorAll('input[type="radio"]').forEach(input => {
                input.checked = saved.answers[input.name] === input.value;
            });
            current = Math.min(saved.step || 0, steps.length - 1);
        }
    } catch { /* Draft storage is optional; server submission still works. */ }
    const save = () => {
        const answers = {};
        form.querySelectorAll('input[type="radio"]:checked').forEach(input => answers[input.name] = input.value);
        try { sessionStorage.setItem(key, JSON.stringify({ answers, step: current, time: Date.now() })); } catch { }
    };
    function renderReview() {
        const list = form.querySelector("[data-review-list]");
        list.replaceChildren();
        const categories = new Set();
        let count = 0;
        questions.forEach((step, index) => {
            const answer = step.querySelector('input:checked');
            if (!answer) return;
            count++;
            categories.add(step.dataset.category);
            const item = document.createElement("li");
            item.className = "live-review-item";
            const title = document.createElement("strong");
            title.textContent = step.dataset.prompt;
            const choice = document.createElement("p");
            choice.textContent = answer.parentElement.querySelector(".option-text").textContent;
            const edit = document.createElement("button");
            edit.type = "button";
            edit.className = "text-link";
            edit.textContent = "ویرایش";
            edit.addEventListener("click", () => show(index));
            item.append(title, choice, edit);
            list.append(item);
        });
        const valid = count >= 5 && categories.size >= 3;
        form.querySelector("[data-review-status]").textContent = valid
            ? `${formatter.format(count)} انتخاب از ${formatter.format(categories.size)} دسته؛ آماده‌ای!`
            : "حداقل ۵ سؤال از ۳ دسته را انتخاب کن.";
        form.querySelector("[data-publish]").disabled = !valid;
        return valid;
    }
    function show(index, focus = true) {
        current = Math.max(0, Math.min(index, steps.length - 1));
        steps.forEach((step, i) => step.hidden = i !== current);
        form.querySelector("[data-step-label]").textContent = current === questions.length
            ? "بازبینی انتخاب‌ها" : `مرحله ${formatter.format(current + 1)} از ${formatter.format(questions.length)}`;
        form.querySelector("[data-step-progress]").value = current + 1;
        form.querySelector("[data-wizard-back]").disabled = current === 0;
        form.querySelector("[data-wizard-next]").hidden = current === questions.length;
        if (current === questions.length) renderReview();
        if (focus) { const heading = steps[current].querySelector("h2"); heading.tabIndex = -1; heading.focus({ preventScroll: true }); }
        save();
    }
    form.querySelectorAll("[data-skip]").forEach((button, index) => {
        button.hidden = false;
        button.addEventListener("click", () => {
            questions[index].querySelectorAll("input").forEach(input => input.checked = false);
            show(index + 1);
        });
    });
    form.querySelector("[data-wizard-back]").addEventListener("click", () => show(current - 1));
    form.querySelector("[data-wizard-next]").addEventListener("click", () => show(current + 1));
    form.addEventListener("change", save);
    form.addEventListener("submit", event => {
        if (!renderReview()) { event.preventDefault(); show(questions.length); return; }
        form.setAttribute("aria-busy", "true");
        form.querySelector("[data-publish]").disabled = true;
        // Keep the draft until the dashboard confirms that publication succeeded.
    });
    form.querySelector("[data-wizard-meta]").hidden = false;
    form.querySelector("[data-wizard-navigation]").hidden = false;
    form.querySelector("[data-draft-hint]").hidden = false;
    show(current, false);
})();
