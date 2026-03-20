using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using StudySync.Models;
using StudySync.Services;
using Microsoft.Maui.ApplicationModel;

namespace StudySync.ViewModels;

public class ResultViewModel // Note: removed ObservableObject inheritance
{
    private readonly DatabaseService _databaseService;
    private readonly OCRService _ocrService;
    private readonly string _imagePath;

    private string _extractedText;
    private bool _isProcessing;
    private bool _isTextExtracted;

    public string ExtractedText
    {
        get => _extractedText;
        set
        {
            if (_extractedText != value)
            {
                _extractedText = value;
                // Property changed notification would go here
            }
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set
        {
            if (_isProcessing != value)
            {
                _isProcessing = value;
                // Property changed notification would go here
            }
        }
    }

    public bool IsTextExtracted
    {
        get => _isTextExtracted;
        set
        {
            if (_isTextExtracted != value)
            {
                _isTextExtracted = value;
                // Property changed notification would go here
            }
        }
    }

    public ICommand SaveNoteCommand { get; }
    public ICommand ShareNoteCommand { get; }
    public ICommand ProcessAgainCommand { get; }

    public ResultViewModel(string imagePath)
    {
        _databaseService = new DatabaseService();
        _ocrService = new OCRService();
        _imagePath = imagePath;
        _extractedText = string.Empty; // Initialize to empty string

        SaveNoteCommand = new AsyncRelayCommand(SaveNoteAsync);
        ShareNoteCommand = new AsyncRelayCommand(ShareNoteAsync);
        ProcessAgainCommand = new AsyncRelayCommand(ProcessImageAsync);

        // Start processing immediately
        MainThread.BeginInvokeOnMainThread(async () => await ProcessImageAsync());
    }

    private async Task ProcessImageAsync()
    {
        try
        {
            IsProcessing = true;
            IsTextExtracted = false;
            ExtractedText = "Processing image...";

            var text = await _ocrService.RecognizeTextAsync(_imagePath);

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

    private async Task SaveNoteAsync()
    {
        try
        {
            var note = new Note
            {
                ImagePath = _imagePath,
                ExtractedText = ExtractedText,
                CreatedAt = DateTime.Now,
                IsShared = false,
                IsAnonymous = false,
                Upvotes = 0,
                CourseCode = "Unknown",
                ContentType = "Notes"
            };

            await _databaseService.SaveNoteAsync(note);

            await Shell.Current.DisplayAlert("Success",
                "Note saved to your vault!", "OK");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error",
                $"Failed to save: {ex.Message}", "OK");
        }
    }

    private async Task ShareNoteAsync()
    {
        try
        {
            var note = new Note
            {
                ImagePath = _imagePath,
                ExtractedText = ExtractedText,
                CreatedAt = DateTime.Now,
                IsShared = true,
                IsAnonymous = true,
                Upvotes = 0,
                CourseCode = "Unknown",
                ContentType = "Notes"
            };

            await _databaseService.SaveNoteAsync(note);

            await Shell.Current.DisplayAlert("Success",
                "Note shared anonymously to the feed!", "OK");

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error",
                $"Failed to share: {ex.Message}", "OK");
        }
    }
}