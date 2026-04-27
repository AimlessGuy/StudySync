using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudySync.Models;
using StudySync.Services;
using System.Collections.ObjectModel;

namespace StudySync.ViewModels;

public partial class NotificationsViewModel : ObservableObject
{
    private readonly NotificationService _notificationService;

    public ObservableCollection<AppNotification> Notifications { get; } = new();

    [ObservableProperty]
    private int unreadCount;

    public NotificationsViewModel(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [RelayCommand]
    public async Task LoadNotificationsAsync()
    {
        var notifications = await _notificationService.GetNotificationsAsync();

        Notifications.Clear();
        foreach (var notification in notifications)
            Notifications.Add(notification);

        UnreadCount = Notifications.Count(notification => !notification.IsRead);
    }

    [RelayCommand]
    private async Task OpenNotificationAsync(AppNotification? notification)
    {
        if (notification == null)
            return;

        await _notificationService.MarkAsReadAsync(notification);
        await LoadNotificationsAsync();
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        await _notificationService.MarkAllAsReadAsync();
        await LoadNotificationsAsync();
    }
}
