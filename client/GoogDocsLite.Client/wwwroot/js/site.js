(() => {
    const html = document.documentElement;
    const themeSelector = document.getElementById("themeSelector");
    const palette = document.getElementById("commandPalette");
    const paletteButton = document.getElementById("commandPaletteButton");
    const paletteInput = document.getElementById("cmdkInput");
    const paletteList = document.getElementById("cmdkList");
    const shortcutToggleButton = document.getElementById("shortcutToggleButton");

    const ThemeStorageKey = "googdocs_theme";
    const themes = ["paper", "sepia", "slate"];

    function applyTheme(theme) {
        const safeTheme = themes.includes(theme) ? theme : "paper";
        html.setAttribute("data-theme", safeTheme);

        if (themeSelector) {
            themeSelector.value = safeTheme;
        }

        localStorage.setItem(ThemeStorageKey, safeTheme);
    }

    function toggleTheme() {
        const currentTheme = html.getAttribute("data-theme") || "paper";
        const currentIndex = themes.indexOf(currentTheme);
        const nextTheme = themes[(currentIndex + 1) % themes.length];
        applyTheme(nextTheme);
    }

    function openPalette() {
        if (!palette) {
            return;
        }

        palette.classList.remove("d-none");
        palette.setAttribute("aria-hidden", "false");

        if (paletteInput) {
            paletteInput.value = "";
            filterPaletteItems("");
            window.setTimeout(() => paletteInput.focus(), 10);
        }
    }

    function closePalette() {
        if (!palette) {
            return;
        }

        palette.classList.add("d-none");
        palette.setAttribute("aria-hidden", "true");
    }

    function getPaletteItems() {
        if (!paletteList) {
            return [];
        }

        return Array.from(paletteList.querySelectorAll(".cmdk-item"));
    }

    function filterPaletteItems(query) {
        const normalizedQuery = (query || "").trim().toLowerCase();
        const items = getPaletteItems();

        items.forEach((item, index) => {
            const text = (item.textContent || "").toLowerCase();
            const visible = normalizedQuery.length === 0 || text.includes(normalizedQuery);
            item.classList.toggle("d-none", !visible);
            item.classList.toggle("is-selected", visible && index === 0 && normalizedQuery.length === 0);
        });
    }

    function navigate(url) {
        window.location.href = url;
    }

    function runPaletteAction(action) {
        if (!action) {
            return;
        }

        switch (action) {
            case "go-home":
                navigate("/");
                break;
            case "go-docs":
                navigate("/docs");
                break;
            case "go-invites":
                navigate("/docs/invites");
                break;
            case "new-document":
                if (window.location.pathname.toLowerCase().startsWith("/docs")) {
                    document.dispatchEvent(new CustomEvent("cmdk:new-document"));
                } else {
                    navigate("/docs?new=1");
                }
                break;
            case "toggle-theme":
                toggleTheme();
                break;
            default:
                break;
        }

        closePalette();
    }

    function getSelectedPaletteAction() {
        const items = getPaletteItems();
        const selected = items.find((item) => item.classList.contains("is-selected") && !item.classList.contains("d-none"));
        return selected?.getAttribute("data-cmdk-action") || null;
    }

    function selectNextPaletteItem(step) {
        const items = getPaletteItems().filter((item) => !item.classList.contains("d-none"));
        if (items.length === 0) {
            return;
        }

        const currentIndex = items.findIndex((item) => item.classList.contains("is-selected"));
        const nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + step + items.length) % items.length;

        items.forEach((item, index) => {
            item.classList.toggle("is-selected", index === nextIndex);
        });
    }

    function toggleShortcutHints() {
        document.body.classList.toggle("show-shortcuts");
    }

    const persistedTheme = localStorage.getItem(ThemeStorageKey);
    applyTheme(persistedTheme || "paper");

    if (themeSelector) {
        themeSelector.addEventListener("change", (event) => {
            applyTheme(event.target.value);
        });
    }

    if (paletteButton) {
        paletteButton.addEventListener("click", openPalette);
    }

    if (shortcutToggleButton) {
        shortcutToggleButton.addEventListener("click", toggleShortcutHints);
    }

    if (palette) {
        palette.addEventListener("click", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLElement)) {
                return;
            }

            if (target.hasAttribute("data-cmdk-close")) {
                closePalette();
                return;
            }

            const item = target.closest(".cmdk-item");
            if (item instanceof HTMLElement) {
                const action = item.getAttribute("data-cmdk-action");
                runPaletteAction(action);
            }
        });
    }

    if (paletteInput) {
        paletteInput.addEventListener("input", (event) => {
            const target = event.target;
            if (!(target instanceof HTMLInputElement)) {
                return;
            }

            const query = target.value;
            const normalizedQuery = (query || "").trim().toLowerCase();
            const items = getPaletteItems();
            let firstVisibleSelected = false;

            items.forEach((item) => {
                const text = (item.textContent || "").toLowerCase();
                const visible = normalizedQuery.length === 0 || text.includes(normalizedQuery);
                item.classList.toggle("d-none", !visible);

                if (visible && !firstVisibleSelected) {
                    item.classList.add("is-selected");
                    firstVisibleSelected = true;
                } else {
                    item.classList.remove("is-selected");
                }
            });
        });

        paletteInput.addEventListener("keydown", (event) => {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                selectNextPaletteItem(1);
                return;
            }

            if (event.key === "ArrowUp") {
                event.preventDefault();
                selectNextPaletteItem(-1);
                return;
            }

            if (event.key === "Enter") {
                event.preventDefault();
                runPaletteAction(getSelectedPaletteAction());
            }
        });
    }

    document.addEventListener("keydown", (event) => {
        const isMac = navigator.platform.toLowerCase().includes("mac");
        const commandPressed = isMac ? event.metaKey : event.ctrlKey;

        if (commandPressed && event.key.toLowerCase() === "k") {
            event.preventDefault();
            if (palette?.classList.contains("d-none")) {
                openPalette();
            } else {
                closePalette();
            }
            return;
        }

        if (commandPressed && event.key === "/") {
            event.preventDefault();
            toggleShortcutHints();
            return;
        }

        if (event.key === "Escape") {
            closePalette();
        }
    });

    getPaletteItems().forEach((item) => {
        item.addEventListener("mouseenter", () => {
            getPaletteItems().forEach((other) => other.classList.remove("is-selected"));
            item.classList.add("is-selected");
        });
    });
})();
