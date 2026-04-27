using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using StudySync.Services;
using StudySync.ViewModels;
using StudySync.Views;

namespace StudySync;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<ISystemNotificationService, NoOpSystemNotificationService>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddTransient<AuthViewModel>();
        builder.Services.AddTransient<AccountViewModel>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<CameraViewModel>();
        builder.Services.AddTransient<VaultViewModel>();
        builder.Services.AddTransient<NoteDetailViewModel>();
        builder.Services.AddTransient<SectionsViewModel>();
        builder.Services.AddTransient<NotificationsViewModel>();
        builder.Services.AddTransient<SharedPostDetailViewModel>();
        builder.Services.AddTransient<SectionDetailViewModel>();
        
        builder.Services.AddTransient<AuthPage>();
        builder.Services.AddTransient<AccountPage>();
        builder.Services.AddTransient<Views.MainPage>();
        builder.Services.AddTransient<CameraPage>();
        builder.Services.AddTransient<VaultPage>();
        builder.Services.AddTransient<ResultPage>(); 
        builder.Services.AddTransient<SectionsPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<NoteDetailPage>();
        builder.Services.AddTransient<SharedPostDetailPage>();
        builder.Services.AddTransient<SectionDetailPage>();

#if ANDROID
        builder.Services.AddSingleton<ISystemNotificationService, AndroidSystemNotificationService>();
#endif

        return builder.Build();
    }
}
