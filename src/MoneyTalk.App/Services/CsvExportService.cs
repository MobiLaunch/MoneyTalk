using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MoneyTalk.App.Services;

/// <summary>Builds and saves plain CSV exports for list pages (customers, items, tickets). No
/// third-party CSV library — RFC 4180 quoting is a handful of lines, and .NET has no built-in CSV
/// writer worth pulling a package in for.</summary>
public static class CsvExportService
{
    public static string ToCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        return sb.ToString();
    }

    /// <summary>Shows a save-file picker and writes the CSV — <paramref name="ownerWindow"/> must
    /// be <see cref="App.MainWindow"/> so the unpackaged app's file picker can resolve an HWND.</summary>
    public static async Task<bool> SaveAsync(Microsoft.UI.Xaml.Window ownerWindow, string suggestedFileName, string csv)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedFileName };
        picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var file = await picker.PickSaveFileAsync();
        if (file == null) return false;

        await FileIO.WriteTextAsync(file, csv);
        return true;
    }

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
