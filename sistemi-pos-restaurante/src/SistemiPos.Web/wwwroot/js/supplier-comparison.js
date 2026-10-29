(() => {
    const productSearch = document.querySelector("[data-comparison-product-search]");
    const productId = document.querySelector("[data-comparison-product-id]");
    const productForm = document.querySelector("[data-product-comparison-filter]");
    const normalize = value => (value || "").trim().toLocaleLowerCase("es");
    productSearch?.addEventListener("change", () => {
        const option = [...document.querySelectorAll("#comparison-products option")].find(x => normalize(x.value) === normalize(productSearch.value));
        productId.value = option?.dataset.id || "";
        if (productId.value) productForm.submit();
    });
    const supplierSearch = document.querySelector("[data-relation-supplier-search]");
    const supplierId = document.querySelector("[data-relation-supplier-id]");
    supplierSearch?.addEventListener("change", () => {
        const option = [...document.querySelectorAll("#comparison-suppliers option")].find(x => normalize(x.value) === normalize(supplierSearch.value));
        supplierId.value = option?.dataset.id || "";
    });
    const terms = document.querySelector("[data-relation-terms]");
    const credit = document.querySelector("[data-relation-credit]");
    const updateCredit = () => {
        if (!terms || !credit) return;
        credit.hidden = terms.value !== "2";
        credit.querySelector("input").required = !credit.hidden;
    };
    terms?.addEventListener("change", updateCredit);
    updateCredit();
})();
