(() => {
    const printReceipt = () => window.print();
    document.querySelector('[data-print-receipt]')?.addEventListener('click', printReceipt);

    const image = document.querySelector('[data-business-logo]');
    if (image) {
        const updateLogo = () => {
            const loaded = image.complete && image.naturalWidth > 0;
            image.hidden = !loaded;
            const fallback = document.querySelector('[data-logo-fallback]');
            if (fallback) fallback.hidden = loaded;
        };
        image.addEventListener('load', updateLogo);
        image.addEventListener('error', updateLogo);
        if (image.complete) updateLogo();
    }

    const returnToSystem = () => {
        window.location.replace(document.body.dataset.returnUrl || '/pos');
    };
    document.querySelector('[data-close-receipt]')?.addEventListener('click', () => {
        window.close();
        window.setTimeout(() => { if (!window.closed) returnToSystem(); }, 150);
    });

    document.addEventListener('keydown', event => {
        if (event.key !== 'Enter' || event.repeat || event.defaultPrevented) return;
        if (event.target.closest('button, a, input, select, textarea')) return;
        event.preventDefault();
        printReceipt();
    });
    if (document.body.dataset.autoPrint === 'true') {
        window.addEventListener('load', () => window.setTimeout(printReceipt, 250), { once: true });
    }
})();
