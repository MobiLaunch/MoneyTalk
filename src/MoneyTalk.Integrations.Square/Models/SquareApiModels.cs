using System.Text.Json.Serialization;

namespace MoneyTalk.Integrations.Square.Models;

internal class SquareMoney
{
    [JsonPropertyName("amount")] public long Amount { get; set; }
    [JsonPropertyName("currency")] public string Currency { get; set; } = "USD";
    public decimal ToDecimal() => Amount / 100m;
}

internal class SquareOAuthTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("expires_at")] public string? ExpiresAt { get; set; }
    [JsonPropertyName("merchant_id")] public string MerchantId { get; set; } = string.Empty;
}

internal class SquareLocationsResponse
{
    [JsonPropertyName("locations")] public List<SquareLocationDto>? Locations { get; set; }
}

internal class SquareLocationDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("address")] public SquareAddressDto? Address { get; set; }
}

internal class SquareAddressDto
{
    [JsonPropertyName("address_line_1")] public string? AddressLine1 { get; set; }
    [JsonPropertyName("locality")] public string? Locality { get; set; }
    [JsonPropertyName("administrative_district_level_1")] public string? State { get; set; }

    public override string ToString() => string.Join(", ", new[] { AddressLine1, Locality, State }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

internal class SquareListPaymentsResponse
{
    [JsonPropertyName("payments")] public List<SquarePaymentDto>? Payments { get; set; }
    [JsonPropertyName("cursor")] public string? Cursor { get; set; }
}

internal class SquarePaymentDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("amount_money")] public SquareMoney? AmountMoney { get; set; }
    [JsonPropertyName("tip_money")] public SquareMoney? TipMoney { get; set; }
    [JsonPropertyName("processing_fee")] public List<SquareProcessingFeeDto>? ProcessingFee { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("order_id")] public string? OrderId { get; set; }
    [JsonPropertyName("receipt_url")] public string? ReceiptUrl { get; set; }
}

internal class SquareProcessingFeeDto
{
    [JsonPropertyName("amount_money")] public SquareMoney? AmountMoney { get; set; }
}

internal class SquareListPayoutsResponse
{
    [JsonPropertyName("payouts")] public List<SquarePayoutDto>? Payouts { get; set; }
    [JsonPropertyName("cursor")] public string? Cursor { get; set; }
}

internal class SquarePayoutDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("amount_money")] public SquareMoney? AmountMoney { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("destination")] public SquarePayoutDestinationDto? Destination { get; set; }
}

internal class SquarePayoutDestinationDto
{
    [JsonPropertyName("type")] public string? Type { get; set; }
}
