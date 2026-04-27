using SQLite;

namespace StudySync.Models;

public class NotebookNote
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int NotebookId { get; set; }

    [Indexed]
    public int NoteId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
