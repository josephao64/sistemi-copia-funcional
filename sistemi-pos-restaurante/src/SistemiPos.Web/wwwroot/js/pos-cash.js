(() => {
    const start = () => {
        const pos = document.querySelector('[data-pos][data-cash-state-url]');
        if (!pos) return;
        const one = selector => pos.querySelector(selector);
        const all = selector => Array.from(pos.querySelectorAll(selector));
        const terminal = one('[data-pos-terminal]');
        const hasTerminals = Boolean(terminal && Array.from(terminal.options).some(option => option.value));
        const unmatchedRegister = !hasTerminals ? pos.dataset.currentRegisterCode : '';
        const terminalHelp = hasTerminals ? 'Selecciona una terminal para consultar o abrir caja.' : unmatchedRegister
            ? `Tu caja actual (${unmatchedRegister}) no tiene una terminal compatible. Consulta y cierra ese turno; después vuelve a Ventas para seleccionar una terminal y abrir su caja con tu usuario.`
            : 'No hay puestos de venta disponibles. Administración debe configurar la terminal, su caja y su bodega en esta sucursal.';
        const token = one('[name="__RequestVerificationToken"]')?.value;
        let state = null, anchor = null, sequence = 0, busy = false, checkoutValidation = null, lastClockTick = performance.now();
        const revisions = new Map();
        const text = (selector, value) => { const element = one(selector); if (element) element.textContent = value; };
        const show = (selector, visible) => all(selector).forEach(element => element.hidden = !visible);
        const showSalesWorkspace = visible => {
            pos.classList.toggle('is-cash-open', visible);
            show('[data-pos-sale-heading]', visible);
            show('[data-pos-sale-panel]', visible);
            show('[data-cash-context]', visible);
            show('[data-pos-open-intro]', !visible);
            show('[data-pos-opening-message]', !visible);
            one('[data-pos-opening-stage]')?.classList.toggle('is-open', visible);
            if (!visible) {
                const checkout = one('[data-checkout-dialog]');
                if (checkout?.open) checkout.close();
            }
        };
        const money = (amount, currency = state?.currency) => {
            const c = currency;
            if (amount == null || !c) return '—';
            const parts = Number(amount).toFixed(c.decimals).split('.');
            const n = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, c.thousandsSeparator)
                + (c.decimals ? c.decimalSeparator + parts[1] : '');
            return c.symbolPosition === 'After' ? `${n} ${c.symbol}` : `${c.symbol} ${n}`;
        };
        const date = instant => instant && state ? new Intl.DateTimeFormat('es-GT', {
            timeZone: state.timezone, year: 'numeric', month: 'short', day: '2-digit',
            weekday: 'short', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false
        }).format(new Date(instant)) : '—';
        const gate = (valid, message = '') => {
            pos.dataset.cashValidated = valid ? 'true' : 'false';
            text('[data-cash-warning]', message);
            text('[data-pos-opening-message]', message || 'Selecciona un puesto de venta y abre tu caja.');
            show('[data-cash-warning]', Boolean(message));
            text('[data-cash-payment-warning]', message);
            show('[data-cash-payment-warning]', !valid);
            pos.dispatchEvent(new CustomEvent('pos:cash-state'));
        };
        const drawClock = () => {
            if (!anchor || !state) return;
            const tick = performance.now();
            if (!document.hidden && tick - lastClockTick > 5000) { lastClockTick = tick; refresh(true); return; }
            lastClockTick = tick;
            const now = anchor.server + performance.now() - anchor.monotonic;
            text('[data-cash-clock]', date(now));
            if (state.session) {
                const end = state.session.closedAtUtc ? Date.parse(state.session.closedAtUtc) : now;
                const minutes = Math.max(0, Math.floor((end - Date.parse(state.session.openedAtUtc)) / 60000));
                const hours = Math.floor(minutes / 60);
                text('[data-cash-duration]', `${hours.toString().padStart(2, '0')} h ${(minutes % 60).toString().padStart(2, '0')} min${state.status === 'OPEN' ? ' abierto' : ' de turno'}`);
                if (state.status === 'OPEN' && state.longShiftHours != null && minutes >= state.longShiftHours * 60)
                    text('[data-cash-duration]', `${hours} h ${minutes % 60} min · turno prolongado`);
            }
        };
        const row = (label, value, parent) => {
            const line = document.createElement('div'); line.className = 'pos-cash-summary-row';
            const name = document.createElement('span'); name.textContent = label;
            const amount = document.createElement('strong'); amount.textContent = money(value);
            line.append(name, amount); parent.append(line);
        };
        const drawSummary = () => {
            const content = one('[data-cash-summary-content]');
            const movements = one('[data-cash-summary-movements]');
            content.replaceChildren(); movements.replaceChildren();
            const session = state?.session;
            const live = Boolean(state && anchor && navigator.onLine);
            text('[data-cash-menu-status]', !live ? 'Estado sin actualizar' : state.status === 'OPEN' ? '● Caja abierta' : state.status === 'CLOSED' ? '○ Caja cerrada' : unmatchedRegister ? 'Caja actual sin terminal compatible' : 'Selecciona una terminal');
            one('[data-cash-summary-dialog]').dataset.state = live ? state.status.toLowerCase() : 'checking';
            text('[data-cash-summary-context]', state ? `${state.branch} · ${state.terminal || 'Sin terminal seleccionada'}` : 'No se ha podido consultar la caja.');
            text('[data-cash-menu-owner]', session?.cashier || state?.currentUser || '—');
            text('[data-cash-menu-opened]', session ? date(session.openedAtUtc) : unmatchedRegister ? 'Consulta el turno de tu caja actual' : 'Sin apertura registrada');
            text('[data-cash-menu-fund]', session ? session.openingAmount == null ? 'Protegido' : money(session.openingAmount) : '—');
            text('[data-cash-menu-closed]', session?.closedAtUtc ? `${date(session.closedAtUtc)}${session.closedBy ? ' · ' + session.closedBy : ''}` : session ? 'Turno en curso' : '—');
            text('[data-cash-menu-help]', !live ? 'Conecta con el servidor para actualizar el estado y habilitar las operaciones de caja.'
                : state.status === 'NO_TERMINAL' ? terminalHelp
                : state.status === 'OPEN' ? state.permissions.canOperate ? 'La apertura ya está registrada. Puedes continuar vendiendo o consultar el arqueo y cerrar el turno desde este menú.' : 'Este turno no puede ser operado por tu usuario. Consulta con el responsable de caja.'
                : state.permissions.canOpen ? session ? 'El turno mostrado ya está cerrado. Abre una caja nueva con su propio fondo inicial para comenzar a cobrar.' : 'No hay un turno abierto. Registra el fondo inicial y confirma la apertura para comenzar a cobrar.'
                : 'No hay un turno abierto y tu usuario no tiene permiso para abrir caja. Solicita la apertura al responsable.');
            show('[data-cash-open]', Boolean(state?.permissions.canOpen));
            all('[data-cash-open], [data-cash-movement-open], [data-cash-close-open]').forEach(button => button.disabled = !live || busy);
            show('[data-cash-blind-message]', Boolean(session && !session.summary));
            text('[data-cash-blind-message]', state?.permissions.canClose ? 'Los totales están protegidos por tus permisos o por el conteo ciego. Puedes declarar el efectivo al cerrar.' : 'Los importes de este turno están protegidos por tus permisos.');
            show('[data-cash-movement-open]', Boolean(state?.permissions.canIncome || state?.permissions.canExpense));
            show('[data-cash-close-open]', Boolean(state?.permissions.canClose));
            show('[data-cash-details-link]', Boolean(session));
            if (session) one('[data-cash-details-link]').href = `/Cash/Details/${encodeURIComponent(session.id)}`;
            show('[data-cash-receipt-link]', session?.status === 'CLOSED' && state.permissions.canReceipt);
            if (session) one('[data-cash-receipt-link]').href = `/pos/caja/${encodeURIComponent(session.id)}/comprobante`;
            const visibleSummary = Boolean(live && state?.status === 'OPEN' && state.permissions.canSeeTotals && session?.summary);
            show('[data-cash-shift-summary]', visibleSummary);
            text('[data-cash-shift-sales]', visibleSummary ? money(session.summary.cashSales - session.summary.cashRefunds + session.summary.cardSales + session.summary.transferSales + session.summary.otherSales) : '—');
            text('[data-cash-shift-expected]', visibleSummary ? money(session.summary.expectedCash) : '—');
            if (!session?.summary) return;
            const s = session.summary;
            row('Fondo inicial', session.openingAmount, content);
            row('Cobros en efectivo (sin descontar devoluciones)', s.cashSales, content);
            row('Tarjeta / POS, neto de devoluciones', s.cardSales, content);
            row('Transferencia, neto de devoluciones', s.transferSales, content);
            row('Otros medios, neto de devoluciones', s.otherSales, content);
            row('Entradas manuales de efectivo', s.income, content);
            row('Salidas manuales de efectivo', s.expenses, content);
            row('Devoluciones en efectivo', s.cashRefunds, content);
            row('Otros egresos de efectivo', s.otherCashOut, content);
            row('Cobros de cuentas por cobrar', s.collections, content);
            row('Efectivo esperado', s.expectedCash, content);
            if (s.countedCash != null) { row('Efectivo contado', s.countedCash, content); row('Diferencia', s.difference, content); }
            const heading = document.createElement('h3'); heading.textContent = 'Movimientos registrados'; movements.append(heading);
            for (const item of s.movements) {
                const line = document.createElement('div'); line.className = 'pos-cash-movement-line';
                const copy = document.createElement('span'); copy.textContent = `${item.reason} · ${item.user} · ${date(item.atUtc)}${item.reference ? ' · ' + item.reference : ''}`;
                const value = document.createElement('strong'); value.textContent = money(item.amount);
                line.append(copy, value);
                if (item.canReverse && (item.type === 'Expense' ? state.permissions.canIncome : state.permissions.canExpense)) {
                    const button = document.createElement('button'); button.type = 'button'; button.className = 'button-secondary'; button.textContent = 'Reversar';
                    button.addEventListener('click', () => openMovement(item)); line.append(button);
                }
                movements.append(line);
            }
        };
        const accept = (next, sent, received) => {
            if (next.terminalId !== (terminal?.value || null)) return false;
            const previous = state;
            if (next.session && (revisions.get(next.session.id) ?? -1) > next.session.revision) return false;
            if (next.session) revisions.set(next.session.id, next.session.revision);
            state = next;
            anchor = { server: Date.parse(next.serverNow), monotonic: (sent + received) / 2 };
            lastClockTick = performance.now();
            pos.dataset.cashSessionId = next.session?.id || '';
            pos.dataset.cashTerminalId = next.terminalId || '';
            pos.dataset.currencySymbol = next.currency.symbol;
            pos.dataset.currencyDecimals = next.currency.decimals;
            pos.dataset.currencyPosition = next.currency.symbolPosition;
            pos.dataset.currencyDecimalSeparator = next.currency.decimalSeparator;
            pos.dataset.currencyThousandsSeparator = next.currency.thousandsSeparator;
            one('[name="CashSessionId"]').value = next.session?.status === 'OPEN' ? next.session.id : '';
            one('[data-cash-context]').dataset.state = next.status.toLowerCase();
            text('[data-cash-branch]', next.branch);
            text('[data-cash-terminal]', next.terminal || 'Sin seleccionar');
            text('[data-cash-status]', next.status === 'OPEN' ? '● Caja abierta' : next.status === 'CLOSED' ? '○ Caja cerrada' : hasTerminals ? '◷ Selecciona una terminal' : unmatchedRegister ? '◷ Caja sin terminal compatible' : '◷ Falta configurar terminal');
            text('[data-cash-cashier]', next.session?.cashier || (unmatchedRegister ? next.currentUser : 'Sin turno activo'));
            text('[data-cash-opened]', next.session ? date(next.session.openedAtUtc) : '—');
            if (!next.session) text('[data-cash-duration]', '—');
            text('[data-cash-sync]', `Sincronizado: ${date(next.serverNow)}`);
            text('[data-cash-opening-amount]', next.session ? next.session.openingAmount == null ? 'Protegido' : money(next.session.openingAmount) : '—');
            text('[data-cash-opened-label]', next.status === 'CLOSED' && next.session ? 'Última apertura' : 'Apertura');
            text('[data-cash-duration-label]', next.status === 'CLOSED' ? 'Duración del último turno' : 'Tiempo abierto');
            text('[data-cash-secondary-context]', `${next.branch} · ${next.session?.cashier || next.currentUser} · ${next.session?.openingAmount == null ? 'Fondo protegido' : 'Fondo: ' + money(next.session.openingAmount)} · Última sincronización: ${date(next.serverNow)}`);
            show('[data-cash-opening-amount]', true);
            show('[data-cash-open]', next.permissions.canOpen);
            show('[data-cash-summary-open]', true);
            all('[data-cash-symbol]').forEach(element => element.textContent = next.currency.symbol);
            all('[data-product-id]').forEach(card => { const price = card.querySelector('.pos-product-price'); if (price) price.textContent = money(card.dataset.productPrice, next.status === 'OPEN' ? next.currency : next.openingCurrency); });
            const valid = next.status === 'OPEN' && next.permissions.canOperate && navigator.onLine;
            showSalesWorkspace(next.status === 'OPEN' && next.permissions.canOperate);
            gate(valid, next.status === 'NO_TERMINAL' ? terminalHelp
                : next.status !== 'OPEN' ? 'Caja cerrada. Abre un turno antes de cobrar.'
                : !next.permissions.canOperate ? 'Esta caja no puede ser operada por tu usuario.' : '');
            if (previous?.permissions.canOperate && !valid) window.showSystemToast?.('warning', 'La caja dejó de estar disponible. Tu pedido se conserva.');
            drawClock(); drawSummary(); return valid;
        };
        const request = async (url, init = {}) => {
            let response;
            try { response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout?.(15000), ...init }); }
            catch { throw new Error('No se pudo conectar con el servidor. Revisa el estado antes de reintentar.'); }
            if (response.redirected) { const error = new Error('Vuelve a iniciar sesión para consultar la caja.'); error.status = 401; throw error; }
            const payload = await response.json().catch(() => ({}));
            if (!response.ok) { const error = new Error(payload.message || 'No se pudo consultar el servidor.'); error.status = response.status; error.code = payload.code; throw error; }
            return payload;
        };
        const refresh = async (invalidate = false) => {
            const id = ++sequence;
            const selected = terminal?.value || '';
            if (invalidate) { anchor = null; one('[data-cash-context]').dataset.state = 'checking'; gate(false, 'Verificando caja con el servidor…'); text('[data-cash-status]', '◷ Verificando caja'); drawSummary(); }
            if (!navigator.onLine) {
                one('[data-cash-context]').dataset.state = 'checking';
                anchor = null; gate(false, 'Sin conexión · datos sin actualizar. Se conserva el pedido.');
                text('[data-cash-status]', '◷ Sin conexión'); drawSummary(); return false;
            }
            const sent = performance.now();
            try {
                const payload = await request(`${pos.dataset.cashStateUrl}${selected ? '?terminalId=' + encodeURIComponent(selected) : ''}`);
                if (id !== sequence) return false;
                return accept(payload, sent, performance.now());
            } catch (error) {
                if (id === sequence) {
                    anchor = null; one('[data-cash-context]').dataset.state = 'checking';
                    if (error.status === 401 || error.status === 403) {
                        state = null; show('[data-cash-opening-amount]', false);
                        showSalesWorkspace(false);
                        all('.pos-cash-dialog[open]').forEach(dialog => dialog.close());
                        one('[data-cash-summary-content]').replaceChildren(); one('[data-cash-summary-movements]').replaceChildren();
                    }
                    gate(false, error.message + ' · datos sin actualizar'); text('[data-cash-status]', '◷ Estado sin validar');
                    drawSummary();
                }
                return false;
            }
        };
        const openDialog = dialog => { if (!dialog.open) dialog.showModal(); };
        const prepare = (dialog, needsPassword = false) => {
            one('[data-cash-summary-dialog]').close();
            dialog.querySelector('[data-cash-command-context]').textContent = `${state.branch} · ${state.terminal} · ${dialog.hasAttribute('data-cash-open-dialog') ? state.currentUser : state.session?.cashier || state.currentUser}`;
            dialog.querySelector('[data-cash-command-error]').textContent = '';
            const field = dialog.querySelector('[data-cash-own-password-field]');
            if (dialog.hasAttribute('data-cash-open-dialog')) dialog.querySelector('[data-cash-symbol]').textContent = state.openingCurrency.symbol;
            if (field) { field.hidden = !needsPassword; field.querySelector('input').required = needsPassword; }
            openDialog(dialog);
        };
        all('[data-cash-open]').forEach(button => button.addEventListener('click', async () => {
            if (busy) return;
            await refresh(true);
            if (anchor && state?.permissions.canOpen) prepare(one('[data-cash-open-dialog]'), state.permissions.needsOpeningPassword);
        }));
        const openCashMenu = async () => { if (busy) return; await refresh(); drawSummary(); openDialog(one('[data-cash-summary-dialog]')); };
        all('[data-cash-summary-open]').forEach(button => button.addEventListener('click', openCashMenu));
        const openMovement = async original => {
            await refresh(true);
            if (!(state?.permissions.canIncome || state?.permissions.canExpense) || pos.dataset.cashValidated !== 'true') return;
            const dialog = one('[data-cash-movement-dialog]'); const form = dialog.querySelector('form');
            form.reset();
            const type = form.elements.namedItem('type');
            type.querySelector('[value="income"]').disabled = !state.permissions.canIncome;
            type.querySelector('[value="expense"]').disabled = !state.permissions.canExpense;
            type.value = original ? (original.type === 'Expense' ? 'income' : 'expense') : state.permissions.canIncome ? 'income' : 'expense';
            form.elements.namedItem('amount').value = original ? Number(original.amount).toFixed(2) : '';
            form.elements.namedItem('reversalOfId').value = original?.id || '';
            form.elements.namedItem('amount').readOnly = Boolean(original);
            type.disabled = Boolean(original);
            form.elements.namedItem('reason').value = original ? `Reverso: ${original.reason}`.slice(0, 500) : '';
            dialog.querySelector('[data-cash-movement-authorization]').hidden = !state.permissions.needsMovementAuthorization;
            prepare(dialog);
        };
        const openClose = async () => {
            await refresh(true);
            if (state?.permissions.canClose && pos.dataset.cashValidated === 'true') {
                const dialog = one('[data-cash-close-dialog]');
                dialog.querySelector('form').reset();
                dialog.dataset.revision = state.session.revision;
                dialog.dataset.sessionId = state.session.id;
                const posClosingLink = dialog.querySelector('[data-cash-pos-closing-link]');
                if (posClosingLink) posClosingLink.href = `/Cash/Details/${encodeURIComponent(state.session.id)}#cierres-pos`;
                prepare(dialog, state.permissions.needsClosingPassword);
            }
        };
        one('[data-cash-movement-open]').addEventListener('click', () => openMovement(null));
        one('[data-cash-close-open]').addEventListener('click', openClose);
        document.querySelectorAll('[data-pos-cash-menu]').forEach(link => link.addEventListener('click', event => {
            event.preventDefault(); event.stopPropagation();
            openCashMenu();
        }));
        all('[data-cash-dialog-close]').forEach(button => button.addEventListener('click', () => { if (!busy) button.closest('dialog').close(); }));
        all('.pos-cash-dialog').forEach(dialog => dialog.addEventListener('cancel', event => { if (busy) event.preventDefault(); }));
        const amount = value => {
            const normalized = value.trim().replace(',', '.');
            if (!/^\d+(?:\.\d{1,2})?$/.test(normalized)) throw new Error('Introduce un importe válido con máximo dos decimales.');
            return normalized;
        };
        const storageKey = (kind, terminalId) => `sistemi:cash-command:${pos.dataset.posUserKey}:${state?.branchId || document.body.dataset.currentBranchId}:${terminalId}:${kind}`;
        const readPending = name => { try { return JSON.parse(localStorage.getItem(name) || 'null'); } catch { return null; } };
        const writePending = (name, pending) => { try { if (pending) localStorage.setItem(name, JSON.stringify(pending)); else localStorage.removeItem(name); } catch { /* The same in-memory key is retained for retries in this page. */ } };
        const pendingInMemory = new Map();
        const digest = async value => Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value)))).map(x => x.toString(16).padStart(2, '0')).join('');
        all('[data-cash-command]').forEach(form => form.addEventListener('submit', async event => {
            event.preventDefault(); if (busy || !form.reportValidity()) return;
            busy = true; form.inert = true; form.setAttribute('aria-busy', 'true');
            form.querySelectorAll('button').forEach(button => button.disabled = true);
            const errorText = form.querySelector('[data-cash-command-error]');
            const kind = form.dataset.cashCommand;
            const selected = terminal?.value;
            let name = null, pending = null;
            try {
                if (!selected || !navigator.onLine) throw new Error('Se requiere una terminal y conexión con el servidor.');
                const valid = await refresh(true);
                name = storageKey(kind, selected);
                pending = pendingInMemory.get(name) || readPending(name);
                if (pending) {
                    const result = await request(`/pos/caja/resultado?terminalId=${encodeURIComponent(selected)}&kind=${encodeURIComponent(kind)}&key=${encodeURIComponent(pending.key)}`).catch(error => { if (error.status === 404) return null; throw error; });
                    if (result) {
                        pendingInMemory.delete(name); writePending(name, null); form.closest('dialog').close();
                        ++sequence; const now = performance.now(); accept(result.state, now, now);
                        if (kind === 'cerrar') openDialog(one('[data-cash-summary-dialog]'));
                        window.showSystemToast?.('success', 'La operación anterior ya estaba registrada.'); return;
                    }
                }
                if (kind === 'abrir' ? !state?.permissions.canOpen : !valid) throw new Error('El estado de la caja no permite esta operación.');
                const value = field => form.elements.namedItem(field)?.value || null;
                let payload;
                if (kind === 'abrir') payload = { terminalId: selected, openingAmount: amount(value('amount')), note: value('note'), password: value('password'), posDeviceIds: Array.from(form.querySelectorAll('[name="posDeviceIds"]:checked')).map(x => x.value) };
                else if (kind === 'movimiento') payload = { terminalId: selected, sessionId: state.session.id, isIncome: value('type') === 'income', amount: amount(value('amount')), reason: value('reason'), reference: value('reference'), authorizerUserName: value('authorizer'), authorizerPassword: value('authorizerPassword'), reversalOfId: value('reversalOfId') };
                else payload = { terminalId: selected, sessionId: form.closest('dialog').dataset.sessionId, expectedRevision: Number(form.closest('dialog').dataset.revision), countedCash: amount(value('amount')), note: value('note'), authorizerUserName: value('authorizer'), authorizerPassword: value('authorizerPassword'), password: value('password') };
                const { password, authorizerPassword, ...publicData } = payload;
                const fingerprint = await digest(JSON.stringify(publicData));
                if (pending) {
                    if (pending.fingerprint !== fingerprint) throw new Error('Hay una operación sin confirmar con otros datos. Revisa el turno antes de cambiar la solicitud.');
                }
                pending ||= { key: crypto.randomUUID(), fingerprint };
                pendingInMemory.set(name, pending); writePending(name, pending);
                busy = true;
                const buttons = Array.from(form.querySelectorAll('button')); buttons.forEach(button => button.disabled = true);
                form.setAttribute('aria-busy', 'true'); errorText.textContent = '';
                const sent = performance.now();
                const result = await request(`/pos/caja/${kind}`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token, 'Idempotency-Key': pending.key }, body: JSON.stringify(payload) });
                pendingInMemory.delete(name); writePending(name, null);
                ++sequence;
                accept(result.state, sent, performance.now());
                form.closest('dialog').close(); form.reset();
                if (kind === 'cerrar') openDialog(one('[data-cash-summary-dialog]'));
                else await refresh();
                window.showSystemToast?.('success', kind === 'abrir' ? 'Caja abierta y registrada.' : kind === 'cerrar' ? 'Cierre registrado. El pedido sin cobrar se conserva.' : 'Movimiento registrado.');
            } catch (error) {
                if (name && error.status && error.status < 500) { pendingInMemory.delete(name); writePending(name, null); }
                errorText.textContent = error.message || 'No se confirmó el resultado. Reintenta con los mismos datos.';
                await refresh();
            } finally {
                busy = false; form.inert = false; form.removeAttribute('aria-busy'); form.querySelectorAll('button').forEach(button => button.disabled = false);
                drawSummary();
            }
        }));
        terminal?.addEventListener('change', () => {
            state = null; anchor = null; all('.pos-cash-dialog[open]').forEach(dialog => dialog.close());
            showSalesWorkspace(false);
            text('[data-cash-terminal]', terminal.selectedOptions[0]?.textContent || 'Sin seleccionar');
            text('[data-cash-opened]', '—'); text('[data-cash-duration]', '—'); text('[data-cash-cashier]', '');
            show('[data-cash-opening-amount]', false); show('[data-cash-open]', false);
            refresh(true);
        });
        window.addEventListener('offline', () => { ++sequence; refresh(true); });
        window.addEventListener('online', () => refresh(true));
        window.addEventListener('pos:cash-invalidated', () => refresh(true));
        document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(true); });
        window.addEventListener('pageshow', () => refresh(true));
        window.setInterval(() => { if (!document.hidden && !busy) refresh(); }, 30000);
        window.setInterval(drawClock, 1000);
        if (window.ResizeObserver) new ResizeObserver(entries => pos.style.setProperty('--pos-cash-context-height', `${entries[0].borderBoxSize?.[0]?.blockSize || entries[0].contentRect.height}px`)).observe(one('[data-cash-context]'));
        window.sistemiPosCash = { validateForCheckout: () => {
            checkoutValidation ||= refresh(true).finally(() => checkoutValidation = null);
            return checkoutValidation;
        }};
        refresh(true).then(() => { if (location.hash === '#caja') { drawSummary(); openDialog(one('[data-cash-summary-dialog]')); } });
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();
