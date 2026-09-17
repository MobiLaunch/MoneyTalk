using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MoneyTalk.App.Dialogs;

/// <summary>A single reusable "add/edit" dialog for simple master-data forms (customers,
/// vendors, items, accounts, tax rates, bank accounts, users) so each of those pages doesn't
/// need its own hand-built dialog XAML file. Built programmatically since the fields differ per
/// entity but the shape — a vertical stack of labeled inputs with Save/Cancel — never does.</summary>
public class SimpleFormDialog : ContentDialog
{
    private readonly Dictionary<string, TextBox> _textBoxes = new();
    private readonly Dictionary<string, ComboBox> _comboBoxes = new();
    private readonly Dictionary<string, CheckBox> _checkBoxes = new();
    private readonly Dictionary<string, NumberBox> _numberBoxes = new();
    private readonly Dictionary<string, DatePicker> _datePickers = new();

    public SimpleFormDialog(string title, IEnumerable<FormFieldDescriptor> fields)
    {
        Title = title;
        PrimaryButtonText = "Save";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        foreach (var field in fields)
        {
            panel.Children.Add(BuildField(field));
        }

        Content = new ScrollViewer { Content = panel, MaxHeight = 520 };
    }

    private FrameworkElement BuildField(FormFieldDescriptor field)
    {
        switch (field)
        {
            case TextFieldDescriptor text:
                var textBox = new TextBox { Header = field.Label, Text = text.InitialValue, PlaceholderText = text.PlaceholderText ?? string.Empty };
                _textBoxes[field.Key] = textBox;
                return textBox;

            case ComboFieldDescriptor combo:
                var comboBox = new ComboBox { Header = field.Label, HorizontalAlignment = HorizontalAlignment.Stretch };
                foreach (var option in combo.Options) comboBox.Items.Add(option);
                comboBox.SelectedIndex = combo.InitialIndex;
                _comboBoxes[field.Key] = comboBox;
                return comboBox;

            case CheckboxFieldDescriptor check:
                var checkBox = new CheckBox { Content = field.Label, IsChecked = check.InitialValue };
                _checkBoxes[field.Key] = checkBox;
                return checkBox;

            case NumberFieldDescriptor number:
                var numberBox = new NumberBox
                {
                    Header = field.Label,
                    Value = number.InitialValue,
                    Minimum = number.Minimum,
                    Maximum = number.Maximum,
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline
                };
                _numberBoxes[field.Key] = numberBox;
                return numberBox;

            case DateFieldDescriptor date:
                var datePicker = new DatePicker { Header = field.Label, Date = date.InitialValue };
                _datePickers[field.Key] = datePicker;
                return datePicker;

            default:
                throw new NotSupportedException($"Unknown field descriptor type {field.GetType().Name}.");
        }
    }

    public string GetText(string key) => _textBoxes[key].Text?.Trim() ?? string.Empty;
    public int GetComboIndex(string key) => _comboBoxes[key].SelectedIndex;
    public string GetComboValue(string key) => _comboBoxes[key].SelectedItem as string ?? string.Empty;
    public bool GetBool(string key) => _checkBoxes[key].IsChecked == true;
    public double GetNumber(string key) => _numberBoxes[key].Value;
    public DateTimeOffset GetDate(string key) => _datePickers[key].Date;
}
