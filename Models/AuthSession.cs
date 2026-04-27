namespace StudySync.Models;

public class AuthSession
{
    public string LocalId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string IdToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Email) || !Email.Contains('@')
            ? "Student"
            : Email.Split('@')[0];

    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
}
