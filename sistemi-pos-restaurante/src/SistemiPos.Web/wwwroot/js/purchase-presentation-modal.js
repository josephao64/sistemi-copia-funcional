(() => {
    const dialog = document.querySelector("[data-presentation-dialog]");
    const form = document.querySelector("[data-presentation-form]");
    if (!dialog || !form || dialog.dataset.initialized === "true") return;

    dialog.dataset.initialized = "true";
    let targetSelect = null;

    document.addEventListener("click", event => {
        const trigger = event.target.closest("[data-open-presentation-modal]");
        if (!trigger) return;
        targetSelect = trigger.closest("[data-presentation-field]")?.querySelector("[data-presentation-catalog-select]") || null;
        dialog.showModal();
        form.querySelector('[name="name"]')?.focus();
    });

    dialog.querySelectorAll("[data-close-presentation]").forEach(button => button.addEventListener("click", () => dialog.close()));

    form.addEventListener("submit", async event => {
        event.preventDefault();
        const error = form.querySelector("[data-presentation-error]");
        error.textContent = "";
        try {
            const response = await fetch(form.dataset.endpoint, { method: "POST", body: new FormData(form) });
            const result = await response.json();
            if (!response.ok) throw new Error(result.message || "No fue posible crear la presentación.");

            document.querySelectorAll("[data-presentation-catalog-select]").forEach(select => {
                if ([...select.options].some(option => option.value === result.id)) return;
                const option = document.createElement("option");
                option.value = result.id;
                option.dataset.units = result.units;
                option.textContent = `${result.name} (${result.units})`;
                select.appendChild(option);
            });

            if (targetSelect) {
                targetSelect.value = result.id;
                targetSelect.dispatchEvent(new Event("change", { bubbles: true }));
            }
            form.reset();
            dialog.close();
            window.showSystemToast?.("success", "La presentación fue creada correctamente.");
        } catch (errorValue) {
            error.textContent = errorValue.message;
        }
    });
})();
