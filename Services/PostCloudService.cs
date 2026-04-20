using Microsoft.Maui.Storage;
using Plugin.Firebase.Firestore;
using StudySync.Models;

namespace StudySync.Services;

public sealed class PostCloudService
{
    private const string CollectionName = "posts";
    private const string DeviceIdPreferenceKey = "studysync_device_id";

    private readonly IFirebaseFirestore _firestore;

    public PostCloudService()
    {
        _firestore = CrossFirebaseFirestore.Current;
    }

    public async Task CreatePostAsync(Note note, IReadOnlyList<Section> sections)
    {
        if (sections.Count == 0)
            throw new InvalidOperationException("Choose at least one section.");

        var document = new PostDocument
        {
            Title = note.Title,
            Text = note.ExtractedText ?? string.Empty,
            PrimarySubjectTag = note.PrimarySubjectTag,
            SecondaryTags = note.SecondaryTags,
            CreatedAt = DateTimeOffset.UtcNow,
            AuthorDeviceId = GetDeviceId(),
            IsAnonymous = note.IsAnonymous,
            Upvotes = note.Upvotes,
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
            .Select(ToSectionPost)
            .OrderByDescending(post => post.CreatedAt)
            .ToList();
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

    private static SectionPost ToSectionPost(PostDocument document)
    {
        return new SectionPost
        {
            Id = document.Id,
            Title = document.Title,
            Text = document.Text,
            PrimarySubjectTag = document.PrimarySubjectTag,
            SecondaryTags = document.SecondaryTags,
            CreatedAt = document.CreatedAt.LocalDateTime,
            AuthorDeviceId = document.AuthorDeviceId,
            IsAnonymous = document.IsAnonymous,
            Upvotes = document.Upvotes,
            SectionInviteCodes = document.SectionInviteCodes.ToList(),
            SectionNames = document.SectionNames.ToList()
        };
    }

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

        [FirestoreProperty("is_anonymous")]
        public bool IsAnonymous { get; set; }

        [FirestoreProperty("upvotes")]
        public int Upvotes { get; set; }

        [FirestoreProperty("section_invite_codes")]
        public IList<string> SectionInviteCodes { get; set; } = new List<string>();

        [FirestoreProperty("section_names")]
        public IList<string> SectionNames { get; set; } = new List<string>();
    }
}
