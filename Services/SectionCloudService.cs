using Microsoft.Maui.Storage;
using Plugin.Firebase.Firestore;
using StudySync.Models;

namespace StudySync.Services;

public sealed class SectionCloudService
{
    private const string CollectionName = "sections";
    private const string DeviceIdPreferenceKey = "studysync_device_id";

    private readonly IFirebaseFirestore _firestore;
    private readonly AuthService _authService;

    public SectionCloudService()
    {
        _firestore = CrossFirebaseFirestore.Current;
        _authService = new AuthService();
    }

    public async Task<List<Section>> GetJoinedSectionsAsync()
    {
        var session = await _authService.GetCurrentSessionAsync();
        var userId = session?.LocalId ?? string.Empty;
        var deviceId = GetDeviceId();

        var snapshot = await _firestore
            .GetCollection(CollectionName)
            .GetDocumentsAsync<SectionDocument>(Source.Default);

        var documents = snapshot.Documents
            .Select(document => document.Data)
            .Where(document => document != null)
            .Where(document =>
                (!string.IsNullOrWhiteSpace(userId) &&
                 document.MemberUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase)) ||
                document.MemberIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            foreach (var document in documents.Where(document =>
                         !document.MemberUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase) &&
                         document.MemberIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase)))
            {
                var reference = _firestore.GetCollection(CollectionName).GetDocument(document.Id);
                await reference.UpdateDataAsync(("member_user_ids", FieldValue.ArrayUnion(userId)));
                document.MemberUserIds.Add(userId);
            }
        }

        return documents
            .Select(document => ToSection(document, userId, deviceId))
            .OrderBy(section => section.Name)
            .ToList();
    }

    public async Task<Section> CreateSectionAsync(string name, string inviteCode)
    {
        var deviceId = GetDeviceId();
        var session = await _authService.GetCurrentSessionAsync();
        var userId = session?.LocalId ?? string.Empty;
        var now = DateTimeOffset.UtcNow;

        var document = new SectionDocument
        {
            Name = name,
            InviteCode = inviteCode,
            CreatedAt = now,
            CreatorDeviceId = deviceId,
            CreatorUserId = userId,
            MemberIds = new List<string> { deviceId },
            MemberUserIds = string.IsNullOrWhiteSpace(userId)
                ? new List<string>()
                : new List<string> { userId }
        };

        var reference = _firestore.GetCollection(CollectionName).CreateDocument();
        await reference.SetDataAsync(document);

        document.Id = reference.Id;
        return ToSection(document, userId, deviceId);
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
        var session = await _authService.GetCurrentSessionAsync();
        var userId = session?.LocalId ?? string.Empty;
        var reference = _firestore.GetCollection(CollectionName).GetDocument(document.Id);

        if (!document.MemberIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase))
        {
            await reference.UpdateDataAsync(("member_ids", FieldValue.ArrayUnion(deviceId)));
            document.MemberIds.Add(deviceId);
        }

        if (!string.IsNullOrWhiteSpace(userId) &&
            !document.MemberUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase))
        {
            await reference.UpdateDataAsync(("member_user_ids", FieldValue.ArrayUnion(userId)));
            document.MemberUserIds.Add(userId);
        }

        return ToSection(document, userId, deviceId);
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

    public async Task LeaveSectionAsync(string inviteCode)
    {
        if (string.IsNullOrWhiteSpace(inviteCode))
            throw new InvalidOperationException("A section code is required.");

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
            return;

        var deviceId = GetDeviceId();
        var session = await _authService.GetCurrentSessionAsync();
        var userId = session?.LocalId ?? string.Empty;
        var reference = _firestore.GetCollection(CollectionName).GetDocument(document.Id);

        if (document.MemberIds.Contains(deviceId, StringComparer.OrdinalIgnoreCase))
            await reference.UpdateDataAsync(("member_ids", FieldValue.ArrayRemove(deviceId)));

        if (!string.IsNullOrWhiteSpace(userId) &&
            document.MemberUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase))
        {
            await reference.UpdateDataAsync(("member_user_ids", FieldValue.ArrayRemove(userId)));
        }
    }

    private static Section ToSection(SectionDocument document, string userId, string deviceId)
    {
        return new Section
        {
            Name = document.Name,
            InviteCode = document.InviteCode,
            CreatedAt = document.CreatedAt.LocalDateTime,
            IsCreator =
                (!string.IsNullOrWhiteSpace(userId) &&
                 string.Equals(document.CreatorUserId, userId, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(document.CreatorDeviceId, deviceId, StringComparison.OrdinalIgnoreCase)
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

        [FirestoreProperty("creator_user_id")]
        public string CreatorUserId { get; set; } = string.Empty;

        [FirestoreProperty("member_ids")]
        public IList<string> MemberIds { get; set; } = new List<string>();

        [FirestoreProperty("member_user_ids")]
        public IList<string> MemberUserIds { get; set; } = new List<string>();
    }
}
