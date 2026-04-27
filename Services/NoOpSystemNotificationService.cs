namespace StudySync.Services;

public class NoOpSystemNotificationService : ISystemNotificationService
{
    public Task ShowPeerAlertAsync(string title, string message) => Task.CompletedTask;
}
