using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using MoneyTalk.App.ViewModels;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class TicketsPage : Page
{
    public TicketsViewModel ViewModel { get; }

    public TicketsPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TicketsViewModel>();
        DataContext = ViewModel;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync();
    }

    private void Grid_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e) =>
        ViewModel.OpenSelectedCommand.Execute(null);

    private async void ExportCsv_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var headers = new[] { "Ticket #", "Customer", "Device", "Issue", "Priority", "Status", "Age (days)", "Balance" };
        var rows = ViewModel.Tickets.Select(t => new[]
        {
            t.TicketNumber, t.CustomerName, t.Device, t.Issue, t.Priority.ToString(), t.Status,
            t.AgeDays.ToString(), t.Balance.ToString("F2")
        });
        await Services.CsvExportService.SaveAsync(App.MainWindow, "tickets.csv", Services.CsvExportService.ToCsv(headers, rows));
    }
}
