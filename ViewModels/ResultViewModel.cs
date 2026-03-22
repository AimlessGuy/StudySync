using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Models;
using StudySync.Services;
using Microsoft.Maui.ApplicationModel;

namespace StudySync.ViewModels;

public partial class ResultViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private readonly OCRService _ocrService;
    private readonly string _imagePath;

    private string _extractedText = string.Empty;
    public string ExtractedText
    {
        get => _extractedText;
        set => SetProperty(ref _extractedText, value);
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

    public ResultViewModel(string imagePath)
    {
        _databaseService = new DatabaseService();
        _ocrService = new OCRService();
        _imagePath = imagePath;

        SaveNoteCommand = new AsyncRelayCommand(() => SaveNote(isShared: false));
        ShareNoteCommand = new AsyncRelayCommand(() => SaveNote(isShared: true));
        ProcessAgainCommand = new AsyncRelayCommand(ProcessImageAsync);

        MainThread.BeginInvokeOnMainThread(async () => await ProcessImageAsync());
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

    private async Task SaveNote(bool isShared)
    {
        try
        {
            var note = new Note
            {
                ImagePath = _imagePath,
                ExtractedText = ExtractedText,
                CreatedAt = DateTime.Now,
                IsShared = isShared,
                IsAnonymous = isShared,
                Upvotes = 0,
                CourseCode = "Unknown",
                ContentType = "Notes"
            };

            await _databaseService.SaveNoteAsync(note);

            await Shell.Current.DisplayAlert("Success",
                isShared ? "Note shared anonymously to the feed!" : "Note saved to your vault!",
                "OK");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}