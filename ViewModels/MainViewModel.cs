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
    private readonly AuthService _authService;
    private readonly NotificationService _notificationService;

    [ObservableProperty]
    private ObservableCollection<Note> recentNotes = new();

    [ObservableProperty]
    private int noteCount;

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private string accountLabel = "Account";

    [ObservableProperty]
    private int unreadNotificationCount;

    public string NotificationsLabel =>
        UnreadNotificationCount > 0 ? $"Notifications ({UnreadNotificationCount})" : "Notifications";

    public MainViewModel(DatabaseService databaseService, AuthService authService, NotificationService notificationService)
    {
        _databaseService = databaseService;
        _authService = authService;
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task LoadNotesAsync()
    {
        var session = await _authService.GetCurrentSessionAsync();
        AccountLabel = session?.DisplayName ?? "Account";
        UnreadNotificationCount = await _notificationService.GetUnreadCountAsync();
        OnPropertyChanged(nameof(NotificationsLabel));

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
        await Shell.Current.GoToAsync(nameof(Views.FeedPage));
    }

    [RelayCommand]
    private async Task GoToSections()
    {
        await Shell.Current.GoToAsync(nameof(Views.SectionsPage));
    }

    [RelayCommand]
    private async Task GoToAccount()
    {
        await Shell.Current.GoToAsync(nameof(Views.AccountPage));
    }

    [RelayCommand]
    private async Task GoToNotifications()
    {
        await Shell.Current.GoToAsync(nameof(Views.NotificationsPage));
    }

    [RelayCommand]
    private async Task SelectNote(Note note)
    {
        // Navigate to note detail page with the note ID
        await Shell.Current.GoToAsync($"{nameof(NoteDetailPage)}?noteId={note.Id}");
    }

}

