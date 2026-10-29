(() => {
    'use strict';
    const settings = JSON.parse(document.getElementById('restaurant-display-settings').textContent);
    const decimals = Math.min(6, Math.max(0, Number(settings.decimals)));
    const format = new Intl.NumberFormat('es-GT', { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
    const money = value => {
        const amount = format.formatToParts(Number(value)).map(part => part.type === 'decimal' ? settings.decimalSeparator : part.type === 'group' ? settings.thousandsSeparator : part.value).join('');
        return settings.position === 'After' ? amount + ' ' + settings.symbol : settings.symbol + ' ' + amount;
    };
    const normalize = value => value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase();
    function confirmAction({ title, description, quantity, reason = false, danger = false, confirm = 'Confirmar' }) {
        const dialog = document.getElementById('restaurant-action-dialog');
        if (!dialog || dialog.open) return Promise.resolve(null);
        const form = dialog.querySelector('form');
        const quantityInput = document.getElementById('restaurant-dialog-quantity');
        const reasonInput = document.getElementById('restaurant-dialog-reason');
        document.getElementById('restaurant-dialog-title').textContent = title;
        document.getElementById('restaurant-dialog-description').textContent = description;
        document.getElementById('restaurant-dialog-quantity-field').hidden = quantity === undefined;
        quantityInput.disabled = quantity === undefined;
        quantityInput.required = quantity !== undefined;
        quantityInput.value = quantity ?? '';
        quantityInput.max = quantity ?? '';
        document.getElementById('restaurant-dialog-reason-field').hidden = !reason;
        reasonInput.disabled = !reason;
        reasonInput.required = reason;
        reasonInput.value = '';
        reasonInput.setCustomValidity('');
        const submit = document.getElementById('restaurant-dialog-confirm');
        submit.textContent = confirm;
        submit.className = danger ? 'button-danger' : 'button-primary';
        return new Promise(resolve => {
            let result = null;
            const onSubmit = event => {
                event.preventDefault();
                reasonInput.setCustomValidity(reason && !reasonInput.value.trim() ? 'Escribe el motivo para continuar.' : '');
                if (!form.reportValidity()) return;
                result = { quantity: quantity === undefined ? undefined : Number(quantityInput.value), reason: reason ? reasonInput.value.trim() : undefined };
                dialog.close();
            };
            const cancel = () => dialog.close();
            const reasonChanged = () => reasonInput.setCustomValidity('');
            form.addEventListener('submit', onSubmit);
            reasonInput.addEventListener('input', reasonChanged);
            dialog.querySelectorAll('[data-dialog-cancel]').forEach(button => button.addEventListener('click', cancel));
            dialog.addEventListener('close', () => {
                form.removeEventListener('submit', onSubmit);
                reasonInput.removeEventListener('input', reasonChanged);
                dialog.querySelectorAll('[data-dialog-cancel]').forEach(button => button.removeEventListener('click', cancel));
                resolve(result);
            }, { once: true });
            dialog.showModal();
            if (quantity !== undefined) quantityInput.focus(); else if (reason) reasonInput.focus(); else submit.focus();
        });
    }
    window.restaurantUI = { money, normalize, confirmAction };
    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-money]').forEach(element => element.textContent = money(element.dataset.money));
        const search = document.getElementById('delivery-search');
        const state = document.getElementById('delivery-status');
        if (search && state) {
            const filter = () => {
                let visible = 0;
                document.querySelectorAll('[data-delivery-card]').forEach(card => {
                    const matchesState = state.value === 'active' ? !['Delivered', 'Cancelled'].includes(card.dataset.status) : !state.value || card.dataset.status === state.value;
                    card.hidden = !matchesState || !normalize(card.textContent).includes(normalize(search.value));
                    if (!card.hidden) visible++;
                });
                document.getElementById('delivery-empty').hidden = visible > 0;
            };
            search.addEventListener('input', filter); state.addEventListener('change', filter);
            document.getElementById('delivery-clear').addEventListener('click', () => { search.value = ''; state.value = 'active'; filter(); });
            filter();
        }
    });
})();

