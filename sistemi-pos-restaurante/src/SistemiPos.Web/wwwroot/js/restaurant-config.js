(() => {
    'use strict';

    const form = document.getElementById('restaurant-configuration');
    if (!form) return;

    // Load initial data
    const rawPayload = document.getElementById('restaurant-config-data')?.textContent;
    if (!rawPayload) return;
    const payload = JSON.parse(rawPayload);
    let config = payload.definition || {};
    config.sections ||= [];
    config.menu ||= [];
    config.stations ||= [];
    config.channels ||= [];
    config.tables ||= [];
    config.zones ||= [];
    config.drivers ||= [];
    config.combos ||= [];
    config.promotions ||= [];

    const products = payload.products || [];
    const components = payload.components || [];
    const productById = new Map(products.map(x => [x.id, x]));
    const componentById = new Map(components.map(x => [x.id, x]));

    const { money, normalize, confirmAction } = window.restaurantUI || {
        money: val => 'Q ' + Number(val || 0).toFixed(2),
        normalize: str => (str || '').toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').trim(),
        confirmAction: async opts => confirm(opts.title + '\n' + opts.description)
    };

    let dirty = false;
    let selectedSectionId = config.sections[0]?.id || null;
    let selectedProductId = null;
    let selectedGroupId = null;
    let posSimulatedProduct = null;
    let posSimSelections = { qty: 1, options: new Set(), exclusions: new Set() };

    // Dom elements
    const stateBadge = document.getElementById('restaurant-config-state');
    const msgEl = form.querySelector('.restaurant-message');
    const currentSectionBadge = document.getElementById('current-section-badge');
    const sectionCardsList = document.getElementById('section-cards-list');
    const btnDeleteSection = document.getElementById('btn-delete-section');
    const productsCardsList = document.getElementById('products-cards-list');
    const inspectorEl = document.getElementById('product-inspector');
    const inspectorSummaryCard = document.getElementById('inspector-summary-card');

    // Filter controls
    const searchInput = document.getElementById('products-search-input');
    const filterStatus = document.getElementById('filter-product-status');
    const filterRecipe = document.getElementById('filter-product-recipe');
    const filterFavBtn = document.getElementById('filter-btn-favorites');
    const filterOrderBtn = document.getElementById('filter-btn-order');
    let onlyFavoritesFilter = false;
    let orderAsc = true;

    const uid = () => crypto.randomUUID();

    function markDirty() {
        dirty = true;
        if (stateBadge) {
            stateBadge.className = 'config-status-pill status-unsaved';
            stateBadge.innerHTML = '<span class="status-dot"></span> Cambios sin guardar';
        }
        if (msgEl) msgEl.textContent = '';
    }

    function markClean() {
        dirty = false;
        if (stateBadge) {
            stateBadge.className = 'config-status-pill status-saved';
            stateBadge.innerHTML = '<span class="status-dot"></span> Sin cambios';
        }
    }

    function getSectionInitials(name) {
        const words = String(name || '').match(/[\p{L}\p{N}]+/gu) || [];
        return words.slice(0, 2).map(word => word[0].toLocaleUpperCase()).join('') || '—';
    }
    const favoriteIcon = '<svg class="config-favorite-icon" viewBox="0 0 24 24" aria-hidden="true"><path d="m12 3 2.8 5.7 6.2.9-4.5 4.4 1.1 6.2-5.6-3-5.6 3 1.1-6.2L3 9.6l6.2-.9Z"/></svg>';

    function getProductImage(item, product) {
        if (product?.imageUrl) return `<img src="${product.imageUrl}" alt="${product.name}" class="product-row-thumb" />`;
        return `<div class="product-row-thumb">${getSectionInitials(product?.name || '')}</div>`;
    }

    // ==========================================
    // TABS SWITCHING LOGIC
    // ==========================================
    const mainTabButtons = document.querySelectorAll('.main-tab-btn');
    const tabPanes = document.querySelectorAll('.tab-pane');

    mainTabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const tabId = btn.dataset.tab;
            mainTabButtons.forEach(b => {
                b.classList.toggle('active', b === btn);
                b.setAttribute('aria-selected', b === btn ? 'true' : 'false');
            });
            tabPanes.forEach(pane => {
                pane.classList.toggle('active', pane.id === tabId);
            });
            if (tabId === 'tab-vista-previa') {
                renderPosPreview();
            } else if (tabId === 'tab-opciones-extras') {
                renderGlobalGroups();
            } else if (tabId === 'tab-estaciones') {
                renderGlobalStations();
            } else if (tabId === 'tab-disponibilidad') {
                renderGlobalChannelsAndTables();
            }
        });
    });

    // Inspector Subtabs
    const subtabButtons = document.querySelectorAll('.subtab-btn');
    const subtabPanes = document.querySelectorAll('.subtab-pane');
    subtabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.dataset.subtab;
            subtabButtons.forEach(b => b.classList.toggle('active', b === btn));
            subtabPanes.forEach(p => p.classList.toggle('active', p.id === targetId));
        });
    });

    // ==========================================
    // 1. GESTIÓN DE SECCIONES (COLUMNA 1)
    // ==========================================
    function renderSections() {
        sectionCardsList.innerHTML = '';
        if (!config.sections.length) {
            sectionCardsList.innerHTML = '<p class="restaurant-empty">No hay secciones comerciales. Pulsa "+ Agregar sección" para crear la primera.</p>';
            btnDeleteSection.disabled = true;
            return;
        }

        // Auto select first if none selected or invalid
        if (!config.sections.some(s => s.id === selectedSectionId)) {
            selectedSectionId = config.sections[0]?.id || null;
        }

        const sorted = config.sections.slice().sort((a, b) => a.sortOrder - b.sortOrder);

        sorted.forEach((sec, idx) => {
            const count = config.menu.filter(m => m.sectionId === sec.id).length;
            const isSelected = sec.id === selectedSectionId;

            const card = document.createElement('div');
            card.className = `section-item-card ${isSelected ? 'is-active-section' : ''}`;
            card.dataset.id = sec.id;

            card.innerHTML = `
                <span class="section-drag-handle" title="Posición">${sec.sortOrder || (idx + 1)}</span>
                <div class="section-item-thumb">${getSectionInitials(sec.name)}</div>
                <div class="section-item-info">
                    <strong>${sec.name || 'Sin nombre'}</strong>
                    <small>${count} productos</small>
                </div>
                <label class="switch-control" title="Activa en el menú" onclick="event.stopPropagation()">
                    <input type="checkbox" class="sec-active-switch" ${sec.active ? 'checked' : ''} />
                    <span class="switch-slider"></span>
                </label>
            `;

            // Toggle active
            const switchEl = card.querySelector('.sec-active-switch');
            switchEl.addEventListener('change', e => {
                sec.active = e.target.checked;
                markDirty();
            });

            // Card click
            card.addEventListener('click', () => {
                selectedSectionId = sec.id;
                renderSections();
                renderProductsList();
            });

            sectionCardsList.appendChild(card);
        });

        // Update delete button state
        const curSec = config.sections.find(s => s.id === selectedSectionId);
        if (curSec) {
            const countInCur = config.menu.filter(m => m.sectionId === curSec.id).length;
            btnDeleteSection.disabled = countInCur > 0;
            if (countInCur > 0) {
                btnDeleteSection.title = 'No puedes eliminar una sección que contiene productos.';
            } else {
                btnDeleteSection.title = 'Eliminar esta sección vacía.';
            }
        } else {
            btnDeleteSection.disabled = true;
        }
    }

    // Add Section
    document.getElementById('btn-add-section').addEventListener('click', () => {
        const name = prompt('Nombre de la nueva sección comercial (ej. Pizzas, Acompañamientos, Bebidas):');
        if (!name || !name.trim()) return;
        const trimmed = name.trim();
        if (config.sections.some(s => s.name.trim().toLowerCase() === trimmed.toLowerCase())) {
            alert('Ya existe una sección con este nombre.');
            return;
        }
        const newSec = {
            id: uid(),
            name: trimmed,
            sortOrder: config.sections.length + 1,
            active: true
        };
        config.sections.push(newSec);
        selectedSectionId = newSec.id;
        markDirty();
        renderSections();
        renderProductsList();
    });

    // Delete Section
    btnDeleteSection.addEventListener('click', async () => {
        const curSec = config.sections.find(s => s.id === selectedSectionId);
        if (!curSec) return;
        const count = config.menu.filter(m => m.sectionId === curSec.id).length;
        if (count > 0) {
            alert(`La sección "${curSec.name}" contiene ${count} productos. Muévelos o quítalos antes de eliminarla.`);
            return;
        }
        const ok = await confirmAction({
            title: `Eliminar sección "${curSec.name}"`,
            description: 'La sección se quitará de la configuración de la sucursal al guardar.',
            danger: true,
            confirm: 'Eliminar'
        });
        if (!ok) return;

        config.sections = config.sections.filter(s => s.id !== curSec.id);
        selectedSectionId = config.sections[0]?.id || null;
        markDirty();
        renderSections();
        renderProductsList();
    });

    // ==========================================
    // 2. PRODUCTOS DE LA SECCIÓN (COLUMNA 2)
    // ==========================================
    function renderProductsList() {
        productsCardsList.innerHTML = '';
        const curSec = config.sections.find(s => s.id === selectedSectionId);
        if (currentSectionBadge) {
            currentSectionBadge.textContent = curSec ? curSec.name : 'Todas';
        }

        if (!selectedSectionId) {
            productsCardsList.innerHTML = '<p class="restaurant-empty">Selecciona o crea una sección en la columna izquierda.</p>';
            closeInspector();
            return;
        }

        let items = config.menu.filter(m => m.sectionId === selectedSectionId);

        // Filter: Status
        const statusVal = filterStatus.value;
        if (statusVal === 'active') items = items.filter(m => m.active !== false);
        if (statusVal === 'inactive') items = items.filter(m => m.active === false);

        // Filter: Recipe
        const recipeVal = filterRecipe.value;
        if (recipeVal === 'with-recipe') {
            items = items.filter(m => productById.get(m.productId)?.hasRecipe);
        } else if (recipeVal === 'retail') {
            items = items.filter(m => !productById.get(m.productId)?.hasRecipe);
        }

        // Filter: Favorites
        if (onlyFavoritesFilter) {
            items = items.filter(m => m.favorite);
        }

        // Filter: Search
        const query = normalize(searchInput.value);
        if (query) {
            items = items.filter(m => {
                const p = productById.get(m.productId);
                const name = normalize(m.customName || p?.name || '');
                const code = normalize(p?.code || '');
                return name.includes(query) || code.includes(query);
            });
        }

        // Sort
        items.sort((a, b) => {
            if (orderAsc) return (a.sortOrder || 0) - (b.sortOrder || 0);
            return (b.sortOrder || 0) - (a.sortOrder || 0);
        });

        if (!items.length) {
            productsCardsList.innerHTML = `
                <div class="restaurant-empty">
                    <strong>No hay productos en esta vista</strong>
                    <p>Pulsa "+ Agregar producto" para seleccionar artículos del catálogo general.</p>
                </div>
            `;
            if (selectedProductId && !config.menu.some(m => m.productId === selectedProductId)) {
                closeInspector();
            }
            return;
        }

        // If no product selected or selected not in current section, select first
        if (!selectedProductId || !items.some(m => m.productId === selectedProductId)) {
            selectedProductId = items[0].productId;
        }

        items.forEach(item => {
            const p = productById.get(item.productId);
            const isSelected = item.productId === selectedProductId;
            const displayName = item.customName || p?.name || 'Artículo no disponible';
            const price = item.customPrice ?? (p?.basePrice || 0);

            const card = document.createElement('div');
            card.className = `product-row-card ${isSelected ? 'is-selected' : ''}`;
            card.dataset.id = item.productId;

            card.innerHTML = `
                ${getProductImage(item, p)}
                <div class="product-row-details">
                    <strong>${displayName}</strong>
                    <span class="product-row-code">${p?.code || 'N/A'}</span>
                    <div class="product-row-badges">
                        <span class="badge-tag-recipe ${p?.hasRecipe ? '' : 'is-no-recipe'}">
                            ${p?.hasRecipe ? '● Receta activa' : '● Reventa'}
                        </span>
                        ${p?.canExcludeComponents ? '<span class="badge-tag-exclusions">✓ Permite exclusiones</span>' : ''}
                    </div>
                </div>
                <div class="product-row-price">${money(price)}</div>
                <button type="button" class="product-fav-btn ${item.favorite ? 'is-favorite' : ''}" title="Destacar como favorito" aria-label="Destacar como favorito">
                    ${favoriteIcon}
                </button>
                <label class="switch-control" title="Visible en el menú / POS" onclick="event.stopPropagation()">
                    <input type="checkbox" class="prod-active-switch" ${item.active !== false ? 'checked' : ''} />
                    <span class="switch-slider"></span>
                </label>
            `;

            // Favorite button
            const favBtn = card.querySelector('.product-fav-btn');
            favBtn.addEventListener('click', e => {
                e.stopPropagation();
                item.favorite = !item.favorite;
                favBtn.classList.toggle('is-favorite', item.favorite);
                markDirty();
                renderProductInspector();
            });

            // Active switch
            const activeSwitch = card.querySelector('.prod-active-switch');
            activeSwitch.addEventListener('change', e => {
                item.active = e.target.checked;
                markDirty();
                renderProductInspector();
            });

            // Card click -> Inspect
            card.addEventListener('click', () => {
                selectedProductId = item.productId;
                renderProductsList();
                renderProductInspector();
            });

            productsCardsList.appendChild(card);
        });

        renderProductInspector();
    }

    // Filter event listeners
    searchInput.addEventListener('input', renderProductsList);
    filterStatus.addEventListener('change', renderProductsList);
    filterRecipe.addEventListener('change', renderProductsList);
    filterFavBtn.addEventListener('click', () => {
        onlyFavoritesFilter = !onlyFavoritesFilter;
        filterFavBtn.classList.toggle('active', onlyFavoritesFilter);
        renderProductsList();
    });
    filterOrderBtn.addEventListener('click', () => {
        orderAsc = !orderAsc;
        filterOrderBtn.textContent = orderAsc ? '⇅ Orden (Asc)' : '⇅ Orden (Desc)';
        renderProductsList();
    });

    // ==========================================
    // 3. INSPECTOR LATERAL DE PRODUCTO (COLUMNA 3)
    // ==========================================
    function closeInspector() {
        selectedProductId = null;
        inspectorEl.style.display = 'none';
    }

    document.getElementById('btn-close-inspector').addEventListener('click', closeInspector);

    function renderProductInspector() {
        if (!selectedProductId) {
            inspectorEl.style.display = 'none';
            return;
        }
        const item = config.menu.find(m => m.productId === selectedProductId);
        const p = productById.get(selectedProductId);
        if (!item) {
            closeInspector();
            return;
        }
        inspectorEl.style.display = 'flex';

        const displayName = item.customName || p?.name || 'Artículo';
        const displayPrice = item.customPrice ?? (p?.basePrice || 0);

        // Top Summary Card
        inspectorSummaryCard.innerHTML = `
            ${getProductImage(item, p)}
            <div style="flex:1; min-width:0;">
                <strong style="display:block; font-size:0.95rem; color:var(--text-primary); white-space:nowrap; overflow:hidden; text-overflow:ellipsis;">
                    ${displayName}
                </strong>
                <span style="display:block; font-size:0.75rem; color:var(--text-secondary);">${p?.code || 'N/A'}</span>
                <div style="display:flex; gap:6px; margin-top:4px; flex-wrap:wrap;">
                    <span class="badge-tag-recipe ${p?.hasRecipe ? '' : 'is-no-recipe'}">
                        ${p?.hasRecipe ? '● Receta activa' : '● Reventa'}
                    </span>
                    ${p?.canExcludeComponents ? '<span class="badge-tag-exclusions">✓ Permite exclusiones</span>' : ''}
                </div>
            </div>
            <button type="button" class="product-fav-btn ${item.favorite ? 'is-favorite' : ''}" id="insp-header-fav-btn" aria-label="Destacar como favorito">${favoriteIcon}</button>
            <label class="switch-control" title="Visible en el menú">
                <input type="checkbox" id="insp-header-active-switch" ${item.active !== false ? 'checked' : ''} />
                <span class="switch-slider"></span>
            </label>
        `;

        document.getElementById('insp-header-fav-btn').addEventListener('click', () => {
            item.favorite = !item.favorite;
            markDirty();
            renderProductsList();
        });
        document.getElementById('insp-header-active-switch').addEventListener('change', e => {
            item.active = e.target.checked;
            markDirty();
            renderProductsList();
        });

        // Populate SUBTAB 1: GENERAL
        const inpName = document.getElementById('insp-custom-name');
        const inpPrice = document.getElementById('insp-custom-price');
        const selSection = document.getElementById('insp-section-id');
        const inpOrder = document.getElementById('insp-sort-order');
        const selStation = document.getElementById('insp-station-id');
        const badgeType = document.getElementById('insp-article-type-badge');
        const swActive = document.getElementById('insp-active-switch');
        const swFavorite = document.getElementById('insp-favorite-switch');
        const txtDesc = document.getElementById('insp-description');
        const counterDesc = document.getElementById('insp-desc-counter');

        inpName.value = item.customName || '';
        inpPrice.value = item.customPrice !== null && item.customPrice !== undefined ? item.customPrice : (p?.basePrice || '');
        inpOrder.value = item.sortOrder || 1;
        swActive.checked = item.active !== false;
        swFavorite.checked = !!item.favorite;
        txtDesc.value = item.description || '';
        counterDesc.textContent = `${txtDesc.value.length}/200`;

        badgeType.textContent = p?.hasRecipe ? 'Se prepara al momento (con receta)' : 'Producto terminado / Reventa';

        // Sections select
        selSection.innerHTML = '';
        config.sections.filter(s => s.active).forEach(s => {
            const opt = document.createElement('option');
            opt.value = s.id;
            opt.textContent = s.name;
            selSection.appendChild(opt);
        });
        selSection.value = item.sectionId || '';

        // Stations select
        selStation.innerHTML = '';
        config.stations.filter(s => s.active).forEach(s => {
            const opt = document.createElement('option');
            opt.value = s.id;
            opt.textContent = s.name;
            selStation.appendChild(opt);
        });
        selStation.value = item.stationId || (config.stations[0]?.id || '');

        // Form listeners
        inpName.oninput = () => {
            item.customName = inpName.value.trim() || null;
            markDirty();
            const titleEl = inspectorSummaryCard.querySelector('strong');
            if (titleEl) titleEl.textContent = item.customName || p?.name;
            const cardTitle = document.querySelector(`.product-row-card[data-id="${item.productId}"] strong`);
            if (cardTitle) cardTitle.textContent = item.customName || p?.name;
        };
        inpPrice.oninput = () => {
            const val = parseFloat(inpPrice.value);
            item.customPrice = !isNaN(val) && val >= 0 ? val : null;
            markDirty();
            const cardPrice = document.querySelector(`.product-row-card[data-id="${item.productId}"] .product-row-price`);
            if (cardPrice) cardPrice.textContent = money(item.customPrice ?? p?.basePrice);
        };
        selSection.onchange = () => {
            item.sectionId = selSection.value;
            markDirty();
            renderSections();
            renderProductsList();
        };
        inpOrder.oninput = () => {
            item.sortOrder = parseInt(inpOrder.value, 10) || 1;
            markDirty();
        };
        selStation.onchange = () => {
            item.stationId = selStation.value;
            markDirty();
        };
        swActive.onchange = () => {
            item.active = swActive.checked;
            markDirty();
            renderProductsList();
        };
        swFavorite.onchange = () => {
            item.favorite = swFavorite.checked;
            markDirty();
            renderProductsList();
        };
        txtDesc.oninput = () => {
            item.description = txtDesc.value;
            counterDesc.textContent = `${txtDesc.value.length}/200`;
            markDirty();
        };

        // Populate SUBTAB 2: RECETA Y EXCLUSIONES
        renderRecipeSubtab(item, p);

        // Populate SUBTAB 3: EXTRAS Y OPCIONES
        renderExtrasSubtab(item);

        // Populate SUBTAB 4: DISPONIBILIDAD
        renderAvailabilitySubtab(item);
    }

    // Subtab 2: Recipe Exclusions
    function renderRecipeSubtab(item, product) {
        const tbody = document.getElementById('insp-recipe-components-tbody');
        tbody.innerHTML = '';
        item.excludableProducts ||= [];

        if (!product?.hasRecipe || !product.recipeComponents?.length) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="5" style="text-align:center; padding:20px; color:var(--text-secondary);">
                        Este producto no tiene una receta activa elaborada al pedido; no admite exclusiones de ingredientes.
                    </td>
                </tr>
            `;
            return;
        }

        product.recipeComponents.forEach(comp => {
            const tr = document.createElement('tr');
            const isExcludable = item.excludableProducts.includes(comp.id);
            const isReq = comp.isRequired || comp.name.toLowerCase().includes('masa') || comp.name.toLowerCase().includes('salsa');

            tr.innerHTML = `
                <td>
                    <span style="font-weight:700; color:var(--text-primary);">${comp.name}</span>
                </td>
                <td class="col-num">${comp.quantity || ''}</td>
                <td>${comp.unit || 'g'}</td>
                <td class="col-center">
                    <label class="switch-control" ${isReq ? 'title="Componente estructural base requerido"' : ''}>
                        <input type="checkbox" class="excl-switch" ${isExcludable ? 'checked' : ''} ${isReq ? 'disabled' : ''} />
                        <span class="switch-slider"></span>
                    </label>
                </td>
                <td class="col-center">
                    <span class="${isReq ? 'badge-required-yes' : 'badge-required-no'}">${isReq ? 'Sí' : 'No'}</span>
                </td>
            `;

            const chk = tr.querySelector('.excl-switch');
            if (chk && !isReq) {
                chk.addEventListener('change', () => {
                    if (chk.checked) {
                        if (!item.excludableProducts.includes(comp.id)) item.excludableProducts.push(comp.id);
                    } else {
                        item.excludableProducts = item.excludableProducts.filter(id => id !== comp.id);
                    }
                    markDirty();
                });
            }

            tbody.appendChild(tr);
        });

        // View full recipe button
        document.getElementById('insp-btn-view-recipe').onclick = () => {
            alert(`Receta de ${product.name}:\n${product.recipeInstructions || 'Consulte el módulo de Producción/Recetas para ver la ficha técnica completa.'}`);
        };
    }

    // Subtab 3: Extras and Options
    function renderExtrasSubtab(item) {
        item.groups ||= [];
        const groupsContainer = document.getElementById('insp-groups-cards-list');
        const selectedGroupSec = document.getElementById('insp-selected-group-section');
        const optionsTbody = document.getElementById('insp-options-tbody');
        const selectedGroupTitle = document.getElementById('insp-selected-group-title');

        groupsContainer.innerHTML = '';
        if (!item.groups.length) {
            groupsContainer.innerHTML = '<p class="restaurant-muted" style="padding:10px 0;">No hay grupos de opciones asignados. Pulsa "+ Agregar grupo".</p>';
            selectedGroupSec.hidden = true;
        } else {
            selectedGroupSec.hidden = false;
            if (!item.groups.some(g => g.id === selectedGroupId)) {
                selectedGroupId = item.groups[0]?.id || null;
            }

            item.groups.forEach(g => {
                g.options ||= [];
                const isSelected = g.id === selectedGroupId;
                const isSingle = g.maximum === 1;
                const isReq = g.minimum > 0;
                const subtitle = `${isReq ? 'Obligatorio' : 'Opcional'} • ${isSingle ? 'Selección única' : 'Selección múltiple'}`;

                const card = document.createElement('div');
                card.className = `modifier-group-card ${isSelected ? 'is-selected' : ''}`;
                card.innerHTML = `
                    <div class="group-card-info">
                        <strong>${g.name || 'Grupo sin nombre'}</strong>
                        <small>${subtitle}</small>
                    </div>
                    <span class="group-card-badge">${g.options.length} opciones</span>
                    <button type="button" class="btn-icon config-icon-button btn-remove-group" title="Quitar grupo" aria-label="Quitar grupo">×</button>
                `;

                card.addEventListener('click', () => {
                    selectedGroupId = g.id;
                    renderExtrasSubtab(item);
                });

                card.querySelector('.btn-remove-group').addEventListener('click', e => {
                    e.stopPropagation();
                    if (confirm(`¿Quitar el grupo "${g.name}" de este producto?`)) {
                        item.groups = item.groups.filter(x => x.id !== g.id);
                        selectedGroupId = item.groups[0]?.id || null;
                        markDirty();
                        renderExtrasSubtab(item);
                    }
                });

                groupsContainer.appendChild(card);
            });

            // Render options for selected group
            const curGroup = item.groups.find(g => g.id === selectedGroupId);
            if (curGroup) {
                selectedGroupTitle.textContent = `Opciones del grupo: ${curGroup.name}`;
                optionsTbody.innerHTML = '';

                curGroup.options.forEach((opt, optIdx) => {
                    opt.components ||= [];
                    const tr = document.createElement('tr');
                    const compDesc = opt.components.map(c => {
                        const compObj = componentById.get(c.productId);
                        return `${c.quantity} ${compObj?.unit || 'g'} - ${compObj?.name || 'Insumo'}`;
                    }).join(', ') || 'Sin consumo vinculado';

                    tr.innerHTML = `
                        <td><strong>${opt.name || 'Opción'}</strong></td>
                        <td class="col-num">${money(opt.price)}</td>
                        <td><small style="color:var(--text-secondary);">${compDesc}</small></td>
                        <td class="col-center">${opt.sortOrder || (optIdx + 1)}</td>
                        <td class="col-center">
                            <label class="switch-control">
                                <input type="checkbox" class="opt-active-switch" ${opt.active !== false ? 'checked' : ''} />
                                <span class="switch-slider"></span>
                            </label>
                        </td>
                        <td class="col-center">
                            <button type="button" class="btn-icon config-icon-button btn-del-opt" title="Quitar opción" aria-label="Quitar opción">×</button>
                        </td>
                    `;

                    tr.querySelector('.opt-active-switch').addEventListener('change', e => {
                        opt.active = e.target.checked;
                        markDirty();
                    });
                    tr.querySelector('.btn-del-opt').addEventListener('click', () => {
                        curGroup.options = curGroup.options.filter(x => x.id !== opt.id);
                        markDirty();
                        renderExtrasSubtab(item);
                    });

                    optionsTbody.appendChild(tr);
                });
            }
        }

        // Add Group Button
        document.getElementById('insp-btn-add-group').onclick = () => {
            const name = prompt('Nombre del grupo (ej. Tamaño, Ingredientes adicionales, Tipo de salsa):');
            if (!name || !name.trim()) return;
            const isSingle = confirm('¿Es de selección única? (Aceptar = Selección única / Cancelar = Selección múltiple)');
            const newGroup = {
                id: uid(),
                name: name.trim(),
                minimum: isSingle ? 1 : 0,
                maximum: isSingle ? 1 : 5,
                options: []
            };
            item.groups.push(newGroup);
            selectedGroupId = newGroup.id;
            markDirty();
            renderExtrasSubtab(item);
        };

        // Add Option Button
        document.getElementById('insp-btn-add-option').onclick = () => {
            const curGroup = item.groups.find(g => g.id === selectedGroupId);
            if (!curGroup) return;
            const optName = prompt('Nombre del extra u opción (ej. Extra queso, Extra jamón):');
            if (!optName || !optName.trim()) return;
            const priceStr = prompt('Precio adicional en Quetzales (ej. 8.00):', '0.00');
            const price = parseFloat(priceStr) || 0;

            const newOpt = {
                id: uid(),
                name: optName.trim(),
                price: price,
                active: true,
                sortOrder: curGroup.options.length + 1,
                components: []
            };
            curGroup.options.push(newOpt);
            markDirty();
            renderExtrasSubtab(item);
        };
    }

    // Subtab 4: Availability
    function renderAvailabilitySubtab(item) {
        const swActive = document.getElementById('insp-disp-active');
        const rAlways = document.getElementById('sched-always');
        const rSpecific = document.getElementById('sched-specific');
        const schedBox = document.getElementById('schedule-specific-box');
        const txtObs = document.getElementById('insp-observations');
        const txtSusp = document.getElementById('insp-suspension');
        const countObs = document.getElementById('insp-obs-counter');
        const countSusp = document.getElementById('insp-susp-counter');

        swActive.checked = item.active !== false;
        rAlways.checked = item.alwaysAvailable !== false;
        rSpecific.checked = item.alwaysAvailable === false;
        schedBox.hidden = rAlways.checked;

        txtObs.value = item.availabilityNotes || '';
        txtSusp.value = item.suspensionReason || '';
        countObs.textContent = `${txtObs.value.length}/200`;
        countSusp.textContent = `${txtSusp.value.length}/200`;

        swActive.onchange = () => {
            item.active = swActive.checked;
            markDirty();
            renderProductsList();
        };

        rAlways.onchange = () => {
            item.alwaysAvailable = true;
            schedBox.hidden = true;
            markDirty();
        };
        rSpecific.onchange = () => {
            item.alwaysAvailable = false;
            schedBox.hidden = false;
            markDirty();
        };

        txtObs.oninput = () => {
            item.availabilityNotes = txtObs.value;
            countObs.textContent = `${txtObs.value.length}/200`;
            markDirty();
        };
        txtSusp.oninput = () => {
            item.suspensionReason = txtSusp.value;
            countSusp.textContent = `${txtSusp.value.length}/200`;
            markDirty();
        };
    }

    // ==========================================
    // 4. SELECTOR DE PRODUCTOS (MODAL PICKER)
    // ==========================================
    const pickerDialog = document.getElementById('restaurant-product-picker');
    const pickerSearch = document.getElementById('restaurant-picker-search');
    const pickerCategory = document.getElementById('restaurant-picker-category');
    const pickerType = document.getElementById('restaurant-picker-type');
    const pickerResults = document.getElementById('restaurant-picker-results');
    const pickerClose = document.getElementById('restaurant-picker-close');
    const pickerDone = document.getElementById('restaurant-picker-done');

    // Populate categories in picker
    const categories = [...new Set(products.map(p => p.catalogCategory))].filter(Boolean).sort();
    categories.forEach(cat => {
        const opt = document.createElement('option');
        opt.value = cat;
        opt.textContent = cat;
        pickerCategory.appendChild(opt);
    });

    function openProductPicker() {
        if (!config.sections.length) {
            alert('Debes crear primero al menos una sección comercial (ej. Pizzas o Bebidas).');
            return;
        }
        pickerSearch.value = '';
        pickerCategory.value = '';
        pickerType.value = '';
        renderPickerResults();
        pickerDialog.showModal();
    }

    document.getElementById('btn-open-picker').addEventListener('click', openProductPicker);
    pickerClose.addEventListener('click', () => pickerDialog.close());
    pickerDone.addEventListener('click', () => pickerDialog.close());

    function renderPickerResults() {
        pickerResults.innerHTML = '';
        const q = normalize(pickerSearch.value);
        const cat = pickerCategory.value;
        const type = pickerType.value;
        const existingIds = new Set(config.menu.map(m => m.productId));

        const matches = products.filter(p => {
            if (cat && p.catalogCategory !== cat) return false;
            if (type && !p.type.includes(type)) return false;
            if (q) {
                const searchStr = normalize(p.name + ' ' + p.code + ' ' + p.catalogCategory);
                if (!searchStr.includes(q)) return false;
            }
            return true;
        });

        if (!matches.length) {
            pickerResults.innerHTML = '<p class="restaurant-empty">No se encontraron artículos con estos filtros.</p>';
            return;
        }

        matches.slice(0, 30).forEach(p => {
            const isAlreadyAdded = existingIds.has(p.id);
            const card = document.createElement('div');
            card.className = 'restaurant-picker-card';
            card.innerHTML = `
                <div>
                    <strong>${p.name}</strong>
                    <small>${p.code} · ${p.type} · ${p.catalogCategory}</small>
                    <div style="display:flex; gap:6px; margin-top:4px;">
                        <span class="badge-tag-recipe ${p.hasRecipe ? '' : 'is-no-recipe'}">
                            ${p.hasRecipe ? '● Receta activa' : '● Reventa'}
                        </span>
                        ${p.canExcludeComponents ? '<span class="badge-tag-exclusions">✓ Permite exclusiones</span>' : ''}
                    </div>
                </div>
                <div>
                    ${isAlreadyAdded
                        ? '<span class="badge-tag-recipe" style="background:var(--surface-secondary); color:var(--text-secondary);">Ya en menú</span>'
                        : '<button type="button" class="button-primary button-sm btn-pick-product">+ Agregar</button>'
                    }
                </div>
            `;

            const addBtn = card.querySelector('.btn-pick-product');
            if (addBtn) {
                addBtn.addEventListener('click', () => {
                    const activeSec = config.sections.find(s => s.id === selectedSectionId) || config.sections[0];
                    const activeStation = config.stations[0] || { id: uid(), name: 'Cocina Caliente', active: true };
                    if (!config.stations.length) config.stations.push(activeStation);

                    const newItem = {
                        productId: p.id,
                        sectionId: activeSec.id,
                        stationId: activeStation.id,
                        sortOrder: config.menu.filter(m => m.sectionId === activeSec.id).length + 1,
                        favorite: false,
                        active: true,
                        customName: null,
                        customPrice: null,
                        description: null,
                        excludableProducts: [],
                        groups: []
                    };
                    config.menu.push(newItem);
                    selectedProductId = p.id;
                    markDirty();
                    renderSections();
                    renderProductsList();
                    renderPickerResults();
                });
            }

            pickerResults.appendChild(card);
        });
    }

    pickerSearch.addEventListener('input', renderPickerResults);
    pickerCategory.addEventListener('change', renderPickerResults);
    pickerType.addEventListener('change', renderPickerResults);

    // ==========================================
    // 5. VISTA PREVIA EN EL POS (PANEL 5)
    // ==========================================
    let posPreviewSelectedSectionId = null;

    function renderPosPreview() {
        const sectionsList = document.getElementById('pos-sections-list');
        const productsGrid = document.getElementById('pos-products-grid');
        sectionsList.innerHTML = '';
        productsGrid.innerHTML = '';

        const activeSecs = config.sections.filter(s => s.active).sort((a, b) => a.sortOrder - b.sortOrder);
        if (!activeSecs.length) {
            productsGrid.innerHTML = '<p class="restaurant-empty">No hay secciones activas para previsualizar.</p>';
            return;
        }

        if (!posPreviewSelectedSectionId || !activeSecs.some(s => s.id === posPreviewSelectedSectionId)) {
            posPreviewSelectedSectionId = activeSecs[0].id;
        }

        // Render Section buttons
        activeSecs.forEach(sec => {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = `pos-sec-btn ${sec.id === posPreviewSelectedSectionId ? 'active' : ''}`;
            btn.innerHTML = `<span>${getSectionInitials(sec.name)}</span> <span>${sec.name}</span>`;
            btn.addEventListener('click', () => {
                posPreviewSelectedSectionId = sec.id;
                renderPosPreview();
            });
            sectionsList.appendChild(btn);
        });

        // Render Products in Grid
        const items = config.menu.filter(m => m.sectionId === posPreviewSelectedSectionId && m.active !== false)
            .sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));

        if (!items.length) {
            productsGrid.innerHTML = '<p class="restaurant-empty">No hay productos visibles en esta sección.</p>';
            return;
        }

        items.forEach(item => {
            const p = productById.get(item.productId);
            const card = document.createElement('div');
            card.className = 'pos-product-card';
            const displayName = item.customName || p?.name || 'Producto';
            const price = item.customPrice ?? (p?.basePrice || 0);

            card.innerHTML = `
                ${getProductImage(item, p).replace('product-row-thumb', 'pos-product-thumb')}
                <div class="pos-product-body">
                    <span class="pos-product-title">${displayName}</span>
                    <div class="pos-product-footer">
                        <span class="pos-product-price">${money(price)}</span>
                        ${item.favorite ? '<span class="config-preview-favorite" aria-label="Favorito">' + favoriteIcon + '</span>' : ''}
                    </div>
                </div>
            `;

            card.addEventListener('click', () => {
                openPosSimulationModal(item, p);
            });

            productsGrid.appendChild(card);
        });
    }

    // Modal Simulation Logic
    const posModal = document.getElementById('restaurant-pos-simulation-modal');
    const posModalBody = document.getElementById('pos-sim-body');
    const posModalTotal = document.getElementById('pos-sim-total');
    const posModalClose = document.getElementById('pos-sim-close');
    const posModalAddBtn = document.getElementById('pos-sim-add-btn');

    posModalClose.addEventListener('click', () => posModal.close());
    posModalAddBtn.addEventListener('click', () => {
        alert(`¡Simulación exitosa!\nSe calculó ${posModalTotal.textContent} para "${posSimulatedProduct?.customName || productById.get(posSimulatedProduct?.productId)?.name}".\n(No se alteró la base de datos de órdenes ni el inventario).`);
        posModal.close();
    });

    function openPosSimulationModal(item, product) {
        posSimulatedProduct = item;
        posSimSelections = { qty: 1, options: new Set(), exclusions: new Set() };
        document.getElementById('pos-sim-title').textContent = item.customName || product?.name || 'Personalizar';
        renderPosModalContent();
        posModal.showModal();
    }

    function renderPosModalContent() {
        const item = posSimulatedProduct;
        const p = productById.get(item.productId);
        const basePrice = item.customPrice ?? (p?.basePrice || 0);
        posModalBody.innerHTML = '';

        // Quantity Row
        const qtyRow = document.createElement('div');
        qtyRow.style.cssText = 'display:flex; justify-content:space-between; align-items:center; padding-bottom:12px; border-bottom:1px solid var(--surface-secondary);';
        qtyRow.innerHTML = `
            <div><strong>Cantidad</strong><small style="display:block; color:var(--text-secondary);">Número de porciones</small></div>
            <div style="display:flex; align-items:center; gap:10px;">
                <button type="button" id="pos-sim-qty-minus" class="pill-btn" style="width:36px; height:36px; font-size:1.2rem;">-</button>
                <strong id="pos-sim-qty-val" style="font-size:1.1rem; min-width:24px; text-align:center;">${posSimSelections.qty}</strong>
                <button type="button" id="pos-sim-qty-plus" class="pill-btn" style="width:36px; height:36px; font-size:1.2rem;">+</button>
            </div>
        `;
        qtyRow.querySelector('#pos-sim-qty-minus').onclick = () => {
            if (posSimSelections.qty > 1) {
                posSimSelections.qty--;
                renderPosModalContent();
            }
        };
        qtyRow.querySelector('#pos-sim-qty-plus').onclick = () => {
            posSimSelections.qty++;
            renderPosModalContent();
        };
        posModalBody.appendChild(qtyRow);

        // Recipe Exclusions
        if (p?.canExcludeComponents && item.excludableProducts?.length) {
            const exclBox = document.createElement('div');
            exclBox.innerHTML = `
                <strong style="display:block; font-size:0.9rem; margin-bottom:4px;">Exclusiones permitidas</strong>
                <p style="font-size:0.75rem; color:var(--text-secondary); margin:0 0 10px;">El cliente puede quitar ingredientes de la receta:</p>
                <div style="display:flex; flex-direction:column; gap:6px;"></div>
            `;
            const list = exclBox.querySelector('div');
            item.excludableProducts.forEach(compId => {
                const comp = p.recipeComponents?.find(c => c.id === compId);
                if (!comp) return;
                const isExcluded = posSimSelections.exclusions.has(comp.id);
                const label = document.createElement('label');
                label.className = 'channel-chip-check';
                label.innerHTML = `
                    <input type="checkbox" ${isExcluded ? 'checked' : ''} />
                    <span>Sin ${comp.name}</span>
                `;
                label.querySelector('input').onchange = e => {
                    if (e.target.checked) posSimSelections.exclusions.add(comp.id);
                    else posSimSelections.exclusions.delete(comp.id);
                    calculatePosModalTotal();
                };
                list.appendChild(label);
            });
            posModalBody.appendChild(exclBox);
        }

        // Modifier Groups
        if (item.groups?.length) {
            item.groups.forEach(group => {
                const gBox = document.createElement('div');
                const isSingle = group.maximum === 1;
                gBox.innerHTML = `
                    <strong style="display:block; font-size:0.9rem; margin-bottom:2px;">${group.name}</strong>
                    <small style="display:block; color:var(--text-secondary); margin-bottom:8px;">${isSingle ? 'Selecciona 1 opción' : `Hasta ${group.maximum} opciones`}</small>
                    <div style="display:flex; flex-direction:column; gap:6px;"></div>
                `;
                const gList = gBox.querySelector('div');
                group.options.filter(o => o.active !== false).forEach(opt => {
                    const isSelected = posSimSelections.options.has(opt.id);
                    const optLabel = document.createElement('label');
                    optLabel.className = 'channel-chip-check';
                    optLabel.innerHTML = `
                        <input type="${isSingle ? 'radio' : 'checkbox'}" name="sim_group_${group.id}" ${isSelected ? 'checked' : ''} />
                        <span style="flex:1;">${opt.name}</span>
                        <strong>${opt.price > 0 ? '+ ' + money(opt.price) : 'Incluido'}</strong>
                    `;
                    optLabel.querySelector('input').onchange = e => {
                        if (isSingle) {
                            group.options.forEach(o => posSimSelections.options.delete(o.id));
                            if (e.target.checked) posSimSelections.options.add(opt.id);
                        } else {
                            if (e.target.checked) posSimSelections.options.add(opt.id);
                            else posSimSelections.options.delete(opt.id);
                        }
                        calculatePosModalTotal();
                    };
                    gList.appendChild(optLabel);
                });
                posModalBody.appendChild(gBox);
            });
        }

        calculatePosModalTotal();
    }

    function calculatePosModalTotal() {
        const item = posSimulatedProduct;
        const p = productById.get(item.productId);
        const basePrice = item.customPrice ?? (p?.basePrice || 0);

        let extrasTotal = 0;
        item.groups?.forEach(g => {
            g.options?.forEach(opt => {
                if (posSimSelections.options.has(opt.id)) {
                    extrasTotal += opt.price || 0;
                }
            });
        });

        const singleTotal = basePrice + extrasTotal;
        const total = singleTotal * posSimSelections.qty;
        posModalTotal.textContent = money(total);
    }

    // ==========================================
    // 6. GESTIÓN GLOBAL: TABS 2, 3, 4
    // ==========================================
    function renderGlobalGroups() {
        const target = document.getElementById('global-groups-container');
        target.innerHTML = '';
        // Collect all distinct groups
        const allGroups = [];
        config.menu.forEach(m => {
            m.groups?.forEach(g => {
                if (!allGroups.some(x => x.name === g.name)) allGroups.push(g);
            });
        });

        if (!allGroups.length) {
            target.innerHTML = '<p class="restaurant-empty">No hay grupos de opciones configurados todavía. Pulsa "+ Crear grupo".</p>';
            return;
        }

        allGroups.forEach(g => {
            const card = document.createElement('div');
            card.className = 'restaurant-config-card';
            card.innerHTML = `
                <div class="restaurant-panel-heading">
                    <h3>${g.name}</h3>
                    <span class="group-card-badge">${g.options?.length || 0} opciones</span>
                </div>
                <p class="restaurant-muted">${g.maximum === 1 ? 'Selección única' : 'Selección múltiple'} · Mínimo: ${g.minimum}, Máximo: ${g.maximum}</p>
            `;
            target.appendChild(card);
        });
    }

    document.getElementById('btn-global-add-group').addEventListener('click', () => {
        const name = prompt('Nombre del nuevo grupo global (ej. Tamaño de Pizza, Salsas):');
        if (!name || !name.trim()) return;
        // Apply to all items in current section
        config.menu.filter(m => m.sectionId === selectedSectionId).forEach(item => {
            item.groups ||= [];
            if (!item.groups.some(g => g.name.toLowerCase() === name.trim().toLowerCase())) {
                item.groups.push({
                    id: uid(),
                    name: name.trim(),
                    minimum: 0,
                    maximum: 1,
                    options: []
                });
            }
        });
        markDirty();
        renderGlobalGroups();
        alert(`Grupo "${name.trim()}" creado y disponible en los productos.`);
    });

    function renderGlobalStations() {
        const target = document.getElementById('global-stations-container');
        target.innerHTML = '';
        if (!config.stations.length) {
            target.innerHTML = '<p class="restaurant-empty">No hay estaciones configuradas. Pulsa "+ Crear estación".</p>';
            return;
        }

        config.stations.forEach(st => {
            const card = document.createElement('div');
            card.className = 'restaurant-config-card';
            card.innerHTML = `
                <div class="restaurant-panel-heading">
                    <h3>${st.name}</h3>
                    <label class="switch-control">
                        <input type="checkbox" class="st-switch" ${st.active ? 'checked' : ''} />
                        <span class="switch-slider"></span>
                    </label>
                </div>
                <p class="restaurant-muted">${st.active ? 'Estación activa y recibiendo comandas' : 'Estación temporalmente deshabilitada'}</p>
            `;
            card.querySelector('.st-switch').onchange = e => {
                st.active = e.target.checked;
                markDirty();
            };
            target.appendChild(card);
        });
    }

    document.getElementById('btn-global-add-station').addEventListener('click', () => {
        const name = prompt('Nombre de la estación (ej. Cocina Caliente, Horno Pizzas, Barra / Bebidas):');
        if (!name || !name.trim()) return;
        config.stations.push({
            id: uid(),
            name: name.trim(),
            active: true
        });
        markDirty();
        renderGlobalStations();
    });

    function renderGlobalChannelsAndTables() {
        // Canales
        const chTarget = document.getElementById('config-channels');
        chTarget.innerHTML = '';
        config.channels.forEach(ch => {
            const div = document.createElement('div');
            div.style.cssText = 'display:flex; justify-content:space-between; align-items:center; padding:8px 0; border-bottom:1px solid var(--border);';
            div.innerHTML = `
                <span>${ch.name}</span>
                <label class="switch-control">
                    <input type="checkbox" ${ch.active ? 'checked' : ''} />
                    <span class="switch-slider"></span>
                </label>
            `;
            div.querySelector('input').onchange = e => { ch.active = e.target.checked; markDirty(); };
            chTarget.appendChild(div);
        });

        // Mesas
        const tblTarget = document.getElementById('config-tables');
        tblTarget.innerHTML = `<p class="restaurant-muted">${config.tables.length} mesas configuradas en ${new Set(config.tables.map(t => t.area)).size} áreas.</p>`;

        // Zonas
        const znTarget = document.getElementById('config-zones');
        znTarget.innerHTML = '';
        config.zones.forEach(z => {
            const div = document.createElement('div');
            div.style.cssText = 'display:flex; justify-content:space-between; align-items:center; padding:8px 0; border-bottom:1px solid var(--border);';
            div.innerHTML = `<span>${z.name}</span><strong>${money(z.fee)}</strong>`;
            znTarget.appendChild(div);
        });

        // Motoristas
        const drTarget = document.getElementById('config-drivers');
        drTarget.innerHTML = `<p class="restaurant-muted">${config.drivers.filter(d => d.active).length} motoristas activos disponibles.</p>`;
    }

    // ==========================================
    // 7. COPIAR MENÚ DESDE OTRA SUCURSAL
    // ==========================================
    const copyDialog = document.getElementById('restaurant-copy-branch-dialog');
    const copySelect = document.getElementById('copy-source-branch-select');
    const copyPreviewBox = document.getElementById('copy-preview-summary');
    const copyBtnConfirm = document.getElementById('copy-dialog-confirm');
    const copyBtnClose = document.getElementById('copy-dialog-close');
    const copyBtnCancel = document.getElementById('copy-dialog-cancel');
    let fetchedClonedDefinition = null;

    document.getElementById('btn-open-copy-branch').addEventListener('click', () => {
        copySelect.value = '';
        copyPreviewBox.hidden = true;
        copyBtnConfirm.disabled = true;
        fetchedClonedDefinition = null;
        copyDialog.showModal();
    });

    copyBtnClose.addEventListener('click', () => copyDialog.close());
    copyBtnCancel.addEventListener('click', () => copyDialog.close());

    copySelect.addEventListener('change', async () => {
        const sourceId = copySelect.value;
        if (!sourceId) {
            copyPreviewBox.hidden = true;
            copyBtnConfirm.disabled = true;
            return;
        }

        try {
            copyBtnConfirm.disabled = true;
            const token = form.querySelector('input[name=__RequestVerificationToken]').value;
            const res = await fetch('/restaurante/copiar-menu', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
                body: JSON.stringify({ sourceBranchId: sourceId })
            });
            const data = await res.json();
            if (!res.ok) throw new Error(data.error || 'No se pudo obtener el menú de la sucursal seleccionada.');

            fetchedClonedDefinition = data.definition;
            document.getElementById('copy-stat-sections').textContent = fetchedClonedDefinition.sections?.length || 0;
            document.getElementById('copy-stat-products').textContent = fetchedClonedDefinition.menu?.length || 0;
            document.getElementById('copy-stat-stations').textContent = fetchedClonedDefinition.stations?.length || 0;
            document.getElementById('copy-conflict-warning').textContent = 'Se creará un borrador en memoria. Deberás pulsar "Guardar configuración" para publicar los cambios.';
            copyPreviewBox.hidden = false;
            copyBtnConfirm.disabled = false;
        } catch (err) {
            alert(err.message);
            copyPreviewBox.hidden = true;
        }
    });

    copyBtnConfirm.addEventListener('click', () => {
        if (!fetchedClonedDefinition) return;
        config = fetchedClonedDefinition;
        selectedSectionId = config.sections[0]?.id || null;
        selectedProductId = null;
        markDirty();
        renderSections();
        renderProductsList();
        copyDialog.close();
        alert('Menú importado con éxito como borrador. Revisa las secciones y guarda la configuración.');
    });

    // ==========================================
    // 8. GUARDADO GLOBAL Y PROTECCIÓN DE CAMBIOS
    // ==========================================
    document.getElementById('btn-discard-changes').addEventListener('click', async () => {
        if (!dirty) return;
        const ok = await confirmAction({
            title: 'Descartar cambios',
            description: 'Se perderán todas las modificaciones no guardadas y se recargarán los valores de la sucursal.',
            danger: true,
            confirm: 'Descartar'
        });
        if (ok) location.reload();
    });

    form.addEventListener('submit', async e => {
        e.preventDefault();
        const submitBtn = document.getElementById('btn-save-configuration');
        submitBtn.disabled = true;
        msgEl.textContent = 'Validando y guardando configuración…';
        msgEl.style.color = 'var(--info)';

        try {
            const token = form.querySelector('input[name=__RequestVerificationToken]').value;
            const revision = Number(form.dataset.revision) || 0;

            const res = await fetch('/restaurante/configuracion', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
                body: JSON.stringify({ revision, definition: config })
            });

            const result = await res.json();
            if (!res.ok) {
                throw new Error(result.error || 'Error al guardar la configuración.');
            }

            markClean();
            msgEl.textContent = '✓ Configuración guardada correctamente.';
            msgEl.style.color = 'var(--success)';
            setTimeout(() => location.reload(), 800);
        } catch (err) {
            msgEl.textContent = err.message;
            msgEl.style.color = 'var(--danger)';
        } finally {
            submitBtn.disabled = false;
        }
    });

    window.addEventListener('beforeunload', e => {
        if (dirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    });

    // Initial render
    renderSections();
    renderProductsList();
})();
