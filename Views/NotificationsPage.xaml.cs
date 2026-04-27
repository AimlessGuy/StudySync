using Microsoft.Extensions.DependencyInjection;
using StudySync.ViewModels;

namespace StudySync.Views;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage()
    {
        InitializeComponent();
        BindingContext = ((App)Application.Current!).Services.GetRequiredService<NotificationsViewModel>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is NotificationsViewModel vm)
            await vm.LoadNotificationsAsync();
    }
}
