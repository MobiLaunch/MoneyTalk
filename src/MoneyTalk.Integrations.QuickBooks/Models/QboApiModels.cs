using System.Text.Json.Serialization;

namespace MoneyTalk.Integrations.QuickBooks.Models;

internal class QboTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpiresInSeconds { get; set; }
}

internal class QboCompanyInfoResponse
{
    [JsonPropertyName("CompanyInfo")] public QboCompanyInfoDto? CompanyInfo { get; set; }
}

internal class QboCompanyInfoDto
{
    [JsonPropertyName("CompanyName")] public string CompanyName { get; set; } = string.Empty;
    [JsonPropertyName("LegalName")] public string? LegalName { get; set; }
    [JsonPropertyName("Country")] public string? Country { get; set; }
}

internal class QboQueryResponseEnvelope<T>
{
    [JsonPropertyName("QueryResponse")] public T? QueryResponse { get; set; }
}

internal class QboAccountQueryResult
{
    [JsonPropertyName("Account")] public List<QboAccountDto>? Account { get; set; }
}

internal class QboAccountDto
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("AccountType")] public string AccountType { get; set; } = string.Empty;
    [JsonPropertyName("AccountSubType")] public string? AccountSubType { get; set; }
    [JsonPropertyName("CurrentBalance")] public decimal CurrentBalance { get; set; }
}

internal class QboCustomerQueryResult
{
    [JsonPropertyName("Customer")] public List<QboCustomerDto>? Customer { get; set; }
}

internal class QboCustomerDto
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("DisplayName")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("PrimaryEmailAddr")] public QboEmailDto? PrimaryEmailAddr { get; set; }
    [JsonPropertyName("Balance")] public decimal Balance { get; set; }
}

internal class QboEmailDto
{
    [JsonPropertyName("Address")] public string? Address { get; set; }
}

internal class QboInvoiceQueryResult
{
    [JsonPropertyName("Invoice")] public List<QboInvoiceDto>? Invoice { get; set; }
}

internal class QboInvoiceDto
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("DocNumber")] public string? DocNumber { get; set; }
    [JsonPropertyName("CustomerRef")] public QboRefDto? CustomerRef { get; set; }
    [JsonPropertyName("TxnDate")] public DateTime TxnDate { get; set; }
    [JsonPropertyName("DueDate")] public DateTime DueDate { get; set; }
    [JsonPropertyName("TotalAmt")] public decimal TotalAmt { get; set; }
    [JsonPropertyName("Balance")] public decimal Balance { get; set; }
    [JsonPropertyName("Line")] public List<QboLineDto>? Line { get; set; }
}

internal class QboRefDto
{
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; set; }
}

internal class QboLineDto
{
    [JsonPropertyName("Description")] public string? Description { get; set; }
    [JsonPropertyName("Amount")] public decimal Amount { get; set; }
    [JsonPropertyName("DetailType")] public string? DetailType { get; set; }
    [JsonPropertyName("SalesItemLineDetail")] public QboSalesItemLineDetailDto? SalesItemLineDetail { get; set; }
}

internal class QboSalesItemLineDetailDto
{
    [JsonPropertyName("ItemRef")] public QboRefDto? ItemRef { get; set; }
    [JsonPropertyName("Qty")] public decimal? Qty { get; set; }
    [JsonPropertyName("UnitPrice")] public decimal? UnitPrice { get; set; }
}
