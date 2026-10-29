(() => {
    const ticket = document.querySelector('[data-sale-ticket]');
    if (!ticket) return;
    const width = ticket.querySelector('[data-ticket-width]');
    width?.addEventListener('change', () => {
        const narrow = width.value === '58';
        ticket.classList.toggle('is-narrow', narrow);
        ticket.querySelector('[data-ticket-size-label]').textContent = `${narrow ? '58' : '80'} mm`;
        // Keep the chosen width when the receipt is reloaded or shared.
        const url = new URL(window.location.href);
        url.searchParams.set('width', narrow ? '58' : '80');
        window.history.replaceState(window.history.state, '', url);
    });
    ticket.querySelector('[data-ticket-print]')?.addEventListener('click', () => window.print());
    if (ticket.dataset.autoPrint === 'true') {
        window.addEventListener('load', () => window.setTimeout(() => window.print(), 250), { once: true });
    }
})();
