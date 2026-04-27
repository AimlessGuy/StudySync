using Microsoft.Extensions.DependencyInjection;
using StudySync.Services;
using StudySync.Views;

namespace StudySync;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthService _authService;

    public IServiceProvider Services => _serviceProvider;

    public App(IServiceProvider serviceProvider, AuthService authService)
    {
        _serviceProvider = serviceProvider;
        _authService = authService;

        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Page rootPage = _authService.HasSavedSession
            ? _serviceProvider.GetRequiredService<AppShell>()
            : new NavigationPage(_serviceProvider.GetRequiredService<AuthPage>());

        return new Window(rootPage);
    }

    public void ShowMainApp()
    {
        if (Current?.Windows.Count > 0)
            Current.Windows[0].Page = _serviceProvider.GetRequiredService<AppShell>();
    }

    public void ShowAuthFlow()
    {
        if (Current?.Windows.Count > 0)
            Current.Windows[0].Page = new NavigationPage(_serviceProvider.GetRequiredService<AuthPage>());
    }
}
