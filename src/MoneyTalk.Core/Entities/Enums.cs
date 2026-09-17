namespace MoneyTalk.Core.Entities;

public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    CostOfGoodsSold,
    Expense
}

public enum AccountSubType
{
    Bank,
    AccountsReceivable,
    OtherCurrentAsset,
    FixedAsset,
    OtherAsset,
    AccountsPayable,
    CreditCard,
    OtherCurrentLiability,
    LongTermLiability,
    OwnersEquity,
    RetainedEarnings,
    Income,
    OtherIncome,
    CostOfGoodsSold,
    Expense,
    OtherExpense
}

/// <summary>Which side of a T-account increases the balance for this account type.</summary>
public enum NormalBalance
{
    Debit,
    Credit
}

public enum JournalEntryStatus
{
    Draft,
    Posted,
    Void
}

public enum JournalSourceType
{
    Manual,
    Invoice,
    Bill,
    CustomerPayment,
    VendorPayment,
    BankTransaction,
    Reconciliation,
    OpeningBalance,
    RecurringTransaction,
    Adjustment,
    RepairTicketPayment
}

public enum InvoiceStatus
{
    Draft,
    Sent,
    PartiallyPaid,
    Paid,
    Overdue,
    Void
}

public enum BillStatus
{
    Open,
    PartiallyPaid,
    Paid,
    Void
}

public enum PaymentMethod
{
    Cash,
    Check,
    CreditCard,
    DebitCard,
    BankTransfer,
    Square,
    PayPal,
    Other
}

public enum ItemType
{
    Inventory,
    NonInventory,
    Service,
    Bundle
}

public enum BankTransactionStatus
{
    Unmatched,
    Matched,
    Reconciled,
    Excluded
}

public enum BankTransactionSource
{
    Manual,
    CsvImport,
    SquareSync,
    QuickBooksSync
}

public enum ReconciliationStatus
{
    InProgress,
    Completed,
    Abandoned
}

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    BiWeekly,
    Monthly,
    Quarterly,
    SemiAnnually,
    Annually
}

public enum RecurringTemplateType
{
    Invoice,
    Bill,
    JournalEntry
}

public enum IntegrationProvider
{
    Square,
    QuickBooksOnline,
    Gemini
}

public enum UserRole
{
    Owner,
    Admin,
    Accountant,
    Bookkeeper,
    ReadOnly
}

public enum AiMessageRole
{
    System,
    User,
    Assistant
}

public enum TicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}

public enum HouseCallStatus
{
    Scheduled,
    Completed,
    Cancelled
}

public enum AppointmentStatus
{
    Scheduled,
    Confirmed,
    Completed,
    Cancelled,
    NoShow
}

public enum TradeInStatus
{
    Pending,
    Accepted,
    Declined,
    Completed
}

/// <summary>Overall physical/functional grade a device is assessed at during trade-in intake.</summary>
public enum ConditionGrade
{
    Excellent,
    Good,
    Fair,
    Poor
}

public enum ScreenCondition
{
    Perfect,
    MinorScratches,
    Cracked,
    Shattered
}
