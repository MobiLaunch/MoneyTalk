namespace MoneyTalk.Core.Entities;

/// <summary>A device buy-back/trade-in evaluation. Pricing fields (<see cref="MarketPrice"/>
/// through <see cref="EstimatedProfit"/>) are computed once by <c>TradeInValuationService</c>
/// (Phase 7) and stored, not recomputed on read, so a completed trade-in's numbers never drift if
/// pricing rules change later.</summary>
public class TradeIn : CompanyOwnedEntity
{
    public Guid? CustomerId { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? ModelNumber { get; set; }
    public string? Imei { get; set; }
    public string? Storage { get; set; }
    public string? Color { get; set; }

    public ConditionGrade ConditionGrade { get; set; } = ConditionGrade.Good;
    public decimal AgeYears { get; set; }
    public ScreenCondition ScreenCondition { get; set; } = ScreenCondition.Perfect;
    public int BatteryHealthPercent { get; set; } = 80;

    /// <summary>Comma-separated free-text lists (e.g. "charging_port, camera"), matching how the
    /// rest of the app stores small editable option sets.</summary>
    public string? FunctionalIssues { get; set; }
    public string? CosmeticIssues { get; set; }
    public string? Accessories { get; set; }

    public bool ICloudLocked { get; set; }
    public bool FrpLocked { get; set; }

    public decimal? MarketPrice { get; set; }
    public decimal? RepairCostEstimate { get; set; }
    public decimal? OfferPrice { get; set; }
    public decimal? EstimatedResaleValue { get; set; }
    public decimal? EstimatedProfit { get; set; }

    public TradeInStatus Status { get; set; } = TradeInStatus.Pending;
    public string? Notes { get; set; }
}
