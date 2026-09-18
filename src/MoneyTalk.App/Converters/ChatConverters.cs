using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
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
        // Indexing the dictionary throws when a key is missing, and converters run during layout
        // where that would take the whole page down — so a missing theme brush degrades instead.
        return Application.Current.Resources.TryGetValue(key, out var brush) && brush is Brush resolved
            ? resolved
            : new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
