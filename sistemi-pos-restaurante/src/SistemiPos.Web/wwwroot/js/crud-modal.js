(() => {
    "use strict";

    const dialog = document.querySelector("[data-crud-modal-dialog]");
    if (!dialog) return;

    const content = dialog.querySelector("[data-crud-modal-content]");
    const loading = dialog.querySelector("[data-crud-modal-loading]");
    const error = dialog.querySelector("[data-crud-modal-error]");
    const title = dialog.querySelector("[data-crud-modal-title]");
    let endpoint = "";
    let createdEvent = "";
    let requestController;

    const setBusy = busy => {
        loading.hidden = !busy;
        content.hidden = busy;
        dialog.querySelectorAll("button").forEach(button => button.disabled = busy);
    };

    const showError = message => {
        error.textContent = "";
        error.hidden = true;
        if (message) window.showSystemToast?.("error", message);
    };

    const executeInlineScripts = documentFragment => {
        documentFragment.querySelectorAll("script:not([src])").forEach(script => {
            if (["application/json", "importmap"].includes(script.type)) return;
            const executable = document.createElement("script");
            executable.textContent = script.textContent;
            document.body.appendChild(executable);
            executable.remove();
        });
    };

    const render = html => {
        const parsed = new DOMParser().parseFromString(html, "text/html");
        const source = parsed.querySelector("main.content");
        if (!source) throw new Error("El servidor no devolvió un formulario válido.");

        const heading = source.querySelector("h1")?.textContent?.trim() || "Formulario";
        title.textContent = heading;
        content.innerHTML = source.innerHTML;
        content.querySelectorAll(".action-row a").forEach(link => {
            if (!/^volver/i.test(link.textContent.trim())) return;
            link.addEventListener("click", event => { event.preventDefault(); dialog.close(); });
        });
        window.initializeQuickCreate?.(content);
        window.initializeNumericInputs?.(content);
        window.initializeProductForm?.(content);
        executeInlineScripts(parsed);
    };

    const load = async (url, eventName) => {
        requestController?.abort();
        requestController = new AbortController();
        endpoint = url;
        createdEvent = eventName || "";
        showError("");
        setBusy(true);
        if (!dialog.open) dialog.showModal();
        try {
            const response = await fetch(url, {
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin",
                signal: requestController.signal
            });
            if (!response.ok) throw new Error(response.status === 403 ? "No tiene permiso para realizar esta acción." : "No fue posible cargar el formulario.");
            render(await response.text());
        } catch (exception) {
            if (exception.name !== "AbortError") showError(exception.message || "No fue posible cargar el formulario.");
        } finally {
            setBusy(false);
        }
    };

    const rememberListContext = () => {
        const values = [...document.querySelectorAll("[data-table-search]")].map(input => input.value);
        if (values.some(Boolean)) sessionStorage.setItem(`sistemi:list-context:${location.pathname}${location.search}`, JSON.stringify(values));
    };

    document.addEventListener("click", event => {
        const opener = event.target.closest("a[data-crud-modal]");
        if (!opener) return;
        event.preventDefault();
        load(opener.href, opener.dataset.crudCreatedEvent);
    });

    dialog.querySelector("[data-crud-modal-close]")?.addEventListener("click", () => dialog.close());
    dialog.addEventListener("close", () => {
        requestController?.abort();
        endpoint = "";
        createdEvent = "";
        content.replaceChildren();
        showError("");
    });

    dialog.addEventListener("submit", async event => {
        const form = event.target.closest("[data-crud-modal-content] form:not([data-quick-create-form])");
        if (!form) return;
        if (event.defaultPrevented) return;
        event.preventDefault();
        if (!form.reportValidity()) { window.showSystemToast?.("warning", "Revisa los datos requeridos del formulario."); return; }

        showError("");
        setBusy(true);
        try {
            const response = await fetch(form.getAttribute("action") || endpoint, {
                method: "POST",
                body: new FormData(form),
                headers: { "X-Requested-With": "XMLHttpRequest" },
                credentials: "same-origin"
            });
            if (response.redirected) {
                rememberListContext();
                dialog.close();
                window.location.reload();
                return;
            }
            if (!response.ok) {
                let message = response.status === 403 ? "No tiene permiso para realizar esta acción." : "No fue posible guardar los cambios.";
                if (response.headers.get("content-type")?.includes("application/json")) {
                    const payload = await response.json();
                    message = payload.message || payload.detail || message;
                }
                throw new Error(message);
            }
            if (response.headers.get("content-type")?.includes("application/json")) {
                const payload = await response.json();
                if (payload.keepOpen) {
                    window.showSystemToast?.("success", payload.message || "El registro fue agregado correctamente.");
                    await load(endpoint, createdEvent);
                    return;
                }
                const eventName = createdEvent;
                dialog.close();
                if (payload.message) window.showSystemToast?.("success", payload.message);
                if (eventName) document.dispatchEvent(new CustomEvent(eventName, { detail: payload }));
                if (payload.reload) window.location.reload();
                return;
            }
            render(await response.text());
        } catch (exception) {
            showError(exception.message || "No fue posible guardar los cambios.");
        } finally {
            if (dialog.open) setBusy(false);
        }
    });
})();
