namespace AccountingERP.Domain.Enums;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum AccountNature
{
    Debit = 1,
    Credit = 2
}

public enum FiscalPeriodStatus
{
    Open = 1,
    Closed = 2,
    Locked = 3
}

public enum FiscalPeriodType
{
    Regular = 1,
    YearEndAdjustment = 2
}

public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

public enum JournalEntryType
{
    Manual = 1,
    OpeningBalance = 2,
    Closing = 3,
    Reversal = 4
}

public enum SourceDocumentType
{
    Manual = 1,
    SalesInvoice = 2,
    PurchaseInvoice = 3,
    CustomerReceipt = 4,
    SupplierPayment = 5,
    StockTransaction = 6,
    AssetDepreciation = 7,
    YearEndClosing = 8
}

public enum InvoiceStatus
{
    Draft = 1,
    Approved = 2,
    PostedToGL = 3,
    Paid = 4,
    PartiallyPaid = 5,
    Cancelled = 6
}

public enum PaymentType
{
    Receipt = 1, // قبض
    Payment = 2  // صرف
}

public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2,
    Cheque = 3
}

public enum ProductType
{
    Storable = 1,
    Service = 2,
    Consumable = 3
}

public enum StockTransactionType
{
    PurchaseIn = 1,
    SalesOut = 2,
    TransferIn = 3,
    TransferOut = 4,
    InventoryAdjustment = 5
}

public enum FixedAssetStatus
{
    Active = 1,
    Disposed = 2,
    FullyDepreciated = 3
}

public enum DepreciationMethod
{
    StraightLine = 1
}

public enum TaxCategory
{
    Sales = 1,
    Purchase = 2,
    Withholding = 3
}
