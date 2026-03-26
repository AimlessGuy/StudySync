using CommunityToolkit.Mvvm.ComponentModel;
using StudySync.Services;
using System;
using System.Threading.Tasks;

namespace StudySync.ViewModels;

// IQueryAttributable lets the ViewModel receive Shell navigation parameters directly
public partial class NoteDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly DatabaseService _databaseService;

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

    public NoteDetailViewModel()
    {
        _databaseService = new DatabaseService();
    }

    // MAUI Shell calls this automatically when navigating with query parameters
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
            var notes = await _databaseService.GetNotesAsync();
            var note = notes.Find(n => n.Id == id);

            if (note != null)
            {
                NoteTitle = string.IsNullOrEmpty(note.Title) ? "Untitled Note" : note.Title;
                NoteText = note.ExtractedText ?? "No text extracted";
                CourseCode = note.CourseCode;
                ContentType = note.ContentType;
                CreatedDate = note.CreatedAt.ToString("MMM dd, yyyy h:mm tt");
            }
            else
            {
                NoteText = "Note not found.";
            }
        }
        catch (Exception ex)
        {
            NoteText = $"Error loading note: {ex.Message}";
        }
    }
}