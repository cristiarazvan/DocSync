(() => {
    const html = document.documentElement;
    const themeToggleButton = document.getElementById("themeToggleButton");
    const themeToggleLabel = document.getElementById("themeToggleLabel");
    const mobileNavToggle = document.getElementById("mobileNavToggle");
    const primaryNav = document.getElementById("primaryNav");

    const ThemeStorageKey = "googdocs_theme";
    const themes = ["light", "dark"];

    function getPreferredTheme() {
        const persisted = localStorage.getItem(ThemeStorageKey);
        if (persisted && themes.includes(persisted)) {
            return persisted;
        }

        return window.matchMedia("(prefers-color-scheme: dark)").matches
            ? "dark"
            : "light";
    }

    function setTheme(theme) {
        const safeTheme = themes.includes(theme) ? theme : "light";
        html.setAttribute("data-theme", safeTheme);
        localStorage.setItem(ThemeStorageKey, safeTheme);

        if (themeToggleLabel) {
            themeToggleLabel.textContent = safeTheme === "dark"
                ? "Light mode"
                : "Dark mode";
        }
    }

    function toggleTheme() {
        const currentTheme = html.getAttribute("data-theme") || "light";
        setTheme(currentTheme === "dark" ? "light" : "dark");
    }

    setTheme(getPreferredTheme());

    if (themeToggleButton) {
        themeToggleButton.addEventListener("click", toggleTheme);
    }

    if (mobileNavToggle && primaryNav) {
        mobileNavToggle.addEventListener("click", () => {
            const isOpen = primaryNav.classList.toggle("is-open");
            mobileNavToggle.setAttribute("aria-expanded", isOpen ? "true" : "false");
        });

        primaryNav.querySelectorAll("a").forEach((link) => {
            link.addEventListener("click", () => {
                primaryNav.classList.remove("is-open");
                mobileNavToggle.setAttribute("aria-expanded", "false");
            });
        });
    }
})();
