using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class FeedViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    private ObservableCollection<Note> _sharedNotes = new();
    public ObservableCollection<Note> SharedNotes
    {
        get => _sharedNotes;
        set => SetProperty(ref _sharedNotes, value);
    }

    private int _sharedCount;
    public int SharedCount
    {
        get => _sharedCount;
        set => SetProperty(ref _sharedCount, value);
    }

    public FeedViewModel()
    {
        _databaseService = new DatabaseService();
    }

    [RelayCommand]
    public async Task LoadFeedAsync()
    {
        var notes = await _databaseService.GetSharedNotesAsync();
        SharedNotes.Clear();
        foreach (var note in notes)
            SharedNotes.Add(note);
        SharedCount = SharedNotes.Count;
    }
}