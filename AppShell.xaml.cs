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
        Routing.RegisterRoute(nameof(Views.FeedPage), typeof(Views.FeedPage));
        Routing.RegisterRoute(nameof(Views.SectionsPage), typeof(Views.SectionsPage));
        Routing.RegisterRoute(nameof(Views.NotificationsPage), typeof(Views.NotificationsPage));
        Routing.RegisterRoute(nameof(Views.SharedPostDetailPage), typeof(Views.SharedPostDetailPage));
        Routing.RegisterRoute(nameof(Views.SectionDetailPage), typeof(Views.SectionDetailPage));
        Routing.RegisterRoute(nameof(Views.AccountPage), typeof(Views.AccountPage));
    }
}
