using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using StudySync.Models;
using StudySync.Services;

namespace StudySync.ViewModels;

public partial class SharedPostDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly PostCloudService _postCloudService;
    private readonly DatabaseService _databaseService;
    private SectionPost? _currentPost;

    private string _postTitle = string.Empty;
    public string PostTitle
    {
        get => _postTitle;
        set => SetProperty(ref _postTitle, value);
    }

    private string _postText = string.Empty;
    public string PostText
    {
        get => _postText;
        set => SetProperty(ref _postText, value);
    }

    private string _primarySubjectTag = string.Empty;
    public string PrimarySubjectTag
    {
        get => _primarySubjectTag;
        set => SetProperty(ref _primarySubjectTag, value);
    }

    private string _sectionSummary = string.Empty;
    public string SectionSummary
    {
        get => _sectionSummary;
        set => SetProperty(ref _sectionSummary, value);
    }

    private string _tagSummary = string.Empty;
    public string TagSummary
    {
        get => _tagSummary;
        set => SetProperty(ref _tagSummary, value);
    }

    private string _createdLabel = string.Empty;
    public string CreatedLabel
    {
        get => _createdLabel;
        set => SetProperty(ref _createdLabel, value);
    }

    private bool _isAnonymous;
    public bool IsAnonymous
    {
        get => _isAnonymous;
        set => SetProperty(ref _isAnonymous, value);
    }

    private int _upvotes;
    public int Upvotes
    {
        get => _upvotes;
        set
        {
            if (SetProperty(ref _upvotes, value))
            {
                OnPropertyChanged(nameof(UpvoteDisplay));
                OnPropertyChanged(nameof(UpvoteButtonText));
            }
        }
    }

    private bool _hasUpvoted;
    public bool HasUpvoted
    {
        get => _hasUpvoted;
        set
        {
            if (SetProperty(ref _hasUpvoted, value))
                OnPropertyChanged(nameof(UpvoteButtonText));
        }
    }

    public string UpvoteDisplay => Upvotes.ToString();
    public string UpvoteButtonText => $"\U0001F44D {UpvoteDisplay}";

    public IAsyncRelayCommand SharePostCommand { get; }
    public IAsyncRelayCommand SaveToVaultCommand { get; }
    public IAsyncRelayCommand UpvotePostCommand { get; }

    public SharedPostDetailViewModel()
    {
        _postCloudService = new PostCloudService();
        _databaseService = new DatabaseService();
        SharePostCommand = new AsyncRelayCommand(SharePostAsync);
        SaveToVaultCommand = new AsyncRelayCommand(SaveToVaultAsync);
        UpvotePostCommand = new AsyncRelayCommand(UpvotePostAsync);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("postId", out var value))
        {
            var postId = value?.ToString();
            if (!string.IsNullOrWhiteSpace(postId))
                _ = LoadPostAsync(postId);
        }
    }

    private async Task LoadPostAsync(string postId)
    {
        try
        {
            var post = await _postCloudService.GetPostByIdAsync(postId);
            if (post == null)
            {
                PostTitle = "Post not found";
                PostText = "This shared post may have been removed.";
                return;
            }

            _currentPost = post;
            PostTitle = string.IsNullOrWhiteSpace(post.Title) ? post.PrimarySubjectTag : post.Title;
            PostText = post.Text;
            PrimarySubjectTag = post.PrimarySubjectTag;
            SectionSummary = post.SectionSummary;
            TagSummary = string.IsNullOrWhiteSpace(post.SecondaryTags) ? "No custom tags" : post.SecondaryTags;
            CreatedLabel = post.CreatedAt.ToString("MMM dd, yyyy h:mm tt");
            IsAnonymous = post.IsAnonymous;
            Upvotes = post.Upvotes;
            HasUpvoted = post.HasUpvoted;
        }
        catch (Exception ex)
        {
            PostTitle = "Error";
            PostText = $"Could not load the shared post: {ex.Message}";
        }
    }

    private async Task SharePostAsync()
    {
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Title = string.IsNullOrWhiteSpace(PostTitle) ? "StudySync Post" : PostTitle,
            Subject = $"{PrimarySubjectTag} shared post",
            Text = $"{PostTitle}\nSubject: {PrimarySubjectTag}\nSections: {SectionSummary}\nTags: {TagSummary}\n\n{PostText}"
        });
    }

    private async Task SaveToVaultAsync()
    {
        if (_currentPost == null)
            return;

        try
        {
            await _databaseService.SaveSharedPostToVaultAsync(_currentPost);
            await Shell.Current.DisplayAlert("Saved", "This post was added to your vault.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Save failed", $"Could not save this post: {ex.Message}", "OK");
        }
    }

    private async Task UpvotePostAsync()
    {
        if (_currentPost == null || string.IsNullOrWhiteSpace(_currentPost.Id))
            return;

        try
        {
            bool isUpvoted = await _postCloudService.UpvotePostAsync(_currentPost.Id);
            await _databaseService.SyncLikedPostAsync(_currentPost, isUpvoted);

            HasUpvoted = isUpvoted;
            Upvotes = isUpvoted
                ? Upvotes + 1
                : Math.Max(0, Upvotes - 1);

            _currentPost.Upvotes = Upvotes;
            _currentPost.HasUpvoted = isUpvoted;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Upvote failed", $"Could not upvote this post: {ex.Message}", "OK");
        }
    }
}
