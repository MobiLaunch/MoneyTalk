using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Converters;

public class InsightSeverityToInfoBarSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        InsightSeverity.Critical => InfoBarSeverity.Error,
        InsightSeverity.Warning => InfoBarSeverity.Warning,
        _ => InfoBarSeverity.Informational
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
