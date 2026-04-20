using Microsoft.Maui.Storage;
using Plugin.Firebase.Firestore;
using StudySync.Models;

namespace StudySync.Services;

public sealed class SectionCloudService
{
    private const string CollectionName = "sections";
    private const string DeviceIdPreferenceKey = "studysync_device_id";

    private readonly IFirebaseFirestore _firestore;

    public SectionCloudService()
    {
        _firestore = CrossFirebaseFirestore.Current;
    }

    public async Task<List<Section>> GetJoinedSectionsAsync()
    {
        var snapshot = await _firestore
            .GetCollection(CollectionName)
            .WhereArrayContains("member_ids", GetDeviceId())
            .GetDocumentsAsync<SectionDocument>(Source.Default);

        return snapshot.Documents
            .Select(document => document.Data)
            .Where(document => document != null)
            .Select(ToSection)
            .OrderBy(section => section.Name)
            .ToList();
    }

    public async Task<Section> CreateSectionAsync(string name, string inviteCode)
    {
        var deviceId = GetDeviceId();
        var now = DateTimeOffset.UtcNow;

        var document = new SectionDocument
        {
            Name = name,
            InviteCode = inviteCode,
            CreatedAt = now,
            CreatorDeviceId = deviceId,
            MemberIds = new List<string> { deviceId }
        };

        var reference = _firestore.GetCollection(CollectionName).CreateDocument();
        await reference.SetDataAsync(document);

        document.Id = reference.Id;
        return ToSection(document);
    }

    public async Task<Section?> JoinSectionByCodeAsync(string inviteCode)
    {
        var normalizedCode = inviteCode.Trim().ToUpperInvariant();

        var snapshot = await _firestore
            .GetCollection(CollectionName)
            .WhereEqualsTo("invite_code", normalizedCode)
            .LimitedTo(1)
            .GetDocumentsAsync<SectionDocument>(Source.Default);

        var document = snapshot.Documents
            .Select(result => result.Data)
            .FirstOrDefault(result => result != null);

        if (document == null)
            return null;

        var deviceId = GetDeviceId();
        if (!document.MemberIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase))
        {
            var reference = _firestore.GetCollection(CollectionName).GetDocument(document.Id);
            await reference.UpdateDataAsync(("member_ids", FieldValue.ArrayUnion(deviceId)));
            document.MemberIds.Add(deviceId);
        }

        return ToSection(document);
    }

    public async Task<bool> InviteCodeExistsAsync(string inviteCode)
    {
        var normalizedCode = inviteCode.Trim().ToUpperInvariant();

        var snapshot = await _firestore
            .GetCollection(CollectionName)
            .WhereEqualsTo("invite_code", normalizedCode)
            .LimitedTo(1)
            .GetDocumentsAsync<SectionDocument>(Source.Default);

        return !snapshot.IsEmpty;
    }

    private static Section ToSection(SectionDocument document)
    {
        var deviceId = GetDeviceId();
        return new Section
        {
            Name = document.Name,
            InviteCode = document.InviteCode,
            CreatedAt = document.CreatedAt.LocalDateTime,
            IsCreator = string.Equals(document.CreatorDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
        };
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

    private sealed class SectionDocument : IFirestoreObject
    {
        [FirestoreDocumentId]
        public string Id { get; set; } = string.Empty;

        [FirestoreProperty("name")]
        public string Name { get; set; } = string.Empty;

        [FirestoreProperty("invite_code")]
        public string InviteCode { get; set; } = string.Empty;

        [FirestoreProperty("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [FirestoreProperty("creator_device_id")]
        public string CreatorDeviceId { get; set; } = string.Empty;

        [FirestoreProperty("member_ids")]
        public IList<string> MemberIds { get; set; } = new List<string>();
    }
}
