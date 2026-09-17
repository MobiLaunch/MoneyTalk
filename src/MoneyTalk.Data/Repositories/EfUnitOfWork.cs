using Microsoft.EntityFrameworkCore;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.Data.Repositories;

public class EfUnitOfWork : IUnitOfWork
{
    private readonly MoneyTalkDbContext _context;

    public EfUnitOfWork(MoneyTalkDbContext context)
    {
        _context = context;

        Companies = new GenericRepository<Company>(context);
        Accounts = new GenericRepository<Account>(context);
        JournalEntries = new GenericRepository<JournalEntry>(context, q => q.Include(e => e.Lines));
        Customers = new GenericRepository<Customer>(context);
        Vendors = new GenericRepository<Vendor>(context);
        Items = new GenericRepository<Item>(context);
        TaxRates = new GenericRepository<TaxRate>(context);
        Invoices = new GenericRepository<Invoice>(context, q => q.Include(i => i.Lines));
        Bills = new GenericRepository<Bill>(context, q => q.Include(b => b.Lines));
        Payments = new GenericRepository<Payment>(context, q => q.Include(p => p.Applications));
        BillPayments = new GenericRepository<BillPayment>(context, q => q.Include(p => p.Applications));
        BankAccounts = new GenericRepository<BankAccount>(context);
        BankTransactions = new GenericRepository<BankTransaction>(context);
        ReconciliationSessions = new GenericRepository<ReconciliationSession>(context);
        Budgets = new GenericRepository<Budget>(context, q => q.Include(b => b.Lines));
        RecurringTransactions = new GenericRepository<RecurringTransaction>(context);
        Attachments = new GenericRepository<Attachment>(context);
        AuditLogEntries = new GenericRepository<AuditLogEntry>(context);
        IntegrationConnections = new GenericRepository<IntegrationConnection>(context);
        AiConversations = new GenericRepository<AiConversation>(context, q => q.Include(c => c.Messages));
        AiInsights = new GenericRepository<AiInsight>(context);
        Users = new GenericRepository<User>(context);
    }

    public IRepository<Company> Companies { get; }
    public IRepository<Account> Accounts { get; }
    public IRepository<JournalEntry> JournalEntries { get; }
    public IRepository<Customer> Customers { get; }
    public IRepository<Vendor> Vendors { get; }
    public IRepository<Item> Items { get; }
    public IRepository<TaxRate> TaxRates { get; }
    public IRepository<Invoice> Invoices { get; }
    public IRepository<Bill> Bills { get; }
    public IRepository<Payment> Payments { get; }
    public IRepository<BillPayment> BillPayments { get; }
    public IRepository<BankAccount> BankAccounts { get; }
    public IRepository<BankTransaction> BankTransactions { get; }
    public IRepository<ReconciliationSession> ReconciliationSessions { get; }
    public IRepository<Budget> Budgets { get; }
    public IRepository<RecurringTransaction> RecurringTransactions { get; }
    public IRepository<Attachment> Attachments { get; }
    public IRepository<AuditLogEntry> AuditLogEntries { get; }
    public IRepository<IntegrationConnection> IntegrationConnections { get; }
    public IRepository<AiConversation> AiConversations { get; }
    public IRepository<AiInsight> AiInsights { get; }
    public IRepository<User> Users { get; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public void Dispose() => _context.Dispose();
}
