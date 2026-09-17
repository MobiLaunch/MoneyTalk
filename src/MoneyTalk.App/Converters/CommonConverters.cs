using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => !(value is bool b && b);
    public object ConvertBack(object value, Type targetType, object parameter, string language) => !(value is bool b && b);
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase)) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility v && v == Visibility.Visible;
}

/// <summary>True when bound to a non-null, non-empty string. Handy for driving an
/// <c>InfoBar.IsOpen</c> off an ErrorMessage property.</summary>
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = value switch
        {
            int i => i,
            System.Collections.ICollection c => c.Count,
            _ => 0
        };
        var visible = count > 0;
        if (parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase)) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        decimal d => d.ToString("C2"),
        double d => d.ToString("C2"),
        int i => i.ToString("C0"),
        _ => "—"
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        decimal.TryParse((value as string)?.Trim('$', ' '), out var result) ? result : 0m;
}

/// <summary>Green for zero-or-positive, red for negative — used on net income, cash forecast,
/// and balance figures throughout the dashboard and reports.</summary>
public class SignedCurrencyBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var amount = value switch { decimal d => d, double d => (decimal)d, int i => i, _ => 0m };
        var key = amount < 0 ? "MoneyTalkNegativeBrush" : "MoneyTalkPositiveBrush";
        return Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

/// <summary>Visible when an inventory item's on-hand quantity has dropped to or below its reorder
/// point — always collapsed for non-inventory items, which have no reorder concept. Drives a
/// small low-stock badge rather than recoloring text, so it doesn't need to know/guess the
/// theme's default foreground brush.</summary>
public class LowStockToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is Item { Type: ItemType.Inventory } item && item.QuantityOnHand <= item.ReorderPoint
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public class ShortDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        DateTime dt => dt.ToString("MMM d, yyyy"),
        DateTimeOffset dto => dto.ToString("MMM d, yyyy"),
        _ => "—"
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
