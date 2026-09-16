namespace MoneyTalk.Core.Entities;

/// <summary>A template that automatically generates an Invoice, Bill, or JournalEntry on a
/// schedule (e.g. monthly rent bill, recurring subscription invoice).</summary>
public class RecurringTransaction : CompanyOwnedEntity
{
    public string Name { get; set; } = string.Empty;
    public RecurringTemplateType TemplateType { get; set; }
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;
    public DateTime NextRunDate { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastRunDate { get; set; }

    /// <summary>Serialized JSON snapshot of the Invoice/Bill/JournalEntry to clone on each run
    /// (customer/vendor, lines, memo, terms) minus the date fields, which get recalculated.</summary>
    public string TemplatePayloadJson { get; set; } = "{}";

    public static DateTime ComputeNextRunDate(DateTime from, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily => from.AddDays(1),
        RecurrenceFrequency.Weekly => from.AddDays(7),
        RecurrenceFrequency.BiWeekly => from.AddDays(14),
        RecurrenceFrequency.Monthly => from.AddMonths(1),
        RecurrenceFrequency.Quarterly => from.AddMonths(3),
        RecurrenceFrequency.SemiAnnually => from.AddMonths(6),
        RecurrenceFrequency.Annually => from.AddYears(1),
        _ => from.AddMonths(1)
    };
}
