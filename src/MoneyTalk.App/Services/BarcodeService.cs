using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace MoneyTalk.App.Services;

public enum LabelBarcodeFormat
{
    Code128,
    QrCode
}

/// <summary>Generates CODE128/QR label images via ZXing.Net's pixel-data writer. This produces
/// raw BGRA32 bytes with no System.Drawing dependency, so the same <see cref="PixelData"/> can
/// feed either an on-screen <see cref="WriteableBitmap"/> (here) or a GDI+ bitmap for printing
/// (see <see cref="PrintService"/>) without encoding the barcode twice.</summary>
public static class BarcodeService
{
    public static PixelData Encode(string content, LabelBarcodeFormat format, int width, int height)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = format == LabelBarcodeFormat.QrCode ? BarcodeFormat.QR_CODE : BarcodeFormat.CODE_128,
            Options = new EncodingOptions { Width = width, Height = height, Margin = 2 }
        };
        return writer.Write(content);
    }

    public static WriteableBitmap ToWriteableBitmap(PixelData pixelData)
    {
        var bitmap = new WriteableBitmap(pixelData.Width, pixelData.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(pixelData.Pixels, 0, pixelData.Pixels.Length);
        }
        bitmap.Invalidate();
        return bitmap;
    }
}
