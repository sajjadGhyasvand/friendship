(() => {
    "use strict";

    const button = document.querySelector("[data-print-share]");
    const message = document.querySelector("[data-print-message]");

    if (!button) {
        return;
    }

    button.addEventListener("click", async () => {
        button.disabled = true;
        message.textContent = "";

        try {
            // Wait for the local font before opening the print dialog.
            if (document.fonts) {
                await document.fonts.ready;
            }

            window.print();
        } catch {
            message.textContent =
                "پنجره‌ی چاپ باز نشد. از منوی چاپ مرورگر استفاده کن.";
        } finally {
            button.disabled = false;
        }
    });

    button.disabled = false;
})();
