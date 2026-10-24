(() => {
    "use strict";

    document.querySelectorAll("[data-account-demo]").forEach((form) => {
        const fields = form.querySelector("fieldset");
        const message = form.querySelector("[data-account-message]");
        const password = form.querySelector("[data-new-password]");
        const confirmation = form.querySelector("[data-confirm-password]");

        const validateConfirmation = () => {
            if (!password || !confirmation) {
                return;
            }

            const mismatch =
                confirmation.value !== "" &&
                confirmation.value !== password.value;

            confirmation.setCustomValidity(
                mismatch ? "تکرار رمز عبور با رمز واردشده یکسان نیست." : ""
            );
        };

        password?.addEventListener("input", validateConfirmation);
        confirmation?.addEventListener("input", validateConfirmation);

        form.addEventListener("input", () => {
            message.textContent = "";
        });

        form.addEventListener("submit", (event) => {
            event.preventDefault();

            validateConfirmation();

            if (!form.reportValidity()) {
                return;
            }

            // UI preview only: no requests, storage, or authentication.
            message.textContent =
                form.dataset.accountDemo === "register"
                    ? "فرم از نظر نمایشی معتبر است؛ هیچ حسابی ساخته نشد."
                    : "فرم تکمیل است؛ اطلاعات ورود بررسی نشده و وارد حساب نشده‌ای.";

            form.querySelectorAll('input[type="password"]').forEach((input) => {
                input.value = "";
            });

            validateConfirmation();
        });

        // Enable only after the no-submit handler has been installed.
        fields.disabled = false;
    });
})();
