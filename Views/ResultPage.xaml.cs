using StudySync.ViewModels;

namespace StudySync.Views;

[QueryProperty(nameof(ImagePath), "imagePath")]
public partial class ResultPage : ContentPage
{
    public ResultPage()
    {
        InitializeComponent();
    }

    public string? ImagePath { get; set; } // Make it nullable

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (!string.IsNullOrEmpty(ImagePath))
        {
            // Load the image
            CapturedImage.Source = ImageSource.FromFile(ImagePath);

            // Set the ViewModel with the image path
            BindingContext = new ResultViewModel(ImagePath);
        }
    }
}