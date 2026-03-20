using StudySync.ViewModels;

namespace StudySync.Views;

public partial class CameraPage : ContentPage
{
    public CameraPage()
    {
        InitializeComponent();
        BindingContext = new CameraViewModel();
    }
}