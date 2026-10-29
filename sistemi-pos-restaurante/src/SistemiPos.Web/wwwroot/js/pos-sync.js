(function () {
    "use strict";

    const databaseName = "sistemi-pos-restaurante";
    const databaseVersion = 1;
    const maxPendingSales = 500;
    const maxOfflineDays = 7;
    const retryableStatuses = new Set(["Pending", "Error", "Syncing"]);
    let syncPromise = null;
    let initialized = false;
    const quantityFormatter = new Intl.NumberFormat("es-GT", { minimumFractionDigits: 0, maximumFractionDigits: 2 });

    const api = {
        initialize,
        refreshBootstrap,
        syncPendingSales,
        getState,
        getSales: async () => (await getState()).sales,
        getLatestReceipt: () => {
            const key = localStorage.getItem("pv-current-context");
            const [userId, branchId] = (key || '').split(':');
            return samePageContext({ userId, branchId }) ? localStorage.getItem("pv-latest-offline-receipt:" + key) : null;
        },
        saveSaleFromForm
    };

    window.SistemiOffline = api;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", initialize);
    } else {
        initialize();
    }

    async function initialize() {
        if (initialized || !document.body.classList.contains("app-body")) {
            return;
        }

        initialized = true;
        await navigator.storage?.persist?.();
        await openDatabase();
        document.querySelector('[data-pos-terminal]')?.addEventListener('change', () => {
            refreshBootstrap({ silent: false }).catch(error => window.showSystemToast?.('error', error.message));
        });
        bindAutomaticOfflineSale();
        wireAutomaticSync();
        await publishState();

        if (navigator.onLine) {
            await syncPendingSales();
            await refreshBootstrap({ silent: true });
        } else {
            await applyLocalStockToSaleForm();
        }
    }

    function wireAutomaticSync() {
        window.addEventListener("online", async () => {
            await publishState();
            await syncPendingSales();
            await refreshBootstrap({ silent: true });
        });
        window.addEventListener("offline", async () => {
            await publishState();
            await applyLocalStockToSaleForm();
        });
        document.addEventListener("visibilitychange", async () => {
            if (document.visibilityState === "visible" && navigator.onLine) {
                await syncPendingSales();
            }
        });
        navigator.serviceWorker?.addEventListener("message", async (event) => {
            if (event.data?.type === "sistemi-pos-restaurante-sync-complete") {
                await publishState();
                if (navigator.onLine) {
                    await refreshBootstrap({ silent: true });
                }
            }
        });
    }

    function bindAutomaticOfflineSale() {
        const form = document.querySelector("[data-auto-offline-sale]");
        if (!form) {
            return;
        }

        const pendingNotice = sessionStorage.getItem("sistemi-pos-restaurante-notice");
        if (pendingNotice) {
            sessionStorage.removeItem("sistemi-pos-restaurante-notice");
            window.setTimeout(() => window.showSystemToast?.("warning", pendingNotice), 0);
        }

        form.addEventListener("submit", async (event) => {
            if (navigator.onLine) {
                return;
            }
            if (!form.checkValidity()) {
                return;
            }

            event.preventDefault();
            event.stopImmediatePropagation();
            const receiptWindow = null;
            const submitButton = form.querySelector("[data-checkout-button]");
            const originalText = submitButton?.textContent;
            if (submitButton) {
                submitButton.disabled = true;
                submitButton.textContent = navigator.onLine ? "PROCESANDO..." : "GUARDANDO OFFLINE...";
            }

            try {
                const sale = await saveSaleFromForm(form);
                window.showSystemToast?.("success", `Venta guardada en el dispositivo. Operación ${sale.clientOperationId}. Se sincronizará automáticamente al regresar Internet.`);
                window.releaseSistemiPosTabLock?.();
                window.location.assign("/pos");
            } catch (error) {
                receiptWindow?.close();
                window.showSystemToast?.("error", error.message || "No fue posible guardar la venta.");
                if (submitButton) {
                    submitButton.disabled = false;
                    submitButton.textContent = originalText;
                }
            }
        });
    }

    async function refreshBootstrap(options) {
        if (!navigator.onLine) {
            if (!options?.silent) {
                throw new Error("Se requiere conexión para actualizar los datos locales.");
            }
            return false;
        }

        try {
            const terminalId = document.querySelector('[name="TerminalId"]')?.value;
            const response = await fetch("/api/offline-pos/bootstrap" + (terminalId ? `?terminalId=${encodeURIComponent(terminalId)}` : ""), {
                credentials: "same-origin",
                cache: "no-store"
            });
            if (!response.ok) {
                throw new Error("No fue posible actualizar los datos locales del POS.");
            }

            const payload = await response.json();
            if (!payload.offlineContext || !samePageContext(payload.offlineContext.context)) throw new Error("Los datos offline no pertenecen a la ubicación y usuario actuales.");
            const pendingSales = (await getAll("pendingSales")).filter((sale) => sale.syncStatus !== "Synced" && sale.contextKey === payload.offlineContext.context.key);
            const reservedByProduct = new Map();
            for (const sale of pendingSales) {
                for (const item of sale.items) {
                    reservedByProduct.set(item.productId, (reservedByProduct.get(item.productId) || 0) + Number(item.quantity));
                }
            }

            const localProducts = payload.products.map((item) => ({
                id: item.id,
                internalCode: item.internalCode,
                barcode: item.barcode,
                name: item.name,
                salePrice: Number(item.salePrice),
                multiPriceEnabled: Boolean(item.multiPriceEnabled),
                multiPriceMode: item.multiPriceMode,
                priceRules: item.priceRules || [],
                lastSyncedStock: Number(item.lastSyncedStock),
                localAvailableStock: Math.max(0, Number(item.lastSyncedStock) - (reservedByProduct.get(item.id) || 0))
            }));
            await replaceBootstrapAtomically({ products: localProducts, customers: payload.customers,
                sessions: payload.cashSessions, bankAccounts: payload.bankAccounts,
                settings: [{ id: "current", ...payload.settings, offlineContext: payload.offlineContext, antiforgeryToken: payload.antiforgeryToken }] });
            localStorage.setItem("pv-current-context", payload.offlineContext.context.key);
            localStorage.setItem("pv-last-successful-sync", payload.settings.syncedAtUtc);
            await applyLocalStockToSaleForm();
            await publishState();
            return true;
        } catch (error) {
            if (!options?.silent) {
                throw error;
            }
            return false;
        }
    }

    async function saveSaleFromForm(form) {
        if (form.closest('[data-cash-state-url]')) throw new Error('El punto de venta requiere validar la caja con el servidor antes de cobrar. El pedido se conserva.');
        const [products, settings, pendingSales] = await Promise.all([
            getAll("products"),
            get("settings", "current"),
            getAll("pendingSales")
        ]);
        if (!settings || products.length === 0) {
            throw new Error("Este dispositivo aún no tiene datos offline. Conéctalo a Internet y abre Ventas una vez para prepararlo.");
        }
        if (!settings.offlineContext || !samePageContext(settings.offlineContext.context))
            throw new Error("Actualiza el catálogo de esta sucursal y usuario antes de operar offline.");

        const notSynced = pendingSales.filter((sale) => sale.syncStatus !== "Synced");
        if (notSynced.length >= maxPendingSales) {
            throw new Error("Se alcanzó el límite de 500 ventas pendientes.");
        }

        const lastSync = settings.syncedAtUtc;
        if (!lastSync || (Date.now() - new Date(lastSync).getTime()) / 86400000 > maxOfflineDays) {
            throw new Error("Se superó el límite de 7 días sin sincronización.");
        }

        const formData = new FormData(form);
        const productMap = new Map(products.map((product) => [product.id, product]));
        const requestedByProduct = new Map();
        const items = [];
        for (const line of form.querySelectorAll("[data-sale-line]")) {
            const productId = line.querySelector("[data-line-product]")?.value;
            const quantity = Number(line.querySelector("[data-line-quantity]")?.value || 0);
            const discountPercent = Number(line.querySelector("[data-line-discount-percent]")?.value || 0);
            if (!productId || quantity <= 0) {
                continue;
            }

            const product = productMap.get(productId);
            if (!product) {
                throw new Error("Uno de los productos ya no está disponible en el catálogo local.");
            }

            const requested = (requestedByProduct.get(productId) || 0) + quantity;
            if (requested > Number(product.localAvailableStock)) {
                throw new Error(`Stock local insuficiente para ${product.name}. Disponible: ${quantityFormatter.format(Number(product.localAvailableStock))}.`);
            }
            requestedByProduct.set(productId, requested);
            const price = window.SistemiPricing.calculate(product, quantity);
            items.push({
                productId,
                quantity,
                discountPercent,
                productName: product.name,
                unitPrice: price.unitPrice,
                grossAmount: price.grossAmount
            });
        }

        if (items.length === 0) {
            throw new Error("La venta debe incluir al menos un producto.");
        }
        if (items.some(item => item.discountPercent > 0)) {
            throw new Error("Las ventas con descuento requieren conexión para validar la autorización del Gerente.");
        }

        const cashSessionId = value(formData, "CashSessionId");
        if (!cashSessionId) {
            throw new Error("Debe seleccionar una sesión de caja.");
        }
        const savedContext = settings.offlineContext.context;
        const selectedTerminal = value(formData, "TerminalId");
        if (cashSessionId !== savedContext.cashSessionId || selectedTerminal && selectedTerminal !== savedContext.terminalId)
            throw new Error("La caja o terminal no coincide con el catálogo offline. Conecta y actualiza los datos.");

        const paymentMethod = value(formData, "PaymentMethod");
        if (!["Cash", "Transfer", "Pos"].includes(paymentMethod)) {
            throw new Error("La forma de pago seleccionada requiere conexión con el servidor.");
        }
        const total = roundMoney(items.reduce((sum, item) => {
            const gross = item.grossAmount;
            return sum + gross * Math.max(0, 1 - item.discountPercent / 100);
        }, 0));
        const receivedAmount = nullableNumber(formData, "ReceivedAmount");
        if (paymentMethod === "Cash" && (receivedAmount === null || receivedAmount < total)) {
            throw new Error("El monto recibido no puede ser menor al total.");
        }

        const localCreatedAtUtc = new Date().toISOString();
        const clientOperationId = `OFF-${Date.now()}-${Math.random().toString(16).slice(2, 8).toUpperCase()}`;
        const transferDate = value(formData, "TransferDateLocal");
        const sale = {
            context: savedContext,
            contextKey: savedContext.key,
            offlineTicket: settings.offlineContext.ticket,
            clientOperationId,
            localCreatedAtUtc,
            cashSessionId,
            customerId: value(formData, "CustomerId") || null,
            customerNit: value(formData, "CustomerNit") || null,
            customerName: value(formData, "CustomerName") || null,
            customerPhone: value(formData, "CustomerPhone") || null,
            customerVisitOrigin: value(formData, "CustomerVisitOrigin") || null,
            customerAddress: value(formData, "CustomerAddress") || null,
            paymentMethod,
            receivedAmount: paymentMethod === "Cash" ? receivedAmount : null,
            transferSourceBank: paymentMethod === "Transfer" ? value(formData, "TransferSourceBank") : null,
            transferSenderName: paymentMethod === "Transfer" ? value(formData, "TransferSenderName") : null,
            transferBankAccountId: paymentMethod === "Transfer" ? value(formData, "TransferBankAccountId") || null : null,
            transferReference: paymentMethod === "Transfer" ? value(formData, "TransferReference") : null,
            transferAmount: paymentMethod === "Transfer" ? nullableNumber(formData, "TransferAmount") ?? total : null,
            transferDateUtc: paymentMethod === "Transfer" ? toIsoDate(transferDate) : null,
            transferStatus: paymentMethod === "Transfer" ? "Received" : null,
            cardType: paymentMethod === "Pos" ? value(formData, "CardType") : null,
            cardLast4: null,
            cardAuthorizationNumber: paymentMethod === "Pos" ? value(formData, "CardAuthorizationNumber") : null,
            cardVoucherReference: paymentMethod === "Pos" ? value(formData, "CardVoucherReference") : null,
            cardAmount: paymentMethod === "Pos" ? nullableNumber(formData, "CardAmount") ?? total : null,
            cardStatus: paymentMethod === "Pos" ? "Approved" : null,
            posDeviceId: paymentMethod === "Pos" ? value(formData, "PosDeviceId") || null : null,
            items,
            total,
            syncStatus: "Pending",
            finalSaleNumber: null,
            error: null
        };

        await saveSaleAndStockAtomically(sale, productMap, requestedByProduct);
        const receipt = buildReceiptPreview(settings, sale);
        localStorage.setItem("pv-latest-offline-receipt:" + savedContext.key, receipt);
        await registerBackgroundSync();
        await publishState();
        return sale;
    }

    async function syncPendingSales(options) {
        if (syncPromise) {
            return syncPromise;
        }
        if (!navigator.onLine) {
            await publishState();
            return false;
        }

        syncPromise = performSync(Boolean(options?.includeConflicts));
        try {
            return await syncPromise;
        } finally {
            syncPromise = null;
        }
    }

    async function performSync(includeConflicts) {
        const sales = (await getAll("pendingSales")).filter((sale) =>
            samePageContext(sale.context) && (retryableStatuses.has(sale.syncStatus) || (includeConflicts && sale.syncStatus === "Conflict")));
        if (sales.length === 0) {
            await publishState();
            return true;
        }

        for (const sale of sales) {
            sale.syncStatus = "Syncing";
            sale.error = null;
            await put("pendingSales", sale);
        }
        await publishState();

        try {
            const settings = await get("settings", "current");
            const csrf = document.querySelector('[name="__RequestVerificationToken"]')?.value || settings?.antiforgeryToken;
            const response = await fetch("/api/offline-pos/sync", {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": csrf || "" },
                credentials: "same-origin",
                body: JSON.stringify({ sales: sales.map(toSyncRequest) })
            });
            if (!response.ok) {
                throw new Error("El servidor rechazó la sincronización.");
            }

            const payload = await response.json();
            for (const result of payload.results) {
                const sale = await get("pendingSales", result.clientOperationId);
                if (!sale) {
                    continue;
                }
                sale.syncStatus = result.status;
                sale.finalSaleNumber = result.saleNumber;
                sale.error = result.error;
                await put("pendingSales", sale);
            }
            await refreshBootstrap({ silent: true });
            await publishState();
            return true;
        } catch (error) {
            for (const sale of sales) {
                sale.syncStatus = "Error";
                sale.error = error.message || "Falló la sincronización con el servidor.";
                await put("pendingSales", sale);
            }
            await registerBackgroundSync();
            await publishState();
            return false;
        }
    }

    function toSyncRequest(sale) {
        return {
            offlineTicket: sale.offlineTicket,
            expectedTotal: sale.total,
            clientOperationId: sale.clientOperationId,
            localCreatedAtUtc: sale.localCreatedAtUtc,
            cashSessionId: sale.cashSessionId,
            customerId: sale.customerId,
            customerNit: sale.customerNit,
            customerName: sale.customerName,
            customerAddress: sale.customerAddress,
            customerPhone: sale.customerPhone,
            customerVisitOrigin: sale.customerVisitOrigin,
            paymentMethod: sale.paymentMethod,
            receivedAmount: sale.receivedAmount,
            transferSourceBank: sale.transferSourceBank,
            transferSenderName: sale.transferSenderName,
            transferBankAccountId: sale.transferBankAccountId,
            transferReference: sale.transferReference,
            transferAmount: sale.transferAmount,
            transferDateUtc: sale.transferDateUtc,
            transferStatus: sale.transferStatus,
            cardType: sale.cardType,
            cardLast4: sale.cardLast4,
            cardAuthorizationNumber: sale.cardAuthorizationNumber,
            cardVoucherReference: sale.cardVoucherReference,
            posDeviceId: sale.posDeviceId,
            cardAmount: sale.cardAmount,
            cardStatus: sale.cardStatus,
            items: sale.items.map((item) => ({
                productId: item.productId,
                quantity: item.quantity,
                discountPercent: item.discountPercent
            }))
        };
    }

    async function getState() {
        const allSales = await getAll("pendingSales");
        const sales = allSales.filter(sale => samePageContext(sale.context));
        const pending = sales.filter((sale) => sale.syncStatus !== "Synced");
        return {
            online: navigator.onLine,
            legacyPendingCount: allSales.filter(sale => !sale.context && sale.syncStatus !== 'Synced').length,
            pendingCount: pending.length,
            lastSync: localStorage.getItem("pv-last-successful-sync"),
            sales
        };
    }

    function samePageContext(context) {
        const normalize = value => String(value || '').replaceAll('-', '').toLowerCase();
        return context && normalize(context.userId) === normalize(document.body.dataset.currentUserId)
            && normalize(context.branchId) === normalize(document.body.dataset.currentBranchId);
    }

    async function publishState() {
        const state = await getState();
        for (const badge of document.querySelectorAll("[data-offline-pending-badge]")) {
            badge.textContent = state.pendingCount.toString();
            badge.hidden = state.pendingCount === 0;
        }
        window.dispatchEvent(new CustomEvent("sistemi:offline-state", { detail: state }));
        return state;
    }

    async function applyLocalStockToSaleForm() {
        const settings = await get("settings", "current");
        if (!samePageContext(settings?.offlineContext?.context)) return;
        const tiles = document.querySelectorAll("[data-product-id]");
        if (tiles.length === 0) {
            return;
        }
        const products = new Map((await getAll("products")).map((product) => [product.id, product]));
        for (const tile of tiles) {
            const product = products.get(tile.dataset.productId);
            if (!product) {
                continue;
            }
            tile.dataset.productStock = product.localAvailableStock;
            if (!navigator.onLine) {
                const unavailable = Number(product.localAvailableStock) <= 0;
                tile.disabled = unavailable;
                tile.setAttribute("aria-disabled", unavailable ? "true" : "false");
                tile.classList.toggle("is-unavailable", unavailable);
                tile.tabIndex = unavailable ? -1 : 0;
                const stock = tile.querySelector(".product-stock");
                if (stock) {
                    stock.textContent = `Último stock sincronizado: ${quantityFormatter.format(Number(product.localAvailableStock))}`;
                }
            }
        }
    }

    function saveSaleAndStockAtomically(sale, productMap, requestedByProduct) {
        return openDatabase().then((db) => new Promise((resolve, reject) => {
            const transaction = db.transaction(["products", "pendingSales", "settings"], "readwrite");
            const productStore = transaction.objectStore("products");
            const active = transaction.objectStore("settings").get("current");
            active.onsuccess = () => {
                if (active.result?.offlineContext?.context?.key !== sale.contextKey) { transaction.abort(); return; }
            for (const [productId, quantity] of requestedByProduct) {
                const current = productStore.get(productId);
                current.onsuccess = () => {
                    const product = current.result;
                    if (!product || Number(product.localAvailableStock) < quantity) { transaction.abort(); return; }
                    product.localAvailableStock = Number((Number(product.localAvailableStock) - quantity).toFixed(2));
                    productStore.put(product);
                };
            }
            const pending = transaction.objectStore("pendingSales").getAll();
            pending.onsuccess = () => { if (pending.result.filter(item => item.syncStatus !== 'Synced').length >= maxPendingSales) transaction.abort(); };
            transaction.objectStore("pendingSales").put(sale);
            };
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error || new Error("No fue posible guardar la venta localmente."));
        }));
    }

    function replaceBootstrapAtomically(stores) {
        return openDatabase().then(db => new Promise((resolve, reject) => {
            const transaction = db.transaction(Object.keys(stores), "readwrite");
            for (const [name, items] of Object.entries(stores)) {
                const store = transaction.objectStore(name); store.clear();
                for (const item of items) store.put(item);
            }
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error || new Error("No fue posible actualizar el catálogo local."));
        }));
    }

    async function registerBackgroundSync() {
        if (!("serviceWorker" in navigator)) {
            return;
        }
        try {
            const registration = await navigator.serviceWorker.ready;
            await registration.sync?.register?.("sistemi-pos-restaurante-sync");
        } catch {
            // La sincronización por eventos online/visibility sigue activa como respaldo.
        }
    }

    function buildReceiptPreview(settings, sale) {
        const lines = [
            settings.businessName,
            "Pendiente de sincronización",
            sale.clientOperationId,
            new Date(sale.localCreatedAtUtc).toLocaleString()
        ];
        for (const item of sale.items) {
            lines.push(`${item.productName} ${item.quantity} x ${Number(item.unitPrice).toFixed(2)}`);
        }
        lines.push(`Total: ${Number(sale.total).toFixed(2)}`);
        lines.push(`Pago: ${sale.paymentMethod}`);
        if (sale.customerNit) {
            lines.push(`Cliente: ${sale.customerName || ""} · NIT ${sale.customerNit}`);
        }
        if (sale.paymentMethod === "Cash") {
            lines.push(`Cambio: ${Number((sale.receivedAmount || 0) - sale.total).toFixed(2)}`);
        }
        if (sale.paymentMethod === "Transfer") {
            lines.push(`Referencia: ${sale.transferReference || ""}`);
        }
        if (sale.paymentMethod === "Pos") {
            lines.push(`Voucher: ${sale.cardVoucherReference || ""}`);
        }
        if (settings.receiptFooter) {
            lines.push(settings.receiptFooter);
        }
        return lines.join("\n");
    }

    function openDatabase() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, databaseVersion);
            request.onupgradeneeded = () => {
                const db = request.result;
                for (const [name, keyPath] of [
                    ["products", "id"], ["customers", "id"], ["sessions", "id"],
                    ["bankAccounts", "id"], ["settings", "id"], ["pendingSales", "clientOperationId"]
                ]) {
                    if (!db.objectStoreNames.contains(name)) {
                        db.createObjectStore(name, { keyPath });
                    }
                }
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    function getAll(storeName) {
        return executeStore(storeName, "readonly", (store) => promisify(store.getAll()));
    }

    function get(storeName, key) {
        return executeStore(storeName, "readonly", (store) => promisify(store.get(key)));
    }

    function put(storeName, item) {
        return executeStore(storeName, "readwrite", (store) => promisify(store.put(item)));
    }

    function replaceStore(storeName, items) {
        return openDatabase().then((db) => new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, "readwrite");
            const store = transaction.objectStore(storeName);
            store.clear();
            for (const item of items) {
                store.put(item);
            }
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        }));
    }

    async function executeStore(storeName, mode, callback) {
        const db = await openDatabase();
        return new Promise((resolve, reject) => {
            const transaction = db.transaction(storeName, mode);
            const store = transaction.objectStore(storeName);
            Promise.resolve(callback(store)).then(resolve).catch(reject);
            transaction.onerror = () => reject(transaction.error);
        });
    }

    function promisify(request) {
        return new Promise((resolve, reject) => {
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    function value(formData, name) {
        return String(formData.get(name) || "").trim();
    }

    function nullableNumber(formData, name) {
        const raw = value(formData, name);
        return raw === "" ? null : Number(raw);
    }

    function toIsoDate(valueToConvert) {
        const date = valueToConvert ? new Date(valueToConvert) : new Date();
        return Number.isNaN(date.getTime()) ? new Date().toISOString() : date.toISOString();
    }

    function roundMoney(amount) {
        return Math.round((amount + Number.EPSILON) * 100) / 100;
    }
})();
