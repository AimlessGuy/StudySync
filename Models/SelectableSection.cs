using CommunityToolkit.Mvvm.ComponentModel;

namespace StudySync.Models;

public class SelectableSection : ObservableObject
{
    private bool _isSelected;

    public string Name { get; set; } = string.Empty;

    public string InviteCode { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
