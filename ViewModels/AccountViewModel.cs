using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Services;

namespace StudySync.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    private readonly AuthService _authService;

    public AccountViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string displayName = "Student";

    [ObservableProperty]
    private string userId = string.Empty;

    [ObservableProperty]
    private bool isSignedIn;

    [RelayCommand]
    public async Task LoadAccountAsync()
    {
        var session = await _authService.GetCurrentSessionAsync();
        if (session == null)
        {
            IsSignedIn = false;
            Email = string.Empty;
            DisplayName = "Student";
            UserId = string.Empty;
            return;
        }

        IsSignedIn = true;
        Email = session.Email;
        DisplayName = session.DisplayName;
        UserId = session.LocalId;
    }

    [RelayCommand]
    public async Task SignOutAsync()
    {
        await _authService.SignOutAsync();
        ((App)Application.Current!).ShowAuthFlow();
    }
}
