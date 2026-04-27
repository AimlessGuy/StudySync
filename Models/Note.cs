using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;
using System;

namespace StudySync.Models;

public class Note : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; } = "Untitled Note";

    public string? ImagePath { get; set; }

    public string? ExtractedText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsShared { get; set; }

    public bool IsAnonymous { get; set; }

    public string SourcePostId { get; set; } = string.Empty;

    public bool IsLiked { get; set; }

    public bool IsVaultSaved { get; set; } = true;

    private int _upvotes;
    public int Upvotes
    {
        get => _upvotes;
        set => SetProperty(ref _upvotes, value);
    }

    public string CourseCode { get; set; } = "Unknown";

    public string ContentType { get; set; } = "Notes";

    public string PrimarySubjectTag { get; set; } = string.Empty;

    public string SecondaryTags { get; set; } = string.Empty;

    public string PreviewText => !string.IsNullOrEmpty(ExtractedText)
        ? (ExtractedText.Length > 100 ? ExtractedText.Substring(0, 100) + "..." : ExtractedText)
        : "No text";

    public string TimeDisplay => GetRelativeTime(CreatedAt);

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    private string GetRelativeTime(DateTime dateTime)
    {
        var span = DateTime.Now - dateTime;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
        return dateTime.ToString("MMM dd");
    }
}
