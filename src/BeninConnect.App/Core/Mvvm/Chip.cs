using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BeninConnect.App.Core.Mvvm;

/// <summary>Puce de filtre ou de sélection (catégories, centres d'intérêt, périodes...).</summary>
public sealed partial class Chip : ObservableObject
{
    private readonly Action<Chip> _onSelected;

    public Chip(string key, string label, Action<Chip> onSelected, bool isSelected = false)
    {
        Key = key;
        Label = label;
        _onSelected = onSelected;
        _isSelected = isSelected;
    }

    public string Key { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;

    [RelayCommand]
    private void Select() => _onSelected(this);

    /// <summary>Sélection exclusive dans une liste de puces.</summary>
    public static void SelectExclusive(IEnumerable<Chip> chips, Chip selected)
    {
        foreach (var chip in chips)
        {
            chip.IsSelected = ReferenceEquals(chip, selected);
        }
    }
}
