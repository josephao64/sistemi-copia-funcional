(() => {
    document.querySelectorAll('.order-action').forEach(form => {
        const method = form.querySelector('[name="Type"]');
        const extras = form.querySelector('[data-extra-packaging]');
        const refresh = () => {
            extras.hidden = method.value !== 'Dispatch';
            extras.querySelectorAll('input,select').forEach(input => input.disabled = extras.hidden);
        };
        method.addEventListener('change', refresh); refresh();
    });
    const button = document.getElementById('add-order-line');
    const container = document.getElementById('order-lines');
    if (!button || !container) return;
    button.addEventListener('click', () => {
        const rows = container.querySelectorAll('.order-line');
        if (rows.length >= 200) return;
        const row = rows[0].cloneNode(true);
        row.querySelectorAll('select, input').forEach(input => {
            input.name = input.name.replace(/Lines\[\d+\]/, `Lines[${rows.length}]`);
            const prefix = input.tagName === 'SELECT' ? 'product' : 'quantity';
            input.id = `${prefix}-${rows.length}`;
            input.value = input.tagName === 'SELECT' ? '' : '1';
            input.closest('.field').querySelector('label').htmlFor = input.id;
        });
        container.appendChild(row);
        row.querySelector('select').focus();
    });
})();
