(() => {
    "use strict";

    const root = document.querySelector("[data-quiz]");

    if (!root) {
        return;
    }

    const isHost = root.dataset.quizMode === "host";

    // UI samples only. No answer key, scoring or server contracts.
    const questions = [
        {
            category: "اوقات فراغت",
            host: "یه روز کاملاً آزاد رو چطور می‌گذرونی؟",
            player: "دوستت یه روز کاملاً آزاد رو چطور می‌گذرونه؟",
            options: [
                "فیلم و سریال توی خونه",
                "بیرون رفتن با دوست‌ها",
                "گشت‌وگذار توی طبیعت",
                "یه سرگرمی شخصی و کمی خلوت"
            ]
        },
        {
            category: "خوراکی‌ها",
            host: "برای یه شام خوشمزه کدوم رو انتخاب می‌کنی؟",
            player: "دوستت برای یه شام خوشمزه کدوم رو انتخاب می‌کنه؟",
            options: [
                "پیتزا",
                "غذای خونگی",
                "برگر",
                "امتحان کردن یه غذای جدید"
            ]
        },
        {
            category: "سفر",
            host: "کدوم مقصد بیشتر هیجان‌زده‌ات می‌کنه؟",
            player: "کدوم مقصد بیشتر دوستت رو هیجان‌زده می‌کنه؟",
            options: [
                "دریا و ساحل",
                "جنگل و کوه",
                "یه شهر پرجنب‌وجوش",
                "یه روستای آروم"
            ]
        },
        {
            category: "عادت‌های روزمره",
            host: "چه زمانی از روز بیشتر انرژی داری؟",
            player: "دوستت چه زمانی از روز بیشتر انرژی داره؟",
            options: [
                "صبح زود",
                "وسط روز",
                "عصر",
                "آخر شب"
            ]
        },
        {
            category: "سرگرمی",
            host: "برای یه دورهمی کدوم رو بیشتر دوست داری؟",
            player: "دوستت برای یه دورهمی کدوم رو بیشتر دوست داره؟",
            options: [
                "بازی گروهی",
                "گپ زدن طولانی",
                "فیلم دیدن",
                "بیرون رفتن و قدم زدن"
            ]
        },
        {
            category: "سلیقه‌ی شخصی",
            host: "کدوم هدیه بیشتر خوشحالت می‌کنه؟",
            player: "کدوم هدیه بیشتر دوستت رو خوشحال می‌کنه؟",
            options: [
                "یه هدیه‌ی دست‌ساز",
                "چیزی که مدت‌ها لازم داشتم",
                "یه تجربه‌ی مشترک",
                "یه سورپرایز غیرمنتظره"
            ]
        },
        {
            category: "حال خوب",
            host: "وقتی روز سختی داشتی، چی بیشتر کمکت می‌کنه؟",
            player: "وقتی دوستت روز سختی داشته، چی بیشتر کمکش می‌کنه؟",
            options: [
                "حرف زدن با یه آدم نزدیک",
                "یه کم تنهایی",
                "موسیقی و استراحت",
                "قدم زدن و عوض کردن فضا"
            ]
        },
        {
            category: "دوستی",
            host: "توی دوستی کدوم ویژگی برات مهم‌تره؟",
            player: "توی دوستی کدوم ویژگی برای دوستت مهم‌تره؟",
            options: [
                "صداقت",
                "همراه بودن توی سختی‌ها",
                "احترام به تفاوت‌ها",
                "وقت گذاشتن برای هم"
            ]
        }
    ];

    const activePanel = root.querySelector("[data-quiz-active]");
    const completePanel = root.querySelector("[data-quiz-complete]");

    const questionCounter = root.querySelector("[data-question-counter]");
    const answerCounter = root.querySelector("[data-answer-counter]");
    const progress = root.querySelector("[data-quiz-progress]");

    const category = root.querySelector("[data-question-category]");
    const title = root.querySelector("[data-question-title]");
    const options = root.querySelector("[data-quiz-options]");
    const status = root.querySelector("[data-quiz-status]");

    const previous = root.querySelector("[data-quiz-previous]");
    const next = root.querySelector("[data-quiz-next]");
    const review = root.querySelector("[data-quiz-review]");
    const completeTitle = root.querySelector("[data-complete-title]");

    const answers = new Array(questions.length).fill(null);
    const markers = ["الف", "ب", "ج", "د"];

    let currentIndex = 0;

    const fa = (value) =>
        String(value).replace(/\d/g, digit => "۰۱۲۳۴۵۶۷۸۹"[digit]);

    function updateProgress() {
        const answered = answers.filter(value => value !== null).length;

        progress.max = questions.length;
        progress.value = answered;

        answerCounter.textContent =
            `${fa(answered)} از ${fa(questions.length)} پاسخ`;

        progress.setAttribute(
            "aria-valuetext",
            `${fa(answered)} سؤال از ${fa(questions.length)} پاسخ داده شده`
        );
    }

    function updateControls() {
        previous.disabled = currentIndex === 0;
        next.disabled = answers[currentIndex] === null;

        next.textContent = currentIndex === questions.length - 1
            ? "پایان نمونه ✓"
            : "سؤال بعدی ←";
    }

    function renderQuestion(moveFocus = false) {
        const question = questions[currentIndex];

        activePanel.hidden = false;
        completePanel.hidden = true;

        questionCounter.textContent =
            `سؤال ${fa(currentIndex + 1)} از ${fa(questions.length)}`;

        category.textContent = question.category;
        title.textContent = isHost ? question.host : question.player;

        const legend = document.createElement("legend");
        legend.className = "sr-only";
        legend.textContent = "یکی از پاسخ‌ها را انتخاب کن";

        options.replaceChildren(legend);

        question.options.forEach((text, optionIndex) => {
            const label = document.createElement("label");
            label.className = "quiz-option";

            const input = document.createElement("input");
            input.type = "radio";
            input.name = `demo-question-${currentIndex}`;
            input.value = String(optionIndex);
            input.checked = answers[currentIndex] === optionIndex;

            const content = document.createElement("span");
            content.className = "quiz-option-content";

            const marker = document.createElement("span");
            marker.className = "option-marker";
            marker.setAttribute("aria-hidden", "true");
            marker.textContent = markers[optionIndex];

            const optionText = document.createElement("span");
            optionText.textContent = text;

            content.append(marker, optionText);
            label.append(input, content);
            options.append(label);

            input.addEventListener("change", () => {
                answers[currentIndex] = optionIndex;

                updateProgress();
                updateControls();

                status.textContent =
                    "انتخاب شد؛ می‌تونی عوضش کنی یا به سؤال بعدی بری.";
            });
        });

        status.textContent = answers[currentIndex] === null
            ? "برای ادامه، یک گزینه انتخاب کن."
            : "انتخاب قبلی نمایش داده شده؛ می‌تونی تغییرش بدی.";

        updateProgress();
        updateControls();

        if (moveFocus) {
            title.focus();
        }
    }

    previous.addEventListener("click", () => {
        if (currentIndex === 0) {
            return;
        }

        currentIndex--;
        renderQuestion(true);
    });

    next.addEventListener("click", () => {
        if (answers[currentIndex] === null) {
            return;
        }

        if (currentIndex < questions.length - 1) {
            currentIndex++;
            renderQuestion(true);
            return;
        }

        // Defensive check: every sample question must have a selection.
        const unanswered = answers.findIndex(value => value === null);

        if (unanswered !== -1) {
            currentIndex = unanswered;
            renderQuestion(true);
            return;
        }

        activePanel.hidden = true;
        completePanel.hidden = false;
        completeTitle.focus();
    });

    review.addEventListener("click", () => {
        currentIndex = 0;
        renderQuestion(true);
    });

    renderQuestion();
})();
