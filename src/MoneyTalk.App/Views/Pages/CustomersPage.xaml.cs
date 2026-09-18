using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class CustomersPage : Page
{
    public CustomersViewModel ViewModel { get; }

    public CustomersPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<CustomersViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private async void AddCustomer_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new SimpleFormDialog("Add Customer", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Customer name" },
            new TextFieldDescriptor { Key = "email", Label = "Email" },
            new TextFieldDescriptor { Key = "phone", Label = "Phone" },
            new NumberFieldDescriptor { Key = "terms", Label = "Payment terms (days)", InitialValue = 30, Minimum = 0, Maximum = 365 },
            new CheckboxFieldDescriptor { Key = "taxExempt", Label = "Tax exempt" },
            new TextFieldDescriptor { Key = "driversLicense", Label = "Driver's license (optional)" },
            new TextFieldDescriptor { Key = "tags", Label = "Tags (comma-separated, optional)" }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.AddCustomerAsync(
            dialog.GetText("name"), dialog.GetText("email"), dialog.GetText("phone"),
            (int)dialog.GetNumber("terms"), dialog.GetBool("taxExempt"),
            dialog.GetText("driversLicense"), dialog.GetText("tags"));
    }

    private async void ViewTickets_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var customer = ViewModel.SelectedCustomer;
        if (customer == null) return;

        var tickets = await ViewModel.GetCustomerTicketsAsync(customer.Id);

        var panel = new StackPanel { Spacing = 8, MinWidth = 420 };
        if (tickets.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "This customer has no repair tickets yet." });
        }
        else
        {
            foreach (var ticket in tickets)
            {
                panel.Children.Add(new Border
                {
                    Style = (Style)Application.Current.Resources["CardBorderStyle"],
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = $"{ticket.TicketNumber} — {ticket.Device}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                            new TextBlock { Text = $"{ticket.Issue} · {ticket.Status} · Balance {ticket.Balance:C2}" }
                        }
                    }
                });
            }
        }

        var dialog = new ContentDialog
        {
            Title = $"Tickets for {customer.Name}",
            Content = new ScrollViewer { Content = panel, MaxHeight = 480 },
            CloseButtonText = "Close",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void CustomersGrid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        var customer = ViewModel.SelectedCustomer;
        if (customer == null) return;

        var dialog = new SimpleFormDialog("Edit Customer", new FormFieldDescriptor[]
        {
            new TextFieldDescriptor { Key = "name", Label = "Customer name", InitialValue = customer.Name },
            new TextFieldDescriptor { Key = "email", Label = "Email", InitialValue = customer.Email ?? string.Empty },
            new TextFieldDescriptor { Key = "phone", Label = "Phone", InitialValue = customer.Phone ?? string.Empty },
            new NumberFieldDescriptor { Key = "terms", Label = "Payment terms (days)", InitialValue = customer.PaymentTermsDays, Minimum = 0, Maximum = 365 },
            new CheckboxFieldDescriptor { Key = "taxExempt", Label = "Tax exempt", InitialValue = customer.TaxExempt },
            new CheckboxFieldDescriptor { Key = "isActive", Label = "Active", InitialValue = customer.IsActive },
            new TextFieldDescriptor { Key = "driversLicense", Label = "Driver's license (optional)", InitialValue = customer.DriversLicense ?? string.Empty },
            new TextFieldDescriptor { Key = "tags", Label = "Tags (comma-separated, optional)", InitialValue = customer.Tags ?? string.Empty }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        await ViewModel.EditCustomerAsync(
            customer.Id, dialog.GetText("name"), dialog.GetText("email"), dialog.GetText("phone"),
            (int)dialog.GetNumber("terms"), dialog.GetBool("taxExempt"), dialog.GetBool("isActive"),
            dialog.GetText("driversLicense"), dialog.GetText("tags"));
    }

    private async void ExportCsv_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var headers = new[] { "Name", "Email", "Phone", "Payment Terms (days)", "Tags", "Balance" };
        var rows = ViewModel.Customers.Select(c => new[]
        {
            c.Name, c.Email, c.Phone, c.PaymentTermsDays.ToString(), c.Tags, c.Balance.ToString("F2")
        });
        await Services.CsvExportService.SaveAsync(App.MainWindow, "customers.csv", Services.CsvExportService.ToCsv(headers, rows));
    }

    private async void ImportCsv_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var rows = await Services.CsvImportService.PickAndParseAsync(App.MainWindow);
        if (rows == null) return;

        var (imported, updated, skipped) = await ViewModel.ImportCustomersAsync(rows);

        var dialog = new ContentDialog
        {
            Title = "Import complete",
            Content = $"Added {imported} new customer(s), updated {updated} existing (matched by email), skipped {skipped} row(s) with no name.",
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}
