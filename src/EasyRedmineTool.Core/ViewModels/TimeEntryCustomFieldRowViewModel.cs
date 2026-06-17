namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;

public partial class TimeEntryCustomFieldRowViewModel : ViewModelBase
{
    public TimeEntryCustomFieldRowViewModel(
        int id,
        string name,
        bool isRequired,
        bool hasPossibleValues,
        bool isSearchableList,
        IEnumerable<string> possibleValues,
        string value)
    {
        Id = id;
        Name = name;
        IsRequired = isRequired;
        HasPossibleValues = hasPossibleValues;
        IsSearchableList = isSearchableList;

        foreach (var possibleValue in possibleValues.Where(possibleValue => !string.IsNullOrWhiteSpace(possibleValue)))
        {
            PossibleValues.Add(possibleValue);
        }

        Value = value;
        if (HasPossibleValues && !string.IsNullOrWhiteSpace(value) && PossibleValues.Contains(value))
        {
            SelectedValue = value;
        }
    }

    public int Id { get; }

    public string Name { get; }

    public bool IsRequired { get; }

    public bool HasPossibleValues { get; }

    public bool IsSearchableList { get; }

    public bool HasCompactPossibleValues => HasPossibleValues && !IsSearchableList;

    public ObservableCollection<string> PossibleValues { get; } = [];

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string? selectedValue;

    partial void OnSelectedValueChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Value = value;
        }
    }

    partial void OnValueChanged(string value)
    {
        if (HasPossibleValues && PossibleValues.Contains(value))
        {
            SelectedValue = value;
        }
    }
}
