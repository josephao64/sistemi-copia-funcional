(() => {
    const dialog = document.querySelector("[data-receivable-payment-dialog]");
    if (!dialog) return;
    const form = dialog.querySelector("form"), id = dialog.querySelector("[data-receivable-account-id]"), amount = dialog.querySelector("[data-receivable-payment-amount]"), label = dialog.querySelector("[data-receivable-account-label]");
    document.querySelectorAll("[data-open-receivable-payment]").forEach(button => button.addEventListener("click", () => { form.reset(); id.value = button.dataset.accountId || ""; label.textContent = button.dataset.accountLabel || ""; amount.max = button.dataset.accountBalance || ""; dialog.showModal(); amount.focus(); }));
    dialog.querySelectorAll("[data-close-receivable-payment]").forEach(button => button.addEventListener("click", () => dialog.close()));
    dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
})();
