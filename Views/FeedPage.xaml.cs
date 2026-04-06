using StudySync.ViewModels;

namespace StudySync.Views;

public partial class FeedPage : ContentPage
{
    public FeedPage()
    {
        InitializeComponent();
        BindingContext = new FeedViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is FeedViewModel vm)
            await vm.LoadFeedAsync();
    }
}