using StudySync.ViewModels;

namespace StudySync.Views;

public partial class NoteDetailPage : ContentPage
{
    public NoteDetailPage()
    {
        InitializeComponent();
        BindingContext = new NoteDetailViewModel();
    }
}