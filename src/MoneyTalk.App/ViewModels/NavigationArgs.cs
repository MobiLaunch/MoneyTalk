namespace MoneyTalk.App.ViewModels;

/// <summary>Navigation parameter for InvoiceEditPage: pass an existing <see cref="InvoiceId"/>
/// to edit that invoice, or leave it null (optionally pre-selecting <see cref="CustomerId"/>) to
/// start a new draft.</summary>
public record InvoiceEditNavigationArgs(Guid? InvoiceId, Guid? CustomerId = null);

/// <summary>Navigation parameter for BillEditPage — same idea as <see cref="InvoiceEditNavigationArgs"/>.</summary>
public record BillEditNavigationArgs(Guid? BillId, Guid? VendorId = null);
