using StudySync.ViewModels;

namespace StudySync.Views;

public partial class AccountPage : ContentPage
{
    public AccountPage(AccountViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is AccountViewModel vm)
            await vm.LoadAccountAsync();
    }
}
