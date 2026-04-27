using StudySync.ViewModels;
using System.Text.Json;

namespace StudySync.Views;

[QueryProperty(nameof(ImagePath), "imagePath")]
[QueryProperty(nameof(ImagePaths), "imagePaths")]
[QueryProperty(nameof(SectionInviteCode), "sectionInviteCode")]
public partial class ResultPage : ContentPage
{
    public ResultPage()
    {
        InitializeComponent();
    }

    public string? ImagePath { get; set; }
    public string? ImagePaths { get; set; }
    public string? SectionInviteCode { get; set; }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        var resolvedPaths = ResolveImagePaths();
        if (resolvedPaths.Count > 0)
        {
            BindingContext = new ResultViewModel(resolvedPaths, SectionInviteCode);

            CapturedImage.Source = ImageSource.FromFile(resolvedPaths[0]);
        }
    }

    private List<string> ResolveImagePaths()
    {
        if (!string.IsNullOrWhiteSpace(ImagePaths))
        {
            try
            {
                var paths = JsonSerializer.Deserialize<List<string>>(ImagePaths);
                if (paths is { Count: > 0 })
                    return paths;
            }
            catch
            {
            }
        }

        return !string.IsNullOrWhiteSpace(ImagePath)
            ? new List<string> { ImagePath }
            : new List<string>();
    }
}
