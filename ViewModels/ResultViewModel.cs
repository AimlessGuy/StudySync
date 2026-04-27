using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace StudySync.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly OCRService _ocrService;
    private readonly PostCloudService _postCloudService;
    private readonly SectionCloudService _sectionCloudService;
    private readonly IReadOnlyList<string> _imagePaths;
    private readonly string _primaryImagePath;
    private readonly string _initialSectionInviteCode;
    private readonly Dictionary<string, List<string>> _knownPrimaryTagsBySection = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<string> PrimarySubjectTags { get; } = new();

    public ObservableCollection<SelectableSection> JoinedSections { get; } = new();
    public ObservableCollection<string> ImagePaths { get; } = new();

    public bool HasMultiplePages => ImagePaths.Count > 1;

    public string PageSummaryText =>
        ImagePaths.Count switch
        {
            0 => string.Empty,
            1 => "1 page",
            _ => $"{ImagePaths.Count} pages combined"
        };

    private string _extractedText = string.Empty;
    public string ExtractedText
    {
        get => _extractedText;
        set => SetProperty(ref _extractedText, value);
    }

    private string _noteTitle = string.Empty;
    public string NoteTitle
    {
        get => _noteTitle;
        set => SetProperty(ref _noteTitle, value);
    }

    private SelectableSection? _selectedSection;
    public SelectableSection? SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
                RefreshPrimaryTagSuggestions();
        }
    }

    private string _selectedPrimarySubjectTag = string.Empty;
    public string SelectedPrimarySubjectTag
    {
        get => _selectedPrimarySubjectTag;
        set
        {
            if (SetProperty(ref _selectedPrimarySubjectTag, value) && !string.IsNullOrWhiteSpace(value))
                PrimarySubjectTagInput = value;
        }
    }

    private string _primarySubjectTagInput = string.Empty;
    public string PrimarySubjectTagInput
    {
        get => _primarySubjectTagInput;
        set => SetProperty(ref _primarySubjectTagInput, value);
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

    private string _ocrWarning = string.Empty;
    public string OcrWarning
    {
        get => _ocrWarning;
        set
        {
            if (SetProperty(ref _ocrWarning, value))
                OnPropertyChanged(nameof(HasOcrWarning));
        }
    }

    public bool HasOcrWarning => !string.IsNullOrWhiteSpace(OcrWarning);

    private double _ocrConfidenceScore;
    public double OcrConfidenceScore
    {
        get => _ocrConfidenceScore;
        set => SetProperty(ref _ocrConfidenceScore, value);
    }

    private string _ocrConfidenceLabel = string.Empty;
    public string OcrConfidenceLabel
    {
        get => _ocrConfidenceLabel;
        set
        {
            if (SetProperty(ref _ocrConfidenceLabel, value))
                OnPropertyChanged(nameof(HasOcrConfidence));
        }
    }

    private string _ocrConfidenceSummary = string.Empty;
    public string OcrConfidenceSummary
    {
        get => _ocrConfidenceSummary;
        set => SetProperty(ref _ocrConfidenceSummary, value);
    }

    public bool HasOcrConfidence => !string.IsNullOrWhiteSpace(OcrConfidenceLabel);

    public string OcrConfidencePercent => $"{Math.Round(OcrConfidenceScore * 100):0}%";

    public Color OcrConfidenceAccentColor =>
        OcrConfidenceScore >= 0.75 ? Color.FromArgb("#166534") :
        OcrConfidenceScore >= 0.5 ? Color.FromArgb("#92400E") :
        Color.FromArgb("#991B1B");

    public Color OcrConfidenceBackgroundColor =>
        OcrConfidenceScore >= 0.75 ? Color.FromArgb("#DCFCE7") :
        OcrConfidenceScore >= 0.5 ? Color.FromArgb("#FEF3C7") :
        Color.FromArgb("#FEE2E2");

    private int _ocrProgressPercent;
    public int OcrProgressPercent
    {
        get => _ocrProgressPercent;
        set => SetProperty(ref _ocrProgressPercent, value);
    }

    public IAsyncRelayCommand ShareNoteCommand { get; }
    public IAsyncRelayCommand ProcessAgainCommand { get; }

    public bool HasJoinedSections => JoinedSections.Count > 0;
    public bool HasNoJoinedSections => JoinedSections.Count == 0;
    public bool HasSuggestedPrimaryTags => PrimarySubjectTags.Count > 0;

    public ResultViewModel(IReadOnlyList<string> imagePaths, string? initialSectionInviteCode = null)
    {
        _databaseService = new DatabaseService();
        _ocrService = new OCRService();
        _postCloudService = new PostCloudService();
        _sectionCloudService = new SectionCloudService();
        _imagePaths = imagePaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        _primaryImagePath = _imagePaths.FirstOrDefault() ?? string.Empty;
        _initialSectionInviteCode = initialSectionInviteCode ?? string.Empty;

        foreach (var imagePath in _imagePaths)
            ImagePaths.Add(imagePath);

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
            OcrWarning = string.Empty;
            OcrConfidenceScore = 0;
            OcrConfidenceLabel = string.Empty;
            OcrConfidenceSummary = string.Empty;
            var pageTexts = new List<string>();
            var pageWarnings = new List<string>();
            var confidenceScores = new List<double>();

            for (int index = 0; index < _imagePaths.Count; index++)
            {
                string imagePath = _imagePaths[index];
                double pageConfidence = 0;
                string pageConfidenceLabel = string.Empty;
                string pageConfidenceSummary = string.Empty;

                var progress = new Progress<OcrProgressUpdate>(update =>
                {
                    double overallProgress = (index + (update.Percentage / 100.0)) / Math.Max(1, _imagePaths.Count);
                    OcrStatus = _imagePaths.Count > 1
                        ? $"Page {index + 1}/{_imagePaths.Count}: {update.StatusMessage}"
                        : update.StatusMessage;
                    OcrProgress = overallProgress;
                    OcrProgressPercent = (int)Math.Round(overallProgress * 100);

                    if (!string.IsNullOrWhiteSpace(update.WarningMessage))
                        OcrWarning = update.WarningMessage;

                    pageConfidence = update.ConfidenceScore;
                    pageConfidenceLabel = update.ConfidenceLabel;
                    pageConfidenceSummary = update.ConfidenceSummary;
                });

                var text = await _ocrService.RecognizeTextAsync(imagePath, progress);
                pageTexts.Add(text);

                if (!string.IsNullOrWhiteSpace(OcrWarning))
                    pageWarnings.Add(OcrWarning);

                if (pageConfidence > 0)
                    confidenceScores.Add(pageConfidence);

                if (index == _imagePaths.Count - 1)
                {
                    OcrConfidenceScore = pageConfidence;
                    OcrConfidenceLabel = pageConfidenceLabel;
                    OcrConfidenceSummary = pageConfidenceSummary;
                }
            }

            ExtractedText = CombinePageTexts(pageTexts);
            NoteTitle = BuildTitle();
            ApplyCombinedWarnings(pageWarnings);
            ApplyCombinedConfidence(confidenceScores);
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

    private async Task ShareToSectionsAsync()
    {
        if (!IsTagSelectionValid())
            return;

        if (SelectedSection == null)
        {
            await Shell.Current.DisplayAlert(
                "Choose a section",
                "Choose the section you want to send this note to before sharing.",
                "OK");
            return;
        }

        try
        {
            var selectedSection = new Section
            {
                Name = SelectedSection.Name,
                InviteCode = SelectedSection.InviteCode
            };

            var note = BuildNote(isShared: true);
            await _databaseService.SaveNoteAsync(note);
            await _postCloudService.CreatePostAsync(note, new List<Section> { selectedSection });

            await Shell.Current.DisplayAlert(
                "Shared",
                $"Your note was posted to {selectedSection.Name}.",
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
            var sections = await GetJoinedSectionsAsync();
            var posts = await _postCloudService.GetPostsForSectionsAsync(sections);
            _knownPrimaryTagsBySection.Clear();

            foreach (var section in sections)
            {
                var knownPrimaryTags = posts
                    .Where(post => post.SectionInviteCodes.Any(code =>
                        string.Equals(code, section.InviteCode, StringComparison.OrdinalIgnoreCase)))
                    .Select(post => post.PrimarySubjectTag)
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag)
                    .ToList();

                _knownPrimaryTagsBySection[section.InviteCode] = knownPrimaryTags;
            }

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

                SelectedSection = JoinedSections.FirstOrDefault(section =>
                                      string.Equals(section.InviteCode, _initialSectionInviteCode, StringComparison.OrdinalIgnoreCase))
                                  ?? JoinedSections.FirstOrDefault();

                OnPropertyChanged(nameof(HasJoinedSections));
                OnPropertyChanged(nameof(HasNoJoinedSections));
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                JoinedSections.Clear();
                SelectedSection = null;
                PrimarySubjectTags.Clear();
                OnPropertyChanged(nameof(HasJoinedSections));
                OnPropertyChanged(nameof(HasNoJoinedSections));
            });
        }
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

    private bool IsTagSelectionValid()
    {
        if (!IsTextExtracted || string.IsNullOrWhiteSpace(ExtractedText))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await Shell.Current.DisplayAlert("Missing text", "Wait for OCR to finish before saving.", "OK"));
            return false;
        }

        if (SelectedSection == null)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await Shell.Current.DisplayAlert("Choose a section", "Pick the section you want to send this note to first.", "OK"));
            return false;
        }

        if (string.IsNullOrWhiteSpace(GetNormalizedPrimarySubjectTag()))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
                await Shell.Current.DisplayAlert("Choose a tag", "Pick an existing primary tag or type a new one before saving or sharing.", "OK"));
            return false;
        }

        return true;
    }

    private string GetNormalizedPrimarySubjectTag()
    {
        var candidate = string.IsNullOrWhiteSpace(PrimarySubjectTagInput)
            ? SelectedPrimarySubjectTag
            : PrimarySubjectTagInput;

        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        var collapsed = string.Join(
            " ",
            candidate
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var existingMatch = PrimarySubjectTags
            .FirstOrDefault(tag => string.Equals(tag, collapsed, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(existingMatch))
            return existingMatch;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(collapsed.ToLowerInvariant());
    }

    private void RefreshPrimaryTagSuggestions()
    {
        var previousSelection = SelectedPrimarySubjectTag;
        PrimarySubjectTags.Clear();

        if (SelectedSection != null &&
            _knownPrimaryTagsBySection.TryGetValue(SelectedSection.InviteCode, out var tags))
        {
            foreach (var tag in tags)
                PrimarySubjectTags.Add(tag);
        }

        SelectedPrimarySubjectTag = PrimarySubjectTags
            .FirstOrDefault(tag => string.Equals(tag, previousSelection, StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;

        OnPropertyChanged(nameof(HasSuggestedPrimaryTags));
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

        var primaryTag = GetNormalizedPrimarySubjectTag();
        return string.IsNullOrWhiteSpace(primaryTag) ? "Untitled Note" : $"{primaryTag} Note";
    }

    private Note BuildNote(bool isShared)
    {
        return new Note
        {
            Title = string.IsNullOrWhiteSpace(NoteTitle) ? BuildTitle() : NoteTitle.Trim(),
            ImagePath = _primaryImagePath,
            ExtractedText = ExtractedText?.Trim(),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsShared = isShared,
            IsAnonymous = isShared,
            Upvotes = 0,
            CourseCode = GetNormalizedPrimarySubjectTag(),
            ContentType = "Notes",
            PrimarySubjectTag = GetNormalizedPrimarySubjectTag(),
            SecondaryTags = NormalizeSecondaryTags()
        };
    }

    private string CombinePageTexts(IReadOnlyList<string> pageTexts)
    {
        var nonEmptyTexts = pageTexts
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        if (nonEmptyTexts.Count <= 1)
            return nonEmptyTexts.FirstOrDefault() ?? string.Empty;

        var sections = nonEmptyTexts
            .Select((text, index) =>
                $"Page {index + 1}{Environment.NewLine}{Environment.NewLine}{text}");

        return string.Join(
            $"{Environment.NewLine}{Environment.NewLine}------------------------------{Environment.NewLine}{Environment.NewLine}",
            sections);
    }

    private void ApplyCombinedWarnings(IEnumerable<string> pageWarnings)
    {
        var distinctWarnings = pageWarnings
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct()
            .ToList();

        OcrWarning = string.Join(Environment.NewLine, distinctWarnings);
    }

    private void ApplyCombinedConfidence(IReadOnlyList<double> confidenceScores)
    {
        if (confidenceScores.Count == 0)
        {
            OnPropertyChanged(nameof(OcrConfidencePercent));
            OnPropertyChanged(nameof(OcrConfidenceAccentColor));
            OnPropertyChanged(nameof(OcrConfidenceBackgroundColor));
            return;
        }

        OcrConfidenceScore = confidenceScores.Min();

        if (_imagePaths.Count > 1)
        {
            if (OcrConfidenceScore >= 0.75)
            {
                OcrConfidenceLabel = "High confidence";
                OcrConfidenceSummary = "The batch looks strong overall. Only light cleanup should be needed.";
            }
            else if (OcrConfidenceScore >= 0.5)
            {
                OcrConfidenceLabel = "Medium confidence";
                OcrConfidenceSummary = "At least one page may need cleanup, but the combined scan should still be usable.";
            }
            else
            {
                OcrConfidenceLabel = "Low confidence";
                OcrConfidenceSummary = "One or more pages look weak. Retaking the weakest page may improve the whole note.";
            }
        }

        OnPropertyChanged(nameof(OcrConfidencePercent));
        OnPropertyChanged(nameof(OcrConfidenceAccentColor));
        OnPropertyChanged(nameof(OcrConfidenceBackgroundColor));
    }
}
