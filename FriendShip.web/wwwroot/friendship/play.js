(() => {
    "use strict";

    const form = document.querySelector("[data-live-play-form]");

    if (!form) {
        return;
    }

    const timer = document.querySelector("[data-play-timer]");
    const timerValue = document.querySelector("[data-timer-value]");
    const timeoutInput = form.querySelector("[data-timeout-input]");
    const submitButton = form.querySelector("[data-answer-button]");
    const status = form.querySelector("[data-submit-status]");

    const code = form.querySelector('[name="Code"]').value;
    const questionId = form.querySelector('[name="RoomQuestionId"]').value;
    const draftKey = 'friendship-answer:' + code + ':' + questionId;
    try {
        const selected = sessionStorage.getItem(draftKey);
        form.querySelectorAll('[name="OptionId"]').forEach(input => input.checked = input.value === selected);
    } catch { }
    function reflectSelection() {
        const selected = form.querySelector('[name="OptionId"]:checked');
        form.classList.toggle("has-selection", !!selected);
        status.textContent = selected ? "انتخابت آماده‌ست؛ ثبتش کن!" : "اول یه گزینه رو انتخاب کن";
    }
    reflectSelection();
    form.addEventListener("change", () => {
        const selected = form.querySelector('[name="OptionId"]:checked');
        try { if (selected) sessionStorage.setItem(draftKey, selected.value); } catch { }
        reflectSelection();
    });
    let submitting = false;
    let intervalId = null;
    let deadline = null;

    const formatter = new Intl.NumberFormat("fa-IR", {
        maximumFractionDigits: 0
    });

    if (timer) {
        const seconds = Number(timer.dataset.seconds);

        if (Number.isFinite(seconds) && seconds >= 0) {
            // Display estimate only; the server owns the actual deadline.
            deadline = performance.now() + seconds * 1000;
        }
    }

    function hasExpired() {
        return deadline !== null && performance.now() >= deadline;
    }

    function markTimeout() {
        timeoutInput.value = "true";

        // A timeout must be submitted even when no radio is selected.
        form.noValidate = true;
    }

    form.addEventListener("submit", (event) => {
        if (!navigator.onLine) {
            event.preventDefault();
            status.textContent = "اتصال قطعه؛ انتخابت محفوظ است. بعد از وصل شدن دوباره ثبت کن.";
            return;
        }
        if (submitting) {
            event.preventDefault();
            return;
        }

        if (hasExpired()) {
            markTimeout();
        }

        submitting = true;

        if (intervalId !== null) {
            window.clearInterval(intervalId);
        }

        form.setAttribute("aria-busy", "true");
        submitButton.disabled = true;

        status.textContent = timeoutInput.value === "true"
            ? "زمان تمام شد؛ در حال ارسال وضعیت به سرور…"
            : "در حال ثبت پاسخ…";

        // Do not disable radio inputs: disabled inputs are not submitted.
        // Normal browser form submission carries the antiforgery token.
    });

    function tick() {
        if (submitting || deadline === null) {
            return;
        }

        const secondsLeft = Math.max(
            0,
            Math.ceil((deadline - performance.now()) / 1000)
        );

        timerValue.textContent = formatter.format(secondsLeft);
        timer.classList.toggle("is-urgent", secondsLeft <= 5);

        if (secondsLeft === 0) {
            markTimeout();
            form.requestSubmit();
        }
    }

    // Refresh persisted pages restored from the browser back/forward cache.
    window.addEventListener("pageshow", (event) => {
        if (event.persisted) {
            window.location.reload();
        }
    });

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) {
            tick();
        }
    });

    if (deadline !== null) {
        intervalId = window.setInterval(tick, 200);
        tick();
    }
})();
