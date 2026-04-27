namespace StudySync.Models;

public class SectionPost
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string PrimarySubjectTag { get; set; } = string.Empty;

    public string SecondaryTags { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string AuthorDeviceId { get; set; } = string.Empty;

    public string AuthorUserId { get; set; } = string.Empty;

    public string AuthorName { get; set; } = string.Empty;

    public bool IsAnonymous { get; set; }

    public int Upvotes { get; set; }

    public bool HasUpvoted { get; set; }

    public List<string> SectionInviteCodes { get; set; } = new();

    public List<string> SectionNames { get; set; } = new();

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title)
        ? PrimarySubjectTag
        : Title;

    public string PosterDisplayName => IsAnonymous
        ? "Anonymous classmate"
        : (!string.IsNullOrWhiteSpace(AuthorName) ? AuthorName : "Shared by a classmate");

    public string PreviewText => !string.IsNullOrWhiteSpace(Text)
        ? (Text.Length > 120 ? Text[..120] + "..." : Text)
        : "No text";

    public string TimeDisplay => GetRelativeTime(CreatedAt);

    public string SectionSummary => SectionNames.Count == 0
        ? "No sections"
        : string.Join(", ", SectionNames);

    public string TagSummary => string.IsNullOrWhiteSpace(SecondaryTags)
        ? "No custom tags"
        : SecondaryTags;

    public string UpvoteDisplay => Upvotes.ToString();

    public string UpvoteButtonText => $"\U0001F44D {UpvoteDisplay}";

    private static string GetRelativeTime(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dateTime.ToString("MMM dd");
    }
}
