(() => {
    const container = document.getElementById('checkout-payments');
    if (container) {
        const splitMode = document.querySelector('[name="SplitMode"]');
        const refreshSplit = () => {
            const mode = splitMode.value;
            const people = document.querySelector('[name="People"]');
            people.closest('.field').hidden = mode !== 'People'; people.disabled = mode !== 'People';
            const amounts = document.getElementById('account-amounts');
            amounts.hidden = mode !== 'Amounts';
            amounts.querySelectorAll('input').forEach(input => input.disabled = mode !== 'Amounts');
            document.getElementById('add-account-amount').hidden = mode !== 'Amounts';
            container.querySelectorAll('[name$=".PartNumber"]').forEach(input => {
                input.closest('.field').hidden = mode === 'None'; input.disabled = mode === 'None';
            });
        };
        const refresh = () => container.querySelectorAll('.checkout-payment').forEach((row, i) => {
            row.querySelector('legend').textContent = `Pago ${i + 1}`;
            row.querySelectorAll('input,select').forEach(input => {
                const oldId = input.id;
                input.name = input.name.replace(/Payments\[\d+\]/, `Payments[${i}]`);
                input.id = input.name.replace(/[.\[\]]/g, '_');
                Array.from(row.querySelectorAll('label')).find(label => label.htmlFor === oldId)?.setAttribute('for', input.id);
            });
            const method = row.querySelector('[name$=".Method"]').value;
            row.querySelectorAll('[data-method-field]').forEach(field => field.hidden = !field.dataset.methodField.split(',').includes(method));
            refreshSplit();
        });
        splitMode.addEventListener('change', refreshSplit);
        container.addEventListener('change', refresh);
        container.addEventListener('click', event => {
            if (event.target.closest('.remove-checkout-payment') && container.children.length > 1) {
                event.target.closest('.checkout-payment').remove(); refresh();
            }
        });
        document.getElementById('add-checkout-payment').addEventListener('click', () => {
            if (container.children.length >= 10) return;
            const row = container.firstElementChild.cloneNode(true);
            row.querySelectorAll('input').forEach(input => input.value = '');
            container.appendChild(row); refresh();
        });
        document.getElementById('add-account-amount').addEventListener('click', () => {
            const amounts = document.getElementById('account-amounts');
            const i = amounts.children.length;
            if (i >= 10) return;
            const field = amounts.firstElementChild.cloneNode(true);
            field.querySelector('label').textContent = `Importe cuenta ${i + 1}`;
            field.querySelector('label').htmlFor = `amount-${i}`;
            const input = field.querySelector('input');
            input.name = `Amounts[${i}]`; input.id = `amount-${i}`; input.value = '0';
            amounts.appendChild(field);
        });
        refresh();
    }
    document.querySelector('[data-open-extended-checkout]')?.addEventListener('click', event => {
        if (!navigator.onLine) { window.showSystemToast?.('error', 'La división de cuenta y el cobro mixto requieren conexión.'); return; }
        const source = event.target.closest('form');
        const form = document.createElement('form');
        form.method = 'post'; form.action = '/pos/cobro/preparar';
        for (const [name, value] of new FormData(source)) {
            const input = document.createElement('input'); input.type = 'hidden'; input.name = name; input.value = value;
            form.appendChild(input);
        }
        document.body.appendChild(form); form.submit();
    });
})();
