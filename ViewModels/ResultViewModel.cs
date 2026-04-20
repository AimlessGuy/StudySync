using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace StudySync.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly OCRService _ocrService;
    private readonly PostCloudService _postCloudService;
    private readonly string _imagePath;

    public ObservableCollection<string> PrimarySubjectTags { get; } = new()
    {
        "Mathematics",
        "Science",
        "English",
        "Filipino",
        "History",
        "Computer Science",
        "Engineering",
        "Business",
        "Medicine/Nursing",
        "Law",
        "Arts",
        "PE"
    };

    public ObservableCollection<SelectableSection> JoinedSections { get; } = new();

    private string _extractedText = string.Empty;
    public string ExtractedText
    {
        get => _extractedText;
        set => SetProperty(ref _extractedText, value);
    }

    private string _selectedPrimarySubjectTag = string.Empty;
    public string SelectedPrimarySubjectTag
    {
        get => _selectedPrimarySubjectTag;
        set => SetProperty(ref _selectedPrimarySubjectTag, value);
    }

    private string _secondaryTagsInput = string.Empty;
    public string SecondaryTagsInput
    {
        get => _secondaryTagsInput;
        set => SetProperty(ref _secondaryTagsInput, value);
    }

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    private bool _isTextExtracted;
    public bool IsTextExtracted
    {
        get => _isTextExtracted;
        set => SetProperty(ref _isTextExtracted, value);
    }

    private double _ocrProgress;
    public double OcrProgress
    {
        get => _ocrProgress;
        set => SetProperty(ref _ocrProgress, value);
    }

    private string _ocrStatus = "Starting...";
    public string OcrStatus
    {
        get => _ocrStatus;
        set => SetProperty(ref _ocrStatus, value);
    }

    private int _ocrProgressPercent;
    public int OcrProgressPercent
    {
        get => _ocrProgressPercent;
        set => SetProperty(ref _ocrProgressPercent, value);
    }

    public IAsyncRelayCommand SaveNoteCommand { get; }
    public IAsyncRelayCommand ShareNoteCommand { get; }
    public IAsyncRelayCommand ProcessAgainCommand { get; }

    public bool HasJoinedSections => JoinedSections.Count > 0;
    public bool HasNoJoinedSections => JoinedSections.Count == 0;

    public ResultViewModel(string imagePath)
    {
        _databaseService = new DatabaseService();
        _ocrService = new OCRService();
        _postCloudService = new PostCloudService();
        _imagePath = imagePath;

        SaveNoteCommand = new AsyncRelayCommand(SaveToVaultAsync);
        ShareNoteCommand = new AsyncRelayCommand(ShareToSectionsAsync);
        ProcessAgainCommand = new AsyncRelayCommand(ProcessImageAsync);

        MainThread.BeginInvokeOnMainThread(async () => await ProcessImageAsync());
        _ = LoadJoinedSectionsAsync();
    }

    private async Task ProcessImageAsync()
    {
        try
        {
            IsProcessing = true;
            IsTextExtracted = false;
            ExtractedText = string.Empty;
            OcrProgress = 0;
            OcrProgressPercent = 0;
            OcrStatus = "Starting...";

            var progress = new Progress<OcrProgressUpdate>(update =>
            {
                OcrStatus = update.StatusMessage;
                OcrProgress = update.Percentage / 100.0;
                OcrProgressPercent = update.Percentage;
            });

            var text = await _ocrService.RecognizeTextAsync(_imagePath, progress);

            ExtractedText = text;
            IsTextExtracted = true;
        }
        catch (Exception ex)
        {
            ExtractedText = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task SaveToVaultAsync()
    {
        if (!IsTagSelectionValid())
            return;

        try
        {
            var note = BuildNote(isShared: false);

            await _databaseService.SaveNoteAsync(note);

            await Shell.Current.DisplayAlert(
                "Success",
                "Note saved to your vault with tags.",
                "OK");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private async Task ShareToSectionsAsync()
    {
        if (!IsTagSelectionValid())
            return;

        var selectedSections = JoinedSections
            .Where(section => section.IsSelected)
            .Select(section => new Section
            {
                Name = section.Name,
                InviteCode = section.InviteCode
            })
            .ToList();

        if (selectedSections.Count == 0)
        {
            await Shell.Current.DisplayAlert(
                "Choose sections",
                "Select at least one joined section before sharing this note.",
                "OK");
            return;
        }

        try
        {
            var note = BuildNote(isShared: true);
            await _databaseService.SaveNoteAsync(note);
            await _postCloudService.CreatePostAsync(note, selectedSections);

            var sectionLabel = selectedSections.Count == 1 ? "1 section" : $"{selectedSections.Count} sections";
            await Shell.Current.DisplayAlert(
                "Shared",
                $"Your note was posted to {sectionLabel}.",
                "OK");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to share: {ex.Message}", "OK");
        }
    }

    private async Task LoadJoinedSectionsAsync()
    {
        try
        {
            var sections = await _databaseService.GetSectionsAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                JoinedSections.Clear();
                foreach (var section in sections)
                {
                    JoinedSections.Add(new SelectableSection
                    {
                        Name = section.Name,
                        InviteCode = section.InviteCode
                    });
                }

                OnPropertyChanged(nameof(HasJoinedSections));
                OnPropertyChanged(nameof(HasNoJoinedSections));
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                JoinedSections.Clear();
                OnPropertyChanged(nameof(HasJoinedSections));
                OnPropertyChanged(nameof(HasNoJoinedSections));
            });
        }
    }

    private bool IsTagSelectionValid()
    {
        if (!IsTextExtracted || string.IsNullOrWhiteSpace(ExtractedText))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await Shell.Current.DisplayAlert("Missing text", "Wait for OCR to finish before saving.", "OK"));
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedPrimarySubjectTag))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await Shell.Current.DisplayAlert("Choose a subject", "Select one primary subject tag before saving or sharing.", "OK"));
            return false;
        }

        return true;
    }

    private string NormalizeSecondaryTags()
    {
        if (string.IsNullOrWhiteSpace(SecondaryTagsInput))
            return string.Empty;

        var tags = SecondaryTagsInput
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return string.Join(", ", tags);
    }

    private string BuildTitle()
    {
        var firstLine = ExtractedText?
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(firstLine))
            return firstLine.Length > 40 ? firstLine[..40] + "..." : firstLine;

        return $"{SelectedPrimarySubjectTag} Note";
    }

    private Note BuildNote(bool isShared)
    {
        return new Note
        {
            Title = BuildTitle(),
            ImagePath = _imagePath,
            ExtractedText = ExtractedText?.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsShared = isShared,
            IsAnonymous = isShared,
            Upvotes = 0,
            CourseCode = SelectedPrimarySubjectTag,
            ContentType = "Notes",
            PrimarySubjectTag = SelectedPrimarySubjectTag,
            SecondaryTags = NormalizeSecondaryTags()
        };
    }
}
