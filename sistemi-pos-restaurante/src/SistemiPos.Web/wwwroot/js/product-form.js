window.initializeProductForm = (root = document) => {
    const editor = root.querySelector('[data-product-editor]');
    if (!editor) return;
    if (editor.dataset.productInitialized) return;
    editor.dataset.productInitialized = 'true';
    const form = editor.querySelector('#product-form');
    const previewUrls = new WeakMap();
    editor.addEventListener('click', event => {
        const trigger = event.target.closest('[data-product-image-trigger]');
        if (!trigger) return;
        trigger.closest('[data-product-image-control]')?.querySelector('[data-product-image-input]')?.click();
    });
    editor.addEventListener('change', event => {
        const imageInput = event.target.closest('[data-product-image-input]');
        if (!imageInput) return;
        const file = imageInput.files?.[0];
        if (!file) return;
        const control = imageInput.closest('[data-product-image-control]');
        const imagePreview = control?.querySelector('[data-product-image-preview]');
        if (!control || !imagePreview) return;
        const previousUrl = previewUrls.get(control);
        if (previousUrl) URL.revokeObjectURL(previousUrl);
        const previewUrl = URL.createObjectURL(file);
        previewUrls.set(control, previewUrl);
        imagePreview.src = previewUrl;
        imagePreview.hidden = false;
        const placeholder = control.querySelector('[data-product-image-placeholder]');
        if (placeholder) placeholder.hidden = true;
        const imageName = control.querySelector('[data-product-image-name]');
        if (imageName) imageName.textContent = file.name;
    });
    const expiryToggle = editor.querySelector('[data-manages-expiry]');
    const lotsToggle = editor.querySelector('[data-manages-lots]');
    expiryToggle?.addEventListener('change', () => { if (expiryToggle.checked && lotsToggle) lotsToggle.checked = true; });
    lotsToggle?.addEventListener('change', () => { if (expiryToggle?.checked) lotsToggle.checked = true; });
    const expiryField = editor.querySelector('[data-expiry-alert-field]');
    const updateExpiry = () => { if (!expiryToggle || !expiryField) return; expiryField.hidden = !expiryToggle.checked; const input = expiryField.querySelector('input'); if (input) { input.required = expiryToggle.checked; input.disabled = !expiryToggle.checked; if (!expiryToggle.checked) input.value = ''; } };
    expiryToggle?.addEventListener('change', updateExpiry);
    updateExpiry();
    const categoryId = editor.querySelector('[data-category-id]');
    const categorySearch = editor.querySelector('[data-category-search]');
    const syncCategory = () => {
        const match = Array.from(editor.querySelectorAll('#product-categories option')).find(x =>
            x.value.localeCompare(categorySearch.value.trim(), 'es', { sensitivity: 'base' }) === 0);
        categoryId.value = match?.dataset.id || '';
    };
    categorySearch?.addEventListener('input', syncCategory);
    document.querySelector('#product-category-modal')?.addEventListener('quickcreate:created', event => {
        const option = document.createElement('option'); option.value = event.detail.text; option.dataset.id = event.detail.id;
        editor.querySelector('#product-categories').appendChild(option); categorySearch.value = event.detail.text; categoryId.value = event.detail.id;
    });
    editor.querySelector('[data-generate-code]')?.addEventListener('click', async event => {
        syncCategory();
        if (!categoryId.value) { window.showSystemToast?.('warning', 'Seleccione una categoría antes de generar el código del producto.'); return; }
        event.currentTarget.disabled = true;
        try {
            const response = await fetch(`${editor.dataset.generateUrl}?categoryId=${encodeURIComponent(categoryId.value)}`);
            const result = await response.json();
            if (!response.ok) throw new Error(result.message);
            editor.querySelector('#InternalCode').value = result.code;
            refreshCombinations();
        } catch (error) { window.showSystemToast?.('error', error.message || 'No fue posible generar el código.'); }
        finally { event.currentTarget.disabled = false; }
    });

    const variantEditor = editor.querySelector('[data-variant-editor]');
    const groups = variantEditor.querySelector('[data-variant-groups]');
    const rows = variantEditor.querySelector('[data-combination-rows]');
    const template = variantEditor.querySelector('[data-combination-template]');
    const baseCode = editor.querySelector('#InternalCode');
    const modes = Array.from(variantEditor.querySelectorAll('[data-variant-mode]'));
    const attributeCatalog = JSON.parse(variantEditor.querySelector('[data-attribute-catalog-json]')?.textContent || '[]');
    const globalValueCatalog = [];
    attributeCatalog.flatMap(item => item.Options || []).forEach(option => {
        if (!globalValueCatalog.some(existing => existing.Value.localeCompare(option.Value, 'es', { sensitivity: 'base' }) === 0))
            globalValueCatalog.push(option);
    });
    globalValueCatalog.sort((left, right) => left.Value.localeCompare(right.Value, 'es', { sensitivity: 'base' }));
    const attributeTypes = variantEditor.querySelector('#variant-types');
    const attributeDialog = root.querySelector('#product-attribute-modal') || document.querySelector('#product-attribute-modal');
    const attributeDialogForm = attributeDialog?.querySelector('[data-attribute-create-form]');
    const attributeName = attributeDialog?.querySelector('[data-attribute-name]');
    const valueDialog = root.querySelector('#product-value-modal') || document.querySelector('#product-value-modal');
    const valueDialogForm = valueDialog?.querySelector('[data-value-create-form]');
    const valueAttributeName = valueDialog?.querySelector('[data-value-attribute-name]');
    const valueName = valueDialog?.querySelector('[data-value-name]');
    const compactVariants = variantEditor.dataset.compactVariants === 'true';
    const compactCapture = variantEditor.querySelector('[data-variant-capture]');
    const configuredPanel = variantEditor.querySelector('[data-configured-attributes]');
    const configuredRows = variantEditor.querySelector('[data-configured-attribute-rows]');
    const addConfiguredVariant = variantEditor.querySelector('[data-add-configured-variant]');
    let attributeTargetGroup;
    let valueTargetGroup;
    const sameText = (left, right) => left.localeCompare(right, 'es', { sensitivity: 'base' }) === 0;
    const groupValues = group => Array.from(group.querySelectorAll('[data-group-value]')).map(chip => chip.dataset.groupValue);
    const normalizeCombinationIndexes = () => Array.from(rows.querySelectorAll('[data-combination-row]')).forEach((row, index) =>
        row.querySelectorAll('[name]').forEach(input => input.name = input.name.replace(/Combinations\[(?:\d+|__index__)\]/, `Combinations[${index}]`)));
    const bindCombination = row => row.querySelector('[data-delete-combination]')?.addEventListener('click', () => {
        row.remove();
        normalizeCombinationIndexes();
    });
    function assignCombinationCodes() {
        let next = Math.max(0, ...Array.from(rows.querySelectorAll('input[name$=".InternalCode"]')).map(x => Number(x.value.split('-').pop()) || 0));
        rows.querySelectorAll('[data-combination-row]').forEach(row => {
            const input = row.querySelector('input[name$=".InternalCode"]');
            if (!input || input.value) return;
            const code = `${baseCode.value || 'CÓDIGO'}-${String(++next).padStart(2, '0')}`;
            input.value = code;
            const target = row.querySelector('[data-combination-code]');
            if (target) target.textContent = code;
        });
    }
    const combinationPairs = key => (key || '').split('|').map(pair => {
        const separator = pair.indexOf('=');
        return separator > 0 ? { type: pair.slice(0, separator).trim(), value: pair.slice(separator + 1).trim() } : null;
    }).filter(Boolean);
    const combinationIdentity = key => combinationPairs(key)
        .map(pair => `${pair.type.toLocaleLowerCase('es')}=${pair.value.toLocaleLowerCase('es')}`)
        .sort((left, right) => left.localeCompare(right, 'es'))
        .join('|');
    const compactCombinationLabel = key => {
        const variantLabel = combinationPairs(key).map(pair => `${pair.type} - ${pair.value}`).join(' / ');
        const productName = editor.querySelector('#Name')?.value.trim();
        return productName ? `${productName} ${variantLabel}` : variantLabel;
    };
    const updateCompactCombinationLabels = () => {
        if (!compactVariants) return;
        rows.querySelectorAll('[data-combination-row]').forEach(row => {
            const label = row.querySelector('[data-combination-label]');
            if (label) label.textContent = compactCombinationLabel(row.dataset.key);
        });
    };
    function refreshCombinations() {
        if (modes.find(x => x.checked)?.value !== 'true') return;
        updateCompactCombinationLabels();
        assignCombinationCodes();
        normalizeCombinationIndexes();
    }
    const normalizeGroups = () => Array.from(groups.querySelectorAll('[data-variant-group]')).forEach((group, groupIndex) => {
        group.querySelector('[data-group-type]').name = `VariantGroups[${groupIndex}].TypeName`;
        group.querySelectorAll('[data-group-value-hidden]').forEach(input => input.remove());
        group.querySelectorAll('[data-group-value]').forEach((chip, valueIndex) => {
            const input = document.createElement('input'); input.type = 'hidden'; input.dataset.groupValueHidden = '';
            input.name = `VariantGroups[${groupIndex}].Values[${valueIndex}]`; input.value = chip.dataset.groupValue;
            chip.appendChild(input);
        });
    });
    const updateValueOptions = group => {
        const datalist = group.querySelector('[data-group-value-options]');
        datalist.replaceChildren(...globalValueCatalog.map(item => {
            const option = document.createElement('option'); option.value = item.Value; return option;
        }));
    };
    function renderConfiguredAttributes() {
        if (!compactVariants || !configuredPanel || !configuredRows) return;
        configuredRows.replaceChildren();
        const rendered = [];
        Array.from(groups.querySelectorAll('[data-variant-group]')).forEach(group => {
            const typeName = group.querySelector('[data-group-type]').value.trim();
            groupValues(group).forEach(value => {
                if (rendered.some(item => sameText(item.typeName, typeName) && sameText(item.value, value))) return;
                rendered.push({ typeName, value });
                const row = document.createElement('div');
                row.className = 'configured-attribute-row';
                const label = document.createElement('span');
                label.textContent = `${typeName} - ${value}`;
                const remove = document.createElement('button');
                remove.type = 'button';
                remove.className = 'icon-button';
                remove.setAttribute('aria-label', `Quitar ${typeName} - ${value}`);
                remove.textContent = '×';
                remove.addEventListener('click', () => {
                    group.querySelectorAll('[data-group-value]').forEach(item => {
                        if (sameText(item.dataset.groupValue, value)) item.remove();
                    });
                    if (!groupValues(group).length) group.remove();
                    normalizeGroups();
                    refreshCombinations();
                    renderConfiguredAttributes();
                });
                row.append(label, remove);
                configuredRows.appendChild(row);
            });
        });
        configuredPanel.hidden = !configuredRows.children.length;
    }
    const getOrCreateConfiguredGroup = typeName => {
        const existing = Array.from(groups.querySelectorAll('[data-variant-group]'))
            .find(group => sameText(group.querySelector('[data-group-type]').value.trim(), typeName));
        if (existing) return existing;
        const group = document.createElement('div');
        group.dataset.variantGroup = '';
        const type = document.createElement('input');
        type.type = 'hidden';
        type.dataset.groupType = '';
        type.value = typeName;
        const values = document.createElement('div');
        values.dataset.groupValues = '';
        group.append(type, values);
        groups.appendChild(group);
        return group;
    };
    const clearConfiguredAttributes = () => {
        groups.replaceChildren();
        normalizeGroups();
        renderConfiguredAttributes();
        if (!compactCapture) return;
        compactCapture.querySelector('[data-group-type]').value = '';
        compactCapture.querySelector('[data-group-value-search]').value = '';
        updateValueOptions(compactCapture);
    };
    const confirmConfiguredVariant = () => {
        const definitions = Array.from(groups.querySelectorAll('[data-variant-group]')).map(group => ({
            type: group.querySelector('[data-group-type]').value.trim(),
            value: groupValues(group)[0]?.trim()
        })).filter(item => item.type && item.value);
        if (!definitions.length) {
            window.showSystemToast?.('warning', 'Agrega al menos un atributo y valor antes de agregar la variante.');
            return;
        }
        const key = definitions.map(item => `${item.type}=${item.value}`).join('|');
        const identity = combinationIdentity(key);
        const duplicate = Array.from(rows.querySelectorAll('[data-combination-row]'))
            .some(row => combinationIdentity(row.dataset.key) === identity);
        if (duplicate) {
            window.showSystemToast?.('warning', 'Esta variante ya fue agregada.');
            return;
        }
        const row = template.content.firstElementChild.cloneNode(true);
        row.dataset.key = key;
        row.querySelector('[data-combination-key]').value = key;
        row.querySelector('[data-combination-label]').textContent = compactCombinationLabel(key);
        const minimumStock = row.querySelector('input[name$=".MinimumStock"]');
        const lastCost = row.querySelector('input[name$=".LastCost"]');
        const salePrice = row.querySelector('input[name$=".SalePrice"]');
        if (minimumStock) minimumStock.value = editor.querySelector('#MinimumStock')?.value || '0';
        if (lastCost) lastCost.value = editor.querySelector('#LastCost')?.value || '0';
        if (salePrice) salePrice.value = editor.querySelector('#SalePrice')?.value || '0';
        rows.appendChild(row);
        bindCombination(row);
        assignCombinationCodes();
        normalizeCombinationIndexes();
        clearConfiguredAttributes();
        compactCapture?.querySelector('[data-group-type]')?.focus();
    };
    const appendValue = (group, value) => {
        const normalized = value.trim();
        if (!normalized || groupValues(group).some(existing => sameText(existing, normalized))) return false;
        const chip = document.createElement('span'); chip.className = 'variant-chip'; chip.dataset.groupValue = normalized;
        const label = document.createElement('span'); label.textContent = normalized;
        const remove = document.createElement('button'); remove.type = 'button'; remove.dataset.removeGroupValue = ''; remove.setAttribute('aria-label', `Quitar ${normalized}`); remove.textContent = '×';
        remove.addEventListener('click', () => { chip.remove(); normalizeGroups(); refreshCombinations(); renderConfiguredAttributes(); });
        chip.append(label, remove); group.querySelector('[data-group-values]').appendChild(chip); return true;
    };
    const bindGroup = group => {
        const type = group.querySelector('[data-group-type]');
        const search = group.querySelector('[data-group-value-search]');
        type.addEventListener('input', () => { updateValueOptions(group); if (!compactVariants) refreshCombinations(); });
        type.addEventListener('keydown', event => {
            if (event.key === 'Enter') event.preventDefault();
        });
        group.querySelector('[data-create-attribute]')?.addEventListener('click', () => {
            attributeTargetGroup = group;
            attributeDialogForm.reset();
            attributeDialog.showModal();
            attributeName.focus();
        });
        group.querySelector('[data-create-value]')?.addEventListener('click', () => {
            const selectedType = attributeCatalog.find(item => sameText(item.Name, type.value.trim()));
            if (!selectedType) {
                window.showSystemToast?.('warning', 'Seleccione un atributo antes de crear un valor.');
                return;
            }
            valueTargetGroup = group;
            valueDialogForm.reset();
            valueAttributeName.value = selectedType.Name;
            valueDialog.showModal();
            valueName.focus();
        });
        const selectCurrentValue = (clearAfterAdding = false, showFeedback = false) => {
            const selectedType = attributeCatalog.find(item => sameText(item.Name, type.value.trim()));
            const selectedValue = globalValueCatalog.find(item => sameText(item.Value, search.value.trim()));
            if (!selectedType) {
                if (showFeedback) window.showSystemToast?.('warning', 'Seleccione un atributo existente o créelo antes de agregar la variante.');
                return false;
            }
            if (!selectedValue) {
                if (showFeedback) window.showSystemToast?.('warning', 'Seleccione un valor existente o créelo antes de agregar la variante.');
                return false;
            }
            search.value = selectedValue.Value;
            const configuredGroup = compactVariants ? Array.from(groups.querySelectorAll('[data-variant-group]'))
                .find(item => sameText(item.querySelector('[data-group-type]').value.trim(), selectedType.Name)) : null;
            const configuredValues = configuredGroup ? groupValues(configuredGroup) : [];
            if (compactVariants && configuredValues.length) {
                const message = configuredValues.some(value => sameText(value, selectedValue.Value))
                    ? 'El atributo y valor ya fueron agregados.'
                    : 'Este atributo ya tiene un valor en la variante actual. Quítalo antes de seleccionar otro.';
                if (showFeedback) window.showSystemToast?.('warning', message);
                return false;
            }
            const targetGroup = compactVariants ? (configuredGroup || getOrCreateConfiguredGroup(selectedType.Name)) : group;
            const added = appendValue(targetGroup, selectedValue.Value);
            if (added) {
                normalizeGroups();
                refreshCombinations();
                renderConfiguredAttributes();
                if (clearAfterAdding) search.value = '';
            } else if (showFeedback) {
                window.showSystemToast?.('warning', 'La variante ya fue agregada.');
            }
            return added;
        };
        if (!compactVariants) search.addEventListener('change', () => selectCurrentValue());
        group.querySelector('[data-add-group-value]')?.addEventListener('click', () => {
            const added = selectCurrentValue(true, true);
            if (added && compactVariants) {
                type.value = '';
                updateValueOptions(group);
                type.focus();
                return;
            }
            search.focus();
        });
        search.addEventListener('keydown', event => {
            if (event.key !== 'Enter') return;
            event.preventDefault();
        });
        group.querySelectorAll('[data-remove-group-value]').forEach(button => button.addEventListener('click', () => { button.closest('[data-group-value]').remove(); normalizeGroups(); refreshCombinations(); }));
        group.querySelector('[data-remove-group]')?.addEventListener('click', () => { group.remove(); normalizeGroups(); refreshCombinations(); });
        updateValueOptions(group);
    };
    editor.querySelector('[data-add-group]')?.addEventListener('click', () => {
        const group = document.createElement('div'); group.className = 'variant-group'; group.dataset.variantGroup = '';
        const listId = `variant-values-${Date.now()}`;
        const createAttributeButton = attributeDialog ? '<button type="button" class="button-secondary" data-create-attribute>+ Crear atributo</button>' : '';
        const createValueButton = valueDialog ? '<button type="button" class="button-secondary" data-create-value>+ Crear valor</button>' : '';
        group.innerHTML = `<div class="field"><label>Atributo</label><div class="catalog-picker"><input class="input" list="variant-types" autocomplete="off" placeholder="Buscar atributo..." data-group-type />${createAttributeButton}</div></div><div class="field"><label>Valor</label><div class="catalog-picker"><input class="input" list="${listId}" autocomplete="off" placeholder="Buscar valor..." data-group-value-search /><datalist id="${listId}" data-group-value-options></datalist>${createValueButton}</div><div class="variant-value-chips" data-group-values></div></div><button type="button" class="icon-button variant-group-remove" data-remove-group aria-label="Eliminar atributo">×</button>`;
        groups.appendChild(group); bindGroup(group); normalizeGroups(); group.querySelector('input').focus();
    });
    attributeDialogForm?.addEventListener('submit', async event => {
        event.preventDefault();
        if (!attributeDialogForm.reportValidity() || !attributeTargetGroup) return;
        const submit = attributeDialogForm.querySelector('button[type="submit"]');
        submit.disabled = true;
        try {
            const response = await fetch(editor.dataset.createAttributeUrl, {
                method: 'POST',
                body: new FormData(attributeDialogForm),
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const result = await response.json();
            if (!response.ok) throw new Error(result.message || 'No fue posible crear el atributo.');
            if (!Array.from(attributeTypes.options).some(option => sameText(option.value, result.text))) {
                const option = document.createElement('option'); option.value = result.text; attributeTypes.appendChild(option);
            }
            if (!attributeCatalog.some(item => sameText(item.Name, result.text))) {
                attributeCatalog.push({ Id: result.id, Name: result.text, Options: [] });
            }
            attributeTargetGroup.querySelector('[data-group-type]').value = result.text;
            updateValueOptions(attributeTargetGroup);
            refreshCombinations();
            attributeDialog.close();
            window.showSystemToast?.('success', 'El atributo fue creado correctamente.');
        } catch (error) {
            window.showSystemToast?.('error', error.message || 'No fue posible crear el atributo.');
        } finally {
            submit.disabled = false;
        }
    });
    attributeDialog?.querySelectorAll('[data-close-attribute-dialog]').forEach(button =>
        button.addEventListener('click', () => attributeDialog.close()));
    valueDialogForm?.addEventListener('submit', async event => {
        event.preventDefault();
        if (!valueDialogForm.reportValidity() || !valueTargetGroup) return;
        const submit = valueDialogForm.querySelector('button[type="submit"]');
        submit.disabled = true;
        try {
            const response = await fetch(editor.dataset.createValueUrl, {
                method: 'POST',
                body: new FormData(valueDialogForm),
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const result = await response.json();
            if (!response.ok) throw new Error(result.message || 'No fue posible crear el valor.');
            const selectedType = attributeCatalog.find(item => sameText(item.Name, valueAttributeName.value));
            if (!selectedType.Options.some(item => sameText(item.Value, result.text)))
                selectedType.Options.push({ Id: result.id, Value: result.text });
            if (!globalValueCatalog.some(item => sameText(item.Value, result.text))) {
                globalValueCatalog.push({ Id: result.id, Value: result.text });
                globalValueCatalog.sort((left, right) => left.Value.localeCompare(right.Value, 'es', { sensitivity: 'base' }));
            }
            updateValueOptions(valueTargetGroup);
            valueTargetGroup.querySelector('[data-group-value-search]').value = result.text;
            if (!compactVariants) {
                appendValue(valueTargetGroup, result.text);
                normalizeGroups();
                refreshCombinations();
            }
            valueDialog.close();
            window.showSystemToast?.('success', 'El valor fue creado correctamente.');
        } catch (error) {
            window.showSystemToast?.('error', error.message || 'No fue posible crear el valor.');
        } finally {
            submit.disabled = false;
        }
    });
    valueDialog?.querySelectorAll('[data-close-value-dialog]').forEach(button =>
        button.addEventListener('click', () => valueDialog.close()));
    const syncVariantMode = () => {
        const enabled = modes.find(x => x.checked)?.value === 'true';
        variantEditor.querySelector('[data-without-variants]').hidden = enabled;
        variantEditor.querySelector('[data-with-variants]').hidden = !enabled;
        const barcode = editor.querySelector('#Barcode'); if (barcode) barcode.disabled = enabled;
        if (enabled) refreshCombinations();
    };
    modes.forEach(mode => mode.addEventListener('change', syncVariantMode));
    if (compactVariants && compactCapture) bindGroup(compactCapture);
    else groups.querySelectorAll('[data-variant-group]').forEach(bindGroup);
    addConfiguredVariant?.addEventListener('click', confirmConfiguredVariant);
    editor.querySelector('#Name')?.addEventListener('input', updateCompactCombinationLabels);
    normalizeGroups();
    rows.querySelectorAll('[data-combination-row]').forEach(bindCombination);
    renderConfiguredAttributes();
    syncVariantMode();

    const multi = editor.querySelector('[data-multi-price-editor]');
    if (multi) {
    const enabled = multi.querySelector('[data-multi-price-enabled]');
    const settings = Array.from(multi.querySelectorAll('[data-multi-price-settings]'));
    const priceRows = multi.querySelector('[data-price-rule-rows]');
    const priceTemplate = multi.querySelector('[data-price-rule-template]');
    const dialog = document.querySelector('#product-price-editor-modal');
    const dialogForm = dialog.querySelector('[data-price-dialog-form]');
    const quantity = dialog.querySelector('[data-dialog-rule-quantity]');
    const price = dialog.querySelector('[data-dialog-rule-price]');
    const active = dialog.querySelector('[data-dialog-rule-active]');
    let editingRow;
    const normalizePrices = () => Array.from(priceRows.querySelectorAll('[data-price-rule-row]')).forEach((row, i) =>
        row.querySelectorAll('[name]').forEach(input => input.name = input.name.replace(/PriceRules\[\d+\]/, `PriceRules[${i}]`)));
    const bindPrice = row => {
        row.querySelector('[data-edit-rule]').onclick = () => { editingRow = row; const inputs = row.querySelectorAll('[data-rule-input]'); quantity.value = inputs[0].value; price.value = inputs[1].value; active.checked = row.querySelector('input[type=checkbox]').checked; dialog.showModal(); };
        row.querySelector('[data-delete-rule]').onclick = () => { row.remove(); normalizePrices(); };
    };
    multi.querySelector('[data-add-price-rule]').onclick = () => { editingRow = null; dialogForm.reset(); active.checked = true; dialog.showModal(); };
    dialogForm.addEventListener('submit', event => {
        event.preventDefault();
        if (!dialogForm.checkValidity()) { window.showSystemToast?.('warning', 'Completa correctamente la cantidad y el precio.'); return; }
        let row = editingRow;
        if (!row) { row = priceTemplate.content.firstElementChild.cloneNode(true); priceRows.appendChild(row); bindPrice(row); }
        const inputs = row.querySelectorAll('[data-rule-input]'); inputs[0].value = quantity.value; inputs[1].value = price.value;
        row.querySelector('input[type=checkbox]').checked = active.checked; normalizePrices(); dialog.close();
    });
    dialog.querySelectorAll('[data-close-price-dialog]').forEach(button => button.addEventListener('click', () => {
        dialogForm.reset();
        editingRow = null;
        dialog.close();
    }));
    const syncPriceMode = () => { const exact = multi.querySelector('input[name=MultiPriceMode]:checked')?.value === 'ExactQuantity'; multi.querySelector('[data-price-heading]').textContent = exact ? 'Precio total' : 'Precio por unidad'; multi.querySelector('[data-multi-price-help]').textContent = exact ? 'Aplica solamente cuando la cantidad coincide exactamente.' : 'Aplica desde la cantidad indicada.'; };
    enabled.addEventListener('change', () => settings.forEach(x => x.classList.toggle('is-hidden', !enabled.checked)));
    multi.querySelectorAll('input[name=MultiPriceMode]').forEach(x => x.addEventListener('change', syncPriceMode));
    priceRows.querySelectorAll('[data-price-rule-row]').forEach(bindPrice); settings.forEach(x => x.classList.toggle('is-hidden', !enabled.checked)); syncPriceMode();
    }

    const articleType = editor.querySelector('#ArticleType');
    const salePriceField = editor.querySelector('[data-product-sale-price]');
    const syncArticleType = () => {
        const ingredient = articleType?.value === '6';
        if (salePriceField) { salePriceField.hidden = ingredient; salePriceField.classList.toggle("is-hidden", ingredient); }
    };
    articleType?.addEventListener('change', syncArticleType);
    syncArticleType();
    form.addEventListener('submit', event => {
        syncCategory(); refreshCombinations();
        const errors = [];
        if (!editor.querySelector('#Name').value.trim()) errors.push('El nombre es obligatorio.');
        if (!baseCode.value.trim()) errors.push('Ingresa o genera el código del producto.');
        const hasUnknownAttribute = Array.from(groups.querySelectorAll('[data-group-type]')).some(input =>
            input.value.trim() && !attributeCatalog.some(item => sameText(item.Name, input.value.trim())));
        if (hasUnknownAttribute) errors.push('Seleccione un atributo existente o créelo antes de guardar el producto.');
        if (modes.find(x => x.checked)?.value === 'true' && !rows.querySelector('[data-combination-row]')) errors.push('Agrega al menos una variante.');
        if (errors.length) { event.preventDefault(); window.showSystemToast?.('error', errors); }
    });
};
window.initializeProductForm();
