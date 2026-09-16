using MoneyTalk.Core.Entities;

namespace MoneyTalk.Core.Interfaces;

/// <summary>One repository per aggregate root, plus a single SaveChangesAsync so a service
/// method (e.g. "post an invoice") can touch several tables and commit them atomically.</summary>
public interface IUnitOfWork
{
    IRepository<Company> Companies { get; }
    IRepository<Account> Accounts { get; }
    IRepository<JournalEntry> JournalEntries { get; }
    IRepository<Customer> Customers { get; }
    IRepository<Vendor> Vendors { get; }
    IRepository<Item> Items { get; }
    IRepository<TaxRate> TaxRates { get; }
    IRepository<Invoice> Invoices { get; }
    IRepository<Bill> Bills { get; }
    IRepository<Payment> Payments { get; }
    IRepository<BillPayment> BillPayments { get; }
    IRepository<BankAccount> BankAccounts { get; }
    IRepository<BankTransaction> BankTransactions { get; }
    IRepository<ReconciliationSession> ReconciliationSessions { get; }
    IRepository<Budget> Budgets { get; }
    IRepository<RecurringTransaction> RecurringTransactions { get; }
    IRepository<Attachment> Attachments { get; }
    IRepository<AuditLogEntry> AuditLogEntries { get; }
    IRepository<IntegrationConnection> IntegrationConnections { get; }
    IRepository<AiConversation> AiConversations { get; }
    IRepository<AiInsight> AiInsights { get; }
    IRepository<User> Users { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
