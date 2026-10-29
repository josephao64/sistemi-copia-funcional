namespace SistemiPos.Domain.Finance;

public enum AccountStatus { Pending = 1, PartiallyPaid = 2, Paid = 3, Overdue = 4, Cancelled = 5, Delinquent = 6 }
public enum FinancialPaymentMethod { Cash = 1, Check = 2, Transfer = 3, CardPos = 4, Other = 5 }
public enum MoneySourceType { PettyCash = 1, SalesCash = 2, BankAccount = 3, Other = 4 }
public enum PettyCashMovementType { Replenishment = 1, Expense = 2, SupplierPayment = 3, Cancellation = 4 }
