using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using MoneyTalk.Core.Entities;

namespace MoneyTalk.App.Converters;

public class MessageRoleToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is AiMessageRole.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}

public class MessageRoleToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var key = value is AiMessageRole.User ? "MoneyTalkAccentBrush" : "CardStrokeColorDefaultBrush";
        return Application.Current.Resources[key];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
