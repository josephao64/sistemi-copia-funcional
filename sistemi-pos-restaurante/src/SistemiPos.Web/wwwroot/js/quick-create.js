(() => {
    const initialize = (root = document) => {
        root.querySelectorAll("[data-catalog-search]:not([data-quick-create-initialized])").forEach(input => {
            input.dataset.quickCreateInitialized = "true";
            input.addEventListener("input", () => {
                const select = document.getElementById(input.dataset.catalogSearch);
                const term = input.value.trim().toLocaleLowerCase();
                [...(select?.options || [])].forEach(option => option.hidden = !!term && !!option.value && !option.text.toLocaleLowerCase().includes(term));
            });
        });
        root.querySelectorAll("[data-quick-create]:not([data-quick-create-initialized])").forEach(dialog => {
            dialog.dataset.quickCreateInitialized = "true";
            const form = dialog.querySelector("[data-quick-create-form]");
            form?.addEventListener("submit", async event => {
                event.preventDefault();
                const error = dialog.querySelector("[data-quick-create-error]");
                if (error) error.textContent = "";
                const response = await fetch(dialog.dataset.endpoint, { method: "POST", body: new FormData(form), headers: { "X-Requested-With": "XMLHttpRequest" } });
                const payload = await response.json().catch(() => ({}));
                if (!response.ok) { window.showSystemToast?.("error", payload.message || "No fue posible guardar."); return; }
                const target = document.getElementById(dialog.dataset.target);
                if (target) {
                    if (target instanceof HTMLSelectElement) {
                        let option = [...target.options].find(x => x.value === String(payload.id));
                        if (!option) { option = new Option(payload.text, payload.id); target.add(option); }
                    }
                    target.value = String(payload.id);
                    target.dispatchEvent(new Event("change", { bubbles: true }));
                }
                dialog.dispatchEvent(new CustomEvent("quickcreate:created", { detail: payload }));
                window.showSystemToast?.("success", "El registro fue guardado correctamente.");
                form.reset(); dialog.close();
            });
        });
    };
    document.addEventListener("click", event => {
        const opener = event.target.closest("[data-quick-create-open]");
        if (opener) document.getElementById(opener.dataset.quickCreateOpen)?.showModal();
        const cancel = event.target.closest("[data-quick-create-cancel]");
        if (cancel) cancel.closest("dialog")?.close();
    });
    window.initializeQuickCreate = initialize;
    initialize();
})();
