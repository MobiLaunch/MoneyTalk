using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using MoneyTalk.App.Dialogs;
using MoneyTalk.App.ViewModels;
using MoneyTalk.Core.Entities;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace MoneyTalk.App.Views.Pages;

public sealed partial class TicketEditPage : Page
{
    public TicketEditViewModel ViewModel { get; }

    // WinUI3's InkCanvas is still experimental-only (not in the stable Windows App SDK package
    // this project targets), so signature capture is a hand-rolled pointer-driven Polyline
    // drawing on a plain Canvas instead — fully stable, no extra package needed.
    private Polyline? _currentStroke;
    private bool _isDrawing;

    public TicketEditPage()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<TicketEditViewModel>();
        DataContext = ViewModel;
    }

    private void SignatureCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDrawing = true;
        var point = e.GetCurrentPoint(SignatureCanvas).Position;
        _currentStroke = new Polyline
        {
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.Black),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        _currentStroke.Points.Add(point);
        SignatureCanvas.Children.Add(_currentStroke);
        SignatureCanvas.CapturePointer(e.Pointer);
    }

    private void SignatureCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDrawing || _currentStroke == null) return;
        _currentStroke.Points.Add(e.GetCurrentPoint(SignatureCanvas).Position);
    }

    private void SignatureCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDrawing = false;
        _currentStroke = null;
        SignatureCanvas.ReleasePointerCapture(e.Pointer);
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var args = e.Parameter as TicketEditNavigationArgs ?? new TicketEditNavigationArgs(null);
        await ViewModel.LoadAsync(args);
    }

    private async void AddLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var itemNames = ViewModel.Items.Select(i => i.Name).ToList();
        if (itemNames.Count == 0) itemNames.Add("(no parts/services — add one on Products & Services)");

        var dialog = new SimpleFormDialog("Add Line", new FormFieldDescriptor[]
        {
            new ComboFieldDescriptor { Key = "item", Label = "Part / Service", Options = itemNames },
            new TextFieldDescriptor { Key = "description", Label = "Description (optional override)" },
            new NumberFieldDescriptor { Key = "quantity", Label = "Quantity", InitialValue = 1, Minimum = 0 },
            new NumberFieldDescriptor { Key = "price", Label = "Unit price", Minimum = 0 }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var itemIndex = dialog.GetComboIndex("item");
        var item = itemIndex >= 0 && itemIndex < ViewModel.Items.Count ? ViewModel.Items[itemIndex] : null;
        var unitPrice = (decimal)dialog.GetNumber("price");
        if (unitPrice == 0 && item != null) unitPrice = item.SalesPrice;

        ViewModel.AddLine(item, dialog.GetText("description"), (decimal)dialog.GetNumber("quantity"), unitPrice);
    }

    private void RemoveLine_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: RepairTicketLineEditRow row })
            ViewModel.RemoveLineCommand.Execute(row);
    }

    private async void RecordPayment_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var accountNames = ViewModel.PaymentAccounts.Select(a => a.BankName).ToList();
        if (accountNames.Count == 0) accountNames.Add("(no bank accounts — add one on Bank Accounts)");

        var dialog = new SimpleFormDialog("Record Payment", new FormFieldDescriptor[]
        {
            new NumberFieldDescriptor { Key = "amount", Label = "Amount", Minimum = 0 },
            new ComboFieldDescriptor { Key = "method", Label = "Method", Options = Enum.GetNames<PaymentMethod>() },
            new ComboFieldDescriptor { Key = "account", Label = "Deposit to", Options = accountNames }
        })
        { XamlRoot = this.XamlRoot };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var accountIndex = dialog.GetComboIndex("account");
        if (accountIndex < 0 || accountIndex >= ViewModel.PaymentAccounts.Count) return;

        var method = Enum.Parse<PaymentMethod>(dialog.GetComboValue("method"));
        await ViewModel.RecordPaymentAsync(
            (decimal)dialog.GetNumber("amount"), DateTime.UtcNow.Date, method, ViewModel.PaymentAccounts[accountIndex].Id);
    }

    private async void AddPhoto_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        await ViewModel.AddPhotoAsync(file.Path);
    }

    private void ClearSignature_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) =>
        SignatureCanvas.Children.Clear();

    private async void SaveSignature_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SignatureCanvas.Children.Count == 0) return;

        var renderTarget = new RenderTargetBitmap();
        await renderTarget.RenderAsync(SignatureCanvas);
        var pixels = await renderTarget.GetPixelsAsync();

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)renderTarget.PixelWidth, (uint)renderTarget.PixelHeight, 96, 96, pixels.ToArray());
        await encoder.FlushAsync();

        var bytes = new byte[stream.Size];
        await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);

        await ViewModel.SaveSignatureAsync(bytes);
    }
}
