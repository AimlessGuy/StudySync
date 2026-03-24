using StudySync.ViewModels;

namespace StudySync.Views;

public partial class VaultPage : ContentPage
{
    public VaultPage()
    {
        InitializeComponent();
        BindingContext = new VaultViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is VaultViewModel vm)
        {
            await vm.LoadNotesAsync();
        }
    }
}