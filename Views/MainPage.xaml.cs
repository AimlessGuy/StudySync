using StudySync.ViewModels;
using StudySync.Services;

namespace StudySync.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        // Create MainViewModel directly with a new DatabaseService
        BindingContext = new MainViewModel(new DatabaseService());
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