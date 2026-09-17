using MoneyTalk.Core.Entities;

namespace MoneyTalk.Core.Accounting;

/// <summary>Faithful port of NovaOps's trade-in offer calculator (components/TradeInWizard.vue)
/// — every constant and formula below is copied from that source, not re-derived, so a shop's
/// trade-in math doesn't change just because the app it runs in did.</summary>
public static class TradeInValuationService
{
    private static readonly Dictionary<ConditionGrade, decimal> GradeDeductions = new()
    {
        [ConditionGrade.Excellent] = 0.05m,
        [ConditionGrade.Good] = 0.15m,
        [ConditionGrade.Fair] = 0.30m,
        [ConditionGrade.Poor] = 0.50m
    };

    private static readonly Dictionary<ScreenCondition, decimal> ScreenDeductions = new()
    {
        [ScreenCondition.Perfect] = 0m,
        [ScreenCondition.MinorScratches] = 0.05m,
        [ScreenCondition.Cracked] = 0.15m,
        [ScreenCondition.Shattered] = 0.28m
    };

    public static readonly IReadOnlyDictionary<string, decimal> AccessoryValues = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
    {
        ["original_box"] = 8m,
        ["charger"] = 6m,
        ["earphones"] = 5m,
        ["case"] = 3m
    };

    public const decimal FunctionalIssueCost = 25m;
    public const decimal CosmeticIssueCost = 10m;
    public const decimal LockPenalty = 0.40m;
    public const decimal AgeDeductionPerYear = 0.06m;
    private const decimal MaxAgeDeductionFraction = 0.40m;
    private const decimal BatteryHealthWeight = 0.15m;
    private const decimal MaxTotalDeductionFraction = 0.92m;

    public record ValuationResult(decimal Deductions, decimal AccessoryBonus, decimal OfferPrice, decimal EstimatedResaleValue, decimal EstimatedProfit);

    /// <param name="manualOfferPrice">If the shop overrides the calculated offer before finalizing
    /// the trade-in, pass it here so <see cref="ValuationResult.EstimatedProfit"/> reflects what
    /// was actually paid rather than the raw calculation.</param>
    public static ValuationResult Calculate(
        decimal marketPrice, ConditionGrade grade, ScreenCondition screenCondition, decimal ageYears,
        int batteryHealthPercent, int functionalIssueCount, int cosmeticIssueCount,
        bool iCloudLocked, bool frpLocked, IReadOnlyList<string> accessories,
        decimal repairCostEstimate = 0m, decimal? manualOfferPrice = null)
    {
        if (marketPrice <= 0)
            return new ValuationResult(0, 0, 0, 0, 0);

        var deductions = 0m;
        deductions += marketPrice * (GradeDeductions.TryGetValue(grade, out var gradePct) ? gradePct : 0m);
        deductions += marketPrice * (ScreenDeductions.TryGetValue(screenCondition, out var screenPct) ? screenPct : 0m);
        deductions += marketPrice * Math.Min(ageYears * AgeDeductionPerYear, MaxAgeDeductionFraction);
        deductions += (100 - batteryHealthPercent) / 100m * marketPrice * BatteryHealthWeight;
        deductions += functionalIssueCount * FunctionalIssueCost;
        deductions += cosmeticIssueCount * CosmeticIssueCost;
        if (iCloudLocked || frpLocked) deductions += marketPrice * LockPenalty;
        deductions = Math.Min(deductions, marketPrice * MaxTotalDeductionFraction);

        var accessoryBonus = accessories.Sum(a => AccessoryValues.TryGetValue(a, out var v) ? v : 0m);

        var rawOffer = marketPrice - deductions - repairCostEstimate + accessoryBonus;
        var offerPrice = Math.Max(Math.Round(rawOffer * 2m, 0, MidpointRounding.AwayFromZero) / 2m, 0m);

        var estimatedResale = Math.Max(marketPrice - deductions * 0.3m, offerPrice + 20m);
        var finalOffer = manualOfferPrice ?? offerPrice;
        var estimatedProfit = estimatedResale - finalOffer - repairCostEstimate;

        return new ValuationResult(deductions, accessoryBonus, offerPrice, estimatedResale, estimatedProfit);
    }

    /// <summary>Standard IMEI (Luhn) checksum validation — a 15-digit IMEI is valid only when its
    /// digits (doubling every second one from the left, per Luhn) sum to a multiple of 10.</summary>
    public static bool IsValidImei(string imei)
    {
        var digits = new string(imei.Where(char.IsDigit).ToArray());
        if (digits.Length != 15) return false;

        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var d = digits[i] - '0';
            if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
        }
        return sum % 10 == 0;
    }
}
