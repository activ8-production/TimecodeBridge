using CommunityToolkit.Mvvm.ComponentModel;

namespace TimecodeBridge.ViewModels;

public class HostSelection : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
