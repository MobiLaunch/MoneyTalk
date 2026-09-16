using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.Data;

public class MoneyTalkDbContext : DbContext
{
    public MoneyTalkDbContext(DbContextOptions<MoneyTalkDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<BillPayment> BillPayments => Set<BillPayment>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<ReconciliationSession> ReconciliationSessions => Set<ReconciliationSession>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RecurringTransaction> RecurringTransactions => Set<RecurringTransaction>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiInsight> AiInsights => Set<AiInsight>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureEnumsAsStrings(modelBuilder);

        modelBuilder.Entity<Account>(b =>
        {
            b.HasIndex(a => new { a.CompanyId, a.Code });
            b.Property(a => a.CurrentBalance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<JournalEntry>(b =>
        {
            b.HasMany(e => e.Lines).WithOne().HasForeignKey(l => l.JournalEntryId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(e => new { e.CompanyId, e.Date });
        });
        modelBuilder.Entity<JournalLine>(b =>
        {
            b.Property(l => l.Debit).HasPrecision(18, 2);
            b.Property(l => l.Credit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Customer>(b => b.Property(c => c.Balance).HasPrecision(18, 2));
        modelBuilder.Entity<Vendor>(b =>
        {
            b.Property(v => v.Balance).HasPrecision(18, 2);
            b.Property(v => v.YtdPayments).HasPrecision(18, 2);
        });
        modelBuilder.Entity<Item>(b =>
        {
            b.Property(i => i.SalesPrice).HasPrecision(18, 2);
            b.Property(i => i.Cost).HasPrecision(18, 2);
            b.Property(i => i.QuantityOnHand).HasPrecision(18, 4);
            b.Property(i => i.ReorderPoint).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Invoice>(b =>
        {
            b.HasMany(i => i.Lines).WithOne().HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            b.Property(i => i.Subtotal).HasPrecision(18, 2);
            b.Property(i => i.TaxTotal).HasPrecision(18, 2);
            b.Property(i => i.Total).HasPrecision(18, 2);
            b.Property(i => i.AmountPaid).HasPrecision(18, 2);
            b.Property(i => i.Balance).HasPrecision(18, 2);
            b.HasIndex(i => new { i.CompanyId, i.Status });
        });
        modelBuilder.Entity<InvoiceLine>(b =>
        {
            b.Property(l => l.Quantity).HasPrecision(18, 4);
            b.Property(l => l.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Bill>(b =>
        {
            b.HasMany(bill => bill.Lines).WithOne().HasForeignKey(l => l.BillId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.Subtotal).HasPrecision(18, 2);
            b.Property(x => x.TaxTotal).HasPrecision(18, 2);
            b.Property(x => x.Total).HasPrecision(18, 2);
            b.Property(x => x.AmountPaid).HasPrecision(18, 2);
            b.Property(x => x.Balance).HasPrecision(18, 2);
            b.HasIndex(x => new { x.CompanyId, x.Status });
        });
        modelBuilder.Entity<BillLine>(b =>
        {
            b.Property(l => l.Quantity).HasPrecision(18, 4);
            b.Property(l => l.UnitPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Payment>(b =>
        {
            b.HasMany(p => p.Applications).WithOne().HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Cascade);
            b.Property(p => p.Amount).HasPrecision(18, 2);
        });
        modelBuilder.Entity<PaymentApplication>(b => b.Property(a => a.AmountApplied).HasPrecision(18, 2));

        modelBuilder.Entity<BillPayment>(b =>
        {
            b.HasMany(p => p.Applications).WithOne().HasForeignKey(a => a.BillPaymentId).OnDelete(DeleteBehavior.Cascade);
            b.Property(p => p.Amount).HasPrecision(18, 2);
        });
        modelBuilder.Entity<BillPaymentApplication>(b => b.Property(a => a.AmountApplied).HasPrecision(18, 2));

        modelBuilder.Entity<BankAccount>(b =>
        {
            b.Property(a => a.CurrentBalance).HasPrecision(18, 2);
            b.Property(a => a.LastReconciledBalance).HasPrecision(18, 2);
        });
        modelBuilder.Entity<BankTransaction>(b => b.Property(t => t.Amount).HasPrecision(18, 2));

        modelBuilder.Entity<ReconciliationSession>(b =>
        {
            b.Property(s => s.StatementBeginningBalance).HasPrecision(18, 2);
            b.Property(s => s.StatementEndingBalance).HasPrecision(18, 2);
            b.Property(s => s.ClearedBalance).HasPrecision(18, 2);

            var guidListComparer = new ValueComparer<List<Guid>>(
                (a, c) => (a ?? new()).SequenceEqual(c ?? new()),
                v => (v ?? new()).Aggregate(0, (hash, id) => HashCode.Combine(hash, id)),
                v => (v ?? new()).ToList());

            b.Property(s => s.ClearedBankTransactionIds)
                .HasConversion(
                    v => string.Join(',', v),
                    v => string.IsNullOrEmpty(v) ? new List<Guid>() : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList())
                .Metadata.SetValueComparer(guidListComparer);
        });

        modelBuilder.Entity<Budget>(b => b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.BudgetId).OnDelete(DeleteBehavior.Cascade));
        modelBuilder.Entity<BudgetLine>(b => b.Property(l => l.Amount).HasPrecision(18, 2));

        modelBuilder.Entity<AiConversation>(b => b.HasMany(c => c.Messages).WithOne().HasForeignKey(m => m.ConversationId).OnDelete(DeleteBehavior.Cascade));

        modelBuilder.Entity<TaxRate>(b => b.Property(t => t.RatePercent).HasPrecision(9, 4));
    }

    private static void ConfigureEnumsAsStrings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>().Property(a => a.Type).HasConversion<string>();
        modelBuilder.Entity<Account>().Property(a => a.SubType).HasConversion<string>();
        modelBuilder.Entity<JournalEntry>().Property(e => e.Status).HasConversion<string>();
        modelBuilder.Entity<JournalEntry>().Property(e => e.SourceType).HasConversion<string>();
        modelBuilder.Entity<Invoice>().Property(i => i.Status).HasConversion<string>();
        modelBuilder.Entity<Bill>().Property(b => b.Status).HasConversion<string>();
        modelBuilder.Entity<Payment>().Property(p => p.Method).HasConversion<string>();
        modelBuilder.Entity<BillPayment>().Property(p => p.Method).HasConversion<string>();
        modelBuilder.Entity<Item>().Property(i => i.Type).HasConversion<string>();
        modelBuilder.Entity<BankTransaction>().Property(t => t.Status).HasConversion<string>();
        modelBuilder.Entity<BankTransaction>().Property(t => t.Source).HasConversion<string>();
        modelBuilder.Entity<ReconciliationSession>().Property(s => s.Status).HasConversion<string>();
        modelBuilder.Entity<RecurringTransaction>().Property(r => r.TemplateType).HasConversion<string>();
        modelBuilder.Entity<RecurringTransaction>().Property(r => r.Frequency).HasConversion<string>();
        modelBuilder.Entity<IntegrationConnection>().Property(c => c.Provider).HasConversion<string>();
        modelBuilder.Entity<AiInsight>().Property(i => i.Severity).HasConversion<string>();
        modelBuilder.Entity<AiMessage>().Property(m => m.Role).HasConversion<string>();
        modelBuilder.Entity<User>().Property(u => u.Role).HasConversion<string>();
    }
}
