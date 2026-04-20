using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using StudySync.Models;
using StudySync.Services;
using System;
using System.Threading.Tasks;

namespace StudySync.ViewModels;

public partial class NoteDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _databaseService;
    private Note? _currentNote;

    private string _noteTitle = string.Empty;
    public string NoteTitle
    {
        get => _noteTitle;
        set => SetProperty(ref _noteTitle, value);
    }

    private string _noteText = string.Empty;
    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
    }

    private string _courseCode = string.Empty;
    public string CourseCode
    {
        get => _courseCode;
        set => SetProperty(ref _courseCode, value);
    }

    private string _contentType = string.Empty;
    public string ContentType
    {
        get => _contentType;
        set => SetProperty(ref _contentType, value);
    }

    private string _createdDate = string.Empty;
    public string CreatedDate
    {
        get => _createdDate;
        set => SetProperty(ref _createdDate, value);
    }

    private string _updatedDate = string.Empty;
    public string UpdatedDate
    {
        get => _updatedDate;
        set => SetProperty(ref _updatedDate, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public IAsyncRelayCommand SaveChangesCommand { get; }
    public IAsyncRelayCommand DeleteNoteCommand { get; }
    public IAsyncRelayCommand ShareNoteCommand { get; }

    public NoteDetailViewModel()
    {
        _databaseService = new DatabaseService();
        SaveChangesCommand = new AsyncRelayCommand(SaveChangesAsync);
        DeleteNoteCommand = new AsyncRelayCommand(DeleteNoteAsync);
        ShareNoteCommand = new AsyncRelayCommand(ShareNoteAsync);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("noteId", out var value) &&
            int.TryParse(value?.ToString(), out int id) && id > 0)
        {
            _ = LoadNoteAsync(id);
        }
    }

    private async Task LoadNoteAsync(int id)
    {
        try
        {
            IsLoading = true;
            NoteText = "Loading...";

            var notes = await _databaseService.GetNotesAsync();
            _currentNote = notes.Find(n => n.Id == id);

            if (_currentNote != null)
            {
                NoteTitle = string.IsNullOrEmpty(_currentNote.Title) ? "Untitled Note" : _currentNote.Title;
                NoteText = _currentNote.ExtractedText ?? "No text extracted";
                CourseCode = _currentNote.CourseCode;
                ContentType = _currentNote.ContentType;
                CreatedDate = _currentNote.CreatedAt.ToString("MMM dd, yyyy h:mm tt");
                UpdatedDate = _currentNote.UpdatedAt.ToString("MMM dd, yyyy h:mm tt");
            }
            else
            {
                NoteTitle = "Note Not Found";
                NoteText = "This note may have been deleted.";
            }
        }
        catch (Exception ex)
        {
            NoteTitle = "Error";
            NoteText = $"Could not load note: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveChangesAsync()
    {
        if (_currentNote == null) return;

        try
        {
            _currentNote.ExtractedText = NoteText;
            _currentNote.Title = NoteTitle;
            _currentNote.UpdatedAt = DateTime.Now;
            UpdatedDate = _currentNote.UpdatedAt.ToString("MMM dd, yyyy h:mm tt");
            await _databaseService.SaveNoteAsync(_currentNote);
            await Shell.Current.DisplayAlert("Saved", "Your edits have been saved.", "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not save: {ex.Message}", "OK");
        }
    }

    private async Task DeleteNoteAsync()
    {
        if (_currentNote == null) return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Note",
            "Are you sure you want to delete this note? This cannot be undone.",
            "Delete", "Cancel");

        if (!confirm) return;

        try
        {
            await _databaseService.DeleteNoteAsync(_currentNote);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not delete: {ex.Message}", "OK");
        }
    }

    private async Task ShareNoteAsync()
    {
        if (_currentNote == null) return;

        try
        {
            var title = string.IsNullOrWhiteSpace(NoteTitle) ? "StudySync Note" : NoteTitle;
            var course = string.IsNullOrWhiteSpace(CourseCode) ? "General" : CourseCode;
            var body = string.IsNullOrWhiteSpace(NoteText) ? "No text available." : NoteText;

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = title,
                Subject = $"{course} note",
                Text = $"{title}\nCourse: {course}\n\n{body}"
            });
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not share note: {ex.Message}", "OK");
        }
    }
}
