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
            .UseMauiCommunityToolkit() // <-- ADD THIS LINE
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services
        builder.Services.AddSingleton<DatabaseService>();

        // Register ViewModels
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<CameraViewModel>();
        builder.Services.AddTransient<VaultViewModel>();
        builder.Services.AddTransient<NoteDetailViewModel>();
        
        // Register Views
        builder.Services.AddTransient<Views.MainPage>();
        builder.Services.AddTransient<CameraPage>();
        builder.Services.AddTransient<VaultPage>();
        builder.Services.AddTransient<ResultPage>(); 
        
        builder.Services.AddTransient<NoteDetailPage>();

        return builder.Build();
    }
}