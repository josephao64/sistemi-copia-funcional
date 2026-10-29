namespace SistemiPos.Domain.Cash;

public enum CashMovementType
{
    SaleCash = 1,
    SaleTransfer = 2,
    SalePos = 3,
    OtherIncome = 4,
    Expense = 5,
    AdjustmentIncrease = 6,
    AdjustmentDecrease = 7,
    RefundCash = 8,
    RefundTransfer = 9,
    RefundPos = 10,
    CollectionCash = 11,
    SupplierPayment = 12,
    CashWithdrawal = 13,
    CashTransfer = 14,
    SaleOther = 15,
    RefundOther = 16
}
