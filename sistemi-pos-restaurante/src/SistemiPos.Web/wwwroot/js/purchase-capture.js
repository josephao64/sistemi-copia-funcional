(() => {
    const form = document.querySelector("[data-purchase-capture-form]");
    if (!form) return;

    const body = form.querySelector("[data-purchase-items]");
    const template = document.querySelector("[data-purchase-item-template]");
    const products = document.querySelector("#purchase-products");
    const suppliers = document.querySelector("#purchase-suppliers");
    const supplierId = form.querySelector("[data-purchase-supplier-id]");
    const supplierSearch = form.querySelector("[data-purchase-supplier-search]");
    const supplierNit = form.querySelector("[data-purchase-supplier-nit]");
    const terms = form.querySelector("[data-purchase-terms]");
    const creditField = form.querySelector("[data-purchase-credit-days]");
    const creditDays = form.querySelector("[data-purchase-credit-days-input]");
    const dueField = form.querySelector("[data-purchase-due-field]");
    const dueDate = form.querySelector("[data-purchase-due-date]");
    const documentDate = form.querySelector("[data-purchase-document-date]");
    const globalType = form.querySelector("[data-purchase-global-discount-type]");
    const globalValue = form.querySelector("[data-purchase-global-discount-value]");
    const confirmDialog = form.querySelector("[data-purchase-confirm-dialog]");
    const calculatorDialog = form.querySelector("[data-purchase-calculator-dialog]");
    const multipriceDialog = form.querySelector("[data-purchase-multiprice-dialog]");
    const multipriceTemplate = form.querySelector("[data-multiprice-rule-template]");
    const symbol = form.dataset.currencySymbol || "Q";
    const decimals = Number(form.dataset.currencyDecimals || 2);
    const taxPercent = Number(form.dataset.taxPercent || 0);
    let activePricingRow = null;
    let activeProductCreationRow = null;
    let suggestedSalePrice = 0;
    let explicitSaveRequested = false;
    const normalize = value => (value || "").trim().toLocaleLowerCase("es");
    const money = value => `${symbol} ${value.toLocaleString("es-GT", { minimumFractionDigits: decimals, maximumFractionDigits: decimals })}`;
    const number = input => Number(input?.value) || 0;
    const round = (value, digits = 2) => {
        const factor = 10 ** digits;
        return Math.round((value + Number.EPSILON) * factor) / factor;
    };

    const lineValues = row => {
        const packages = number(row.querySelector("[data-purchase-package-quantity]"));
        const units = Number(row.querySelector("[data-purchase-presentation]")?.selectedOptions[0]?.dataset.factor) || number(row.querySelector("[data-purchase-units-per-package]"));
        const price = number(row.querySelector("[data-purchase-package-cost]"));
        const type = row.querySelector("[data-purchase-discount-type]")?.value || "0";
        const value = number(row.querySelector("[data-purchase-discount-value]"));
        const gross = packages * price;
        const discount = type === "1" ? gross * Math.min(value, 100) / 100 : type === "2" ? Math.min(value, gross) : 0;
        const unitQuantity = packages * units;
        return { packages, units, unitQuantity, gross, discount, net: gross - discount };
    };

    const calculatedLines = () => {
        const entries = [...body.querySelectorAll("[data-purchase-row]")].map(row => ({ row, ...lineValues(row) }));
        const netSubtotal = round(entries.reduce((sum, item) => sum + item.net, 0));
        const value = number(globalValue);
        const globalDiscount = globalType.value === "1"
            ? round(netSubtotal * Math.min(value, 100) / 100)
            : globalType.value === "2" ? Math.min(round(value), netSubtotal) : 0;
        let remainingDiscount = globalDiscount;
        let remainingNet = netSubtotal;
        entries.forEach((entry, index) => {
            const allocation = index === entries.length - 1
                ? remainingDiscount
                : remainingNet === 0 ? 0 : round(remainingDiscount * entry.net / remainingNet);
            entry.globalDiscount = Math.min(allocation, entry.net);
            entry.final = entry.net - entry.globalDiscount;
            entry.realUnitCost = entry.unitQuantity > 0 ? round(entry.final / entry.unitQuantity, 6) : 0;
            remainingDiscount = round(remainingDiscount - entry.globalDiscount);
            remainingNet = round(remainingNet - entry.net);
        });
        return { entries, netSubtotal, globalDiscount };
    };

    const updateTotals = () => {
        let gross = 0, lineDiscounts = 0;
        const calculation = calculatedLines();
        calculation.entries.forEach(values => {
            const row = values.row;
            row.querySelector("[data-purchase-unit-quantity]").textContent = (values.packages * values.units).toLocaleString("es-GT");
            row.querySelector("[data-purchase-line-total]").textContent = money(values.net);
            gross += values.gross;
            lineDiscounts += values.discount;
        });
        const net = gross - lineDiscounts;
        const finalTotal = net - calculation.globalDiscount;
        form.querySelector("[data-purchase-gross-total]").textContent = money(gross);
        form.querySelector("[data-purchase-line-discounts]").textContent = money(lineDiscounts);
        form.querySelector("[data-purchase-net-subtotal]").textContent = money(net);
        form.querySelector("[data-purchase-final-total]").textContent = money(finalTotal);
        form.querySelector("[data-purchase-confirm-total]").textContent = money(finalTotal);
        if (calculatorDialog?.open && activePricingRow) updateCalculator();
    };

    const syncPresentation = row => {
        const select = row.querySelector("[data-purchase-presentation]");
        const productId = row.querySelector('[data-purchase-product-id]')?.value;
        [...select.options].forEach(option => {
            const unavailable = !!option.dataset.productId && option.dataset.productId !== productId;
            option.disabled = unavailable; option.hidden = unavailable;
        });
        if (select.selectedOptions[0]?.disabled) select.value = '';
        const units = select?.selectedOptions[0]?.dataset.units || "1";
        row.querySelector("[data-purchase-units-per-package]").value = units;
        updateTotals();
    };

    const updateDiscount = row => {
        const type = row.querySelector("[data-purchase-discount-type]");
        const value = row.querySelector("[data-purchase-discount-value]");
        value.hidden = type.value === "0";
        value.required = type.value !== "0";
        if (type.value === "0") value.value = "";
        updateTotals();
    };

    const parseMultipriceRules = row => {
        try {
            const parsed = JSON.parse(row.querySelector("[data-purchase-multiprice-rules]").value || "[]");
            return Array.isArray(parsed) ? parsed.map(rule => ({
                quantity: Number(rule.quantity ?? rule.Quantity) || 0,
                price: Number(rule.price ?? rule.Price) || 0
            })) : [];
        } catch { return []; }
    };

    const updateMultipriceBadge = row => {
        const enabled = row.querySelector("[data-purchase-multiprice-enabled]").value === "true";
        const count = enabled ? parseMultipriceRules(row).length : 0;
        row.querySelector("[data-purchase-multiprice-count]").textContent = count ? `(${count})` : "";
    };

    const updateLastPurchaseCost = (row, cost, hasHistory) => {
        const label = row.querySelector("[data-purchase-last-cost]");
        if (!label) return;
        label.textContent = hasHistory ? `Último costo: ${money(Number(cost) || 0)}` : "Sin historial";
    };

    const hasDuplicateSupplierInvoice = async () => {
        const invoice = form.querySelector('[name="SupplierDocumentNumber"]')?.value?.trim();
        const currentSupplierId = supplierId?.value || form.dataset.supplierId;
        if (!invoice || !currentSupplierId || !form.dataset.supplierInvoiceUrl) return false;
        try {
            const response = await fetch(`${form.dataset.supplierInvoiceUrl}?supplierId=${encodeURIComponent(currentSupplierId)}&invoiceNumber=${encodeURIComponent(invoice)}`);
            if (!response.ok) return false;
            const result = await response.json();
            if (!result.exists) return false;
            window.showSystemToast?.("warning", `La factura ${invoice} ya fue registrada para este proveedor.`);
            return true;
        } catch {
            return false;
        }
    };

    const productName = row => row.querySelector("[data-purchase-product-search]")?.value
        || row.querySelector(".purchase-product-cell strong")?.textContent?.trim()
        || "Producto";

    const updateCalculator = () => {
        if (!activePricingRow) return;
        const line = calculatedLines().entries.find(item => item.row === activePricingRow);
        if (!line) return;
        const unitQuantity = line.unitQuantity;
        const grossUnit = unitQuantity > 0 ? line.gross / unitQuantity : 0;
        const lineDiscountUnit = unitQuantity > 0 ? line.discount / unitQuantity : 0;
        const globalDiscountUnit = unitQuantity > 0 ? line.globalDiscount / unitQuantity : 0;
        const method = calculatorDialog.querySelector("[data-calculator-method]").value;
        const percentage = number(calculatorDialog.querySelector("[data-calculator-percent]"));
        const validPercentage = percentage >= 0 && (method !== "margin" || percentage < 100);
        const calculatedPrice = validPercentage
            ? method === "margin" ? line.realUnitCost / (1 - percentage / 100) : line.realUnitCost * (1 + percentage / 100)
            : 0;
        const appliesTax = activePricingRow.querySelector("[data-purchase-applies-tax]").value === "true";
        const appliedTaxPercent = appliesTax ? taxPercent : 0;
        suggestedSalePrice = validPercentage ? round(calculatedPrice, decimals) : 0;
        const beforeTax = appliedTaxPercent > 0 ? suggestedSalePrice / (1 + appliedTaxPercent / 100) : suggestedSalePrice;
        const taxAmount = suggestedSalePrice - beforeTax;

        calculatorDialog.querySelector("[data-calculator-gross-unit]").textContent = money(grossUnit);
        calculatorDialog.querySelector("[data-calculator-line-discount]").textContent = `- ${money(lineDiscountUnit)}`;
        calculatorDialog.querySelector("[data-calculator-global-discount]").textContent = `- ${money(globalDiscountUnit)}`;
        calculatorDialog.querySelector("[data-calculator-real-cost]").textContent = `${symbol} ${line.realUnitCost.toLocaleString("es-GT", { minimumFractionDigits: 6, maximumFractionDigits: 6 })}`;
        calculatorDialog.querySelector("[data-calculator-before-tax]").textContent = money(beforeTax);
        calculatorDialog.querySelector("[data-calculator-tax-label]").textContent = appliesTax
            ? `Impuesto/IVA incluido (${taxPercent.toLocaleString("es-GT")}%)`
            : "Impuesto/IVA considerado (producto exento)";
        calculatorDialog.querySelector("[data-calculator-tax]").textContent = money(taxAmount);
        calculatorDialog.querySelector("[data-calculator-suggested]").textContent = validPercentage ? money(suggestedSalePrice) : "Porcentaje no válido";
        calculatorDialog.querySelector("[data-calculator-formula]").textContent = method === "margin"
            ? validPercentage
                ? `Costo real ÷ (1 - ${percentage}%). El precio sugerido es final y ya incluye el impuesto configurado cuando aplica.`
                : "El margen debe ser menor que 100%."
            : `Costo real + ${percentage}%. El precio sugerido es final y ya incluye el impuesto configurado cuando aplica.`;
        calculatorDialog.querySelector("[data-use-suggested-price]").disabled = !validPercentage || unitQuantity <= 0;
    };

    const openCalculator = row => {
        if (!row.querySelector("[data-purchase-product-id]").value) {
            window.showSystemToast?.("warning", "Seleccione un producto antes de usar la calculadora.");
            return;
        }
        const values = lineValues(row);
        if (values.unitQuantity <= 0) {
            window.showSystemToast?.("warning", "Indique la presentación y la cantidad para calcular el costo real unitario.");
            return;
        }
        activePricingRow = row;
        calculatorDialog.querySelector("[data-pricing-product-name]").textContent = productName(row);
        updateCalculator();
        calculatorDialog.showModal();
    };

    const appendMultipriceRule = rule => {
        const row = multipriceTemplate.content.firstElementChild.cloneNode(true);
        row.querySelector("[data-multiprice-quantity]").value = rule?.quantity || "";
        row.querySelector("[data-multiprice-price]").value = rule?.price ?? "";
        multipriceDialog.querySelector("[data-multiprice-dialog-rows]").appendChild(row);
    };

    const syncMultipriceDialog = () => {
        const enabled = multipriceDialog.querySelector("[data-multiprice-dialog-enabled]").checked;
        multipriceDialog.querySelector("[data-multiprice-dialog-settings]").hidden = !enabled;
    };

    const openMultiprice = row => {
        if (!row.querySelector("[data-purchase-product-id]").value) {
            window.showSystemToast?.("warning", "Seleccione un producto antes de configurar multiprecios.");
            return;
        }
        activePricingRow = row;
        multipriceDialog.querySelector("[data-multiprice-product-name]").textContent = productName(row);
        multipriceDialog.querySelector("[data-multiprice-dialog-enabled]").checked = row.querySelector("[data-purchase-multiprice-enabled]").value === "true";
        const rows = multipriceDialog.querySelector("[data-multiprice-dialog-rows]");
        rows.replaceChildren();
        parseMultipriceRules(row).forEach(appendMultipriceRule);
        syncMultipriceDialog();
        multipriceDialog.showModal();
    };

    const clearProduct = row => {
        row.querySelector("[data-purchase-product-id]").value = "";
        row.querySelector("[data-purchase-manages-expiry]").value = "false";
        row.querySelector("[data-purchase-manages-lots]").value = "false";
        row.querySelector("[data-purchase-lot-field]").hidden = true;
        const lotNumber = row.querySelector("[data-purchase-lot-number]");
        lotNumber.value = ""; lotNumber.disabled = true;
        row.querySelector("[data-purchase-supplier-product-name]").value = "";
        row.querySelector("[data-purchase-supplier-product-code]").value = "";
        const field = row.querySelector("[data-purchase-expiry-field]");
        const expiry = row.querySelector("[data-purchase-expiry]");
        field.hidden = true; expiry.required = false; expiry.value = "";
        row.querySelector("[data-purchase-sale-price]").value = "";
        row.querySelector("[data-purchase-applies-tax]").value = "true";
        row.querySelector("[data-purchase-pricing-initialized]").value = "false";
        row.querySelector("[data-purchase-multiprice-enabled]").value = "false";
        row.querySelector("[data-purchase-multiprice-rules]").value = "[]";
        updateLastPurchaseCost(row, null, false);
        updateMultipriceBadge(row);
    };

    const selectRelationPresentation = (row, relation) => {
        const select = row.querySelector("[data-purchase-presentation]");
        const match = [...select.options].find(option =>
            option.dataset.units === String(relation.unitsPerPackage)
            && normalize(option.textContent.replace(/\s*\(\d+\)\s*$/, "")) === normalize(relation.packageName));
        const fallback = [...select.options].find(option => option.dataset.units === String(relation.unitsPerPackage));
        if (match || fallback) select.value = (match || fallback).value;
        syncPresentation(row);
    };

    const loadSupplierProduct = async row => {
        const productId = row.querySelector("[data-purchase-product-id]").value;
        const currentSupplierId = supplierId?.value || form.dataset.supplierId;
        updateLastPurchaseCost(row, null, false);
        if (!productId || !currentSupplierId) return;
        try {
            const response = await fetch(`${form.dataset.supplierProductUrl}?productId=${encodeURIComponent(productId)}&supplierId=${encodeURIComponent(currentSupplierId)}`);
            if (!response.ok) return;
            const relation = await response.json();
            if (!relation) return;
            updateLastPurchaseCost(row, relation.lastPackageCost, relation.hasPurchaseHistory);
            row.querySelector("[data-purchase-supplier-product-name]").value = relation.supplierProductName;
            row.querySelector("[data-purchase-supplier-product-code]").value = relation.supplierProductCode || "";
            row.querySelector("[data-purchase-package-cost]").value = relation.lastPackageCost || "";
            selectRelationPresentation(row, relation);
        } catch {
            window.showSystemToast?.("error", "No fue posible cargar la información de compra del producto.");
        }
    };

    const selectProduct = (row, option) => {
        const duplicate = [...body.querySelectorAll("[data-purchase-row]")].find(other =>
            other !== row && other.querySelector("[data-purchase-product-id]").value === option.dataset.id);
        if (duplicate) {
            clearProduct(row);
            row.querySelector("[data-purchase-product-search]").value = "";
            window.showSystemToast?.("warning", "El producto ya forma parte de la compra. Modifique la línea existente.");
            return;
        }
        row.querySelector("[data-purchase-product-id]").value = option.dataset.id;
        syncPresentation(row);
        row.querySelector("[data-purchase-product-search]").value = option.value;
        row.querySelector("[data-purchase-supplier-product-name]").value = option.value;
        row.querySelector("[data-purchase-sale-price]").value = option.dataset.salePrice || "0";
        row.querySelector("[data-purchase-applies-tax]").value = option.dataset.appliesTax || "true";
        row.querySelector("[data-purchase-pricing-initialized]").value = "true";
        row.querySelector("[data-purchase-multiprice-enabled]").value = option.dataset.multipriceEnabled || "false";
        row.querySelector("[data-purchase-multiprice-rules]").value = option.dataset.priceRules || "[]";
        updateMultipriceBadge(row);
        const managesExpiry = option.dataset.expiry === "true";
        const managesLots = option.dataset.lots === "true";
        row.querySelector("[data-purchase-manages-lots]").value = managesLots.toString();
        row.querySelector("[data-purchase-lot-field]").hidden = !managesLots;
        const lotNumber = row.querySelector("[data-purchase-lot-number]");
        lotNumber.disabled = !managesLots;
        lotNumber.value = "";
        row.querySelector("[data-purchase-manages-expiry]").value = managesExpiry.toString();
        const field = row.querySelector("[data-purchase-expiry-field]");
        const expiry = row.querySelector("[data-purchase-expiry]");
        field.hidden = !managesExpiry; expiry.required = managesExpiry;
        if (!managesExpiry) expiry.value = "";
        loadSupplierProduct(row);
    };

    const syncProduct = row => {
        const search = row.querySelector("[data-purchase-product-search]");
        if (!search) return;
        const option = [...(products?.options || [])].find(item => normalize(item.value) === normalize(search.value));
        if (option) selectProduct(row, option); else clearProduct(row);
    };

    const bindRow = row => {
        row.querySelector("[data-purchase-product-search]")?.addEventListener("input", () => syncProduct(row));
        row.querySelector("[data-purchase-product-search]")?.addEventListener("change", () => syncProduct(row));
        row.querySelector("[data-purchase-presentation]")?.addEventListener("change", () => syncPresentation(row));
        row.querySelector("[data-purchase-discount-type]")?.addEventListener("change", () => updateDiscount(row));
        row.querySelectorAll("[data-purchase-package-quantity], [data-purchase-package-cost], [data-purchase-discount-value]").forEach(input => input.addEventListener("input", updateTotals));
        syncPresentation(row); updateDiscount(row); updateMultipriceBadge(row);
    };

    const renumber = () => body.querySelectorAll("[data-purchase-row]").forEach((row, index) =>
        row.querySelectorAll("[name]").forEach(input => input.name = input.name.replace(/Items\[\d+\]/, `Items[${index}]`)));

    const addRow = () => {
        body.insertAdjacentHTML("beforeend", template.innerHTML.replaceAll("__index__", body.children.length));
        const row = body.lastElementChild;
        const unit = [...row.querySelector("[data-purchase-presentation]").options].find(option => option.dataset.units === "1");
        if (unit) row.querySelector("[data-purchase-presentation]").value = unit.value;
        bindRow(row); row.querySelector("[data-purchase-product-search]")?.focus();
        return row;
    };

    const updateTerms = () => {
        const isCredit = terms.value === "2";
        creditField.hidden = !isCredit; dueField.hidden = !isCredit;
        creditDays.required = isCredit; creditDays.disabled = !isCredit; dueDate.disabled = !isCredit;
        if (!isCredit) { creditDays.value = ""; dueDate.value = ""; return; }
        const days = number(creditDays);
        if (!documentDate.value || !Number.isInteger(days) || days <= 0) { dueDate.value = ""; return; }
        const value = new Date(`${documentDate.value}T12:00:00`); value.setDate(value.getDate() + days);
        dueDate.value = `${value.getFullYear()}-${String(value.getMonth() + 1).padStart(2, "0")}-${String(value.getDate()).padStart(2, "0")}`;
    };

    const applySupplier = option => {
        supplierId.value = option?.dataset.id || "";
        if (!option) return;
        supplierNit.value = option.dataset.nit || "";
        terms.value = option.dataset.defaultTerms === "2" ? "2" : "1";
        creditDays.value = terms.value === "2" ? (option.dataset.creditDays || "") : "";
        updateTerms(); body.querySelectorAll("[data-purchase-row]").forEach(loadSupplierProduct);
    };

    const syncSupplier = () => {
        if (!supplierSearch) return;
        const option = [...(suppliers?.options || [])].find(item => normalize(item.value) === normalize(supplierSearch.value));
        applySupplier(option);
    };

    form.querySelector("[data-add-purchase-item]")?.addEventListener("click", addRow);
    body.addEventListener("click", event => {
        const createProduct = event.target.closest("[data-purchase-product-create]");
        if (createProduct) {
            activeProductCreationRow = createProduct.closest("[data-purchase-row]");
            return;
        }
        const calculator = event.target.closest("[data-open-purchase-calculator]");
        if (calculator) { openCalculator(calculator.closest("tr")); return; }
        const multiprice = event.target.closest("[data-open-purchase-multiprice]");
        if (multiprice) { openMultiprice(multiprice.closest("tr")); return; }
        const button = event.target.closest("[data-remove-purchase-item]");
        if (!button) return;
        button.closest("tr").remove();
        if (!body.children.length) addRow();
        renumber(); updateTotals();
    });
    supplierSearch?.addEventListener("input", syncSupplier);
    supplierSearch?.addEventListener("change", syncSupplier);
    terms.addEventListener("change", updateTerms);
    creditDays.addEventListener("input", updateTerms);
    documentDate.addEventListener("change", updateTerms);
    globalType.addEventListener("change", () => {
        globalValue.hidden = globalType.value === "0";
        globalValue.required = globalType.value !== "0";
        if (globalType.value === "0") globalValue.value = "";
        updateTotals();
    });
    globalValue.addEventListener("input", updateTotals);

    document.addEventListener("purchase-capture:product-created", event => {
        const option = document.createElement("option");
        option.value = event.detail.systemName; option.label = event.detail.systemName;
        option.dataset.id = event.detail.id; option.dataset.expiry = Boolean(event.detail.managesExpiry).toString();
        option.dataset.lots = Boolean(event.detail.managesLots).toString();
        option.dataset.salePrice = String(event.detail.salePrice || 0);
        option.dataset.appliesTax = String(event.detail.appliesTax ?? true);
        option.dataset.multipriceEnabled = String(event.detail.multiPriceEnabled ?? false);
        option.dataset.priceRules = JSON.stringify(event.detail.priceRules || []);
        products.appendChild(option);
        const row = activeProductCreationRow?.isConnected
            ? activeProductCreationRow
            : [...body.querySelectorAll("[data-purchase-row]")].find(value => !value.querySelector("[data-purchase-product-id]").value) || addRow();
        activeProductCreationRow = null;
        selectProduct(row, option);
    });
    document.addEventListener("purchase-capture:supplier-created", event => {
        if (!suppliers || !supplierSearch) return;
        const option = document.createElement("option");
        option.value = event.detail.text; option.dataset.id = event.detail.id;
        option.dataset.nit = event.detail.nit || ""; option.dataset.creditDays = event.detail.defaultCreditDays || "";
        option.dataset.defaultTerms = event.detail.defaultTerms || "1";
        suppliers.appendChild(option); supplierSearch.value = option.value; applySupplier(option);
    });

    calculatorDialog.querySelectorAll("[data-close-purchase-calculator]").forEach(button => button.addEventListener("click", () => {
        calculatorDialog.close(); activePricingRow = null;
    }));
    calculatorDialog.querySelector("[data-calculator-method]").addEventListener("change", updateCalculator);
    calculatorDialog.querySelector("[data-calculator-percent]").addEventListener("input", updateCalculator);
    calculatorDialog.querySelector("[data-use-suggested-price]").addEventListener("click", () => {
        if (!activePricingRow) return;
        activePricingRow.querySelector("[data-purchase-sale-price]").value = suggestedSalePrice.toFixed(decimals);
        calculatorDialog.close(); activePricingRow = null;
        window.showSystemToast?.("success", "El precio sugerido se colocó como precio de venta.");
    });

    multipriceDialog.querySelector("[data-multiprice-dialog-enabled]").addEventListener("change", () => {
        syncMultipriceDialog();
        if (multipriceDialog.querySelector("[data-multiprice-dialog-enabled]").checked
            && !multipriceDialog.querySelector("[data-multiprice-rule-row]")) appendMultipriceRule();
    });
    multipriceDialog.querySelector("[data-add-multiprice-rule]").addEventListener("click", () => appendMultipriceRule());
    multipriceDialog.querySelector("[data-multiprice-dialog-rows]").addEventListener("click", event => {
        event.target.closest("[data-remove-multiprice-rule]")?.closest("tr")?.remove();
    });
    multipriceDialog.querySelectorAll("[data-close-purchase-multiprice]").forEach(button => button.addEventListener("click", () => {
        multipriceDialog.close(); activePricingRow = null;
    }));
    multipriceDialog.querySelector("[data-save-purchase-multiprice]").addEventListener("click", () => {
        if (!activePricingRow) return;
        const enabled = multipriceDialog.querySelector("[data-multiprice-dialog-enabled]").checked;
        const rules = [...multipriceDialog.querySelectorAll("[data-multiprice-rule-row]")].map(row => ({
            quantity: number(row.querySelector("[data-multiprice-quantity]")),
            price: number(row.querySelector("[data-multiprice-price]"))
        }));
        if (enabled && !rules.length) {
            window.showSystemToast?.("warning", "Agregue al menos un multiprecio o desactive la opción."); return;
        }
        if (enabled && rules.some(rule => rule.quantity <= 0 || rule.price < 0)) {
            window.showSystemToast?.("warning", "Complete correctamente la cantidad y el precio de cada multiprecio."); return;
        }
        if (enabled && new Set(rules.map(rule => round(rule.quantity, 2))).size !== rules.length) {
            window.showSystemToast?.("warning", "No se permiten cantidades repetidas en los multiprecios."); return;
        }
        activePricingRow.querySelector("[data-purchase-multiprice-enabled]").value = enabled.toString();
        activePricingRow.querySelector("[data-purchase-multiprice-rules]").value = JSON.stringify(enabled ? rules : []);
        updateMultipriceBadge(activePricingRow);
        multipriceDialog.close(); activePricingRow = null;
        window.showSystemToast?.("success", enabled ? "Los multiprecios quedaron configurados para esta compra." : "Los multiprecios quedaron desactivados para este producto.");
    });

    form.querySelector("[data-open-purchase-confirm]")?.addEventListener("click", async () => {
        syncSupplier(); body.querySelectorAll("[data-purchase-row]").forEach(syncProduct);
        const rows = [...body.querySelectorAll("[data-purchase-row]")];
        const ids = rows.map(row => row.querySelector("[data-purchase-product-id]").value).filter(Boolean);
        const messages = [];
        if (supplierId && !supplierId.value) messages.push("Seleccione un proveedor desde las sugerencias.");
        if (!ids.length) messages.push("Agregue al menos un producto.");
        if (ids.length !== rows.length) messages.push("Seleccione un producto válido en cada línea.");
        if (new Set(ids).size !== ids.length) messages.push("Un producto no puede repetirse en la compra.");
        if (rows.some(row => number(row.querySelector("[data-purchase-sale-price]")) < 0)) messages.push("El precio de venta no puede ser negativo.");
        if (rows.some(row => row.querySelector("[data-purchase-discount-type]").value === "1" && number(row.querySelector("[data-purchase-discount-value]")) > 100)) messages.push("El descuento porcentual por línea no puede superar 100%.");
        if (globalType.value === "1" && number(globalValue) > 100) messages.push("El descuento global no puede superar 100%.");
        if (!form.reportValidity() || messages.length) { window.showSystemToast?.("warning", messages[0] || "Revise los datos requeridos de la compra."); return; }
        if (await hasDuplicateSupplierInvoice()) return;
        form.querySelector("[data-purchase-confirm-document]")?.replaceChildren(document.createTextNode(form.querySelector('[name="SupplierDocumentNumber"]').value));
        form.querySelector("[data-purchase-confirm-terms]")?.replaceChildren(document.createTextNode(terms.value === "2" ? `Crédito · ${creditDays.value} días` : "Contado"));
        updateTotals(); confirmDialog.showModal();
    });
    form.querySelector("[data-close-purchase-confirm]")?.addEventListener("click", () => confirmDialog.close());
    form.querySelector("[data-save-purchase]")?.addEventListener("click", () => {
        explicitSaveRequested = true;
        form.requestSubmit();
    });
    form.addEventListener("submit", event => {
        if (explicitSaveRequested) {
            explicitSaveRequested = false;
            return;
        }

        event.preventDefault();
    });

    body.querySelectorAll("[data-purchase-row]").forEach(bindRow);
    globalValue.hidden = globalType.value === "0";
    updateTerms(); updateTotals();
})();
