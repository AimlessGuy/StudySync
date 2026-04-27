using SQLite;

namespace StudySync.Models;

public class AppNotification
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string DedupKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string TimeDisplay =>
        UpdatedAt.Date == DateTime.Now.Date
            ? UpdatedAt.ToString("h:mm tt")
            : UpdatedAt.ToString("MMM dd");
}
