using StudySync.ViewModels;

namespace StudySync.Views;

public partial class SectionsPage : ContentPage
{
    public SectionsPage()
    {
        InitializeComponent();
        BindingContext = new SectionsViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SectionsViewModel vm)
            await vm.LoadSectionsAsync();
    }
}
