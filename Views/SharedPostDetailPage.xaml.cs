using StudySync.ViewModels;

namespace StudySync.Views;

public partial class SharedPostDetailPage : ContentPage
{
    public SharedPostDetailPage()
    {
        InitializeComponent();
        BindingContext = new SharedPostDetailViewModel();
    }
}
