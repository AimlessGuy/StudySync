using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;
using StudySync.Models;

namespace StudySync.Services;

public class AuthService
{
    private const string FirebaseApiKey = "AIzaSyCcWnKXe_szSyxkYoegd5PWtoZSENBfJe8";
    private const string SessionPreferenceKey = "studysync_auth_session";

    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool HasSavedSession =>
        !string.IsNullOrWhiteSpace(Preferences.Default.Get(SessionPreferenceKey, string.Empty));

    public async Task<AuthSession?> GetCurrentSessionAsync()
    {
        var session = LoadSession();
        if (session == null)
            return null;

        if (!session.IsExpired)
            return session;

        try
        {
            return await RefreshSessionAsync(session);
        }
        catch
        {
            await SignOutAsync();
            return null;
        }
    }

    public async Task<AuthSession> SignInAsync(string email, string password)
    {
        return await AuthenticateAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={FirebaseApiKey}",
            email,
            password);
    }

    public async Task<AuthSession> SignUpAsync(string email, string password)
    {
        return await AuthenticateAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={FirebaseApiKey}",
            email,
            password);
    }

    public Task SignOutAsync()
    {
        Preferences.Default.Remove(SessionPreferenceKey);
        return Task.CompletedTask;
    }

    private async Task<AuthSession> AuthenticateAsync(string endpoint, string email, string password)
    {
        var response = await HttpClient.PostAsJsonAsync(endpoint, new
        {
            email,
            password,
            returnSecureToken = true
        });

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildFriendlyErrorAsync(response));

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Firebase returned an empty auth response.");

        var session = new AuthSession
        {
            LocalId = payload.LocalId ?? string.Empty,
            Email = payload.Email ?? email,
            IdToken = payload.IdToken ?? string.Empty,
            RefreshToken = payload.RefreshToken ?? string.Empty,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(ParseExpirySeconds(payload.ExpiresIn))
        };

        SaveSession(session);
        return session;
    }

    private async Task<AuthSession> RefreshSessionAsync(AuthSession session)
    {
        var response = await HttpClient.PostAsync(
            $"https://securetoken.googleapis.com/v1/token?key={FirebaseApiKey}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = session.RefreshToken
            }));

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await BuildFriendlyErrorAsync(response));

        var payload = await response.Content.ReadFromJsonAsync<RefreshResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Firebase returned an empty refresh response.");

        var refreshed = new AuthSession
        {
            LocalId = payload.UserId ?? session.LocalId,
            Email = payload.UserEmail ?? session.Email,
            IdToken = payload.IdToken ?? string.Empty,
            RefreshToken = payload.RefreshToken ?? session.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(ParseExpirySeconds(payload.ExpiresIn))
        };

        SaveSession(refreshed);
        return refreshed;
    }

    private static int ParseExpirySeconds(string? value) =>
        int.TryParse(value, out int seconds) ? seconds : 3600;

    private AuthSession? LoadSession()
    {
        var raw = Preferences.Default.Get(SessionPreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<AuthSession>(raw, JsonOptions);
        }
        catch
        {
            Preferences.Default.Remove(SessionPreferenceKey);
            return null;
        }
    }

    private void SaveSession(AuthSession session)
    {
        var raw = JsonSerializer.Serialize(session, JsonOptions);
        Preferences.Default.Set(SessionPreferenceKey, raw);
    }

    private static async Task<string> BuildFriendlyErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<FirebaseErrorEnvelope>(JsonOptions);
            var code = payload?.Error?.Message ?? response.ReasonPhrase ?? "UNKNOWN_ERROR";

            return code switch
            {
                "EMAIL_EXISTS" => "That email is already registered.",
                "EMAIL_NOT_FOUND" => "No account was found for that email.",
                "INVALID_PASSWORD" => "The password is incorrect.",
                "INVALID_LOGIN_CREDENTIALS" => "The email or password is incorrect.",
                "WEAK_PASSWORD : Password should be at least 6 characters" => "Password must be at least 6 characters.",
                "USER_DISABLED" => "This account has been disabled.",
                _ => $"Authentication failed: {code.Replace('_', ' ').ToLowerInvariant()}."
            };
        }
        catch
        {
            return "Authentication failed. Please try again.";
        }
    }

    private sealed class AuthResponse
    {
        public string? LocalId { get; set; }
        public string? Email { get; set; }
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? ExpiresIn { get; set; }
    }

    private sealed class RefreshResponse
    {
        public string? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? IdToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? ExpiresIn { get; set; }
    }

    private sealed class FirebaseErrorEnvelope
    {
        public FirebaseError? Error { get; set; }
    }

    private sealed class FirebaseError
    {
        public string? Message { get; set; }
    }
}
