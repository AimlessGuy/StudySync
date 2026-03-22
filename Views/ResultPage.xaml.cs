using StudySync.ViewModels;

namespace StudySync.Views;

[QueryProperty(nameof(ImagePath), "imagePath")]
public partial class ResultPage : ContentPage
{
    public ResultPage()
    {
        InitializeComponent();
    }

    public string? ImagePath { get; set; }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        if (!string.IsNullOrEmpty(ImagePath))
        {
            // 1. Set BindingContext FIRST so all bindings (IsProcessing, OcrProgress, etc.) wire up
            BindingContext = new ResultViewModel(ImagePath);

            // 2. Load image preview using x:Name reference (safe now that InitializeComponent ran)
            CapturedImage.Source = ImageSource.FromFile(ImagePath);
        }
    }
}