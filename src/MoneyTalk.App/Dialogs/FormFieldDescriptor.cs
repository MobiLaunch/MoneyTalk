namespace MoneyTalk.App.Dialogs;

public abstract class FormFieldDescriptor
{
    public required string Key { get; init; }
    public required string Label { get; init; }
}

public class TextFieldDescriptor : FormFieldDescriptor
{
    public string InitialValue { get; init; } = string.Empty;
    public string? PlaceholderText { get; init; }
}

public class ComboFieldDescriptor : FormFieldDescriptor
{
    public required IReadOnlyList<string> Options { get; init; }
    public int InitialIndex { get; init; }
}

public class CheckboxFieldDescriptor : FormFieldDescriptor
{
    public bool InitialValue { get; init; }
}

public class NumberFieldDescriptor : FormFieldDescriptor
{
    public double InitialValue { get; init; }
    public double Minimum { get; init; } = 0;
    public double Maximum { get; init; } = 1_000_000_000;
}
