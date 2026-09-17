using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MoneyTalk.Core.Accounting;
using MoneyTalk.Core.Entities;
using MoneyTalk.Core.Interfaces;

namespace MoneyTalk.App.ViewModels;

public class PhotoAttachmentRow
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
}

public partial class TicketEditViewModel : ViewModelBase
{
    private readonly RepairTicketService _ticketService;
    private readonly Services.INavigationService _navigationService;

    private Guid? _ticketId;
    private decimal _currentBalance;

    public static IReadOnlyList<string> PriorityOptions { get; } = Enum.GetNames<TicketPriority>();

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Item> Items { get; } = new();
    public ObservableCollection<BankAccount> PaymentAccounts { get; } = new();
    public ObservableCollection<string> StatusOptions { get; } = new();
    public ObservableCollection<RepairTicketLineEditRow> Lines { get; } = new();
    public ObservableCollection<RepairTicketNote> Notes { get; } = new();
    public ObservableCollection<RepairTicketPayment> Payments { get; } = new();
    public ObservableCollection<PhotoAttachmentRow> Photos { get; } = new();

    [ObservableProperty] private string ticketNumber = string.Empty;
    [ObservableProperty] private Customer? selectedCustomer;
    [ObservableProperty] private string device = string.Empty;
    [ObservableProperty] private string? deviceModel;
    [ObservableProperty] private string? deviceDescription;
    [ObservableProperty] private string issue = string.Empty;
    [ObservableProperty] private string status = "Open";
    [ObservableProperty] private string priorityText = nameof(TicketPriority.Normal);
    [ObservableProperty] private string? serialNumber;
    [ObservableProperty] private int warrantyDays;
    [ObservableProperty] private DateTimeOffset warrantyStartDate = DateTimeOffset.Now.Date;
    [ObservableProperty] private decimal price;
    [ObservableProperty] private string balanceDisplay = "$0.00";
    [ObservableProperty] private string newNoteText = string.Empty;
    [ObservableProperty] private string? signatureImagePath;

    public bool IsNew => _ticketId == null;
    public bool CanRecordPayment => !IsNew && _currentBalance > 0;
    public bool HasSignature => !string.IsNullOrEmpty(SignatureImagePath);

    partial void OnSignatureImagePathChanged(string? value) => OnPropertyChanged(nameof(HasSignature));

    public TicketEditViewModel(
        Func<IUnitOfWork> unitOfWorkFactory, Services.LocalSettingsService settingsService,
        RepairTicketService ticketService, Services.INavigationService navigationService)
        : base(unitOfWorkFactory, settingsService)
    {
        _ticketService = ticketService;
        _navigationService = navigationService;
    }

    public async Task LoadAsync(TicketEditNavigationArgs args)
    {
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var companyId = ActiveCompanyId;

            var company = await uow.Companies.GetByIdAsync(companyId)
                ?? throw new InvalidOperationException("No active company is selected.");
            StatusOptions.Clear();
            foreach (var s in company.TicketStatuses.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                StatusOptions.Add(s);

            var customers = await uow.Customers.FindAsync(c => c.CompanyId == companyId && c.IsActive);
            Customers.Clear();
            foreach (var customer in customers.OrderBy(c => c.Name)) Customers.Add(customer);

            var items = await uow.Items.FindAsync(i => i.CompanyId == companyId && i.IsActive);
            Items.Clear();
            foreach (var item in items.OrderBy(i => i.Name)) Items.Add(item);

            var bankAccounts = await uow.BankAccounts.FindAsync(b => b.CompanyId == companyId && b.IsActive);
            PaymentAccounts.Clear();
            foreach (var account in bankAccounts) PaymentAccounts.Add(account);

            if (args.TicketId.HasValue)
            {
                var ticket = await uow.RepairTickets.GetByIdAsync(args.TicketId.Value)
                    ?? throw new InvalidOperationException("This ticket no longer exists.");

                _ticketId = ticket.Id;
                TicketNumber = ticket.TicketNumber;
                SelectedCustomer = ticket.CustomerId.HasValue ? Customers.FirstOrDefault(c => c.Id == ticket.CustomerId) : null;
                Device = ticket.Device;
                DeviceModel = ticket.DeviceModel;
                DeviceDescription = ticket.DeviceDescription;
                Issue = ticket.Issue;
                Status = ticket.Status;
                PriorityText = ticket.Priority.ToString();
                SerialNumber = ticket.SerialNumber;
                WarrantyDays = ticket.WarrantyDays;
                WarrantyStartDate = ticket.WarrantyStartDate ?? DateTimeOffset.Now.Date;
                Price = ticket.Price;
                SignatureImagePath = ticket.SignatureImagePath;
                _currentBalance = ticket.Balance;
                BalanceDisplay = ticket.Balance.ToString("C2");

                Lines.Clear();
                foreach (var line in ticket.Lines)
                    Lines.Add(new RepairTicketLineEditRow { ItemId = line.ItemId, Description = line.Description, Quantity = line.Quantity, UnitPrice = line.UnitPrice });

                Notes.Clear();
                foreach (var note in ticket.Notes.OrderByDescending(n => n.CreatedAtUtc)) Notes.Add(note);

                Payments.Clear();
                foreach (var payment in ticket.Payments.OrderByDescending(p => p.PaidAtUtc)) Payments.Add(payment);

                var attachments = await uow.Attachments.FindAsync(a => a.RelatedEntityType == "RepairTicket" && a.RelatedEntityId == ticket.Id);
                Photos.Clear();
                foreach (var attachment in attachments) Photos.Add(new PhotoAttachmentRow { Id = attachment.Id, FileName = attachment.FileName, FilePath = attachment.FilePath });
            }
            else
            {
                var existingCount = (await uow.RepairTickets.FindAsync(t => t.CompanyId == companyId)).Count;
                TicketNumber = $"TKT-{1001 + existingCount}";
                Status = StatusOptions.FirstOrDefault() ?? "Open";
                SelectedCustomer = args.CustomerId.HasValue ? Customers.FirstOrDefault(c => c.Id == args.CustomerId) : null;
            }

            OnPropertyChanged(nameof(IsNew));
            OnPropertyChanged(nameof(CanRecordPayment));
        });
    }

    public void AddLine(Item? item, string description, decimal quantity, decimal unitPrice)
    {
        var row = new RepairTicketLineEditRow
        {
            ItemId = item?.Id,
            Description = string.IsNullOrWhiteSpace(description) ? item?.Name ?? "Part / Service" : description,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
        Lines.Add(row);
    }

    [RelayCommand]
    private void RemoveLine(RepairTicketLineEditRow? row)
    {
        if (row != null) Lines.Remove(row);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Device)) { ErrorMessage = "Enter the device type first."; return; }
        if (string.IsNullOrWhiteSpace(Issue)) { ErrorMessage = "Describe the issue first."; return; }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            var ticket = _ticketId.HasValue
                ? await uow.RepairTickets.GetByIdAsync(_ticketId.Value) ?? throw new InvalidOperationException("This ticket no longer exists.")
                : new RepairTicket { CompanyId = ActiveCompanyId, TicketNumber = TicketNumber };

            ticket.CustomerId = SelectedCustomer?.Id;
            ticket.Device = Device.Trim();
            ticket.DeviceModel = DeviceModel;
            ticket.DeviceDescription = DeviceDescription;
            ticket.Issue = Issue.Trim();
            ticket.Status = Status;
            ticket.Priority = Enum.Parse<TicketPriority>(PriorityText);
            ticket.SerialNumber = SerialNumber;
            ticket.WarrantyDays = WarrantyDays;
            ticket.WarrantyStartDate = WarrantyDays > 0 ? WarrantyStartDate.DateTime : null;
            ticket.Price = Price;

            ticket.Lines.Clear();
            foreach (var row in Lines)
                ticket.Lines.Add(new RepairTicketLine { RepairTicketId = ticket.Id, ItemId = row.ItemId, Description = row.Description, Quantity = row.Quantity, UnitPrice = row.UnitPrice });

            await _ticketService.SaveAsync(uow, ticket);
            _ticketId = ticket.Id;
            OnPropertyChanged(nameof(IsNew));
        });
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewNoteText)) return;
        if (!_ticketId.HasValue) { ErrorMessage = "Save the ticket before adding notes."; return; }

        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _ticketService.AddNoteAsync(uow, _ticketId.Value, NewNoteText.Trim(), "You");

            var updated = await uow.RepairTickets.GetByIdAsync(_ticketId.Value);
            Notes.Clear();
            if (updated != null)
                foreach (var note in updated.Notes.OrderByDescending(n => n.CreatedAtUtc)) Notes.Add(note);

            NewNoteText = string.Empty;
        });
    }

    public async Task<bool> RecordPaymentAsync(decimal amount, DateTime paymentDate, PaymentMethod method, Guid depositToAccountId)
    {
        if (!_ticketId.HasValue) return false;

        var success = false;
        await RunBusyAsync(async () =>
        {
            using var uow = NewUnitOfWork();
            await _ticketService.RecordPaymentAsync(uow, _ticketId.Value, amount, paymentDate, method, depositToAccountId);

            var updated = await uow.RepairTickets.GetByIdAsync(_ticketId.Value);
            if (updated != null)
            {
                _currentBalance = updated.Balance;
                BalanceDisplay = updated.Balance.ToString("C2");
                Payments.Clear();
                foreach (var payment in updated.Payments.OrderByDescending(p => p.PaidAtUtc)) Payments.Add(payment);
            }
            OnPropertyChanged(nameof(CanRecordPayment));
            success = true;
        });
        return success;
    }

    public async Task<bool> AddPhotoAsync(string sourceFilePath)
    {
        if (!_ticketId.HasValue) { ErrorMessage = "Save the ticket before attaching photos."; return false; }

        var success = false;
        await RunBusyAsync(async () =>
        {
            var fileName = Path.GetFileName(sourceFilePath);
            var storedName = $"{Guid.NewGuid():N}_{fileName}";
            var destinationPath = Path.Combine(Services.AppPaths.AttachmentsFolder, storedName);
            File.Copy(sourceFilePath, destinationPath, overwrite: false);

            using var uow = NewUnitOfWork();
            var attachment = new Attachment
            {
                CompanyId = ActiveCompanyId,
                RelatedEntityType = "RepairTicket",
                RelatedEntityId = _ticketId.Value,
                FileName = fileName,
                FilePath = destinationPath,
                FileSizeBytes = new FileInfo(destinationPath).Length
            };
            await uow.Attachments.AddAsync(attachment);
            await uow.SaveChangesAsync();

            Photos.Add(new PhotoAttachmentRow { Id = attachment.Id, FileName = attachment.FileName, FilePath = attachment.FilePath });
            success = true;
        });
        return success;
    }

    public async Task<bool> SaveSignatureAsync(byte[] pngBytes)
    {
        if (!_ticketId.HasValue) { ErrorMessage = "Save the ticket before capturing a signature."; return false; }

        var success = false;
        await RunBusyAsync(async () =>
        {
            var fileName = $"signature_{_ticketId.Value:N}.png";
            var destinationPath = Path.Combine(Services.AppPaths.AttachmentsFolder, fileName);
            await File.WriteAllBytesAsync(destinationPath, pngBytes);

            using var uow = NewUnitOfWork();
            var ticket = await uow.RepairTickets.GetByIdAsync(_ticketId.Value) ?? throw new InvalidOperationException("This ticket no longer exists.");
            ticket.SignatureImagePath = destinationPath;
            uow.RepairTickets.Update(ticket);
            await uow.SaveChangesAsync();

            SignatureImagePath = destinationPath;
            success = true;
        });
        return success;
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
