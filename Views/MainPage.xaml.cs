using Microsoft.Extensions.DependencyInjection;
using StudySync.ViewModels;

namespace StudySync.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = ((App)Application.Current!).Services.GetRequiredService<MainViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (BindingContext is MainViewModel vm)
            await vm.LoadNotesAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MainViewModel vm)
            await vm.LoadNotesAsync();
    }
}
