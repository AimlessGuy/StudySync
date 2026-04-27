using Microsoft.Maui.Storage;
using Plugin.Firebase.Firestore;
using StudySync.Models;

namespace StudySync.Services;

public sealed class PostCloudService
{
    private const string CollectionName = "posts";
    private const string DeviceIdPreferenceKey = "studysync_device_id";

    private readonly IFirebaseFirestore _firestore;
    private readonly AuthService _authService;

    public PostCloudService()
    {
        _firestore = CrossFirebaseFirestore.Current;
        _authService = new AuthService();
    }

    public async Task CreatePostAsync(Note note, IReadOnlyList<Section> sections)
    {
        if (sections.Count == 0)
            throw new InvalidOperationException("Choose at least one section.");

        var session = await _authService.GetCurrentSessionAsync();

        var document = new PostDocument
        {
            Title = note.Title,
            Text = note.ExtractedText ?? string.Empty,
            PrimarySubjectTag = note.PrimarySubjectTag,
            SecondaryTags = note.SecondaryTags,
            CreatedAt = DateTimeOffset.UtcNow,
            AuthorDeviceId = GetDeviceId(),
            AuthorUserId = session?.LocalId ?? string.Empty,
            AuthorName = note.IsAnonymous ? string.Empty : (session?.DisplayName ?? string.Empty),
            IsAnonymous = note.IsAnonymous,
            Upvotes = note.Upvotes,
            UpvoterDeviceIds = new List<string>(),
            UpvoterUserIds = new List<string>(),
            SectionInviteCodes = sections
                .Select(section => section.InviteCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SectionNames = sections
                .Select(section => section.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        await _firestore
            .GetCollection(CollectionName)
            .CreateDocument()
            .SetDataAsync(document);
    }

    public async Task<List<SectionPost>> GetPostsForSectionsAsync(IReadOnlyList<Section> sections)
    {
        if (sections.Count == 0)
            return new List<SectionPost>();

        var session = await _authService.GetCurrentSessionAsync();
        var userId = session?.LocalId ?? string.Empty;
        var deviceId = GetDeviceId();
        var inviteCodes = sections
            .Select(section => section.InviteCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var snapshot = await _firestore
            .GetCollection(CollectionName)
            .GetDocumentsAsync<PostDocument>(Source.Default);

        return snapshot.Documents
            .Select(document => document.Data)
            .Where(document => document != null)
            .Where(document => document.SectionInviteCodes.Any(inviteCodes.Contains))
            .Select(document => ToSectionPost(document, userId, deviceId))
            .OrderByDescending(post => post.CreatedAt)
            .ToList();
    }

    public async Task<SectionPost?> GetPostByIdAsync(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
            return null;

        var session = await _authService.GetCurrentSessionAsync();
        var userId = session?.LocalId ?? string.Empty;
        var deviceId = GetDeviceId();
        var snapshot = await _firestore
            .GetCollection(CollectionName)
            .GetDocumentsAsync<PostDocument>(Source.Default);

        var document = snapshot.Documents
            .Select(result => result.Data)
            .FirstOrDefault(result => result != null && string.Equals(result.Id, postId, StringComparison.Ordinal));

        return document == null ? null : ToSectionPost(document, userId, deviceId);
    }

    public async Task<bool> UpvotePostAsync(string postId)
    {
        if (string.IsNullOrWhiteSpace(postId))
            return false;

        var snapshot = await _firestore
            .GetCollection(CollectionName)
            .GetDocumentsAsync<PostDocument>(Source.Default);

        var document = snapshot.Documents
            .Select(result => result.Data)
            .FirstOrDefault(result => result != null && string.Equals(result.Id, postId, StringComparison.Ordinal));

        if (document == null)
            return false;

        var session = await _authService.GetCurrentSessionAsync();
        var userId = session?.LocalId ?? string.Empty;
        var deviceId = GetDeviceId();
        var reference = _firestore.GetCollection(CollectionName).GetDocument(document.Id);
        bool hasUpvotedByUser =
            !string.IsNullOrWhiteSpace(userId) &&
            document.UpvoterUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase);
        bool hasUpvotedByDevice = document.UpvoterDeviceIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase);
        bool hasUpvoted = hasUpvotedByUser || hasUpvotedByDevice;

        if (hasUpvoted)
        {
            if (hasUpvotedByDevice)
                await reference.UpdateDataAsync(("upvoter_device_ids", FieldValue.ArrayRemove(deviceId)));

            if (hasUpvotedByUser)
                await reference.UpdateDataAsync(("upvoter_user_ids", FieldValue.ArrayRemove(userId)));

            await reference.UpdateDataAsync(("upvotes", Math.Max(0, document.Upvotes - 1)));
            return false;
        }

        await reference.UpdateDataAsync(("upvoter_device_ids", FieldValue.ArrayUnion(deviceId)));
        if (!string.IsNullOrWhiteSpace(userId))
            await reference.UpdateDataAsync(("upvoter_user_ids", FieldValue.ArrayUnion(userId)));
        await reference.UpdateDataAsync(("upvotes", document.Upvotes + 1));
        return true;
    }

    private static string GetDeviceId()
    {
        var deviceId = Preferences.Default.Get(DeviceIdPreferenceKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(deviceId))
            return deviceId;

        deviceId = Guid.NewGuid().ToString("N");
        Preferences.Default.Set(DeviceIdPreferenceKey, deviceId);
        return deviceId;
    }

    private static SectionPost ToSectionPost(PostDocument document, string userId, string deviceId)
    {
        bool hasUpvoted =
            (!string.IsNullOrWhiteSpace(userId) &&
             document.UpvoterUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase)) ||
            document.UpvoterDeviceIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase);

        return new SectionPost
        {
            Id = document.Id,
            Title = document.Title,
            Text = document.Text,
            PrimarySubjectTag = document.PrimarySubjectTag,
            SecondaryTags = document.SecondaryTags,
            CreatedAt = document.CreatedAt.LocalDateTime,
            AuthorDeviceId = document.AuthorDeviceId,
            AuthorUserId = document.AuthorUserId,
            AuthorName = document.AuthorName,
            IsAnonymous = document.IsAnonymous,
            Upvotes = ConvertToInt(document.Upvotes),
            HasUpvoted = hasUpvoted,
            SectionInviteCodes = document.SectionInviteCodes.ToList(),
            SectionNames = document.SectionNames.ToList()
        };
    }

    private static int ConvertToInt(long value) =>
        value > int.MaxValue ? int.MaxValue :
        value < int.MinValue ? int.MinValue :
        (int)value;

    private sealed class PostDocument : IFirestoreObject
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("title")]
        public string Title { get; set; } = string.Empty;

        [FirestoreProperty("text")]
        public string Text { get; set; } = string.Empty;

        [FirestoreProperty("primary_subject_tag")]
        public string PrimarySubjectTag { get; set; } = string.Empty;

        [FirestoreProperty("secondary_tags")]
        public string SecondaryTags { get; set; } = string.Empty;

        [FirestoreProperty("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [FirestoreProperty("author_device_id")]
        public string AuthorDeviceId { get; set; } = string.Empty;

        [FirestoreProperty("author_user_id")]
        public string AuthorUserId { get; set; } = string.Empty;

        [FirestoreProperty("author_name")]
        public string AuthorName { get; set; } = string.Empty;

        [FirestoreProperty("is_anonymous")]
        public bool IsAnonymous { get; set; }

        [FirestoreProperty("upvotes")]
        public long Upvotes { get; set; }

        [FirestoreProperty("upvoter_device_ids")]
        public IList<string> UpvoterDeviceIds { get; set; } = new List<string>();

        [FirestoreProperty("upvoter_user_ids")]
        public IList<string> UpvoterUserIds { get; set; } = new List<string>();

        [FirestoreProperty("section_invite_codes")]
        public IList<string> SectionInviteCodes { get; set; } = new List<string>();

        [FirestoreProperty("section_names")]
        public IList<string> SectionNames { get; set; } = new List<string>();
    }
}
