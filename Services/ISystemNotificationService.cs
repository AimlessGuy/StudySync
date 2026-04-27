namespace StudySync.Services;

public interface ISystemNotificationService
{
    Task ShowPeerAlertAsync(string title, string message);
}
