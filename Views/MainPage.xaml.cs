using StudySync.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace StudySync.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        TrySetBindingContext();
    }

    private void TrySetBindingContext()
    {
        if (BindingContext != null) return;

        BindingContext = Application.Current?.Handler?.MauiContext?.Services.GetService<MainViewModel>();
        System.Diagnostics.Debug.WriteLine($"BindingContext set: {BindingContext != null}");
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        TrySetBindingContext();
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