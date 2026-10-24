(() => {
    "use strict";

    const toPersianNumber = (value) =>
        String(value).replace(/\d/g, digit => "۰۱۲۳۴۵۶۷۸۹"[digit]);

    // Room list filters: local sample cards only.
    const roomList = document.querySelector("[data-room-list]");

    if (roomList) {
        const filters = Array.from(
            roomList.querySelectorAll("[data-room-filter]")
        );

        const cards = Array.from(
            roomList.querySelectorAll("[data-room-state]")
        );

        const count = roomList.querySelector("[data-room-count]");
        const empty = roomList.querySelector("[data-room-empty]");

        const applyFilter = (selected) => {
            let visibleCount = 0;

            for (const card of cards) {
                const visible =
                    selected === "all" ||
                    card.dataset.roomState === selected;

                card.hidden = !visible;

                if (visible) {
                    visibleCount++;
                }
            }

            for (const button of filters) {
                button.setAttribute(
                    "aria-pressed",
                    String(button.dataset.roomFilter === selected)
                );
            }

            if (count) {
                count.textContent =
                    `${toPersianNumber(visibleCount)} اتاق نمونه`;
            }

            if (empty) {
                empty.hidden = visibleCount !== 0;
            }
        };

        for (const button of filters) {
            button.addEventListener("click", () => {
                applyFilter(button.dataset.roomFilter);
            });
        }

        applyFilter("all");
    }

    // Create room: validation and a text preview, no persistence.
    const createForm = document.querySelector("[data-create-room-demo]");

    if (createForm) {
        const title = createForm.querySelector("[data-room-title]");
        const counter = createForm.querySelector("[data-title-counter]");
        const questions = createForm.querySelector("[data-question-count]");
        const duration = createForm.querySelector("[data-room-duration]");
        const message = createForm.querySelector("[data-form-message]");
        const submit = createForm.querySelector("[data-demo-submit]");

        // Attach the submit blocker before enabling the button.
        createForm.addEventListener("submit", (event) => {
            event.preventDefault();

            title.setCustomValidity(
                title.value.trim()
                    ? ""
                    : "لطفاً اسم اتاق را وارد کن."
            );

            if (!createForm.reportValidity()) {
                return;
            }

            const questionLabel =
                questions.options[questions.selectedIndex].text;

            const durationLabel =
                duration.options[duration.selectedIndex].text;

            message.textContent =
                `پیش‌نمایش: «${title.value.trim()}»، ` +
                `${questionLabel}، ${durationLabel}. ` +
                "فرم بررسی شد؛ هیچ اتاقی ساخته یا ذخیره نشده است.";
        });

        createForm.addEventListener("input", () => {
            message.textContent = "";
        });

        createForm.addEventListener("change", () => {
            message.textContent = "";
        });

        const updateTitle = () => {
            title.setCustomValidity("");

            counter.textContent =
                `${toPersianNumber(title.value.length)} / ` +
                toPersianNumber(title.maxLength);
        };

        title.addEventListener("input", updateTitle);

        updateTitle();
        submit.disabled = false;
    }

    // Join room: do not invent a code format or claim membership.
    const joinForm = document.querySelector("[data-join-room-demo]");

    if (joinForm) {
        const code = joinForm.querySelector("[data-room-code]");
        const message = joinForm.querySelector("[data-form-message]");
        const submit = joinForm.querySelector("[data-demo-submit]");

        joinForm.addEventListener("submit", (event) => {
            event.preventDefault();

            code.setCustomValidity(
                code.value.trim()
                    ? ""
                    : "لطفاً کد اتاق را وارد کن."
            );

            if (!joinForm.reportValidity()) {
                return;
            }

            message.textContent =
                "کد وارد شد؛ این صفحه هنوز به سرور متصل نیست. " +
                "وجود اتاق تأیید نشده و عضویتی انجام نشده است.";
        });

        code.addEventListener("input", () => {
            code.setCustomValidity("");
            message.textContent = "";
        });

        submit.disabled = false;
    }
})();
