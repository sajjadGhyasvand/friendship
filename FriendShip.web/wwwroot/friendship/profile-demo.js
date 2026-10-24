(() => {
    "use strict";

    const form = document.querySelector("[data-profile-edit-demo]");

    if (!form) {
        return;
    }

    const fields = form.querySelector("fieldset");
    const input = form.querySelector("[data-profile-name-input]");
    const preview = document.querySelector("[data-profile-name-preview]");
    const message = form.querySelector("[data-profile-edit-message]");

    const updatePreview = () => {
        const name = input.value.trim();

        input.setCustomValidity(
            name.length < 2 || name.length > 40
                ? "نام نمایشی باید بین ۲ تا ۴۰ نویسه باشد."
                : ""
        );

        // Treat user input as text, never as HTML.
        preview.textContent = name || "نام نمایشی";
    };

    input.addEventListener("input", () => {
        message.textContent = "";
        updatePreview();
    });

    form.addEventListener("submit", (event) => {
        event.preventDefault();

        updatePreview();

        if (!form.reportValidity()) {
            return;
        }

        message.textContent =
            "نام برای این پیش‌نمایش معتبر است؛ هیچ تغییری ذخیره نشد.";
    });

    form.addEventListener("reset", () => {
        // Reset values are applied after the reset event.
        queueMicrotask(() => {
            updatePreview();
            message.textContent = "مقدار نمونه بازگردانده شد.";
        });
    });

    updatePreview();
    fields.disabled = false;
})();
