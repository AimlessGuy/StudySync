using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class SectionDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _databaseService;
    private readonly PostCloudService _postCloudService;
    private readonly SectionCloudService _sectionCloudService;
    private readonly List<SectionPost> _allPosts = new();
    private const string AllSubjectsOption = "All subjects";

    public ObservableCollection<SectionPost> Posts { get; } = new();
    public ObservableCollection<string> SubjectFilters { get; } = new() { AllSubjectsOption };

    private string _sectionName = "Section";
    public string SectionName
    {
        get => _sectionName;
        set => SetProperty(ref _sectionName, value);
    }

    private string _inviteCode = string.Empty;
    public string InviteCode
    {
        get => _inviteCode;
        set => SetProperty(ref _inviteCode, value);
    }

    private int _postCount;
    public int PostCount
    {
        get => _postCount;
        set => SetProperty(ref _postCount, value);
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

    public SectionDetailViewModel()
    {
        _databaseService = new DatabaseService();
        _postCloudService = new PostCloudService();
        _sectionCloudService = new SectionCloudService();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("sectionName", out var sectionName))
            SectionName = Uri.UnescapeDataString(sectionName?.ToString() ?? "Section");

        if (query.TryGetValue("inviteCode", out var inviteCode))
        {
            InviteCode = Uri.UnescapeDataString(inviteCode?.ToString() ?? string.Empty);
            _ = LoadPostsAsync();
        }
    }

    [RelayCommand]
    public async Task LoadPostsAsync()
    {
        try
        {
            Posts.Clear();
            _allPosts.Clear();

            if (string.IsNullOrWhiteSpace(InviteCode))
            {
                PostCount = 0;
                return;
            }

            var section = await GetJoinedSectionAsync(InviteCode);
            if (section == null)
            {
                PostCount = 0;
                return;
            }

            var posts = await _postCloudService.GetPostsForSectionsAsync(new[] { section });
            _allPosts.AddRange(posts);
            RebuildSubjectFilters(posts.Select(post => post.PrimarySubjectTag));
            ApplyFilters();
        }
        catch (Exception ex)
        {
            Posts.Clear();
            _allPosts.Clear();
            PostCount = 0;
            await Shell.Current.DisplayAlert("Section error", $"Could not load posts for this section: {ex.Message}", "OK");
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
            await LoadPostsAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Upvote failed", $"Could not upvote this post: {ex.Message}", "OK");
        }
    }

    private async Task SavePostToVaultAsync(SectionPost post)
    {
        try
        {
            await _databaseService.SaveSharedPostToVaultAsync(post);
            await Shell.Current.DisplayAlert("Saved", "This post was added to your vault.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Save failed", $"Could not save this post to your vault: {ex.Message}", "OK");
        }
    }

    private static async Task SharePostExternallyAsync(SectionPost post)
    {
        var title = string.IsNullOrWhiteSpace(post.Title) ? "StudySync Post" : post.Title;

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = title,
            Subject = $"{post.PrimarySubjectTag} shared post",
            Text = $"{title}\nSubject: {post.PrimarySubjectTag}\nSections: {post.SectionSummary}\nTags: {post.TagSummary}\n\n{post.Text}"
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

    private void ApplyFilters()
    {
        var filteredPosts = _allPosts
            .Where(post => SelectedSubjectFilter == AllSubjectsOption ||
                           string.Equals(post.PrimarySubjectTag, SelectedSubjectFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Posts.Clear();
        foreach (var post in filteredPosts)
            Posts.Add(post);

        PostCount = Posts.Count;
    }

    private async Task<Section?> GetJoinedSectionAsync(string inviteCode)
    {
        try
        {
            var sections = await _sectionCloudService.GetJoinedSectionsAsync();
            foreach (var section in sections)
                await _databaseService.SaveSectionAsync(section);

            return sections.FirstOrDefault(section =>
                string.Equals(section.InviteCode, inviteCode, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return await _databaseService.GetSectionByInviteCodeAsync(inviteCode);
        }
    }
}
