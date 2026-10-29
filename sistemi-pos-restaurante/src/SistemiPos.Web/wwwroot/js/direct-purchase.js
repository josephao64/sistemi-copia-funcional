(() => {
    const form = document.querySelector("[data-direct-purchase-form]");
    if (!form) return;

    const body = form.querySelector("[data-direct-items]");
    const template = document.querySelector("[data-direct-item-template]");
    const supplierList = document.querySelector("#direct-suppliers");
    const productList = document.querySelector("#direct-products");
    const supplierId = form.querySelector("[data-direct-supplier-id]");
    const supplierSearch = form.querySelector("[data-direct-supplier-search]");
    const supplierNit = form.querySelector("[data-direct-supplier-nit]");
    const terms = form.querySelector("[data-direct-terms]");
    const creditField = form.querySelector("[data-direct-credit-days]");
    const creditDays = form.querySelector("[data-direct-credit-days-input]");
    const dueField = form.querySelector("[data-direct-due-field]");
    const dueDate = form.querySelector("[data-direct-due-date]");
    const documentDate = form.querySelector("[data-direct-document-date]");
    const totalElement = form.querySelector("[data-direct-total]");
    const confirmTotal = form.querySelector("[data-direct-confirm-total]");
    const confirmDialog = form.querySelector("[data-direct-confirm-dialog]");
    const currencySymbol = form.dataset.currencySymbol || "Q";
    const currencyDecimals = Number(form.dataset.currencyDecimals || 2);
    const normalize = value => (value || "").trim().toLocaleLowerCase("es");
    const money = value => `${currencySymbol} ${value.toLocaleString("es-GT", { minimumFractionDigits: currencyDecimals, maximumFractionDigits: currencyDecimals })}`;

    const productOptions = () => [...(productList?.options || [])];
    const supplierOptions = () => [...(supplierList?.options || [])];
    const renumber = () => body.querySelectorAll("[data-direct-item-row]").forEach((row, index) => {
        row.querySelectorAll("[name]").forEach(input => {
            input.name = input.name.replace(/Items\[\d+\]/, `Items[${index}]`);
        });
    });

    const updateTotals = () => {
        let total = 0;
        body.querySelectorAll("[data-direct-item-row]").forEach(row => {
            const packages = Number(row.querySelector("[data-direct-package-quantity]")?.value) || 0;
            const unitsPerPackage = Number(row.querySelector("[data-direct-units-per-package]")?.value) || 0;
            const packageCost = Number(row.querySelector("[data-direct-package-cost]")?.value) || 0;
            const lineTotal = packages * packageCost;
            row.querySelector("[data-direct-unit-quantity]").textContent = (packages * unitsPerPackage).toLocaleString("es-GT");
            row.querySelector("[data-direct-unit-cost]").textContent = money(unitsPerPackage > 0 ? packageCost / unitsPerPackage : 0);
            row.querySelector("[data-direct-line-total]").textContent = money(lineTotal);
            total += lineTotal;
        });
        totalElement.textContent = money(total);
        if (confirmTotal) confirmTotal.textContent = money(total);
    };

    const clearProduct = row => {
        row.querySelector("[data-direct-product-id]").value = "";
        row.querySelector("[data-direct-manages-expiry]").value = "false";
        const expiryField = row.querySelector("[data-direct-expiry-field]");
        const expiry = row.querySelector("[data-direct-expiry]");
        expiryField.hidden = true;
        expiry.required = false;
        expiry.value = "";
        row.querySelector("[data-direct-supplier-product-name]").value = "";
        row.querySelector("[data-direct-supplier-product-code]").value = "";
    };

    const loadSupplierProduct = async row => {
        const productId = row.querySelector("[data-direct-product-id]").value;
        const name = row.querySelector("[data-direct-supplier-product-name]");
        if (!productId || !supplierId.value) return;
        try {
            const response = await fetch(`${form.dataset.supplierProductUrl}?productId=${encodeURIComponent(productId)}&supplierId=${encodeURIComponent(supplierId.value)}`);
            if (!response.ok) return;
            const relation = await response.json();
            if (!relation) return;
            name.value = relation.supplierProductName;
            row.querySelector("[data-direct-supplier-product-code]").value = relation.supplierProductCode || "";
            row.querySelector("[data-direct-package-type]").value = relation.packageType;
            row.querySelector("[data-direct-units-per-package]").value = relation.unitsPerPackage;
            row.querySelector("[data-direct-package-cost]").value = relation.lastPackageCost;
            updateTotals();
        } catch {
            window.showSystemToast?.("error", "No fue posible cargar la información del proveedor para el producto.");
        }
    };

    const selectProduct = (row, option) => {
        const duplicate = [...body.querySelectorAll("[data-direct-item-row]")].find(other =>
            other !== row && other.querySelector("[data-direct-product-id]").value === option.dataset.id);
        if (duplicate) {
            clearProduct(row);
            row.querySelector("[data-direct-product-search]").value = "";
            window.showSystemToast?.("warning", "El producto ya forma parte de la compra. Modifique la línea existente.");
            duplicate.querySelector("[data-direct-package-quantity]")?.focus();
            return;
        }
        const managesExpiry = option.dataset.expiry === "true";
        row.querySelector("[data-direct-product-id]").value = option.dataset.id;
        row.querySelector("[data-direct-product-search]").value = option.value;
        row.querySelector("[data-direct-manages-expiry]").value = managesExpiry.toString();
        const expiryField = row.querySelector("[data-direct-expiry-field]");
        const expiry = row.querySelector("[data-direct-expiry]");
        expiryField.hidden = !managesExpiry;
        expiry.required = managesExpiry;
        if (!managesExpiry) expiry.value = "";
        const supplierProductName = row.querySelector("[data-direct-supplier-product-name]");
        if (!supplierProductName.value) supplierProductName.value = option.value;
        loadSupplierProduct(row);
    };

    const synchronizeProduct = row => {
        const search = row.querySelector("[data-direct-product-search]");
        const option = productOptions().find(value => normalize(value.value) === normalize(search.value));
        if (option) selectProduct(row, option);
        else clearProduct(row);
    };

    const bindRow = row => {
        const search = row.querySelector("[data-direct-product-search]");
        search?.addEventListener("input", () => synchronizeProduct(row));
        search?.addEventListener("change", () => synchronizeProduct(row));
        row.querySelectorAll("[data-direct-package-quantity], [data-direct-units-per-package], [data-direct-package-cost]").forEach(input => input.addEventListener("input", updateTotals));
    };

    const addRow = () => {
        body.insertAdjacentHTML("beforeend", template.innerHTML.replaceAll("__index__", body.children.length));
        const row = body.lastElementChild;
        bindRow(row);
        row.querySelector("[data-direct-product-search]")?.focus();
        return row;
    };

    const updateTerms = () => {
        const isCredit = terms.value === "2";
        creditField.hidden = !isCredit;
        dueField.hidden = !isCredit;
        creditDays.required = isCredit;
        if (!isCredit) {
            creditDays.value = "";
            dueDate.value = "";
            return;
        }
        const days = Number(creditDays.value);
        if (!documentDate.value || !Number.isInteger(days) || days <= 0) {
            dueDate.value = "";
            return;
        }
        const calculated = new Date(`${documentDate.value}T12:00:00`);
        calculated.setDate(calculated.getDate() + days);
        dueDate.value = `${calculated.getFullYear()}-${String(calculated.getMonth() + 1).padStart(2, "0")}-${String(calculated.getDate()).padStart(2, "0")}`;
    };

    const selectSupplier = option => {
        supplierId.value = option.dataset.id;
        supplierSearch.value = option.value;
        supplierNit.value = option.dataset.nit || "";
        const days = Number(option.dataset.creditDays);
        if (Number.isInteger(days) && days > 0) {
            terms.value = "2";
            creditDays.value = days.toString();
        } else {
            terms.value = "1";
            creditDays.value = "";
        }
        updateTerms();
        body.querySelectorAll("[data-direct-item-row]").forEach(loadSupplierProduct);
    };

    const synchronizeSupplier = () => {
        const option = supplierOptions().find(value => normalize(value.value) === normalize(supplierSearch.value));
        if (option) selectSupplier(option);
        else supplierId.value = "";
    };

    supplierSearch?.addEventListener("input", synchronizeSupplier);
    supplierSearch?.addEventListener("change", synchronizeSupplier);
    terms?.addEventListener("change", updateTerms);
    creditDays?.addEventListener("input", updateTerms);
    documentDate?.addEventListener("change", updateTerms);
    form.querySelector("[data-add-direct-item]")?.addEventListener("click", addRow);
    body.addEventListener("click", event => {
        const button = event.target.closest("[data-remove-direct-item]");
        if (!button) return;
        button.closest("tr").remove();
        if (!body.children.length) addRow();
        renumber();
        updateTotals();
    });

    document.querySelector("#direct-supplier-modal")?.addEventListener("quickcreate:created", event => {
        const option = document.createElement("option");
        option.value = event.detail.text;
        option.dataset.id = event.detail.id;
        option.dataset.nit = event.detail.nit || "";
        option.dataset.creditDays = event.detail.defaultCreditDays || "";
        supplierList.appendChild(option);
        selectSupplier(option);
    });

    document.addEventListener("direct-purchase:product-created", event => {
        const option = document.createElement("option");
        option.value = event.detail.systemName;
        option.label = [event.detail.systemName, event.detail.supplierName, event.detail.systemDescription, event.detail.supplierDescription].filter(Boolean).join(" · ");
        option.dataset.id = event.detail.id;
        option.dataset.expiry = Boolean(event.detail.managesExpiry).toString();
        productList.appendChild(option);
        const row = [...body.querySelectorAll("[data-direct-item-row]")].find(value => !value.querySelector("[data-direct-product-id]").value) || addRow();
        selectProduct(row, option);
    });

    form.querySelector("[data-open-direct-confirm]")?.addEventListener("click", () => {
        synchronizeSupplier();
        body.querySelectorAll("[data-direct-item-row]").forEach(synchronizeProduct);
        const rows = [...body.querySelectorAll("[data-direct-item-row]")];
        const messages = [];
        if (!supplierId.value) messages.push("Seleccione un proveedor desde las sugerencias.");
        if (!rows.some(row => row.querySelector("[data-direct-product-id]").value)) messages.push("Agregue al menos un producto.");
        if (rows.some(row => !row.querySelector("[data-direct-product-id]").value)) messages.push("Seleccione un producto válido en cada línea.");
        if (new Set(rows.map(row => row.querySelector("[data-direct-product-id]").value).filter(Boolean)).size !== rows.filter(row => row.querySelector("[data-direct-product-id]").value).length) messages.push("Un producto no puede repetirse en la compra.");
        if (!form.reportValidity() || messages.length) {
            window.showSystemToast?.("warning", messages.length ? messages : "Revise los campos requeridos de la compra.");
            return;
        }
        updateTotals();
        confirmDialog.showModal();
    });
    form.querySelector("[data-close-direct-confirm]")?.addEventListener("click", () => confirmDialog.close());

    body.querySelectorAll("[data-direct-item-row]").forEach(bindRow);
    updateTerms();
    updateTotals();
})();
