namespace EasyRedmineTool.Core.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Collections.ObjectModel;

public partial class TimeEntryCustomFieldRowViewModel : ViewModelBase
{
    private const int MaxFilteredResults = 80;
    public TimeEntryCustomFieldRowViewModel(
        int id,
        string name,
        bool isRequired,
        bool hasPossibleValues,
        bool isSearchableList,
        bool isMultiple,
        IEnumerable<string> possibleValues,
        IEnumerable<string>? selectedValues = null)
    {
        Id = id;
        Name = name;
        IsRequired = isRequired;
        HasPossibleValues = hasPossibleValues;
        SupportsSearchableList = isSearchableList;
        IsMultiple = isMultiple;

        foreach (var possibleValue in possibleValues.Where(possibleValue => !string.IsNullOrWhiteSpace(possibleValue)))
        {
            PossibleValues.Add(possibleValue);
        }

        foreach (var selectedValue in selectedValues ?? [])
        {
            if (!string.IsNullOrWhiteSpace(selectedValue) && !SelectedValues.Contains(selectedValue))
            {
                SelectedValues.Add(selectedValue);
            }
        }

        SelectedValues.CollectionChanged += (_, _) => SyncSelectedValueChips();
        SyncSelectedValueChips();
        SyncSingleValueFromSelection();
        RefreshFilteredPossibleValues();
    }

    public int Id { get; }

    public string Name { get; }

    public bool IsRequired { get; }

    public bool HasPossibleValues { get; }

    public bool IsMultiple { get; }

    public bool SupportsSearchableList { get; }

    public bool HasCompactPossibleValues => HasPossibleValues && !SupportsSearchableList && !IsMultiple;

    public bool IsSearchableList => HasPossibleValues && SupportsSearchableList && !IsMultiple;

    public bool HasMultipleSearchableList => HasPossibleValues && SupportsSearchableList && IsMultiple;

    public bool HasMultipleCompactList => HasPossibleValues && !SupportsSearchableList && IsMultiple;

    public bool ShowSearchablePicker => HasPossibleValues && SupportsSearchableList;

    public ObservableCollection<string> PossibleValues { get; } = [];

    public ObservableCollection<string> FilteredPossibleValues { get; } = [];

    public ObservableCollection<string> SelectedValues { get; } = [];

    public ObservableCollection<TimeEntryCustomFieldSelectedChipViewModel> SelectedValueChips { get; } = [];

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string? selectedValue;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string? selectedSearchResult;

    partial void OnSearchTextChanged(string value) => RefreshFilteredPossibleValues();

    partial void OnSelectedSearchResultChanged(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (IsMultiple)
        {
            TryAddSelectedValue(value);
            SearchText = string.Empty;
        }
        else
        {
            Value = value;
            SelectedValue = value;
            SearchText = value;
        }

        SelectedSearchResult = null;
    }

    partial void OnSelectedValueChanged(string? value)
    {
        if (!IsMultiple && !string.IsNullOrWhiteSpace(value))
        {
            Value = value;
        }
    }

    partial void OnValueChanged(string value)
    {
        if (!IsMultiple && HasPossibleValues && PossibleValues.Contains(value))
        {
            SelectedValue = value;
        }
    }

    [RelayCommand]
    private void AddSearchSelection()
    {
        TryAddSelectedValue(SearchText);
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void RemoveSelectedValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        SelectedValues.Remove(value);
        SyncSingleValueFromSelection();
    }

    [RelayCommand]
    private void ToggleCompactSelection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (SelectedValues.Contains(value))
        {
            SelectedValues.Remove(value);
        }
        else
        {
            SelectedValues.Add(value);
        }

        SyncSingleValueFromSelection();
    }

    public bool IsCompactValueSelected(string value) => SelectedValues.Contains(value);

    internal void RemoveChipValue(string value) => RemoveSelectedValue(value);

    private void SyncSelectedValueChips()
    {
        SelectedValueChips.Clear();
        foreach (var value in SelectedValues)
        {
            SelectedValueChips.Add(new TimeEntryCustomFieldSelectedChipViewModel(this, value));
        }
    }

    private void TryAddSelectedValue(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var trimmed = candidate.Trim();
        if (HasPossibleValues && !PossibleValues.Any(value => string.Equals(value, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var existing = SelectedValues.FirstOrDefault(value => string.Equals(value, trimmed, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return;
        }

        var canonical = PossibleValues.FirstOrDefault(value => string.Equals(value, trimmed, StringComparison.OrdinalIgnoreCase)) ?? trimmed;
        SelectedValues.Add(canonical);
        SyncSingleValueFromSelection();
    }

    private void RefreshFilteredPossibleValues()
    {
        FilteredPossibleValues.Clear();

        if (!ShowSearchablePicker)
        {
            return;
        }

        var query = SearchText.Trim();
        IEnumerable<string> source = PossibleValues;
        if (!string.IsNullOrEmpty(query))
        {
            source = PossibleValues.Where(value => value.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var value in source.Take(MaxFilteredResults))
        {
            FilteredPossibleValues.Add(value);
        }
    }

    private void SyncSingleValueFromSelection()
    {
        if (IsMultiple)
        {
            return;
        }

        Value = SelectedValues.FirstOrDefault() ?? string.Empty;
        SelectedValue = Value;
    }
}

public sealed partial class TimeEntryCustomFieldSelectedChipViewModel(TimeEntryCustomFieldRowViewModel parent, string value)
{
    public string Value { get; } = value;

    [RelayCommand]
    private void Remove() => parent.RemoveChipValue(Value);
}
