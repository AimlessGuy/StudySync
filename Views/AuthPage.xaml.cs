using StudySync.ViewModels;

namespace StudySync.Views;

public partial class AuthPage : ContentPage
{
    public AuthPage(AuthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
