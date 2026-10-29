(function () {
    "use strict";

    const elements = {
        connectionState: document.getElementById("offline-connection-state"),
        lastSync: document.getElementById("offline-last-sync"),
        pendingCount: document.getElementById("offline-pending-count"),
        pendingBody: document.getElementById("offline-pending-sales-body"),
        receiptPreview: document.getElementById("offline-receipt-preview"),
        refreshData: document.getElementById("offline-refresh-data"),
        syncNow: document.getElementById("offline-sync-now"),
        actionMessage: document.getElementById("offline-action-message")
    };

    if (!elements.pendingBody || !window.SistemiOffline) {
        return;
    }

    window.addEventListener("sistemi:offline-state", (event) => render(event.detail));
    elements.refreshData?.addEventListener("click", refreshData);
    elements.syncNow?.addEventListener("click", syncNow);
    renderCurrentState();

    async function refreshData() {
        setBusy(elements.refreshData, true);
        elements.actionMessage.textContent = "Actualizando datos locales...";
        try {
            await window.SistemiOffline.refreshBootstrap();
            elements.actionMessage.textContent = "Datos locales actualizados correctamente.";
        } catch (error) {
            elements.actionMessage.textContent = error.message || "No fue posible actualizar los datos.";
        } finally {
            setBusy(elements.refreshData, false);
            await renderCurrentState();
        }
    }

    async function syncNow() {
        setBusy(elements.syncNow, true);
        elements.actionMessage.textContent = "Sincronizando operaciones pendientes...";
        try {
            const succeeded = await window.SistemiOffline.syncPendingSales({ includeConflicts: true });
            elements.actionMessage.textContent = succeeded
                ? "Sincronización finalizada."
                : "No fue posible completar la sincronización; se reintentará automáticamente.";
        } finally {
            setBusy(elements.syncNow, false);
            await renderCurrentState();
        }
    }

    async function renderCurrentState() {
        await render(await window.SistemiOffline.getState());
    }

    async function render(state) {
        elements.connectionState.textContent = state.online ? "Conectado" : "Sin conexión";
        elements.pendingCount.textContent = `${state.pendingCount} operaciones pendientes.`;
        if (state.legacyPendingCount > 0) elements.pendingCount.textContent += ` Hay ${state.legacyPendingCount} operaciones anteriores sin ubicación verificable, conservadas para conciliación manual.`;
        elements.lastSync.textContent = state.lastSync
            ? `Última actualización: ${new Date(state.lastSync).toLocaleString()}`
            : "Sin sincronización previa.";
        elements.syncNow.disabled = !state.online || state.pendingCount === 0;
        elements.refreshData.disabled = !state.online;
        elements.receiptPreview.textContent = window.SistemiOffline.getLatestReceipt() || "Aún no hay comprobante provisional.";

        elements.pendingBody.innerHTML = "";
        for (const sale of [...state.sales].sort((a, b) => new Date(b.localCreatedAtUtc) - new Date(a.localCreatedAtUtc))) {
            const row = document.createElement("tr");
            appendCell(row, sale.clientOperationId);
            appendCell(row, new Date(sale.localCreatedAtUtc).toLocaleString());
            appendCell(row, translateStatus(sale.syncStatus));
            appendCell(row, Number(sale.total).toFixed(2));
            appendCell(row, sale.finalSaleNumber || "-");
            appendCell(row, sale.error || "-");
            elements.pendingBody.appendChild(row);
        }

        if (state.sales.length === 0) {
            const row = document.createElement("tr");
            const cell = document.createElement("td");
            cell.colSpan = 6;
            cell.textContent = "No hay ventas guardadas localmente.";
            row.appendChild(cell);
            elements.pendingBody.appendChild(row);
        }
    }

    function appendCell(row, value) {
        const cell = document.createElement("td");
        cell.textContent = value;
        row.appendChild(cell);
    }

    function translateStatus(status) {
        return ({ Pending: "Pendiente", Syncing: "Sincronizando", Synced: "Sincronizada", Error: "Error", Conflict: "Conflicto" })[status] || status;
    }

    function setBusy(button, busy) {
        if (!button) {
            return;
        }
        button.disabled = busy;
        button.setAttribute("aria-busy", busy.toString());
    }
})();
