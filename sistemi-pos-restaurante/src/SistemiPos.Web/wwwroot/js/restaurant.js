(() => {
    'use strict';
    const { money, normalize, confirmAction } = window.restaurantUI;
    const pending = new Map();
    const uuid = () => crypto.randomUUID();
    async function post(url, data, scope) {
        const message = scope.querySelector('.restaurant-message');
        if (message) message.textContent = 'Guardando…';
        const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(url + JSON.stringify(data)));
        const key = 'restaurant-operation-' + [...new Uint8Array(digest)].map(x => x.toString(16).padStart(2, '0')).join('');
        if (!pending.has(key)) { const operation = sessionStorage.getItem(key) || uuid(); pending.set(key, operation); sessionStorage.setItem(key, operation); }
        const buttons = [...scope.querySelectorAll('button')]; buttons.forEach(b => b.disabled = true);
        try {
            const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json',
                RequestVerificationToken: document.querySelector('input[name="__RequestVerificationToken"]').value },
                body: JSON.stringify({ ...data, operationId: pending.get(key) }) });
            if (response.redirected) throw new Error('La sesión venció. Inicia sesión para continuar.');
            const result = await response.json();
            if (!response.ok) throw new Error(result.error || 'No fue posible guardar. Revisa permisos y datos.');
            pending.delete(key); sessionStorage.removeItem(key);
            if (scope.classList.contains('kitchen-card')) location.reload(); else location.href = result.url || location.href;
        } catch (error) { if (message) message.textContent = error.message; }
        finally { buttons.forEach(b => b.disabled = false); }
    }
    document.querySelectorAll('[data-action]').forEach(button => button.addEventListener('click', async () => {
        const scope = button.closest('[data-order]');
        const data = { revision: Number(scope.dataset.revision), action: button.dataset.action, lineId: button.dataset.line || null };
        const descriptions = {
            submit: 'Se enviará la comanda a cocina y se reservará el inventario necesario.',
            start: 'Indica la cantidad que comenzarás a preparar. Se consumirá su inventario reservado.',
            ready: 'Confirma que esta preparación está lista para entregar.',
            deliver: 'Confirma que el cliente recibió el pedido. Esta acción no registra un pago.',
            cancel: button.dataset.returnIntact ? 'Registra el motivo y la cantidad que regresa físicamente intacta al inventario.' : 'Las cantidades pendientes liberan su reserva. Las cantidades preparadas se registran como merma.',
            assign: 'Se asignará el motorista seleccionado a este pedido.',
            depart: 'Confirma que el motorista sale con el pedido.',
            incident: 'Describe lo ocurrido durante el reparto. El pedido seguirá en ruta hasta confirmar su entrega.',
            'move-table': 'Se moverá el pedido a la mesa seleccionada. Escribe el motivo.',
            'close-table': 'Se liberará el turno si todos sus pedidos están entregados o cancelados. Esta acción no confirma el pago.'
        };
        const confirmation = await confirmAction({ title: button.textContent.trim(), description: descriptions[data.action] || 'Confirma la operación.',
            quantity: button.dataset.quantity, reason: !!button.dataset.reason, danger: data.action === 'cancel' });
        if (!confirmation) return;
        Object.assign(data, confirmation);
        if (button.dataset.returnIntact) data.returnIntact = true;
        if (data.action === 'assign') { data.targetId = document.getElementById('restaurant-driver')?.value; data.route = document.getElementById('restaurant-route')?.value; }
        if (data.action === 'move-table') data.targetId = document.getElementById('restaurant-table')?.value;
        await post(`/restaurante/${scope.dataset.order}/accion`, data, scope);
    }));
    document.getElementById('restaurant-refresh-draft')?.addEventListener('click', () => {
        const scope = document.getElementById('restaurant-actions');
        post('/restaurante/borrador', { orderId: scope.dataset.order, revision: Number(scope.dataset.revision), order: JSON.parse(document.getElementById('restaurant-draft-data').textContent) }, scope);
    });
    const filter = () => {
        const q = normalize(document.getElementById('restaurant-filter')?.value || '');
        const status = document.getElementById('restaurant-status')?.value;
        const orderChannel = document.getElementById('restaurant-channel')?.value;
        const service = document.getElementById('restaurant-service')?.value;
        let visibleOrders = 0;
        document.querySelectorAll('[data-order-row]').forEach(row => {
            row.hidden = !normalize(row.textContent).includes(q) || !!status && row.dataset.status !== status || !!orderChannel && row.dataset.channel !== orderChannel || !!service && row.dataset.service !== service;
            if (!row.hidden) visibleOrders++;
        });
        const resultCount = document.getElementById('restaurant-result-count');
        if (resultCount) resultCount.textContent = visibleOrders + ' pedidos';
        const empty = document.getElementById('restaurant-no-results');
        if (empty) empty.hidden = visibleOrders > 0;
        const station = document.getElementById('kitchen-station-filter')?.value;
        const channel = document.getElementById('kitchen-channel-filter')?.value;
        document.querySelectorAll('.kitchen-card').forEach(card => card.hidden = !!station && card.dataset.station !== station || !!channel && card.dataset.channel !== channel);
        document.querySelectorAll('[data-lane]').forEach(lane => {
            const count = lane.querySelectorAll('.kitchen-card:not([hidden])').length;
            lane.querySelector('[data-lane-count]').textContent = count;
            lane.querySelector('[data-lane-empty]').hidden = count > 0;
        });
    };
    ['restaurant-filter', 'restaurant-status', 'restaurant-channel', 'restaurant-service', 'kitchen-station-filter', 'kitchen-channel-filter'].forEach(id => document.getElementById(id)?.addEventListener('input', filter));
    document.getElementById('restaurant-clear-filters')?.addEventListener('click', () => {
        ['restaurant-filter', 'restaurant-status', 'restaurant-channel', 'restaurant-service'].forEach(id => document.getElementById(id).value = '');
        filter();
    });
    filter();
    if (document.getElementById('kitchen-station-filter')) {
        ['kitchen-station-filter','kitchen-channel-filter'].forEach(id => { const input=document.getElementById(id); input.value=sessionStorage.getItem(id)||''; input.addEventListener('change',()=>sessionStorage.setItem(id,input.value)); });
        filter();
        setInterval(()=>{ if(!document.hidden && !document.querySelector('dialog[open]') && !document.querySelector('.kitchen-card button:disabled') && !document.querySelector('.restaurant-message:not(:empty)')) location.reload(); },15000);
    }

    const kdsApp = document.getElementById('kds-app');
    if (kdsApp) {
        document.body.classList.add('kds-mode');

        // Live Clock
        const clockEl = document.getElementById('kds-clock');
        if (clockEl) {
            const updateClock = () => {
                const now = new Date();
                clockEl.textContent = now.toTimeString().split(' ')[0];
            };
            updateClock();
            setInterval(updateClock, 1000);
        }

        // Sidebar navigation toggle button
        document.getElementById('kds-menu-toggle')?.addEventListener('click', () => {
            const toggle = document.querySelector('[data-sidebar-toggle]');
            if (toggle) {
                toggle.click();
            } else {
                document.getElementById('app-sidebar')?.classList.toggle('is-open');
            }
        });

        // Station Pills Filtering
        const stationPills = document.querySelectorAll('.kds-station-pill');
        const countBadge = document.getElementById('kds-visible-count');
        const kdsCards = document.querySelectorAll('.kds-card');
        const placeholder = document.getElementById('kds-card-placeholder');
        const noTickets = document.getElementById('kds-no-tickets');

        function applyStationFilter(selectedStation) {
            let visible = 0;
            kdsCards.forEach(card => {
                const match = !selectedStation || card.dataset.station === selectedStation;
                card.hidden = !match;
                if (match) visible++;
            });
            if (countBadge) countBadge.textContent = visible;
            if (noTickets) noTickets.hidden = visible > 0;
            if (placeholder) placeholder.hidden = visible === 0;
        }

        const savedStation = sessionStorage.getItem('kds_selected_station');
        if (savedStation !== null) {
            stationPills.forEach(p => {
                const isMatch = p.dataset.stationId === savedStation;
                p.classList.toggle('is-active', isMatch);
                p.setAttribute('aria-selected', isMatch);
            });
            applyStationFilter(savedStation);
        } else {
            const firstActive = document.querySelector('.kds-station-pill.is-active');
            if (firstActive) applyStationFilter(firstActive.dataset.stationId);
        }

        stationPills.forEach(pill => {
            pill.addEventListener('click', () => {
                stationPills.forEach(p => { p.classList.remove('is-active'); p.setAttribute('aria-selected', 'false'); });
                pill.classList.add('is-active');
                pill.setAttribute('aria-selected', 'true');
                const stId = pill.dataset.stationId;
                sessionStorage.setItem('kds_selected_station', stId);
                applyStationFilter(stId);
            });
        });

        // View Mode Switcher: Cards vs List
        const gridContainer = document.getElementById('kds-grid-container');
        const btnViewList = document.getElementById('kds-view-list');
        const btnViewGrid = document.getElementById('kds-view-grid');

        function setViewMode(mode) {
            const isList = mode === 'list';
            gridContainer?.classList.toggle('is-list-view', isList);
            btnViewList?.classList.toggle('is-active', isList);
            btnViewGrid?.classList.toggle('is-active', !isList);
            sessionStorage.setItem('kds_view_mode', mode);
        }

        const savedView = sessionStorage.getItem('kds_view_mode') || 'grid';
        setViewMode(savedView);

        btnViewList?.addEventListener('click', () => setViewMode('list'));
        btnViewGrid?.addEventListener('click', () => setViewMode('grid'));

        // Item checkboxes persistence
        document.querySelectorAll('.kds-item-chk-input').forEach(chk => {
            const lineId = chk.dataset.lineChk;
            const block = chk.closest('.kds-item-block');
            const key = 'kds_item_chk_' + lineId;
            if (sessionStorage.getItem(key) === '1') {
                chk.checked = true;
                block?.classList.add('is-checked');
            }
            chk.addEventListener('change', () => {
                block?.classList.toggle('is-checked', chk.checked);
                if (chk.checked) sessionStorage.setItem(key, '1');
                else sessionStorage.removeItem(key);
            });
        });

        // Notification button
        document.querySelectorAll('[data-kds-notify]').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const orderNum = btn.dataset.orderNumber || 'el pedido';
                const waiter = btn.dataset.waiter ? ` (${btn.dataset.waiter})` : '';
                const msg = `🔔 Notificación enviada al mesero/repartidor${waiter} para ${orderNum}.`;

                const toast = document.createElement('div');
                toast.className = 'kds-toast-notification';
                toast.textContent = msg;
                document.body.appendChild(toast);
                requestAnimationFrame(() => toast.classList.add('is-visible'));
                setTimeout(() => {
                    toast.classList.remove('is-visible');
                    setTimeout(() => toast.remove(), 300);
                }, 3500);

                btn.style.borderColor = '#16a34a';
                btn.style.color = '#16a34a';
                setTimeout(() => { btn.style.borderColor = ''; btn.style.color = ''; }, 1500);
            });
        });

        // Historial dialog
        const historyModal = document.getElementById('kds-history-modal');
        document.getElementById('kds-btn-history')?.addEventListener('click', () => historyModal?.showModal());
        document.getElementById('kds-history-close')?.addEventListener('click', () => historyModal?.close());

        // Dividir Comanda dialog
        const splitModal = document.getElementById('kds-split-modal');
        const splitForm = document.getElementById('kds-split-form');
        const splitInputQty = document.getElementById('kds-split-input-qty');
        const splitHelp = document.getElementById('kds-split-help');
        let currentSplitData = null;

        document.querySelectorAll('[data-kds-split]').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.stopPropagation();
                const scope = btn.closest('[data-order]');
                currentSplitData = {
                    orderId: btn.dataset.orderId || scope?.dataset.order,
                    revision: Number(scope?.dataset.revision || 1),
                    lineId: btn.dataset.lineId,
                    maxQty: Number(btn.dataset.maxQty || 1),
                    orderNumber: btn.dataset.orderNumber || '',
                    scope: scope
                };
                if (splitInputQty) {
                    splitInputQty.max = currentSplitData.maxQty;
                    splitInputQty.value = Math.max(1, Math.floor(currentSplitData.maxQty / 2));
                }
                if (splitHelp) {
                    splitHelp.textContent = `Pedido ${currentSplitData.orderNumber} · Cantidad actual en línea: ${currentSplitData.maxQty}`;
                }
                splitModal?.showModal();
            });
        });
        document.getElementById('kds-split-close')?.addEventListener('click', () => splitModal?.close());
        document.getElementById('kds-split-cancel')?.addEventListener('click', () => splitModal?.close());

        splitForm?.addEventListener('submit', async (e) => {
            e.preventDefault();
            if (!currentSplitData) return;
            const qty = Number(splitInputQty.value);
            splitModal?.close();
            const data = {
                revision: currentSplitData.revision,
                action: 'start',
                lineId: currentSplitData.lineId,
                quantity: qty,
                reason: `División de preparación en cocina: iniciando ${qty} unidad(es).`
            };
            await post(`/restaurante/${currentSplitData.orderId}/accion`, data, currentSplitData.scope);
        });

        // Auto reload every 15s when idle
        setInterval(() => {
            if (!document.hidden && !document.querySelector('dialog[open]') && !document.querySelector('.kds-card button:disabled:not(.is-done)') && !document.querySelector('.restaurant-message:not(:empty)')) {
                location.reload();
            }
        }, 15000);
    }
    const form = document.getElementById('restaurant-order-form'); if (!form) return;
    const data = JSON.parse(document.getElementById('restaurant-menu-data').textContent);
    const cart = []; const combos = [];
    const edit = document.getElementById('restaurant-edit-data');
    if (edit) {
        const original = JSON.parse(edit.textContent, (key, value) => value && typeof value === 'object' && !Array.isArray(value) ? Object.fromEntries(Object.entries(value).map(([k,v]) => [k[0].toLowerCase() + k.slice(1),v])) : value);
        cart.push(...original.items); combos.push(...original.combos);
        Object.entries(original).forEach(([name,value]) => { const input = form.elements.namedItem(name); if(input) input.value = value ?? ''; });
    }
    const el = (tag, text, className) => { const node = document.createElement(tag); if (text) node.textContent = text; if (className) node.className = className; return node; };
    const serviceInput = form.elements.namedItem('serviceType');
    function updateServiceFields() {
        if (!serviceInput) return;
        form.querySelectorAll('[data-service-field]').forEach(field => {
            const show = field.dataset.serviceField === serviceInput.value;
            field.hidden = !show;
            field.querySelectorAll('input,select').forEach(input => { input.disabled = !show; input.required = show; });
        });
        form.elements.namedItem('phone').required = serviceInput.value === '3';
    }
    serviceInput?.addEventListener('change', updateServiceFields);
    updateServiceFields();
    function drawOptions(entry, product, row) {
        entry.optionIds ||= []; entry.excludedProducts ||= [];
        product.configuration.groups.forEach(group => {
            row.append(el('p', `${group.name} (seleccionar ${group.minimum}–${group.maximum})`));
            group.options.filter(o => o.active).forEach(option => {
                const label = el('label'); const input = el('input'); input.type = 'checkbox'; input.checked = entry.optionIds.includes(option.id);
                input.addEventListener('change', () => entry.optionIds = input.checked ? [...entry.optionIds, option.id] : entry.optionIds.filter(x => x !== option.id));
                label.append(input, document.createTextNode(` ${option.name} +${money(option.price)}`)); row.append(label);
            });
        });
        (product.configuration.excludableProducts || []).forEach(id => {
            const label = el('label'); const input = el('input'); input.type = 'checkbox'; input.checked = entry.excludedProducts.includes(id);
            input.addEventListener('change', () => entry.excludedProducts = input.checked ? [...entry.excludedProducts, id] : entry.excludedProducts.filter(x => x !== id));
            label.append(input, document.createTextNode(` Sin ${data.components?.find(p => p.id === id)?.name || id}`)); row.append(label);
        });
    }
    function drawCart() {
        const target = document.getElementById('restaurant-cart'); target.replaceChildren();
        const count = document.getElementById('restaurant-cart-count');
        if (count) count.textContent = cart.length + combos.length;
        if (!cart.length && !combos.length) {
            const empty = el('div', '', 'restaurant-empty');
            empty.append(el('strong', 'Tu pedido está vacío'), el('p', 'Agrega productos del menú. Aquí podrás elegir cantidades, extras y exclusiones.'));
            target.append(empty);
        }
        cart.forEach((entry, index) => {
            const product = data.menu.find(x => x.id === entry.productId); const row = el('div', '', 'restaurant-cart-line');
            row.append(el('strong', product?.name || 'Producto retirado del menú: quítalo para continuar'));
            const qty = el('input', '', 'input'); qty.type = 'number'; qty.min = '.01'; qty.step = '.01'; qty.value = entry.quantity;
            qty.required = true; qty.setAttribute('aria-label', 'Cantidad de ' + (product?.name || 'producto')); qty.addEventListener('change', () => entry.quantity = Number(qty.value)); row.append(qty);
            if (product) drawOptions(entry, product, row);
            const remove = el('button', 'Quitar', 'button-secondary'); remove.type = 'button'; remove.onclick = () => { cart.splice(index, 1); drawCart(); }; row.append(remove); target.append(row);
        });
        combos.forEach((entry, index) => {
            const combo = data.combos.find(c => c.id === entry.comboId); const row = el('div', '', 'restaurant-cart-line'); row.append(el('strong', combo ? `${combo.name} · ${money(combo.price)} + extras seleccionados` : 'Combo retirado: quítalo para continuar'));
            let offset = 0;
            combo?.groups.forEach(group => { for (let i = 0; i < group.quantity; i++) {
                const pickIndex = offset++; const select = el('select', '', 'input'); const label = el('label', group.name);
                group.productIds.forEach(id => { const p = data.menu.find(p => p.id === id); if (p) { const option = el('option', p.name); option.value = id; select.append(option); } });
                if (entry.picks[pickIndex]) select.value = entry.picks[pickIndex].productId;
                entry.picks[pickIndex] ||= { groupId: group.id, productId: select.value, optionIds: [], excludedProducts: [] };
                select.addEventListener('change', () => { entry.picks[pickIndex] = { groupId: group.id, productId: select.value, optionIds: [], excludedProducts: [] }; drawCart(); }); label.append(select); row.append(label);
                const product = data.menu.find(p => p.id === entry.picks[pickIndex].productId);
                if (product) drawOptions(entry.picks[pickIndex], product, row);
            } });
            const remove = el('button', 'Quitar combo', 'button-secondary'); remove.type = 'button'; remove.onclick = () => { combos.splice(index, 1); drawCart(); }; row.append(remove); target.append(row);
        });
    }
    function drawMenu() {
        const target = document.getElementById('restaurant-menu'); target.replaceChildren();
        const q = normalize(document.getElementById('restaurant-menu-search')?.value || '');
        const category = document.getElementById('restaurant-menu-category')?.value;
        const favorites = document.getElementById('restaurant-menu-favorites')?.checked;
        const matches = data.menu.filter(p => normalize(`${p.name} ${p.code} ${p.category}`).includes(q) && (!category || p.category === category) && (!favorites || p.favorite));
        let previousSection = null;
        matches.forEach(product => {
            if (product.category !== previousSection) {
                previousSection = product.category;
                target.append(el('h3', previousSection, 'restaurant-menu-section'));
            }
            const card = el('article', '', 'restaurant-product');
            const media = el('div', product.name.slice(0, 2).toLocaleUpperCase(), 'restaurant-product-media'); media.setAttribute('aria-hidden', 'true');
            if (product.imageUrl && (/^(\/(?!\/)|https:\/\/)/).test(product.imageUrl)) {
                const img = el('img'); img.src = product.imageUrl; img.alt = ''; img.loading = 'lazy';
                img.addEventListener('error', () => media.textContent = product.name.slice(0, 2).toLocaleUpperCase());
                media.replaceChildren(img);
            }
            const body = el('div', '', 'restaurant-product-body');
            body.append(el('p', product.category), el('strong', `${product.favorite ? '★ ' : ''}${product.name}`), el('span', money(product.price), 'restaurant-price'), el('p', `Disponible base: ${product.available}`));
            const button = el('button', '+ Agregar', 'button-secondary'); button.type = 'button'; button.setAttribute('aria-label', 'Agregar ' + product.name);
            button.onclick = () => { cart.push({ productId: product.id, quantity: 1, optionIds: [], excludedProducts: [] }); drawCart(); };
            body.append(button); card.append(media, body); target.append(card);
        });
        if (!matches.length) { const empty = el('div', '', 'restaurant-empty'); empty.append(el('strong', 'No hay productos para mostrar'), el('p', data.menu.length ? 'Cambia la búsqueda o los filtros.' : 'Revisa el menú, las estaciones y los productos habilitados en esta sucursal.')); target.append(empty); }
    }
    ['restaurant-menu-search','restaurant-menu-category','restaurant-menu-favorites'].forEach(id => document.getElementById(id)?.addEventListener('input', drawMenu)); drawMenu(); drawCart();
    form.elements.namedItem('warehouseId')?.addEventListener('change', async event => {
        try { const response = await fetch(`/restaurante/menu?warehouseId=${encodeURIComponent(event.target.value)}`); if(!response.ok) throw new Error(); const updated = await response.json(); data.menu = updated.menu; drawMenu(); }
        catch { form.querySelector('.restaurant-message').textContent = 'No se pudo actualizar disponibilidad. Revisa la conexión.'; }
    });
    const comboTarget = document.getElementById('restaurant-combos');
    data.combos.filter(c => c.active && new Date(c.fromUtc) <= new Date() && new Date(c.toUtc) > new Date()).forEach(combo => {
        const button = el('button', `${combo.name} · ${money(combo.price)}`, 'button-secondary'); button.type = 'button'; button.onclick = () => { combos.push({ comboId: combo.id, picks: [] }); drawCart(); }; comboTarget.append(button);
    });
    if (!comboTarget.children.length) comboTarget.append(el('p', 'No hay combos vigentes.', 'restaurant-muted'));
    form.addEventListener('submit', event => {
        event.preventDefault(); if (!form.reportValidity()) return;
        if (!cart.length && !combos.length) { form.querySelector('.restaurant-message').textContent = 'Agrega al menos un producto.'; return; }
        if (form.dataset.revision) { post(form.dataset.endpoint, { revision: Number(form.dataset.revision), items: cart, combos }, form); return; }
        const value = name => { const input = form.elements.namedItem(name); return input.disabled ? '' : input.value; };
        post(form.dataset.endpoint, { orderId: form.dataset.draftId || null, revision: Number(form.dataset.draftRevision || 0), order: {
            warehouseId: value('warehouseId'), channelId: value('channelId'), serviceType: Number(value('serviceType')),
            tableId: value('tableId') || null, externalReference: value('externalReference') || null,
            customerName: value('customerName'), phone: value('phone'), address: value('address'), zoneId: value('zoneId') || null,
            notes: value('notes'), items: cart, combos
        } }, form);
    });
})();
