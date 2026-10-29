window.addEventListener("load", () => {
    if ("serviceWorker" in navigator) {
        navigator.serviceWorker.register("/service-worker.js").catch(() => {
            // El POS conserva el flujo conectado cuando el navegador no admite el registro.
        });
    }
});

document.addEventListener("DOMContentLoaded", () => {
    initializeSystemNotifications();
    initializeSessionExpirationHandling();
    initializeSystemConfirmations();
    initializeDiscountApprovalNotifications();
    initializeNumericInputs();
    initializeMoneyInputs();
    const body = document.body;
    const updateConnectionState = () => {
        document.querySelectorAll("[data-connection-state]").forEach(element => {
            element.classList.toggle("is-offline", !navigator.onLine);
            const label = element.querySelector("[data-connection-label]");
            if (label) label.textContent = navigator.onLine ? "En línea" : "Sin conexión";
        });
    };
    updateConnectionState();
    window.addEventListener("online", updateConnectionState);
    window.addEventListener("offline", updateConnectionState);
    const sidebarToggle = document.querySelector("[data-sidebar-toggle]");
    const sidebarClose = document.querySelector("[data-sidebar-close]");

    const closeSidebar = () => body.classList.remove("sidebar-open");
    sidebarToggle?.addEventListener("click", () => body.classList.toggle("sidebar-open"));
    sidebarClose?.addEventListener("click", closeSidebar);
    initializeSidebarSections();
    initializeSidebarScroll();
    document.addEventListener("submit", event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() === "get" || form.matches("[data-server-sale]")) return;
        if (navigator.onLine) return;
        event.preventDefault();
        window.showSystemToast?.("error", "No se pudo conectar con el servidor. Verifique la conexión e intente nuevamente.");
    }, true);
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") closeSidebar();
        if (event.key === "F2" && !event.ctrlKey && !event.metaKey && !event.altKey) {
            const saleLink = document.querySelector('a[href="/pos"]');
            if (saleLink) {
                event.preventDefault();
                window.location.assign(saleLink.href);
            }
        }
    });

    document.querySelectorAll("[data-go-back]").forEach(link => link.addEventListener("click", event => {
        if (!document.referrer) return;
        const previous = new URL(document.referrer);
        const previousIsUseful = previous.origin === window.location.origin &&
            previous.href !== window.location.href &&
            !previous.pathname.startsWith("/Account/Login") &&
            !previous.pathname.startsWith("/Account/AccessDenied");
        if (previousIsUseful && window.history.length > 1) {
            event.preventDefault();
            window.history.back();
        }
    }));

    localizeInterfaceValues();
    initializeTableFilters();
    initializeSupplierFilters();
    initializeCustomerFilters();
    initializeAutoFilters();
    initializeNitLookup();
    if (!document.querySelector("[data-pos-tab-lock]")) initializePos();
    initializeCashClose();
    initializeCashCount();
    initializeCashPosAssignment();
    initializePositiveIntegerInputs();
    initializeLinkedRows();
    initializeSubmissionLocks();
    initializeDialogCancellation();
});

function initializeDiscountApprovalNotifications() {
    const links = Array.from(document.querySelectorAll("[data-discount-approvals-link]"));
    if (!links.length) return;
    const refresh = async () => {
        try {
            const response = await fetch("/DiscountApprovals/PendingCount", { credentials: "same-origin" });
            if (!response.ok) return;
            const payload = await response.json();
            links.forEach(link => {
                const badge = link.querySelector("[data-discount-approval-count]");
                if (!badge) return;
                badge.textContent = String(payload.count || 0);
                badge.hidden = !payload.count;
            });
        } catch { }
    };
    refresh();
    window.setInterval(refresh, 15000);
}

function initializeMoneyInputs() {
    const body = document.body;
    const symbol = body.dataset.globalCurrencySymbol || "Q";
    const after = body.dataset.globalCurrencyPosition === "After";
    const monetaryName = /(?:amount|price|cost|balance|total)$/i;
    const decorate = (root = document) => {
        root.querySelectorAll?.('input:not([type="hidden"]):not([data-money-input-ready])').forEach(input => {
            const identifier = input.name || input.id || "";
            if (!input.hasAttribute("data-money-field") && !monetaryName.test(identifier)) return;
            if (input.closest(".money-input")) { input.dataset.moneyInputReady = "true"; return; }
            const wrapper = document.createElement("div");
            wrapper.className = `money-input${after ? " currency-after" : ""}`;
            const marker = document.createElement("span");
            marker.textContent = symbol;
            marker.setAttribute("aria-hidden", "true");
            input.parentNode.insertBefore(wrapper, input);
            wrapper.append(marker, input);
            input.dataset.moneyInputReady = "true";
        });
    };
    decorate();
    new MutationObserver(records => records.forEach(record => record.addedNodes.forEach(node => {
        if (node.nodeType !== Node.ELEMENT_NODE) return;
        if (node.matches?.("input")) decorate(node.parentElement);
        else decorate(node);
    }))).observe(body, { childList: true, subtree: true });
}

function initializeSupplierFilters() {
    document.querySelectorAll("[data-supplier-filter-search]").forEach(search => {
        const hidden = search.parentElement?.querySelector("[data-supplier-filter-id]");
        const list = document.getElementById(search.getAttribute("list"));
        const synchronize = () => {
            const option = Array.from(list?.options || []).find(item => item.value === search.value);
            if (hidden) hidden.value = option?.dataset.id || "";
        };
        search.addEventListener("input", synchronize);
        search.addEventListener("change", synchronize);
    });
}

function initializeCustomerFilters() {
    document.querySelectorAll("[data-customer-filter-search]").forEach(search => {
        const hidden = search.parentElement?.querySelector("[data-customer-filter-id]");
        const list = document.getElementById(search.getAttribute("list"));
        const synchronize = () => {
            const option = Array.from(list?.options || []).find(item => item.value === search.value);
            if (hidden) hidden.value = option?.dataset.id || "";
        };
        search.addEventListener("input", synchronize);
        search.addEventListener("change", synchronize);
    });
}

function initializeAutoFilters() {
    document.querySelectorAll("[data-auto-filter-form]").forEach(form => {
        form.querySelectorAll("select, input:not([type='hidden'])").forEach(control => {
            control.addEventListener("change", () => form.requestSubmit());
        });
        form.querySelectorAll("[data-auto-filter-search]").forEach(control => {
            let debounceId;
            control.addEventListener("input", () => {
                window.clearTimeout(debounceId);
                debounceId = window.setTimeout(() => form.requestSubmit(), 350);
            });
        });
    });
}

function initializeDialogCancellation() {
    document.addEventListener("cancel", event => {
        if (event.target instanceof HTMLDialogElement &&
            event.target.matches(".crud-modal, .quick-create-modal")) {
            event.preventDefault();
        }
    }, true);
    document.addEventListener("click", event => {
        const button = event.target.closest('dialog button[type="button"]');
        if (!button || button.textContent.trim().localeCompare("Cancelar", "es", { sensitivity: "base" }) !== 0) return;
        const dialog = button.closest("dialog");
        if (!dialog) return;
        event.preventDefault();
        dialog.querySelector("form")?.reset();
        if (dialog.open) dialog.close();
    });
}

function initializeSessionExpirationHandling() {
    const sessionExpiredMessage = "Tu sesión ha expirado. Inicia sesión nuevamente.";
    let redirectScheduled = false;

    const redirectToLogin = () => {
        if (redirectScheduled || window.location.pathname.startsWith("/Account/Login")) return;
        redirectScheduled = true;
        window.showSystemToast?.("warning", sessionExpiredMessage, "Sesión expirada");
        const returnUrl = encodeURIComponent(`${window.location.pathname}${window.location.search}`);
        window.setTimeout(() => {
            window.location.assign(`/Account/Login?reason=expired&ReturnUrl=${returnUrl}`);
        }, 900);
    };

    const originalFetch = window.fetch.bind(window);
    window.fetch = async (...args) => {
        const response = await originalFetch(...args);
        if (response.status === 401) redirectToLogin();
        return response;
    };
}

function initializeSystemNotifications() {
    const region = document.querySelector("[data-toast-region]");
    if (!region) return;

    const titles = { error: "Revisa la información", warning: "Advertencia", success: "Operación exitosa", info: "Información" };
    const shown = new Set();
    const clean = value => (value || "").replace(/\s+/g, " ").trim();
    window.showSystemToast = (type, messages, title) => {
        const items = [...new Set((Array.isArray(messages) ? messages : [messages]).map(clean).filter(Boolean))];
        if (!items.length) return;
        const key = items.join("|");
        if (shown.has(key)) return;
        shown.add(key);
        if (typeof region.showPopover === "function") {
            try {
                region.classList.remove("toast-fallback");
                if (region.matches(":popover-open")) region.hidePopover();
                region.showPopover();
            } catch {
                region.classList.add("toast-fallback");
                const dialogs = document.querySelectorAll("dialog[open]");
                (dialogs[dialogs.length - 1] || document.body).appendChild(region);
            }
        } else {
            region.classList.add("toast-fallback");
            const dialogs = document.querySelectorAll("dialog[open]");
            (dialogs[dialogs.length - 1] || document.body).appendChild(region);
        }
        const toast = document.createElement("section");
        toast.className = `system-toast toast-${type}`;
        toast.setAttribute("role", type === "error" || type === "warning" ? "alert" : "status");
        const content = items.length === 1
            ? `<p>${escapeToastText(items[0])}</p>`
            : `<ul>${items.map(item => `<li>${escapeToastText(item)}</li>`).join("")}</ul>`;
        toast.innerHTML = `<span class="toast-icon" aria-hidden="true"></span><div><strong>${escapeToastText(title || titles[type] || titles.info)}</strong>${content}</div><button type="button" aria-label="Cerrar notificación">×</button>`;
        let removed = false;
        const removeToast = () => {
            if (removed) return;
            removed = true;
            shown.delete(key);
            toast.remove();
            if (!region.children.length && region.matches?.(":popover-open")) region.hidePopover();
        };
        toast.querySelector("button").addEventListener("click", removeToast);
        region.appendChild(toast);
        window.setTimeout(removeToast, type === "error" ? 9000 : 6000);
    };

    document.querySelectorAll("[data-system-notification]").forEach(element =>
        window.showSystemToast(element.dataset.notificationType || "info", element.textContent));
    document.querySelectorAll(".alert-error:not([hidden]), .alert-success:not([hidden])").forEach(element => {
        window.showSystemToast(element.classList.contains("alert-error") ? "error" : "success", element.textContent);
        element.hidden = true;
    });
    const observer = new MutationObserver(mutations => mutations.forEach(mutation => {
        const element = mutation.target.nodeType === Node.ELEMENT_NODE ? mutation.target : mutation.target.parentElement;
        const alert = element?.closest?.(".alert-error:not([hidden]), .alert-success:not([hidden])");
        if (alert && clean(alert.textContent)) {
            window.showSystemToast(alert.classList.contains("alert-error") ? "error" : "success", alert.textContent);
            alert.hidden = true;
        }
    }));
    observer.observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ["class", "hidden"] });
}

function initializeSystemConfirmations() {
    const dialog = document.querySelector("[data-system-confirm-dialog]");
    if (!dialog) return;

    const title = dialog.querySelector("[data-system-confirm-title]");
    const message = dialog.querySelector("[data-system-confirm-message]");
    const acceptButton = dialog.querySelector("[data-system-confirm-accept]");
    const cancelButton = dialog.querySelector("[data-system-confirm-cancel]");
    let resolveConfirmation;

    const finish = accepted => {
        if (dialog.open) dialog.close();
        resolveConfirmation?.(accepted);
        resolveConfirmation = null;
    };

    window.showSystemConfirmation = (options = {}) => new Promise(resolve => {
        resolveConfirmation = resolve;
        title.textContent = options.title || "Confirmar operación";
        message.textContent = options.message || "Esta operación requiere su confirmación para continuar.";
        acceptButton.textContent = options.confirmText || "Confirmar";
        dialog.showModal();
        cancelButton.focus();
    });

    acceptButton.addEventListener("click", () => finish(true));
    cancelButton.addEventListener("click", () => finish(false));
    dialog.addEventListener("cancel", event => { event.preventDefault(); finish(false); });
    dialog.addEventListener("click", event => { if (event.target === dialog) finish(false); });

    document.addEventListener("submit", async event => {
        const form = event.target.closest("form[data-confirm]");
        if (!form || form.dataset.confirmed === "true") {
            if (form) delete form.dataset.confirmed;
            return;
        }
        event.preventDefault();
        const accepted = await window.showSystemConfirmation({
            title: form.dataset.confirmTitle,
            message: form.dataset.confirm,
            confirmText: form.dataset.confirmText
        });
        if (!accepted) return;
        form.dataset.confirmed = "true";
        form.requestSubmit(event.submitter || undefined);
    });
}

window.showSystemBanner = (type, message, title) => {
    const host = document.querySelector("main");
    if (!host || !message) return null;
    const banner = document.createElement("section");
    banner.className = `system-banner banner-${type || "info"}`;
    banner.setAttribute("role", type === "error" || type === "warning" ? "alert" : "status");
    const icon = document.createElement("span");
    icon.setAttribute("aria-hidden", "true");
    icon.textContent = type === "error" ? "×" : type === "warning" ? "!" : type === "success" ? "✓" : "i";
    const copy = document.createElement("div");
    const heading = document.createElement("strong");
    heading.textContent = title || ({ success: "Operación exitosa", warning: "Advertencia", error: "Ocurrió un problema", info: "Información" }[type] || "Información");
    const body = document.createElement("p");
    body.textContent = message;
    copy.append(heading, body);
    const close = document.createElement("button");
    close.type = "button";
    close.setAttribute("aria-label", "Cerrar aviso");
    close.textContent = "×";
    close.addEventListener("click", () => banner.remove());
    banner.append(icon, copy, close);
    host.prepend(banner);
    return banner;
};

function escapeToastText(value) {
    const element = document.createElement("span");
    element.textContent = value;
    return element.innerHTML;
}

function initializeNumericInputs(root = document) {
    root.querySelectorAll('input[type="number"]').forEach(input => {
        input.classList.add("number-input");
        if (/(quantity|cantidad|stock|days|dias|count)/i.test(`${input.name} ${input.id}`)
            && !/(price|precio|cost|costo|amount|monto|total)/i.test(`${input.name} ${input.id}`)) input.dataset.integer = "";
    });
    root.querySelectorAll("[data-integer]").forEach(input => {
        if (input.dataset.integerInitialized) return;
        input.dataset.integerInitialized = "true";
        input.setAttribute("inputmode", "numeric");
        input.addEventListener("keydown", event => {
            if ([".", ",", "-", "+", "e", "E"].includes(event.key)) event.preventDefault();
        });
        input.addEventListener("input", () => { input.value = input.value.replace(/\D/g, ""); });
    });
}

function initializeSubmissionLocks() {
    document.querySelectorAll("form[data-submission-lock]").forEach(form => {
        form.addEventListener("submit", event => {
            window.setTimeout(() => {
                if (event.defaultPrevented) return;
                form.querySelectorAll('button[type="submit"], button:not([type])').forEach(button => {
                    button.disabled = true;
                    button.setAttribute("aria-busy", "true");
                });
            }, 0);
        });
    });
}

function initializeLinkedRows() {
    document.querySelectorAll("[data-row-link]").forEach(row => {
        const open = () => { if (row.dataset.rowLink) window.location.assign(row.dataset.rowLink); };
        row.addEventListener("click", event => { if (!event.target.closest("a, button, input, select, textarea")) open(); });
        row.addEventListener("keydown", event => { if (event.key === "Enter") { event.preventDefault(); open(); } });
    });
}

function initializeCashClose() {
    const dialog = document.querySelector("[data-cash-close-dialog]");
    const openButton = document.querySelector("[data-open-cash-close]");
    if (!dialog || !openButton) return;
    openButton.addEventListener("click", () => {
        const blocker = openButton.dataset.cashCloseBlocked?.trim();
        if (blocker) {
            window.showSystemToast?.("warning", blocker, "No puedes cerrar la caja");
            return;
        }
        dialog.showModal();
    });
    dialog.querySelectorAll("[data-close-cash-close]").forEach(button => button.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
}

function initializeCashCount() {
    document.querySelectorAll("[data-cash-count]").forEach(form => {
    const inputs = [...form.querySelectorAll("[data-cash-quantity]")];
    const totalOutputs = [...form.querySelectorAll("[data-cash-count-total]")];
    const totalInput = form.querySelector("[data-cash-count-value]");
    const symbol = form.dataset.currencySymbol || "Q";
    const formatMoney = value => `${symbol} ${value.toLocaleString("es-GT", { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    const quantityOf = input => Math.min(2147483647, Math.max(0, Math.trunc(Number(input.value) || 0)));

    const updateTotal = () => {
        let cents = 0, pieces = 0;
        const groups = new Map();
        inputs.forEach(input => {
            const quantity = quantityOf(input);
            const denominationCents = Math.round((Number(input.dataset.denominationValue) || 0) * 100);
            const subtotal = quantity * denominationCents;
            cents += subtotal;
            pieces += quantity;
            const rowOutput = input.closest("tr")?.querySelector("[data-cash-line-subtotal]");
            if (rowOutput) rowOutput.textContent = formatMoney(subtotal / 100);
            input.closest("tr")?.classList.toggle("has-cash-quantity", quantity > 0);
            const group = input.closest("[data-cash-kind]")?.dataset.cashKind;
            if (group) groups.set(group, (groups.get(group) || 0) + subtotal);
            const decrease = input.closest(".cashier-quantity-control")?.querySelector('[data-cash-step="-1"]');
            const increase = input.closest(".cashier-quantity-control")?.querySelector('[data-cash-step="1"]');
            if (decrease) decrease.disabled = quantity === 0;
            if (increase) increase.disabled = quantity === 2147483647;
        });
        totalOutputs.forEach(output => output.textContent = formatMoney(cents / 100));
        if (totalInput) totalInput.value = (cents / 100).toFixed(2);
        form.querySelectorAll("[data-cash-piece-count]").forEach(output => output.textContent = pieces.toLocaleString("es-GT"));
        form.querySelectorAll("[data-cash-kind]").forEach(panel => {
            const output = panel.querySelector("[data-cash-kind-total]");
            if (output) output.textContent = formatMoney((groups.get(panel.dataset.cashKind) || 0) / 100);
        });
        form.querySelectorAll("[data-cash-summary-kind]").forEach(output => {
            output.textContent = formatMoney((groups.get(output.dataset.cashSummaryKind) || 0) / 100);
        });
    };

    form.addEventListener("click", event => {
        const button = event.target.closest("[data-cash-step]");
        if (!button || !form.contains(button)) return;
        const input = button.closest(".cashier-quantity-control")?.querySelector("[data-cash-quantity]");
        if (!input) return;
        input.value = Math.min(2147483647, Math.max(0, quantityOf(input) + Number(button.dataset.cashStep))).toString();
        input.dispatchEvent(new Event("input", { bubbles: true }));
    });
    inputs.forEach(input => {
        const selectInitialZero = event => {
            if (input.value !== "0") return;
            if (event.type === "mouseup") event.preventDefault();
            input.select();
        };
        input.addEventListener("focus", selectInitialZero);
        input.addEventListener("mouseup", selectInitialZero);
        input.addEventListener("blur", () => {
            if (input.value !== "") return;
            input.value = "0";
            updateTotal();
        });
        input.addEventListener("keydown", event => {
            if ([".", ",", "-", "+", "e", "E"].includes(event.key)) event.preventDefault();
        });
        input.addEventListener("input", () => {
            if (input.value !== "") input.value = quantityOf(input).toString();
            updateTotal();
        });
    });
    updateTotal();
    });
}

function initializeCashPosAssignment() {
    const section = document.querySelector("[data-cash-pos-assignment]");
    if (!section) return;
    const usePos = section.querySelector("[data-cash-pos-use]");
    const options = section.querySelector("[data-cash-pos-options]");
    if (!usePos || !options) return;
    const posInputs = [...options.querySelectorAll('input[name="PosDeviceIds"]')];
    const updateOptions = () => {
        const disabled = !usePos.checked;
        options.hidden = disabled;
        posInputs.forEach(input => {
            input.disabled = disabled;
            if (disabled) input.checked = false;
        });
    };
    usePos.addEventListener("change", updateOptions);
    updateOptions();
}

function initializePositiveIntegerInputs() {
    document.querySelectorAll("[data-positive-integer]").forEach(input => {
        input.addEventListener("input", () => {
            input.value = input.value.replace(/\D/g, "");
        });
    });
}

function initializeSidebarSections() {
    const sections = Array.from(document.querySelectorAll("[data-sidebar-section]"));
    if (!sections.length) return;

    const storagePrefix = "sistemi.sidebar.section.";
    const setExpanded = (section, expanded, persist = true) => {
        const toggle = section.querySelector("[data-sidebar-section-toggle]");
        const indicator = section.querySelector(".sidebar-section-indicator");
        section.classList.toggle("is-collapsed", !expanded);
        toggle?.setAttribute("aria-expanded", expanded.toString());
        if (indicator) indicator.textContent = expanded ? "▾" : "▸";
        if (persist) {
            try {
                window.localStorage.setItem(`${storagePrefix}${section.dataset.sidebarSection}`, expanded ? "expanded" : "collapsed");
            } catch {
                // La navegación sigue operando si el navegador bloquea localStorage.
            }
        }
    };

    sections.forEach(section => {
        let expanded = true;
        try {
            expanded = window.localStorage.getItem(`${storagePrefix}${section.dataset.sidebarSection}`) !== "collapsed";
        } catch {
            expanded = true;
        }
        if (section.querySelector(".sidebar-link.is-active")) expanded = true;
        setExpanded(section, expanded, false);

        section.querySelector("[data-sidebar-section-toggle]")?.addEventListener("click", () => {
            setExpanded(section, section.classList.contains("is-collapsed"));
        });
    });
}

function initializeSidebarScroll() {
    const sidebarNavigation = document.querySelector(".sidebar-nav");
    if (!sidebarNavigation) return;

    const storageKey = "sistemi.sidebar.scrollTop";
    const persistPosition = () => {
        try {
            window.sessionStorage.setItem(storageKey, sidebarNavigation.scrollTop.toString());
        } catch {
            // La navegación continúa funcionando aunque el navegador bloquee el almacenamiento de sesión.
        }
    };

    try {
        const savedPosition = Number(window.sessionStorage.getItem(storageKey));
        if (Number.isFinite(savedPosition) && savedPosition >= 0) {
            window.requestAnimationFrame(() => {
                sidebarNavigation.scrollTop = savedPosition;
            });
        }
    } catch {
        // El desplazamiento simplemente inicia arriba si no hay almacenamiento disponible.
    }

    sidebarNavigation.addEventListener("scroll", persistPosition, { passive: true });
    sidebarNavigation.addEventListener("click", event => {
        if (event.target.closest("a")) persistPosition();
    });
    window.addEventListener("pagehide", persistPosition);
}

function localizeInterfaceValues() {
    const labels = new Map([
        ["Confirmed", "Confirmada"],
        ["Cancelled", "Anulada"],
        ["PartiallyReturned", "Devolución parcial"],
        ["Returned", "Devuelta"],
        ["Draft", "Borrador"],
        ["Cash", "Efectivo"],
        ["Transfer", "Transferencia"],
        ["Pos", "Tarjeta / POS"],
        ["Pending", "Pendiente"],
        ["Received", "Recibida"],
        ["Verified", "Verificada"],
        ["Rejected", "Rechazada"],
        ["Approved", "Aprobada"]
    ]);
    const statusClasses = new Map([
        ["Confirmada", "status-success"],
        ["Verificada", "status-success"],
        ["Aprobada", "status-success"],
        ["Borrador", "status-neutral"],
        ["Pendiente", "status-warning"],
        ["Recibida", "status-info"],
        ["Anulada", "status-danger"],
        ["Rechazada", "status-danger"],
        ["Devuelta", "status-neutral"],
        ["Devolución parcial", "status-warning"]
    ]);

    document.querySelectorAll(".data-table td").forEach(cell => {
        const current = cell.textContent.trim();
        const translated = labels.get(current) || current;
        if (translated !== current) cell.textContent = translated;
        const statusClass = statusClasses.get(translated);
        if (statusClass && !cell.querySelector(".status-badge")) {
            cell.innerHTML = `<span class="status-badge ${statusClass}">${translated}</span>`;
        }
    });
}

function initializeTableFilters() {
    const normalizeSearchValue = value => (value || "")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLocaleLowerCase("es");

    const contextKey = `sistemi:list-context:${location.pathname}${location.search}`;
    let restoredValues = [];
    try { restoredValues = JSON.parse(sessionStorage.getItem(contextKey) || "[]"); } catch { restoredValues = []; }
    sessionStorage.removeItem(contextKey);
    document.querySelectorAll("[data-filter-table]").forEach((container, index) => {
        const input = container.querySelector("[data-table-search]");
        const limitSelect = container.querySelector("[data-table-limit]");
        const rows = Array.from(container.querySelectorAll("tbody tr:not([data-filter-empty])"));
        const emptyMessage = container.querySelector("[data-filter-empty]");
        if (!input) return;
        const permittedLimits = limitSelect ? [...limitSelect.options].map(option => option.value) : [];
        if (limitSelect && !permittedLimits.includes(limitSelect.value)) {
            limitSelect.value = permittedLimits.includes(limitSelect.dataset.defaultTableLimit)
                ? limitSelect.dataset.defaultTableLimit
                : "10";
        }
        const synchronizeLimit = () => {
            if (!limitSelect) return;
            document.querySelectorAll("[data-table-limit-field]").forEach(field => { field.value = limitSelect.value; });
            document.querySelectorAll("[data-period-option]").forEach(link => {
                const url = new URL(link.href, window.location.origin);
                url.searchParams.set("latestTransactionsLimit", limitSelect.value);
                link.href = `${url.pathname}${url.search}${url.hash}`;
            });
            const currentUrl = new URL(window.location.href);
            currentUrl.searchParams.set("latestTransactionsLimit", limitSelect.value);
            window.history.replaceState(null, "", `${currentUrl.pathname}${currentUrl.search}${currentUrl.hash}`);
        };
        const applyFilter = () => {
            const query = normalizeSearchValue(input.value.trim());
            const requestedLimit = Number(limitSelect?.value);
            const limit = Number.isFinite(requestedLimit) ? Math.min(50, Math.max(1, requestedLimit)) : Number.POSITIVE_INFINITY;
            let visible = 0;
            let matchesCount = 0;
            rows.forEach(row => {
                const matches = !query || normalizeSearchValue(row.textContent).includes(query);
                if (matches) matchesCount += 1;
                const isVisible = matches && matchesCount <= limit;
                row.hidden = !isVisible;
                if (isVisible) visible += 1;
            });
            if (emptyMessage) emptyMessage.hidden = visible !== 0;
        };
        if (restoredValues[index]) input.value = restoredValues[index];
        input.addEventListener("input", applyFilter);
        limitSelect?.addEventListener("change", () => {
            applyFilter();
            synchronizeLimit();
        });
        applyFilter();
    });
}

function initializeNitLookup() {
    const container = document.querySelector("[data-nit-lookup]");
    if (!container) return;

    const nitInput = container.querySelector("[data-customer-nit-input]");
    const nameInput = container.querySelector("[data-customer-name-input]");
    const addressInput = container.querySelector("[data-customer-address-input]");
    const phoneInput = container.querySelector("[data-customer-phone-input]");
    const visitOriginInput = container.querySelector("[data-customer-visit-origin-input]");
    const customerIdInput = document.querySelector("[data-customer-id-input]");
    const creditDueDateInput = document.querySelector("[data-credit-due-date]");
    const creditTerms = document.querySelector("[data-credit-terms]");
    const searchButton = container.querySelector("[data-customer-nit-search]");
    const status = container.querySelector("[data-customer-nit-status]");
    let debounceId;
    let phoneDebounceId;
    let requestController;
    let phoneRequestController;

    const normalizeNit = value => (value || "").replace(/[^a-zA-Z0-9]/g, "").toUpperCase();
    const setStatus = message => {
        if (status) status.textContent = message;
    };
    const isFinalConsumer = () => normalizeNit(nitInput?.value) === "CF";
    const applyCustomer = result => {
        if (result.name) nameInput.value = result.name;
        if (result.address) addressInput.value = result.address;
        if (phoneInput) {
            phoneInput.value = result.phone || phoneInput.value || "";
            phoneInput.readOnly = Boolean(result.customerId && result.phone);
        }
        if (visitOriginInput) {
            visitOriginInput.value = result.visitOrigin || "";
            visitOriginInput.readOnly = Boolean(result.customerId && result.visitOrigin);
        }
        if (customerIdInput) customerIdInput.value = result.customerId || "";
        if (creditDueDateInput) {
            const dueDate = new Date();
            dueDate.setHours(12, 0, 0, 0);
            dueDate.setDate(dueDate.getDate() + Number(result.creditDays || 0));
            creditDueDateInput.value = dueDate.toISOString().slice(0, 10);
        }
        if (creditTerms) creditTerms.textContent = `${result.creditDays || 0} días de crédito · ${result.graceDays || 0} días adicionales de gracia.`;
    };
    const lookup = async () => {
        const normalizedNit = normalizeNit(nitInput?.value);
        if (!normalizedNit || normalizedNit === "CF") {
            setStatus("Consumidor final: nombre, ciudad, teléfono y procedencia son opcionales.");
            return;
        }

        if (!navigator.onLine) {
            setStatus("No se pudo conectar con el servidor. Verifique la conexión e intente nuevamente.");
            return;
        }

        requestController?.abort();
        requestController = new AbortController();
        searchButton.disabled = true;
        setStatus("Consultando NIT...");
        try {
            const response = await fetch(`/Sales/LookupCustomerByNit?nit=${encodeURIComponent(nitInput.value)}`, {
                credentials: "same-origin",
                cache: "no-store",
                signal: requestController.signal
            });
            if (!response.ok) throw new Error("No fue posible consultar el NIT.");
            const result = await response.json();
            if (result.nit) nitInput.value = result.nit;
            applyCustomer(result);
            setStatus(result.message || "Completa la razón social manualmente.");
        } catch (error) {
            if (error.name !== "AbortError") {
                setStatus("La consulta no está disponible. Ingresa la razón social manualmente.");
            }
        } finally {
            searchButton.disabled = false;
        }
    };

    nitInput?.addEventListener("beforeinput", event => {
        if (!isFinalConsumer() || !event.inputType?.startsWith("insert") || !event.data) return;
        event.preventDefault();
        nitInput.value = event.data;
        nitInput.dispatchEvent(new Event("input", { bubbles: true }));
    });
    nitInput?.addEventListener("input", () => {
        if (!nitInput.value.trim()) {
            nitInput.value = "C/F";
            setStatus("Consumidor final: nombre, ciudad, teléfono y procedencia son opcionales.");
        }
        if (customerIdInput) customerIdInput.value = "";
        if (phoneInput) phoneInput.readOnly = false;
        if (visitOriginInput) visitOriginInput.readOnly = false;
        window.clearTimeout(debounceId);
        if (!isFinalConsumer() && normalizeNit(nitInput.value).length >= 2) {
            debounceId = window.setTimeout(lookup, 500);
        }
    });
    phoneInput?.addEventListener("input", () => {
        if (!isFinalConsumer() || phoneInput.readOnly) return;
        if (customerIdInput) customerIdInput.value = "";
        window.clearTimeout(phoneDebounceId);
        const normalizedPhone = phoneInput.value.replace(/\D/g, "");
        if (normalizedPhone.length < 6 || !navigator.onLine) return;
        phoneDebounceId = window.setTimeout(async () => {
            phoneRequestController?.abort();
            phoneRequestController = new AbortController();
            try {
                const response = await fetch(`/Sales/LookupCustomerByPhone?phone=${encodeURIComponent(phoneInput.value)}`, {
                    credentials: "same-origin",
                    cache: "no-store",
                    signal: phoneRequestController.signal
                });
                if (!response.ok) return;
                const result = await response.json();
                if (!result.found) return;
                applyCustomer(result);
                setStatus(result.visitOrigin
                    ? "Cliente recuperado por teléfono; la procedencia ya está registrada."
                    : "Cliente recuperado por teléfono; puedes completar la procedencia si lo deseas.");
            } catch (error) {
                if (error.name !== "AbortError") setStatus("No fue posible buscar por teléfono; puedes continuar la venta.");
            }
        }, 500);
    });
    searchButton?.addEventListener("click", lookup);
    if (!nitInput?.value?.trim()) nitInput.value = "C/F";
    if (isFinalConsumer()) setStatus("Consumidor final: nombre, ciudad, teléfono y procedencia son opcionales.");
}

function initializePos() {
    const pos = document.querySelector("[data-pos]");
    if (!pos) return;
    if (pos.dataset.posInitialized === "true") return;
    pos.dataset.posInitialized = "true";

    const saleForm = pos.querySelector("form[data-server-sale]");
    const productRows = Array.from(pos.querySelectorAll("[data-product-id]"));
    const products = new Map(productRows.map(row => [row.dataset.productId, {
        name: row.dataset.productName,
        salePrice: Number(row.dataset.productPrice || 0),
        multiPriceEnabled: row.dataset.multiPriceEnabled === "true",
        multiPriceMode: row.dataset.multiPriceMode,
        priceRules: JSON.parse(row.dataset.priceRules || "[]")
    }]));
    const cartLines = pos.querySelector("[data-cart-lines]");
    const lineTemplate = pos.querySelector("#sale-line-template");
    const emptyState = pos.querySelector("[data-cart-empty]");
    const lineCounts = Array.from(pos.querySelectorAll("[data-line-count]"));
    const total = pos.querySelector("[data-sale-total]");
    const subtotal = pos.querySelector("[data-sale-subtotal]");
    const discountTotal = pos.querySelector("[data-sale-discount]");
    const checkoutButton = pos.querySelector("[data-checkout-button]");
    const openCheckoutButton = pos.querySelector("[data-open-checkout]");
    const cashIsValid = () => !pos.hasAttribute("data-cash-state-url") || pos.dataset.cashValidated === "true"
        && pos.dataset.cashSessionId === saleForm?.querySelector('[name="CashSessionId"]')?.value
        && pos.dataset.cashTerminalId === pos.querySelector('[data-pos-terminal]')?.value;
    const generateProformaButton = pos.querySelector("[data-generate-proforma]");
    const checkoutDialog = pos.querySelector("[data-checkout-dialog]");
    const checkoutDialogTotal = pos.querySelector("[data-checkout-dialog-total]");
    const discountApprovalState = pos.querySelector("[data-discount-approval-state]");
    const discountApprovalTitle = pos.querySelector("[data-discount-approval-title]");
    const discountApprovalMessage = pos.querySelector("[data-discount-approval-message]");
    const lineDiscountDialog = pos.querySelector("[data-line-discount-dialog]");
    const lineDiscountDialogTitle = pos.querySelector("[data-line-discount-dialog-title]");
    const lineDiscountProduct = pos.querySelector("[data-line-discount-product]");
    const lineDiscountFields = pos.querySelector("[data-line-discount-fields]");
    const discountTypeInput = pos.querySelector("[data-discount-type]");
    const discountValueInput = pos.querySelector("[data-discount-value]");
    const discountValueLabel = pos.querySelector("[data-discount-value-label]");
    const discountAvailable = pos.querySelector("[data-line-discount-available]");
    const discountComment = pos.querySelector("[data-discount-comment]");
    const discountApprovalId = pos.querySelector("[data-discount-approval-id]");
    const discountError = pos.querySelector("[data-line-discount-error]");
    const confirmLineDiscount = pos.querySelector("[data-confirm-line-discount]");
    const productFilter = pos.querySelector("[data-product-filter]");
    let productSearchForm = pos.querySelector("[data-product-search-form]");
    if (productFilter && !productSearchForm) {
        productSearchForm = document.createElement("form");
        productSearchForm.id = "product-search-form";
        productSearchForm.autocomplete = "off";
        productSearchForm.dataset.productSearchForm = "";
        pos.prepend(productSearchForm);
    }
    if (productFilter && productSearchForm) productFilter.setAttribute("form", productSearchForm.id);
    const productFilterEmpty = pos.querySelector("[data-product-filter-empty]");
    const productResultCount = pos.querySelector("[data-product-result-count]");
    const categoryFilters = Array.from(pos.querySelectorAll("[data-product-category-filter]"));
    const catalogList = pos.querySelector("[data-product-list]");
    const catalogViewButtons = Array.from(pos.querySelectorAll("[data-catalog-view]"));
    const quickPaymentButtons = Array.from(pos.querySelectorAll("[data-quick-payment]"));
    let selectedCategory = "";
    const productDetailDialog = pos.querySelector("[data-product-detail-dialog]");
    const receivedAmount = pos.querySelector("[data-received-amount]");
    const transferAmount = pos.querySelector("[data-transfer-amount]");
    const transferDate = pos.querySelector("[data-transfer-date]");
    const transferDateDisplay = pos.querySelector("[data-transfer-date-display]");
    const changeAmount = pos.querySelector("[data-change-amount]");
    const paymentSelect = pos.querySelector("[data-payment-select]");
    const paymentTabs = Array.from(pos.querySelectorAll("[data-payment-tab]"));
    const paymentPanels = Array.from(pos.querySelectorAll("[data-payment-panel]"));
    const paymentDetailDialogs = Array.from(pos.querySelectorAll("[data-payment-detail-dialog]"));
    let activeDiscountLine = null;
    let lineDiscountMode = "apply";
    let approvalPollTimer = null;
    let approvalIsPending = false;
    let saleIsSubmitting = false;
    let validatingCashForSale = false;
    let receiptWindow = null;
    let outOfStockSearchNotified = false;
    const checkoutControls = checkoutDialog ? Array.from(checkoutDialog.querySelectorAll("input, select, textarea, button")) : [];
    const checkoutControlStates = new Map(checkoutControls.map(control => [control, control.disabled]));

    const setCheckoutLocked = locked => {
        checkoutControls.forEach(control => { control.disabled = locked || checkoutControlStates.get(control) === true; });
        checkoutDialog?.classList.toggle("is-approval-locked", locked);
    };
    const showApprovalState = (kind, title, message, locked) => {
        if (!discountApprovalState) return;
        discountApprovalState.hidden = false;
        discountApprovalState.classList.toggle("is-approved", kind === "approved");
        discountApprovalState.classList.toggle("is-error", kind === "error");
        if (discountApprovalTitle) discountApprovalTitle.textContent = title;
        if (discountApprovalMessage) discountApprovalMessage.textContent = message;
        setCheckoutLocked(locked);
    };
    const hideApprovalState = () => {
        if (discountApprovalState) discountApprovalState.hidden = true;
        setCheckoutLocked(false);
    };
    const prepareReceiptWindow = () => {
        if (receiptWindow && !receiptWindow.closed) return receiptWindow;
        receiptWindow = window.open("", "_blank", "popup,width=1100,height=800");
        return receiptWindow;
    };
    const closePreparedReceiptWindow = () => {
        if (receiptWindow && !receiptWindow.closed) receiptWindow.close();
        receiptWindow = null;
    };

    if (transferDateDisplay && transferDate) {
        const syncTransferDate = () => {
            if (!transferDateDisplay.value) return;
            const currentTime = transferDate.value.includes("T") ? transferDate.value.split("T")[1] : "00:00";
            transferDate.value = `${transferDateDisplay.value}T${currentTime}`;
        };
        transferDateDisplay.addEventListener("change", syncTransferDate);
        transferDateDisplay.addEventListener("beforeinput", event => event.preventDefault());
        transferDateDisplay.addEventListener("paste", event => event.preventDefault());
        transferDateDisplay.addEventListener("drop", event => event.preventDefault());
        transferDateDisplay.addEventListener("keydown", event => {
            if (/^[0-9/]$/.test(event.key)) event.preventDefault();
        });
    }

    const formatMoney = value => {
        const decimals = Number(pos.dataset.currencyDecimals || 2);
        const parts = value.toFixed(decimals).split('.');
        const number = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, pos.dataset.currencyThousandsSeparator || ',')
            + (decimals ? (pos.dataset.currencyDecimalSeparator || '.') + parts[1] : '');
        const symbol = pos.dataset.currencySymbol || 'Q';
        return pos.dataset.currencyPosition === 'After' ? `${number} ${symbol}` : `${symbol} ${number}`;
    };
    const getLines = () => Array.from(pos.querySelectorAll("[data-sale-line]"));
    const isFixedDiscount = type => type === "FixedAmount" || type === "2";
    const getLineQuote = line => {
        const quantity = Number(line.querySelector("[data-line-quantity]")?.value || 0);
        const productId = line.querySelector("[data-line-product]")?.value;
        const product = products.get(productId) || { salePrice: Number(line.dataset.productPrice || 0) };
        return { quantity, quote: window.SistemiPricing.calculate(product, quantity) };
    };
    const getLineDiscount = (line, grossValue) => {
        const type = line.querySelector("[data-line-discount-type]")?.value || "0";
        const rawValue = Number(line.querySelector("[data-line-discount-value]")?.value || 0);
        const amount = isFixedDiscount(type) ? rawValue : grossValue * rawValue / 100;
        return { type, rawValue, amount: Math.round((amount + Number.EPSILON) * 100) / 100 };
    };
    const updateLineDiscountDisplay = (line, grossValue) => {
        const discount = getLineDiscount(line, grossValue);
        const applyButton = line.querySelector("[data-apply-line-discount]");
        const summary = line.querySelector("[data-line-discount-summary]");
        const copy = line.querySelector("[data-line-discount-copy]");
        const isApplied = discount.rawValue > 0;
        if (applyButton) {
            applyButton.hidden = isApplied;
            applyButton.textContent = "Aplicar descuento";
        }
        if (summary) summary.hidden = !isApplied;
        if (copy && isApplied) {
            copy.textContent = isFixedDiscount(discount.type)
                ? `Descuento: -${formatMoney(discount.amount)}`
                : `Descuento: ${discount.rawValue.toLocaleString("es-GT")}% (-${formatMoney(discount.amount)})`;
        }
    };
    const updateDiscountValueLabel = () => {
        const fixed = isFixedDiscount(discountTypeInput?.value || "");
        if (discountValueLabel) discountValueLabel.textContent = fixed ? `Monto fijo (${pos.dataset.currencySymbol || "Q"})` : "Porcentaje (%)";
        if (discountValueInput) discountValueInput.max = fixed ? "" : "100";
    };
    const openLineDiscountDialog = (line, mode) => {
        if (!lineDiscountDialog) return;
        activeDiscountLine = line;
        lineDiscountMode = mode;
        const { quote } = getLineQuote(line);
        const currentType = line.querySelector("[data-line-discount-type]")?.value || "Percentage";
        const currentValue = line.querySelector("[data-line-discount-value]")?.value || "";
        if (lineDiscountDialogTitle) lineDiscountDialogTitle.textContent = mode === "remove" ? "Quitar descuento" : mode === "change" ? "Cambiar descuento" : "Aplicar descuento";
        if (lineDiscountProduct) lineDiscountProduct.textContent = line.querySelector(".sale-line-copy > strong")?.textContent || "Producto";
        if (lineDiscountFields) lineDiscountFields.hidden = mode === "remove";
        if (discountTypeInput) discountTypeInput.value = currentType === "None" || currentType === "0" ? "Percentage" : currentType;
        if (discountValueInput) discountValueInput.value = mode === "apply" ? "" : currentValue;
        if (discountAvailable) discountAvailable.textContent = mode === "remove" ? "" : `Total disponible en la línea: ${formatMoney(quote.grossAmount)}`;
        if (discountComment) discountComment.value = mode === "apply" ? "" : line.querySelector("[data-line-discount-comment]")?.value || "";
        if (discountError) discountError.textContent = "";
        updateDiscountValueLabel();
        lineDiscountDialog.showModal();
        (mode === "remove" ? confirmLineDiscount : discountValueInput)?.focus();
    };
    const invalidateDiscountApproval = () => {
        if (discountApprovalId?.value) {
            discountApprovalId.value = "";
            window.showSystemToast?.("info", "El carrito cambió. El descuento deberá enviarse nuevamente para aprobación.");
        }
        approvalIsPending = false;
        if (approvalPollTimer) window.clearTimeout(approvalPollTimer);
        approvalPollTimer = null;
        hideApprovalState();
    };
    const normalizeIndexes = () => getLines().forEach((line, index) => line.querySelectorAll("[name]").forEach(input => {
        input.name = input.name.replace(/Items\[\d+\]/, `Items[${index}]`);
    }));
    const removeLineDiscount = line => {
        line.querySelector("[data-line-discount-type]").value = "None";
        line.querySelector("[data-line-discount-value]").value = "0";
        line.querySelector("[data-line-discount-percent]").value = "0";
        line.querySelector("[data-line-discount-comment]").value = "";
        invalidateDiscountApproval();
        updateSummary();
    };
    const bindLine = line => {
        if (pos.classList.contains('pos-sales-workspace')) {
            const source = productRows.find(row => row.dataset.productId === line.querySelector('[data-line-product]')?.value);
            const thumbnail = document.createElement('span');
            thumbnail.className = 'pos-line-thumbnail'; thumbnail.setAttribute('aria-hidden', 'true');
            thumbnail.textContent = (source?.dataset.productName || 'P').slice(0, 1).toUpperCase();
            if (source?.dataset.productImage) {
                const image = document.createElement('img'); image.src = source.dataset.productImage; image.alt = ''; image.loading = 'lazy';
                image.addEventListener('error', () => image.remove(), { once: true }); thumbnail.append(image);
            }
            line.querySelector('.sale-line-copy')?.prepend(thumbnail);
        }
        line.querySelector("[data-increase]")?.addEventListener("click", () => { const input = line.querySelector("[data-line-quantity]"); input.value = (Number(input.value || 0) + 1).toString(); invalidateDiscountApproval(); updateSummary(); });
        line.querySelector("[data-decrease]")?.addEventListener("click", () => { const input = line.querySelector("[data-line-quantity]"); input.value = Math.max(.01, Number(input.value || 1) - 1).toString(); invalidateDiscountApproval(); updateSummary(); });
        line.querySelector("[data-remove-line]")?.addEventListener("click", () => { line.remove(); invalidateDiscountApproval(); normalizeIndexes(); updateSummary(); });
        line.querySelector("[data-apply-line-discount]")?.addEventListener("click", () => openLineDiscountDialog(line, "apply"));
        line.querySelector("[data-change-line-discount]")?.addEventListener("click", () => openLineDiscountDialog(line, "change"));
        line.querySelector("[data-remove-line-discount]")?.addEventListener("click", () => removeLineDiscount(line));
        line.querySelector("[data-line-quantity]")?.addEventListener("input", () => { invalidateDiscountApproval(); updateSummary(); });
    };
    const addProduct = (productId, forceNew = false) => {
        const product = products.get(productId);
        const source = productRows.find(row => row.dataset.productId === productId);
        if (!product || !cartLines || !lineTemplate || source?.disabled || source?.getAttribute("aria-disabled") === "true") return;
        const existing = forceNew ? null : getLines().find(line => line.querySelector("[data-line-product]")?.value === productId);
        if (existing) { const quantity = existing.querySelector("[data-line-quantity]"); quantity.value = (Number(quantity.value || 0) + 1).toString(); invalidateDiscountApproval(); updateSummary(); return; }
        const fragment = lineTemplate.content.cloneNode(true);
        const line = fragment.querySelector("[data-sale-line]");
        const index = getLines().length;
        line.querySelectorAll("[name]").forEach(input => input.name = input.name.replace("__index__", index));
        line.dataset.productPrice = product.salePrice;
        line.querySelector("[data-line-product]").value = productId;
        line.querySelector("[data-line-name]").textContent = product.name;
        cartLines.insertBefore(fragment, emptyState);
        invalidateDiscountApproval();
        bindLine(cartLines.querySelectorAll("[data-sale-line]")[index]);
        updateSummary();
        cartLines.scrollTop = cartLines.scrollHeight;
    };
    const updateSummary = () => {
        let selectedLines = 0;
        let amount = 0;
        let grossAmount = 0;
        let hasInvalidDiscount = false;
        getLines().forEach(line => {
            const quantity = Number(line.querySelector('[data-line-quantity]')?.value || 0);
            if (quantity <= 0) return;
            selectedLines += 1;
            const productId = line.querySelector('[data-line-product]')?.value;
            const product = products.get(productId) || { salePrice: Number(line.dataset.productPrice || 0) };
            const quote = window.SistemiPricing.calculate(product, quantity);
            const grossValue = quote.grossAmount;
            const discount = getLineDiscount(line, grossValue);
            const persistedPercent = line.querySelector("[data-line-discount-percent]");
            if (persistedPercent) persistedPercent.value = isFixedDiscount(discount.type) && grossValue > 0
                ? (discount.amount / grossValue * 100).toString()
                : discount.rawValue.toString();
            const hasDiscount = discount.rawValue > 0;
            const discountIsValid = discount.rawValue >= 0 && discount.amount <= grossValue;
            hasInvalidDiscount ||= !discountIsValid;
            const value = grossValue - Math.min(grossValue, Math.max(0, discount.amount));
            grossAmount += grossValue;
            amount += value;
            const totalCell = line.querySelector("[data-line-total]");
            if (totalCell) totalCell.textContent = formatMoney(value);
            const meta = line.querySelector("[data-line-meta]");
            if (meta) {
                meta.textContent = quote.appliedMode === "ExactQuantity"
                    ? `${formatMoney(grossValue)} total por ${quantity}`
                    : `${formatMoney(quote.unitPrice)} c/u${quote.appliedMode ? " · multiprecio" : ""}`;
            }
            updateLineDiscountDisplay(line, grossValue);
        });
        lineCounts.forEach(counter => counter.textContent = selectedLines.toString());
        const clearCart = pos.querySelector('[data-clear-cart]');
        if (clearCart) clearCart.disabled = selectedLines === 0 || saleIsSubmitting || validatingCashForSale || approvalIsPending;
        const selectedProductIds = new Set(getLines().map(line => line.querySelector("[data-line-product]")?.value));
        productRows.forEach(row => row.classList.toggle("is-in-cart", selectedProductIds.has(row.dataset.productId)));
        quickPaymentButtons.forEach(button => button.disabled = selectedLines === 0 || hasInvalidDiscount || !cashIsValid());
        if (emptyState) emptyState.hidden = selectedLines !== 0;
        if (subtotal) subtotal.textContent = formatMoney(grossAmount);
        if (discountTotal) discountTotal.textContent = formatMoney(grossAmount - amount);
        if (total) total.textContent = formatMoney(amount);
        if (checkoutDialogTotal) checkoutDialogTotal.textContent = formatMoney(amount);
        const expectedTotal = saleForm?.querySelector('[data-expected-total]');
        if (expectedTotal) expectedTotal.value = amount.toFixed(2);
        if (openCheckoutButton) {
            const label = openCheckoutButton.querySelector('[data-open-checkout-label]') || openCheckoutButton;
            label.textContent = `Cobrar ${formatMoney(amount)}`;
            openCheckoutButton.disabled = selectedLines === 0 || hasInvalidDiscount || !cashIsValid();
            openCheckoutButton.title = hasInvalidDiscount ? "Corrija el descuento antes de cobrar." : "";
        }
        if (checkoutButton) {
            checkoutButton.textContent = `CONFIRMAR ${formatMoney(amount)}`;
            checkoutButton.disabled = selectedLines === 0 || hasInvalidDiscount || !cashIsValid() || saleIsSubmitting || validatingCashForSale || approvalIsPending;
        }
        if (transferAmount) transferAmount.value = amount.toFixed(Number(pos.dataset.currencyDecimals || 2));
        if (transferDate && !transferDate.value) {
            const now = new Date();
            now.setMinutes(now.getMinutes() - now.getTimezoneOffset());
            transferDate.value = now.toISOString().slice(0, 16);
        }
        if (transferDateDisplay && transferDate) {
            transferDateDisplay.value = transferDate.value.slice(0, 10);
        }
        if (changeAmount && receivedAmount) {
            const received = Number((receivedAmount.value || "").replace(/[^0-9.,-]/g, "").replace(",", ".")) || 0;
            const change = Math.max(0, received - amount);
            changeAmount.textContent = formatMoney(change);
        }
    };

    const normalizeSearch = value => (value || "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("es-GT").trim();
    const filterProducts = () => {
        const term = normalizeSearch(productFilter?.value);
        let visible = 0;
        let unavailable = 0;
        productRows.forEach(row => {
            const haystack = normalizeSearch(`${row.dataset.productName || ""} ${row.dataset.productCode || ""} ${row.dataset.productBarcode || ""} ${row.dataset.productCategoryName || ""}`);
            row.hidden = (term.length > 0 && !haystack.includes(term))
                || (selectedCategory !== "" && row.dataset.productCategory !== selectedCategory);
            if (!row.hidden) {
                visible += 1;
                if (Number(row.dataset.productStock || 0) <= 0) unavailable += 1;
            }
        });
        if (productResultCount) productResultCount.textContent = `${visible} ${visible === 1 ? "producto" : "productos"}`;
        if (productFilterEmpty) productFilterEmpty.hidden = visible !== 0;
        const onlyUnavailableResults = term.length > 0 && visible > 0 && unavailable === visible;
        if (onlyUnavailableResults && !outOfStockSearchNotified) {
            window.showSystemToast?.(
                "warning",
                "El producto buscado está agotado y no se puede agregar a la venta.",
                "Producto sin existencia");
        }
        outOfStockSearchNotified = onlyUnavailableResults;
    };

    const showProductDetail = row => {
        if (!productDetailDialog) return;
        productDetailDialog.querySelector("[data-detail-name]").textContent = row.dataset.productName || "Producto";
        productDetailDialog.querySelector("[data-detail-description]").textContent = row.dataset.productDescription || "Sin descripción registrada.";
        productDetailDialog.querySelector("[data-detail-price]").textContent = formatMoney(Number(row.dataset.productPrice || 0));
        productDetailDialog.querySelector("[data-detail-stock]").textContent = Number(row.dataset.productStock || 0).toLocaleString("es-GT", { maximumFractionDigits: 2 });
        productDetailDialog.querySelector("[data-detail-code]").textContent = row.dataset.productCode || "No registrado";
        const image = productDetailDialog.querySelector("[data-detail-image]");
        const emptyImage = productDetailDialog.querySelector("[data-detail-image-empty]");
        const imageUrl = row.dataset.productImage || "";
        if (image) { image.src = imageUrl; image.alt = imageUrl ? `Imagen de ${row.dataset.productName || "producto"}` : ""; image.hidden = !imageUrl; }
        if (emptyImage) emptyImage.hidden = Boolean(imageUrl);
        productDetailDialog.showModal();
    };

    productRows.forEach(row => {
        const addButton = row.querySelector("[data-add-product]");
        if (addButton) {
            addButton.addEventListener("click", () => addProduct(row.dataset.productId));
        } else {
            // Keep the previous interaction for any cached POS markup using rows.
            row.addEventListener("dblclick", event => {
                if (event.target.closest("[data-product-detail]")) return;
                addProduct(row.dataset.productId);
            });
            row.addEventListener("keydown", event => {
                if (event.key !== "Enter" || event.target.closest("[data-product-detail]")) return;
                event.preventDefault();
                addProduct(row.dataset.productId);
            });
        }
        const image = row.querySelector("[data-catalog-image]");
        image?.addEventListener("error", () => image.hidden = true);
        if (image?.complete && image.naturalWidth === 0) image.hidden = true;
        const detailButton = row.querySelector("[data-product-detail]");
        detailButton?.addEventListener("click", event => { event.stopPropagation(); showProductDetail(row); });
        detailButton?.addEventListener("dblclick", event => event.stopPropagation());
    });
    const updateCategoryFilters = () => categoryFilters.forEach(button => {
        const active = button.dataset.productCategoryFilter === selectedCategory;
        button.classList.toggle("is-active", active);
        button.setAttribute("aria-pressed", active.toString());
    });
    categoryFilters.forEach(button => button.addEventListener("click", () => {
        selectedCategory = button.dataset.productCategoryFilter;
        updateCategoryFilters();
        filterProducts();
    }));
    catalogViewButtons.forEach(button => button.addEventListener("click", () => {
        if (catalogList) catalogList.dataset.catalogLayout = button.dataset.catalogView;
        catalogViewButtons.forEach(candidate => candidate.setAttribute("aria-pressed", (candidate === button).toString()));
    }));
    productSearchForm?.addEventListener("submit", event => {
        event.preventDefault();
        const term = normalizeSearch(productFilter?.value);
        if (!term) return;
        // A scanned exact code is resolved across the catalog, even with a category selected.
        const exactMatches = productRows.filter(row => [row.dataset.productBarcode, row.dataset.productCode]
            .some(code => code && normalizeSearch(code) === term));
        const visibleRows = productRows.filter(row => !row.hidden);
        const match = exactMatches.length === 1 ? exactMatches[0]
            : exactMatches.length === 0 && visibleRows.length === 1 ? visibleRows[0] : null;
        if (!match) return;
        if (match.getAttribute("aria-disabled") === "true") {
            window.showSystemToast?.("warning", "El producto está agotado y no se puede agregar a la venta.");
            return;
        }
        addProduct(match.dataset.productId);
        clearProductSearch();
    });
    productFilter?.addEventListener("input", filterProducts);
    const clearProductSearch = () => {
        if (!productFilter) return;
        productFilter.value = "";
        productFilter.defaultValue = "";
        productFilter.setAttribute("value", "");
        filterProducts();
    };
    const resetCatalogFilters = () => {
        selectedCategory = "";
        updateCategoryFilters();
        clearProductSearch();
        window.requestAnimationFrame(clearProductSearch);
        window.setTimeout(clearProductSearch, 0);
    };
    window.addEventListener("pagehide", clearProductSearch);
    window.addEventListener("pageshow", resetCatalogFilters);
    resetCatalogFilters();
    pos.querySelector("[data-clear-catalog-filters]")?.addEventListener("click", () => {
        resetCatalogFilters();
        productFilter?.focus();
    });
    document.addEventListener("keydown", event => {
        if (event.key === "F2" && !pos.hidden && !document.querySelector("dialog[open]")) {
            event.preventDefault();
            productFilter?.focus();
            productFilter?.select();
        }
    });

    openCheckoutButton?.addEventListener("click", () => { if (checkoutDialog && !checkoutDialog.open) checkoutDialog.showModal(); });
    generateProformaButton?.addEventListener("click", async () => {
        if (getLines().length === 0) {
            window.showSystemToast?.("warning", "Debe agregar al menos un producto para generar la proforma.");
            return;
        }
        if (!navigator.onLine) {
            window.showSystemToast?.("error", "Se requiere conexión con el servidor para generar la proforma.");
            return;
        }

        const proformaWindow = window.open("", "_blank");
        generateProformaButton.disabled = true;
        try {
            const response = await fetch(generateProformaButton.dataset.proformaUrl, {
                method: "POST",
                body: new FormData(saleForm),
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            if (!response.ok) {
                const payload = await response.json().catch(() => ({}));
                throw new Error(payload.message || "No fue posible generar la proforma.");
            }
            const pdfUrl = URL.createObjectURL(await response.blob());
            if (proformaWindow) {
                proformaWindow.location.href = pdfUrl;
            } else {
                const link = document.createElement("a");
                link.href = pdfUrl;
                link.download = "proforma.pdf";
                link.click();
            }
            window.setTimeout(() => URL.revokeObjectURL(pdfUrl), 60000);
        } catch (error) {
            proformaWindow?.close();
            window.showSystemToast?.("error", error.message || "No fue posible generar la proforma.");
        } finally {
            generateProformaButton.disabled = false;
        }
    });
    pos.querySelectorAll("[data-close-checkout]").forEach(button => button.addEventListener("click", () => { if (!approvalIsPending) checkoutDialog?.close(); }));
    checkoutDialog?.addEventListener("cancel", event => { if (approvalIsPending) event.preventDefault(); });
    pos.querySelectorAll("[data-close-product-detail]").forEach(button => button.addEventListener("click", () => productDetailDialog?.close()));
    productDetailDialog?.addEventListener("click", event => { if (event.target === productDetailDialog) productDetailDialog.close(); });
    pos.querySelectorAll("[data-close-line-discount]").forEach(button => button.addEventListener("click", () => lineDiscountDialog?.close()));
    lineDiscountDialog?.addEventListener("click", event => { if (event.target === lineDiscountDialog) lineDiscountDialog.close(); });
    discountTypeInput?.addEventListener("change", updateDiscountValueLabel);
    confirmLineDiscount?.addEventListener("click", async () => {
        if (!activeDiscountLine || !lineDiscountDialog) return;
        const existingType = activeDiscountLine.querySelector("[data-line-discount-type]")?.value || "Percentage";
        const existingValue = Number(activeDiscountLine.querySelector("[data-line-discount-value]")?.value || 0);
        const type = lineDiscountMode === "remove" ? existingType : discountTypeInput?.value || "Percentage";
        const value = lineDiscountMode === "remove" ? existingValue : Number(discountValueInput?.value || 0);
        const { quote } = getLineQuote(activeDiscountLine);
        const amount = isFixedDiscount(type) ? value : quote.grossAmount * value / 100;
        if (value <= 0) {
            if (discountError) discountError.textContent = "El descuento debe ser mayor a cero.";
            return;
        }
        if (amount > quote.grossAmount || (!isFixedDiscount(type) && value > 100)) {
            if (discountError) discountError.textContent = "El descuento no puede superar el total de la línea.";
            return;
        }

        confirmLineDiscount.disabled = true;
        if (discountError) discountError.textContent = "";
        try {
            if (lineDiscountMode === "remove") {
                activeDiscountLine.querySelector("[data-line-discount-type]").value = "None";
                activeDiscountLine.querySelector("[data-line-discount-value]").value = "0";
                activeDiscountLine.querySelector("[data-line-discount-percent]").value = "0";
                activeDiscountLine.querySelector("[data-line-discount-comment]").value = "";
            } else {
                activeDiscountLine.querySelector("[data-line-discount-type]").value = type;
                activeDiscountLine.querySelector("[data-line-discount-value]").value = value.toString();
                activeDiscountLine.querySelector("[data-line-discount-percent]").value = isFixedDiscount(type) ? "0" : value.toString();
                activeDiscountLine.querySelector("[data-line-discount-comment]").value = discountComment?.value.trim() || "";
            }
            invalidateDiscountApproval();
            lineDiscountDialog.close();
            updateSummary();
        } catch (error) {
            if (discountError) discountError.textContent = error.message;
        } finally {
            confirmLineDiscount.disabled = false;
        }
    });

    getLines().forEach(bindLine);
    pos.querySelector('[data-clear-cart]')?.addEventListener('click', async () => {
        if (saleIsSubmitting || validatingCashForSale || approvalIsPending || !getLines().length) return;
        const accepted = await window.showSystemConfirmation?.({ title: 'Limpiar pedido', message: 'Se quitarán los productos del pedido que aún no has cobrado.', confirmText: 'Limpiar pedido' });
        if (!accepted || saleIsSubmitting || validatingCashForSale || approvalIsPending) return;
        getLines().forEach(line => line.remove());
        invalidateDiscountApproval(); normalizeIndexes(); updateSummary();
        productFilter?.focus();
    });

    const setPayment = (value, openDetails = false) => {
        if (paymentSelect) paymentSelect.value = value;
        paymentTabs.forEach(tab => {
            const active = tab.dataset.paymentValue === value;
            tab.classList.toggle("is-active", active);
            tab.setAttribute("aria-pressed", active.toString());
        });
        paymentPanels.forEach(panel => panel.classList.toggle("is-active", panel.dataset.paymentPanel === value));
        if (openDetails) {
            const detailDialog = paymentDetailDialogs.find(dialog => dialog.dataset.paymentDetailDialog === value);
            detailDialog?.showModal();
        }
    };
    paymentTabs.forEach(tab => tab.addEventListener("click", () => setPayment(tab.dataset.paymentValue, true)));
    paymentSelect?.addEventListener("change", () => setPayment(paymentSelect.value));
    quickPaymentButtons.forEach(button => button.addEventListener("click", () => {
        if (openCheckoutButton?.disabled) return;
        openCheckoutButton?.click();
        setPayment(button.dataset.quickPayment, true);
    }));
    paymentDetailDialogs.forEach(dialog => {
        dialog.querySelectorAll("[data-close-payment-detail]").forEach(button => button.addEventListener("click", () => dialog.close()));
        dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
    });
    receivedAmount?.addEventListener("input", updateSummary);
    const resetSaleAfterSuccess = () => {
        const soldQuantities = new Map();
        getLines().forEach(line => {
            const productId = line.querySelector("[data-line-product]")?.value;
            const quantity = Number(line.querySelector("[data-line-quantity]")?.value || 0);
            if (productId && quantity > 0) soldQuantities.set(productId, (soldQuantities.get(productId) || 0) + quantity);
            line.remove();
        });

        soldQuantities.forEach((quantity, productId) => {
            const row = productRows.find(candidate => candidate.dataset.productId === productId);
            if (!row) return;
            const stock = Math.max(0, Number(row.dataset.productStock || 0) - quantity);
            const minimumStock = Number(row.dataset.productMinimumStock || 0);
            const stockLabel = row.querySelector(".product-stock");
            row.dataset.productStock = stock.toString();
            row.classList.toggle("is-unavailable", stock <= 0);
            row.setAttribute("aria-disabled", stock <= 0 ? "true" : "false");
            const addButton = row.querySelector("[data-add-product]");
            if (addButton) addButton.disabled = stock <= 0;
            else row.tabIndex = stock <= 0 ? -1 : 0;
            if (stockLabel) {
                stockLabel.textContent = stock <= 0 ? "Agotado" : `Existencia: ${stock.toLocaleString("es-GT")}`;
                stockLabel.classList.toggle("is-out", stock <= 0);
                stockLabel.classList.toggle("is-low", stock > 0 && stock <= minimumStock);
                stockLabel.classList.toggle("is-normal", stock > minimumStock);
            }
        });

        saleForm.reset();
        if (discountApprovalId) discountApprovalId.value = "";
        approvalIsPending = false;
        if (approvalPollTimer) window.clearTimeout(approvalPollTimer);
        approvalPollTimer = null;
        activeDiscountLine = null;
        hideApprovalState();
        checkoutDialog?.close();
        paymentDetailDialogs.forEach(dialog => dialog.open && dialog.close());

        const customerNit = saleForm.querySelector("[data-customer-nit-input]");
        saleForm.querySelector("[data-customer-phone-input]")?.removeAttribute("readonly");
        saleForm.querySelector("[data-customer-visit-origin-input]")?.removeAttribute("readonly");
        if (transferDate) transferDate.value = "";
        if (transferDateDisplay) transferDateDisplay.value = "";
        customerNit?.dispatchEvent(new Event("input", { bubbles: true }));
        if (paymentSelect) setPayment(paymentSelect.value);
        updateSummary();
        filterProducts();
        productFilter?.focus();
    };
    const submitSale = async (approvedContinuation = false) => {
        if (saleIsSubmitting || validatingCashForSale || (approvalIsPending && !approvedContinuation)) return;

        if (approvedContinuation) setCheckoutLocked(false);
        if (!saleForm?.reportValidity()) {
            if (approvedContinuation) {
                showApprovalState("error", "Revise los datos de cobro", "El descuento fue aprobado, pero falta completar información requerida para registrar la venta.", false);
            }
            return;
        }
        if (!navigator.onLine) {
            showApprovalState("error", "Sin conexión con el servidor", "No se pudo conectar con el servidor. Verifique la conexión e intente nuevamente.", false);
            return;
        }

        if (pos.hasAttribute('data-cash-state-url')) {
            validatingCashForSale = true;
            if (checkoutButton) { checkoutButton.disabled = true; checkoutButton.textContent = 'Validando caja…'; checkoutButton.setAttribute('aria-busy', 'true'); }
            try {
                if (!await window.sistemiPosCash?.validateForCheckout()) {
                    if (approvedContinuation) setCheckoutLocked(false);
                    window.showSystemToast?.('warning', 'No se pudo validar el turno con el servidor. El pedido se conserva.');
                    return;
                }
            } finally {
                validatingCashForSale = false;
                checkoutButton?.removeAttribute('aria-busy');
                updateSummary();
            }
        }

        if (!approvedContinuation) prepareReceiptWindow();
        const formData = new FormData(saleForm);
        saleIsSubmitting = true;
        if (approvedContinuation) {
            showApprovalState("approved", "Descuento aprobado", "Registrando la venta automáticamente…", true);
        } else if (checkoutButton) {
            checkoutButton.disabled = true;
            checkoutButton.textContent = 'Registrando venta…';
            checkoutButton.setAttribute("aria-busy", "true");
        }

        try {
            const response = await fetch(saleForm.action || window.location.href, {
                method: "POST",
                body: formData,
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });
            const payload = await response.json().catch(() => ({}));
            if (response.status === 202 && payload.approvalRequired) {
                discountApprovalId.value = payload.approvalId;
                approvalIsPending = true;
                showApprovalState(
                    "pending",
                    "Descuento pendiente de aprobación",
                    payload.message || "La solicitud fue enviada a un superior. Esta ventana continuará automáticamente cuando sea aprobada.",
                    true);
                if (checkoutDialog && !checkoutDialog.open) checkoutDialog.showModal();
                approvalPollTimer = window.setTimeout(pollDiscountApproval, 1000);
                return;
            }
            if (!response.ok) {
                const responseError = new Error(payload.message || payload.detail || "No fue posible completar el cobro.");
                responseError.offlineEligible = response.status >= 500;
                throw responseError;
            }
            if (!payload.redirectUrl) throw new Error("El servidor no devolvió el comprobante de la venta.");
            const targetWindow = receiptWindow && !receiptWindow.closed
                ? receiptWindow
                : prepareReceiptWindow();
            if (!targetWindow) {
                window.location.assign(payload.redirectUrl);
                return;
            }
            targetWindow.location.replace(payload.redirectUrl);
            receiptWindow = null;
            resetSaleAfterSuccess();
            window.dispatchEvent(new CustomEvent('pos:cash-invalidated'));
        } catch (error) {
            closePreparedReceiptWindow();
            if (!pos.hasAttribute('data-cash-state-url') && (error instanceof TypeError || error.offlineEligible === true) && window.SistemiOffline?.saveSaleFromForm) {
                try {
                    const offlineSale = await window.SistemiOffline.saveSaleFromForm(saleForm);
                    window.showSystemToast?.("success", `Venta guardada en el dispositivo. Operación ${offlineSale.clientOperationId}. Se sincronizará automáticamente al recuperar la conexión.`);
                    window.releaseSistemiPosTabLock?.();
                    window.location.assign("/pos");
                    return;
                } catch (offlineError) {
                    error = offlineError;
                }
            }
            approvalIsPending = false;
            showApprovalState("error", "No se pudo completar la venta", error.message || "No fue posible completar el cobro.", false);
            if (checkoutButton) checkoutButton.textContent = discountApprovalId?.value ? "REINTENTAR COBRO" : "CONFIRMAR COBRO";
            if (checkoutDialog && !checkoutDialog.open) checkoutDialog.showModal();
        } finally {
            saleIsSubmitting = false;
            if (checkoutButton && !approvalIsPending) {
                checkoutButton.disabled = checkoutControlStates.get(checkoutButton) === true || !cashIsValid();
                checkoutButton.removeAttribute("aria-busy");
            }
        }
    };
    const pollDiscountApproval = async () => {
        if (!approvalIsPending || !discountApprovalId?.value) return;
        try {
            const url = `${pos.dataset.discountApprovalStatusUrl}/${encodeURIComponent(discountApprovalId.value)}`;
            const response = await fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(payload.message || "No se pudo consultar la aprobación.");
            if (payload.status === "Approved") {
                approvalIsPending = false;
                showApprovalState("approved", "Descuento aprobado", `Aprobado por ${payload.resolvedByUserName || "un superior"}. Registrando la venta automáticamente…`, true);
                await submitSale(true);
                return;
            }
            if (payload.status === "Rejected") {
                approvalIsPending = false;
                discountApprovalId.value = "";
                closePreparedReceiptWindow();
                showApprovalState("error", "Descuento rechazado", payload.decisionComment ? `Motivo: ${payload.decisionComment}` : "El superior rechazó el descuento. Puede revisar el cobro o cerrar esta ventana para cambiarlo.", false);
                if (checkoutButton) checkoutButton.textContent = "SOLICITAR APROBACIÓN NUEVAMENTE";
                if (checkoutDialog && !checkoutDialog.open) checkoutDialog.showModal();
                return;
            }
        } catch (error) {
            showApprovalState("pending", "Esperando aprobación", `${error.message || "No se pudo consultar la aprobación."} Se intentará nuevamente.`, true);
        }
        approvalPollTimer = window.setTimeout(pollDiscountApproval, 3000);
    };
    saleForm?.addEventListener("submit", async event => {
        event.preventDefault();
        await submitSale(false);
    });
    let cashSessionForDiscounts = saleForm?.querySelector('[name="CashSessionId"]')?.value;
    pos.addEventListener('pos:cash-state', () => {
        const currentSession = saleForm?.querySelector('[name="CashSessionId"]')?.value;
        if (currentSession !== cashSessionForDiscounts) {
            invalidateDiscountApproval();
            cashSessionForDiscounts = currentSession;
        }
        updateSummary();
    });
    if (paymentSelect) setPayment(paymentSelect.value);
    updateSummary();
    if (discountApprovalId?.value) {
        approvalIsPending = true;
        showApprovalState("pending", "Descuento pendiente de aprobación", "La solicitud fue enviada a un superior. Esta ventana continuará automáticamente cuando sea aprobada.", true);
        if (checkoutDialog && !checkoutDialog.open) checkoutDialog.showModal();
        approvalPollTimer = window.setTimeout(pollDiscountApproval, 1000);
    }
}
