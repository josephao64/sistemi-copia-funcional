(function () {
    "use strict";

    function calculate(product, quantity) {
        const normalizedQuantity = round(Number(quantity), 2);
        const basePrice = Number(product.salePrice ?? product.price ?? 0);
        if (!(normalizedQuantity > 0)) {
            return { unitPrice: basePrice, grossAmount: 0, appliedMode: null, appliedRule: null };
        }

        const enabled = product.multiPriceEnabled === true || String(product.multiPriceEnabled).toLowerCase() === "true";
        const mode = product.multiPriceMode;
        const rules = (product.priceRules || []).map(rule => ({
            id: rule.id ?? rule.Id,
            quantity: rule.quantity ?? rule.Quantity,
            price: rule.price ?? rule.Price,
            isActive: rule.isActive ?? rule.IsActive
        })).filter(rule => rule.isActive === true || String(rule.isActive).toLowerCase() === "true");
        if (!enabled) {
            return baseQuote(basePrice, normalizedQuantity);
        }

        let rule = null;
        if (mode === "MinimumQuantity") {
            rule = rules
                .filter(item => Number(item.quantity) <= normalizedQuantity)
                .sort((left, right) => Number(right.quantity) - Number(left.quantity))[0] || null;
        } else if (mode === "ExactQuantity") {
            rule = rules.find(item => Math.abs(Number(item.quantity) - normalizedQuantity) < 0.000001) || null;
        }

        if (!rule) {
            return baseQuote(basePrice, normalizedQuantity);
        }

        const price = Number(rule.price);
        if (mode === "ExactQuantity") {
            return { unitPrice: round(price / normalizedQuantity, 6), grossAmount: round(price, 2), appliedMode: mode, appliedRule: rule };
        }

        return { unitPrice: round(price, 2), grossAmount: round(price * normalizedQuantity, 2), appliedMode: mode, appliedRule: rule };
    }

    function baseQuote(basePrice, quantity) {
        return { unitPrice: round(basePrice, 2), grossAmount: round(basePrice * quantity, 2), appliedMode: null, appliedRule: null };
    }

    function round(value, decimals) {
        const factor = 10 ** decimals;
        return Math.round((Number(value) + Number.EPSILON) * factor) / factor;
    }

    window.SistemiPricing = { calculate };
})();
