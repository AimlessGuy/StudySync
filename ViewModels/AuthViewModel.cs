using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Services;

namespace StudySync.ViewModels;

public partial class AuthViewModel : ObservableObject
{
    private readonly AuthService _authService;

    public AuthViewModel(AuthService authService)
    {
        _authService = authService;
        PrimaryActionCommand = new AsyncRelayCommand(SubmitAsync);
        ToggleModeCommand = new RelayCommand(ToggleMode);
    }

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool isSignUpMode;

    [ObservableProperty]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string TitleText => IsSignUpMode ? "Create your StudySync account" : "Welcome back";

    public string SubtitleText => IsSignUpMode
        ? "Use your email so your notes, reactions, and sections can follow you across devices."
        : "Sign in to keep your sections and shared study feed connected.";

    public string PrimaryActionText => IsSignUpMode ? "Create Account" : "Sign In";

    public string TogglePromptText => IsSignUpMode
        ? "Already have an account? Sign in"
        : "Need an account? Create one";

    public bool ShowConfirmPassword => IsSignUpMode;

    public IAsyncRelayCommand PrimaryActionCommand { get; }

    public IRelayCommand ToggleModeCommand { get; }

    private async Task SubmitAsync()
    {
        if (IsBusy)
            return;

        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter both email and password.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        if (IsSignUpMode && Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        try
        {
            IsBusy = true;
            OnPropertyChanged(nameof(IsNotBusy));

            if (IsSignUpMode)
                await _authService.SignUpAsync(Email.Trim(), Password);
            else
                await _authService.SignInAsync(Email.Trim(), Password);

            ((App)Application.Current!).ShowMainApp();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsNotBusy));
        }
    }

    private void ToggleMode()
    {
        IsSignUpMode = !IsSignUpMode;
        ErrorMessage = string.Empty;
        ConfirmPassword = string.Empty;
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SubtitleText));
        OnPropertyChanged(nameof(PrimaryActionText));
        OnPropertyChanged(nameof(TogglePromptText));
        OnPropertyChanged(nameof(ShowConfirmPassword));
    }
}
