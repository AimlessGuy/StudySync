using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class FeedViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly PostCloudService _postCloudService;
    private readonly SectionCloudService _sectionCloudService;
    private readonly NotificationService _notificationService;
    private readonly List<SectionPost> _allSharedPosts = new();

    public ObservableCollection<string> SubjectFilters { get; } = new() { AllSubjectsOption };
    public ObservableCollection<string> SectionFilters { get; } = new() { AllSectionsOption };
    private const string AllSubjectsOption = "All subjects";
    private const string AllSectionsOption = "All sections";

    private ObservableCollection<SectionPost> _sharedPosts = new();
    public ObservableCollection<SectionPost> SharedPosts
    {
        get => _sharedPosts;
        set => SetProperty(ref _sharedPosts, value);
    }

    private string _selectedSubjectFilter = AllSubjectsOption;
    public string SelectedSubjectFilter
    {
        get => _selectedSubjectFilter;
        set
        {
            if (SetProperty(ref _selectedSubjectFilter, value))
                ApplyFilters();
        }
    }

    private string _selectedSectionFilter = AllSectionsOption;
    public string SelectedSectionFilter
    {
        get => _selectedSectionFilter;
        set
        {
            if (SetProperty(ref _selectedSectionFilter, value))
                ApplyFilters();
        }
    }

    private int _sharedCount;
    public int SharedCount
    {
        get => _sharedCount;
        set => SetProperty(ref _sharedCount, value);
    }

    private string _peerAlertSummary = string.Empty;
    public string PeerAlertSummary
    {
        get => _peerAlertSummary;
        set
        {
            if (SetProperty(ref _peerAlertSummary, value))
                OnPropertyChanged(nameof(HasPeerAlert));
        }
    }

    public bool HasPeerAlert => !string.IsNullOrWhiteSpace(PeerAlertSummary);

    private string _trendingSummary = string.Empty;
    public string TrendingSummary
    {
        get => _trendingSummary;
        set
        {
            if (SetProperty(ref _trendingSummary, value))
                OnPropertyChanged(nameof(HasTrendingSummary));
        }
    }

    public bool HasTrendingSummary => !string.IsNullOrWhiteSpace(TrendingSummary);

    public FeedViewModel()
    {
        _databaseService = new DatabaseService();
        _postCloudService = new PostCloudService();
        _sectionCloudService = new SectionCloudService();
        _notificationService = new NotificationService();
    }

    public async Task LoadFeedAsync()
    {
        try
        {
            var joinedSections = await GetJoinedSectionsAsync();
            var posts = await _postCloudService.GetPostsForSectionsAsync(joinedSections);

            _allSharedPosts.Clear();
            _allSharedPosts.AddRange(posts);
            await _notificationService.SyncPeerAlertAsync(posts);
            RebuildSubjectFilters(posts.Select(post => post.PrimarySubjectTag));
            RebuildSectionFilters(joinedSections.Select(section => section.Name));
            UpdateInsights();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _allSharedPosts.Clear();
            SharedPosts.Clear();
            SharedCount = 0;
            SectionFilters.Clear();
            SectionFilters.Add(AllSectionsOption);
            PeerAlertSummary = string.Empty;
            TrendingSummary = string.Empty;

            await Shell.Current.DisplayAlert(
                "Feed error",
                $"Could not load shared posts right now: {ex.Message}",
                "OK");
        }
    }

    [RelayCommand]
    public async Task OpenPostAsync(SectionPost? post)
    {
        if (post == null || string.IsNullOrWhiteSpace(post.Id))
            return;

        await Shell.Current.GoToAsync($"{nameof(Views.SharedPostDetailPage)}?postId={Uri.EscapeDataString(post.Id)}");
    }

    [RelayCommand]
    public async Task SavePostToVaultAsync(SectionPost? post)
    {
        if (post == null)
            return;

        try
        {
            await _databaseService.SaveSharedPostToVaultAsync(post);

            await Shell.Current.DisplayAlert(
                "Saved",
                "This post was added to your vault.",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Save failed",
                $"Could not save this post to your vault: {ex.Message}",
                "OK");
        }
    }

    [RelayCommand]
    public async Task ShowPostActionsAsync(SectionPost? post)
    {
        if (post == null)
            return;

        var action = await Shell.Current.DisplayActionSheet(
            "Post Actions",
            "Cancel",
            null,
            "Save to My Vault",
            "Share Externally");

        switch (action)
        {
            case "Save to My Vault":
                await SavePostToVaultAsync(post);
                break;
            case "Share Externally":
                await SharePostExternallyAsync(post);
                break;
        }
    }

    [RelayCommand]
    public async Task UpvotePostAsync(SectionPost? post)
    {
        if (post == null || string.IsNullOrWhiteSpace(post.Id))
            return;

        try
        {
            bool isUpvoted = await _postCloudService.UpvotePostAsync(post.Id);
            await _databaseService.SyncLikedPostAsync(post, isUpvoted);
            await LoadFeedAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Upvote failed", $"Could not upvote this post: {ex.Message}", "OK");
        }
    }

    private static async Task SharePostExternallyAsync(SectionPost post)
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = string.IsNullOrWhiteSpace(post.Title) ? "StudySync Post" : post.Title,
            Subject = $"{post.PrimarySubjectTag} shared post",
            Text = $"{post.Title}\nSubject: {post.PrimarySubjectTag}\nSections: {post.SectionSummary}\nTags: {post.TagSummary}\n\n{post.Text}"
        });
    }

    private void RebuildSubjectFilters(IEnumerable<string?> subjects)
    {
        var distinctSubjects = subjects
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(subject => subject)
            .ToList();

        SubjectFilters.Clear();
        SubjectFilters.Add(AllSubjectsOption);
        foreach (var subject in distinctSubjects)
            SubjectFilters.Add(subject!);

        if (!SubjectFilters.Contains(SelectedSubjectFilter))
            SelectedSubjectFilter = AllSubjectsOption;
    }

    private void RebuildSectionFilters(IEnumerable<string?> sections)
    {
        var distinctSections = sections
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(section => section)
            .ToList();

        SectionFilters.Clear();
        SectionFilters.Add(AllSectionsOption);
        foreach (var section in distinctSections)
            SectionFilters.Add(section!);

        if (!SectionFilters.Contains(SelectedSectionFilter))
            SelectedSectionFilter = AllSectionsOption;
    }

    private void ApplyFilters()
    {
        var filteredPosts = _allSharedPosts
            .Where(post =>
                (SelectedSubjectFilter == AllSubjectsOption ||
                 string.Equals(post.PrimarySubjectTag, SelectedSubjectFilter, StringComparison.OrdinalIgnoreCase)) &&
                (SelectedSectionFilter == AllSectionsOption ||
                 post.SectionNames.Any(section =>
                     string.Equals(section, SelectedSectionFilter, StringComparison.OrdinalIgnoreCase))))
            .ToList();

        SharedPosts.Clear();
        foreach (var post in filteredPosts)
            SharedPosts.Add(post);

        SharedCount = SharedPosts.Count;
    }

    private void UpdateInsights()
    {
        var now = DateTime.Now;
        var todayPosts = _allSharedPosts
            .Where(post => post.CreatedAt.Date == now.Date)
            .ToList();

        if (todayPosts.Count == 0)
        {
            PeerAlertSummary = "No new section notes have been shared today yet.";
        }
        else
        {
            int peerCount = todayPosts
                .Select(post => post.AuthorDeviceId)
                .Where(deviceId => !string.IsNullOrWhiteSpace(deviceId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            string topTodaySubject = todayPosts
                .Where(post => !string.IsNullOrWhiteSpace(post.PrimarySubjectTag))
                .GroupBy(post => post.PrimarySubjectTag)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key)
                .Select(group => group.Key)
                .FirstOrDefault() ?? "class notes";

            PeerAlertSummary = peerCount <= 1
                ? $"Someone shared {topTodaySubject} notes today."
                : $"{peerCount} classmates shared {topTodaySubject} notes today.";
        }

        var recentPosts = _allSharedPosts
            .Where(post => post.CreatedAt >= now.AddDays(-7))
            .ToList();

        if (recentPosts.Count == 0)
        {
            TrendingSummary = "No trending subject yet. Share a note to kick things off.";
            return;
        }

        var topSubjectGroup = recentPosts
            .Where(post => !string.IsNullOrWhiteSpace(post.PrimarySubjectTag))
            .GroupBy(post => post.PrimarySubjectTag)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Sum(post => post.Upvotes))
            .ThenBy(group => group.Key)
            .FirstOrDefault();

        if (topSubjectGroup == null)
        {
            TrendingSummary = "No trending subject yet. Share a note to kick things off.";
            return;
        }

        int postCount = topSubjectGroup.Count();
        int upvoteCount = topSubjectGroup.Sum(post => post.Upvotes);
        TrendingSummary = upvoteCount > 0
            ? $"Trending: {topSubjectGroup.Key} with {postCount} posts and {upvoteCount} upvotes this week."
            : $"Trending: {topSubjectGroup.Key} with {postCount} posts this week.";
    }

    private async Task<List<Section>> GetJoinedSectionsAsync()
    {
        try
        {
            var sections = await _sectionCloudService.GetJoinedSectionsAsync();
            foreach (var section in sections)
                await _databaseService.SaveSectionAsync(section);

            return sections;
        }
        catch
        {
            return await _databaseService.GetSectionsAsync();
        }
    }
}
