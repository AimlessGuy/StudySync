using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Models;
using StudySync.Services;
using StudySync.Views;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class VaultViewModel : ObservableObject
{
    private readonly DatabaseService _databaseService;
    private VaultCollectionKind _currentCollectionKind = VaultCollectionKind.AllNotes;
    private Notebook? _selectedNotebook;

    public ObservableCollection<Note> Notes { get; } = new();
    public ObservableCollection<Notebook> Notebooks { get; } = new();

    [ObservableProperty]
    private int noteCount;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string collectionTitle = "All Notes";

    [ObservableProperty]
    private string collectionSubtitle = "Everything you've kept in StudySync";

    public bool IsAllNotesSelected => _currentCollectionKind == VaultCollectionKind.AllNotes;
    public bool IsMyNotesSelected => _currentCollectionKind == VaultCollectionKind.MyNotes;
    public bool IsLikedNotesSelected => _currentCollectionKind == VaultCollectionKind.LikedNotes;
    public bool IsNotebookMode => _currentCollectionKind == VaultCollectionKind.Notebook;
    public string SelectedNotebookName => _selectedNotebook?.Name ?? string.Empty;
    public bool HasNotebooks => Notebooks.Count > 0;
    public bool HasNoNotebooks => Notebooks.Count == 0;

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
            await LoadNotebooksAsync();
            await ReloadCurrentCollectionAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ShowAllNotesAsync()
    {
        _currentCollectionKind = VaultCollectionKind.AllNotes;
        _selectedNotebook = null;
        RaiseSelectionStateChanged();
        await ReloadCurrentCollectionAsync();
    }

    [RelayCommand]
    private async Task ShowMyNotesAsync()
    {
        _currentCollectionKind = VaultCollectionKind.MyNotes;
        _selectedNotebook = null;
        RaiseSelectionStateChanged();
        await ReloadCurrentCollectionAsync();
    }

    [RelayCommand]
    private async Task ShowLikedNotesAsync()
    {
        _currentCollectionKind = VaultCollectionKind.LikedNotes;
        _selectedNotebook = null;
        RaiseSelectionStateChanged();
        await ReloadCurrentCollectionAsync();
    }

    [RelayCommand]
    private async Task SelectNotebookAsync(Notebook? notebook)
    {
        if (notebook == null)
            return;

        _currentCollectionKind = VaultCollectionKind.Notebook;
        _selectedNotebook = notebook;
        RaiseSelectionStateChanged();
        await ReloadCurrentCollectionAsync();
    }

    [RelayCommand]
    private async Task CreateNotebookAsync()
    {
        var name = await Shell.Current.DisplayPromptAsync(
            "New Notebook",
            "Give your notebook a name.",
            accept: "Create",
            cancel: "Cancel",
            placeholder: "Exam review");

        if (string.IsNullOrWhiteSpace(name))
            return;

        var notebook = new Notebook
        {
            Name = name.Trim(),
            CreatedAt = DateTime.Now
        };

        try
        {
            await _databaseService.SaveNotebookAsync(notebook);
            await LoadNotebooksAsync();

            var selected = Notebooks.FirstOrDefault(item =>
                string.Equals(item.Name, notebook.Name, StringComparison.OrdinalIgnoreCase));

            if (selected != null)
                await SelectNotebookAsync(selected);
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Notebook error", $"Could not create this notebook: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task SelectNoteAsync(Note? note)
    {
        if (note == null)
            return;

        await Shell.Current.GoToAsync($"{nameof(NoteDetailPage)}?noteId={note.Id}");
    }

    [RelayCommand]
    private async Task ShowNoteActionsAsync(Note? note)
    {
        if (note == null)
            return;

        var actions = new List<string> { "Open", "Add to Notebook" };
        if (_currentCollectionKind == VaultCollectionKind.Notebook && _selectedNotebook != null)
            actions.Add("Remove from This Notebook");

        var action = await Shell.Current.DisplayActionSheet(
            "Note Actions",
            "Cancel",
            null,
            actions.ToArray());

        switch (action)
        {
            case "Open":
                await SelectNoteAsync(note);
                break;
            case "Add to Notebook":
                await AddNoteToNotebookAsync(note);
                break;
            case "Remove from This Notebook":
                await RemoveNoteFromCurrentNotebookAsync(note);
                break;
        }
    }

    private async Task LoadNotebooksAsync()
    {
        var notebooks = await _databaseService.GetNotebooksAsync();

        Notebooks.Clear();
        foreach (var notebook in notebooks)
            Notebooks.Add(notebook);

        OnPropertyChanged(nameof(HasNotebooks));
        OnPropertyChanged(nameof(HasNoNotebooks));
    }

    private async Task ReloadCurrentCollectionAsync()
    {
        List<Note> notes;

        switch (_currentCollectionKind)
        {
            case VaultCollectionKind.MyNotes:
                notes = await _databaseService.GetMyPostedNotesAsync();
                CollectionTitle = "My Notes";
                CollectionSubtitle = "Notes you've posted and kept in your vault";
                break;
            case VaultCollectionKind.LikedNotes:
                notes = await _databaseService.GetLikedNotesAsync();
                CollectionTitle = "Liked Notes";
                CollectionSubtitle = "Posts you gave a thumbs-up to";
                break;
            case VaultCollectionKind.Notebook:
                if (_selectedNotebook == null)
                {
                    notes = new List<Note>();
                    CollectionTitle = "Notebook";
                    CollectionSubtitle = "Select a notebook to view its notes";
                }
                else
                {
                    notes = await _databaseService.GetNotesForNotebookAsync(_selectedNotebook.Id);
                    CollectionTitle = _selectedNotebook.Name;
                    CollectionSubtitle = "Your personal notebook";
                }
                break;
            default:
                notes = await _databaseService.GetVaultNotesAsync();
                CollectionTitle = "All Notes";
                CollectionSubtitle = "Everything you've kept in StudySync";
                break;
        }

        Notes.Clear();
        foreach (var note in notes)
            Notes.Add(note);

        NoteCount = Notes.Count;
    }

    private async Task AddNoteToNotebookAsync(Note note)
    {
        if (Notebooks.Count == 0)
        {
            await CreateNotebookAsync();
            if (Notebooks.Count == 0)
                return;
        }

        var notebookOptions = Notebooks.Select(notebook => notebook.Name).ToList();
        notebookOptions.Add("+ New Notebook");

        var choice = await Shell.Current.DisplayActionSheet(
            "Add to Notebook",
            "Cancel",
            null,
            notebookOptions.ToArray());

        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel")
            return;

        if (choice == "+ New Notebook")
        {
            await CreateNotebookAsync();
            if (_selectedNotebook != null)
            {
                await _databaseService.AddNoteToNotebookAsync(note.Id, _selectedNotebook.Id);
                if (_currentCollectionKind == VaultCollectionKind.Notebook)
                    await ReloadCurrentCollectionAsync();
            }

            return;
        }

        var notebook = Notebooks.FirstOrDefault(item =>
            string.Equals(item.Name, choice, StringComparison.OrdinalIgnoreCase));

        if (notebook == null)
            return;

        await _databaseService.AddNoteToNotebookAsync(note.Id, notebook.Id);
        await Shell.Current.DisplayAlert("Added", $"This note is now in {notebook.Name}.", "OK");

        if (_currentCollectionKind == VaultCollectionKind.Notebook &&
            _selectedNotebook != null &&
            _selectedNotebook.Id == notebook.Id)
        {
            await ReloadCurrentCollectionAsync();
        }
    }

    private async Task RemoveNoteFromCurrentNotebookAsync(Note note)
    {
        if (_selectedNotebook == null)
            return;

        await _databaseService.RemoveNoteFromNotebookAsync(note.Id, _selectedNotebook.Id);
        await ReloadCurrentCollectionAsync();
    }

    private void RaiseSelectionStateChanged()
    {
        OnPropertyChanged(nameof(IsAllNotesSelected));
        OnPropertyChanged(nameof(IsMyNotesSelected));
        OnPropertyChanged(nameof(IsLikedNotesSelected));
        OnPropertyChanged(nameof(IsNotebookMode));
        OnPropertyChanged(nameof(SelectedNotebookName));
    }

    private enum VaultCollectionKind
    {
        AllNotes,
        MyNotes,
        LikedNotes,
        Notebook
    }
}
