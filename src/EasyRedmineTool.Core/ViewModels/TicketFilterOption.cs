namespace EasyRedmineTool.Core.ViewModels;

public sealed class TicketFilterOption<T>
{
    public TicketFilterOption(string label, T value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }

    public T Value { get; }
}
