using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace MoneyTalk.App.Services;

/// <summary>Parses CSV back into header-keyed rows — the counterpart to
/// <see cref="CsvExportService"/>. Hand-rolled RFC 4180 parsing (quoted fields, doubled-quote
/// escaping, embedded commas/newlines) since .NET has no built-in CSV reader and a shop's export
/// from another system won't necessarily match this app's own column order.</summary>
public static class CsvImportService
{
    /// <summary>Shows an open-file picker and parses the chosen CSV, or returns null if the user
    /// cancelled. <paramref name="ownerWindow"/> must be <see cref="App.MainWindow"/> so the
    /// unpackaged app's file picker can resolve an HWND.</summary>
    public static async Task<List<Dictionary<string, string>>?> PickAndParseAsync(Microsoft.UI.Xaml.Window ownerWindow)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var file = await picker.PickSingleFileAsync();
        if (file == null) return null;

        var text = await FileIO.ReadTextAsync(file);
        return ParseCsv(text);
    }

    /// <summary>Returns each data row as a header-name-keyed (case-insensitive) dictionary. The
    /// first row is always treated as the header row.</summary>
    public static List<Dictionary<string, string>> ParseCsv(string csvText)
    {
        var rows = ParseRows(csvText);
        var result = new List<Dictionary<string, string>>();
        if (rows.Count == 0) return result;

        var headers = rows[0];
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headers.Count; c++)
                record[headers[c].Trim()] = c < row.Count ? row[c] : string.Empty;
            result.Add(record);
        }

        return result;
    }

    /// <summary>Returns the first non-blank value found under any of <paramref name="keys"/> —
    /// lets a caller accept a few header-name spellings for the same column.</summary>
    public static string? Get(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return null;
    }

    private static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                // Accept all common line endings. When CRLF is encountered, consume the LF here
                // so it produces exactly one record boundary; a lone CR must also end its row.
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
