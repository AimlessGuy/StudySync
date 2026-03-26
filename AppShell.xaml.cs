namespace StudySync;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register all push-navigation pages here
        // These will NOT appear as tabs — they are navigated to with GoToAsync()
        Routing.RegisterRoute(nameof(Views.CameraPage), typeof(Views.CameraPage));
        Routing.RegisterRoute(nameof(Views.VaultPage), typeof(Views.VaultPage));
        Routing.RegisterRoute(nameof(Views.ResultPage), typeof(Views.ResultPage));
        Routing.RegisterRoute(nameof(Views.NoteDetailPage), typeof(Views.NoteDetailPage));
    }
}