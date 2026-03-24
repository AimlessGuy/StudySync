using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Models;
using StudySync.Services;
using StudySync.Views;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;

    [ObservableProperty]
    private ObservableCollection<Note> recentNotes = new();

    [ObservableProperty]
    private int noteCount;

    [ObservableProperty]
    private bool isEmpty;

    public MainViewModel(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [RelayCommand]
    public async Task LoadNotesAsync()
    {
        var all = await _databaseService.GetNotesAsync();
        NoteCount = all.Count;
        RecentNotes.Clear();
        var recent = all.Take(5).ToList();
        foreach (var n in recent)
            RecentNotes.Add(n);
        IsEmpty = RecentNotes.Count == 0;
    }

    [RelayCommand]
    private async Task GoToCamera()
    {
        System.Diagnostics.Debug.WriteLine("🟢 GoToCamera executed");
        await Shell.Current.GoToAsync(nameof(CameraPage));
    }

    [RelayCommand]
    private async Task GoToVault()
    {
        System.Diagnostics.Debug.WriteLine("🟢 GoToVault executed");
        await Shell.Current.GoToAsync(nameof(VaultPage));
    }

    [RelayCommand]
    private async Task GoToFeed()
    {
        System.Diagnostics.Debug.WriteLine("🟢 GoToFeed executed");
        await Shell.Current.DisplayAlert("Coming Soon", "Feed will be in Phase 3", "OK");
    }

    [RelayCommand]
    private async Task SelectNote(Note note)
    {
        // Navigate to note detail page with the note ID
        await Shell.Current.GoToAsync($"{nameof(NoteDetailPage)}?noteId={note.Id}");
    }

}

