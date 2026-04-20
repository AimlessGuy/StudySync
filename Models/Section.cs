using SQLite;
using System;

namespace StudySync.Models;

public class Section
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Unique]
    public string InviteCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsCreator { get; set; }
}
