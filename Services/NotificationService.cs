using StudySync.Models;

namespace StudySync.Services;

public class NotificationService
{
    private readonly DatabaseService _databaseService;
    private readonly ISystemNotificationService _systemNotificationService;

    public NotificationService()
    {
        _databaseService = new DatabaseService();
        _systemNotificationService = ((App?)Application.Current)?.Services.GetService(typeof(ISystemNotificationService)) as ISystemNotificationService
                                     ?? new NoOpSystemNotificationService();
    }

    public NotificationService(DatabaseService databaseService, ISystemNotificationService systemNotificationService)
    {
        _databaseService = databaseService;
        _systemNotificationService = systemNotificationService;
    }

    public Task<List<AppNotification>> GetNotificationsAsync() =>
        _databaseService.GetNotificationsAsync();

    public Task<int> GetUnreadCountAsync() =>
        _databaseService.GetUnreadNotificationCountAsync();

    public Task MarkAllAsReadAsync() =>
        _databaseService.MarkAllNotificationsReadAsync();

    public Task MarkAsReadAsync(AppNotification notification) =>
        _databaseService.MarkNotificationReadAsync(notification);

    public async Task SyncPeerAlertAsync(IReadOnlyList<SectionPost> posts)
    {
        var todayPosts = posts
            .Where(post => post.CreatedAt.Date == DateTime.Now.Date)
            .ToList();

        if (todayPosts.Count == 0)
            return;

        int peerCount = todayPosts
            .Select(post => post.AuthorUserId)
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (peerCount == 0)
        {
            peerCount = todayPosts
                .Select(post => post.AuthorDeviceId)
                .Where(deviceId => !string.IsNullOrWhiteSpace(deviceId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        }

        string topSubject = todayPosts
            .Where(post => !string.IsNullOrWhiteSpace(post.PrimarySubjectTag))
            .GroupBy(post => post.PrimarySubjectTag)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "new";

        string message = peerCount <= 1
            ? $"A classmate shared {topSubject} notes today."
            : $"{peerCount} classmates shared {topSubject} notes today.";

        bool shouldNotify = await _databaseService.SaveNotificationAsync(new AppNotification
        {
            DedupKey = $"peer-alert-{DateTime.Now:yyyyMMdd}",
            Title = "Peer Alert",
            Message = message,
            Type = "peer-alert",
            UpdatedAt = DateTime.Now,
            CreatedAt = DateTime.Now
        });

        if (shouldNotify)
            await _systemNotificationService.ShowPeerAlertAsync("StudySync Peer Alert", message);
    }
}
