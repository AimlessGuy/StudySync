using CommunityToolkit.Mvvm.ComponentModel;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class FeedViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly PostCloudService _postCloudService;

    private ObservableCollection<SectionPost> _sharedPosts = new();
    public ObservableCollection<SectionPost> SharedPosts
    {
        get => _sharedPosts;
        set => SetProperty(ref _sharedPosts, value);
    }

    private int _sharedCount;
    public int SharedCount
    {
        get => _sharedCount;
        set => SetProperty(ref _sharedCount, value);
    }

    public FeedViewModel()
    {
        _databaseService = new DatabaseService();
        _postCloudService = new PostCloudService();
    }

    public async Task LoadFeedAsync()
    {
        try
        {
            var joinedSections = await _databaseService.GetSectionsAsync();
            var posts = await _postCloudService.GetPostsForSectionsAsync(joinedSections);

            SharedPosts.Clear();
            foreach (var post in posts)
                SharedPosts.Add(post);

            SharedCount = SharedPosts.Count;
        }
        catch (Exception ex)
        {
            SharedPosts.Clear();
            SharedCount = 0;

            await Shell.Current.DisplayAlert(
                "Feed error",
                $"Could not load shared posts right now: {ex.Message}",
                "OK");
        }
    }
}
