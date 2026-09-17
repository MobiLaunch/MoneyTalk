using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using ZXing.Rendering;

namespace MoneyTalk.App.Services;

/// <summary>Sends receipts and labels to the Windows print queue via classic GDI+
/// (<see cref="System.Drawing.Printing.PrintDocument"/>) rather than the WinRT
/// Windows.Graphics.Printing/PrintManager stack. Research for this project found that stack's
/// PrintManagerInterop.GetForWindow/ShowPrintUIForWindowAsync has multiple open WindowsAppSDK and
/// microsoft-ui-xaml GitHub reports of "Invalid window handle" failures specific to unpackaged
/// desktop apps (WindowsPackageType=None, as this app is) — GDI+ printing goes through the same
/// Windows print queue/driver spooler with none of that interop risk, and this app is
/// Windows-only regardless, so the classic API costs nothing.</summary>
public static class PrintService
{
    public static IReadOnlyList<string> GetInstalledPrinterNames()
    {
        var names = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters) names.Add(name);
        return names;
    }

    /// <summary>Prints a plain itemized receipt. <paramref name="printerName"/> may be empty to
    /// use the system's default printer.</summary>
    public static void PrintReceipt(string? printerName, string title, IReadOnlyList<string> lines)
    {
        using var doc = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(printerName)) doc.PrinterSettings.PrinterName = printerName;

        using var titleFont = new Font("Consolas", 12, FontStyle.Bold);
        using var lineFont = new Font("Consolas", 10);

        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics ?? throw new InvalidOperationException("No print surface available.");
            float y = 20;
            g.DrawString(title, titleFont, Brushes.Black, 20, y);
            y += titleFont.GetHeight(g) + 10;
            foreach (var line in lines)
            {
                g.DrawString(line, lineFont, Brushes.Black, 20, y);
                y += lineFont.GetHeight(g) + 2;
            }
            e.HasMorePages = false;
        };

        doc.Print();
    }

    /// <summary>Prints a small barcode label (item SKU, ticket number, etc.) — two lines of text
    /// above a CODE128/QR barcode image, sized for a typical 2"x1" or 3"x1.5" label roll.</summary>
    public static void PrintLabel(string? printerName, string primaryText, string secondaryText, PixelData barcode)
    {
        using var doc = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(printerName)) doc.PrinterSettings.PrinterName = printerName;

        using var barcodeBitmap = ToGdiBitmap(barcode);
        using var primaryFont = new Font("Segoe UI", 10, FontStyle.Bold);
        using var secondaryFont = new Font("Segoe UI", 9);

        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics ?? throw new InvalidOperationException("No print surface available.");
            float y = 10;
            g.DrawString(primaryText, primaryFont, Brushes.Black, 10, y);
            y += primaryFont.GetHeight(g) + 2;
            g.DrawString(secondaryText, secondaryFont, Brushes.Black, 10, y);
            y += secondaryFont.GetHeight(g) + 6;
            g.DrawImage(barcodeBitmap, 10, y, barcodeBitmap.Width, barcodeBitmap.Height);
            e.HasMorePages = false;
        };

        doc.Print();
    }

    /// <summary>Copies ZXing's BGRA32 pixel bytes straight into a GDI+ bitmap — the in-memory
    /// byte layout of <see cref="PixelFormat.Format32bppArgb"/> is the same B,G,R,A order ZXing
    /// already produces, so no per-pixel conversion is needed.</summary>
    private static Bitmap ToGdiBitmap(PixelData pixelData)
    {
        var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppArgb);
        var bounds = new Rectangle(0, 0, pixelData.Width, pixelData.Height);
        var bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }
        return bitmap;
    }
}
