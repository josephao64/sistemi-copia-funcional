(() => {
    const panel = document.querySelector('[data-sale-pdf-job]');
    if (!panel) return;
    const form = panel.querySelector('[data-pdf-request]');
    const status = panel.querySelector('[data-pdf-status]');
    const retry = panel.querySelector('[data-pdf-retry]');
    const download = panel.querySelector('[data-pdf-download]');
    const key = form.elements.requestKey;
    const url = new URL(location.href);
    url.searchParams.set('requestKey', key.value);
    history.replaceState(history.state, '', url);
    let job = null, busy = false, failed = false, deadline = 0;
    const json = async response => {
        const data = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(data.message || data.detail || 'No se pudo consultar el PDF. Puedes reintentar.');
        return data;
    };
    const check = async () => {
        try {
            const data = await json(await fetch(job.statusUrl, { credentials: 'same-origin', cache: 'no-store', headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' } }));
            if (data.status === 'Completed') {
                status.textContent = 'PDF listo. La descarga comenzará automáticamente.';
                download.href = data.downloadUrl; download.hidden = false;
                retry.hidden = true; busy = false;
                location.assign(data.downloadUrl);
                return;
            }
            if (data.status === 'Failed') {
                failed = true;
                throw new Error('No fue posible generar este PDF después de los reintentos. Puedes solicitarlo nuevamente o abrir el ticket.');
            }
            if (performance.now() >= deadline) {
                status.textContent = 'El documento sigue pendiente. La solicitud está guardada; puedes continuar vendiendo y consultar de nuevo.';
                retry.textContent = 'Consultar de nuevo'; retry.hidden = false; busy = false;
                return;
            }
            status.textContent = 'Preparando el PDF en segundo plano… El ticket ya está disponible.';
            window.setTimeout(check, 2000);
        } catch (error) {
            status.textContent = error.message;
            retry.textContent = failed ? 'Solicitar nuevamente' : 'Reintentar consulta';
            retry.hidden = false; busy = false;
        }
    };
    const request = async () => {
        if (busy) return;
        busy = true; retry.hidden = true; deadline = performance.now() + 45000;
        if (failed) {
            key.value = crypto.randomUUID(); job = null; failed = false;
            url.searchParams.set('requestKey', key.value); history.replaceState(history.state, '', url);
        }
        try {
            if (!job) job = await json(await fetch(form.action, {
                method: 'POST', body: new FormData(form), credentials: 'same-origin', cache: 'no-store',
                headers: { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
            }));
            await check();
        } catch (error) {
            status.textContent = error.message; retry.hidden = false; retry.textContent = 'Reintentar solicitud'; busy = false;
        }
    };
    form.addEventListener('submit', event => { event.preventDefault(); request(); });
    request();
})();
