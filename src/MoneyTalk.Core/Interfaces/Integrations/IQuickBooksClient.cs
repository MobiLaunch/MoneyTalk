namespace MoneyTalk.Core.Interfaces.Integrations;

public record QuickBooksOAuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    string RealmId);

public record QboCompanyInfo(string RealmId, string CompanyName, string? LegalName, string? Country);

public record QboAccount(string Id, string Name, string AccountType, string AccountSubType, decimal CurrentBalance);

public record QboCustomer(string Id, string DisplayName, string? Email, decimal Balance);

public record QboInvoiceLine(string Description, decimal Amount, decimal Quantity, decimal UnitPrice);

public record QboInvoice(
    string Id,
    string DocNumber,
    string CustomerId,
    DateTime TxnDate,
    DateTime DueDate,
    decimal TotalAmount,
    decimal Balance,
    IReadOnlyList<QboInvoiceLine> Lines);

/// <summary>Client for the QuickBooks Online Accounting API. Used both to import an existing
/// QuickBooks company's chart of accounts / customers / open invoices, and to push
/// MoneyTalk-created invoices back out so a business can keep QuickBooks as a system of record
/// for its accountant while using MoneyTalk day-to-day.</summary>
public interface IQuickBooksClient
{
    string BuildAuthorizationUrl(string state);
    Task<QuickBooksOAuthTokens> ExchangeAuthorizationCodeAsync(string code, string realmId, CancellationToken ct = default);
    Task<QuickBooksOAuthTokens> RefreshTokenAsync(string refreshToken, string realmId, CancellationToken ct = default);

    Task<QboCompanyInfo> GetCompanyInfoAsync(string accessToken, string realmId, CancellationToken ct = default);
    Task<IReadOnlyList<QboAccount>> QueryAccountsAsync(string accessToken, string realmId, CancellationToken ct = default);
    Task<IReadOnlyList<QboCustomer>> QueryCustomersAsync(string accessToken, string realmId, CancellationToken ct = default);
    Task<IReadOnlyList<QboInvoice>> QueryInvoicesAsync(string accessToken, string realmId, CancellationToken ct = default);
    Task<QboInvoice> CreateInvoiceAsync(string accessToken, string realmId, QboInvoice invoice, CancellationToken ct = default);
}
