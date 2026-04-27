using StudySync.ViewModels;

namespace StudySync.Views;

public partial class SectionDetailPage : ContentPage
{
    public SectionDetailPage()
    {
        InitializeComponent();
        BindingContext = new SectionDetailViewModel();
    }
}
