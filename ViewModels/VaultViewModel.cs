using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Models;
using StudySync.Services;
using StudySync.Views; // Add this for NoteDetailPage
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace StudySync.ViewModels;

public partial class VaultViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    private ObservableCollection<Note> _notes = new();
    public ObservableCollection<Note> Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    private int _noteCount;
    public int NoteCount
    {
        get => _noteCount;
        set => SetProperty(ref _noteCount, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public VaultViewModel()
    {
        _databaseService = new DatabaseService();
    }

    [RelayCommand]
    public async Task LoadNotesAsync()
    {
        try
        {
            IsLoading = true;
            var allNotes = await _databaseService.GetNotesAsync();

            Notes.Clear();
            foreach (var note in allNotes)
            {
                Notes.Add(note);
            }

            NoteCount = Notes.Count;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectNote(Note note)
    {
        if (note != null)
        {
            await Shell.Current.GoToAsync($"{nameof(NoteDetailPage)}?noteId={note.Id}");
        }
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.GoToAsync("..");
    }
}