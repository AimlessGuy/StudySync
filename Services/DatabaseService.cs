using SQLite;
using StudySync.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace StudySync.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;
        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "studysync.db3");

        public DatabaseService()
        {
            _database = new SQLiteAsyncConnection(DbPath);
            InitializeDatabaseAsync().GetAwaiter().GetResult();
        }

        private async Task InitializeDatabaseAsync()
        {
            await _database.CreateTableAsync<Note>().ConfigureAwait(false);
            await _database.CreateTableAsync<Section>().ConfigureAwait(false);
            await _database.CreateTableAsync<AppNotification>().ConfigureAwait(false);
            await _database.CreateTableAsync<Notebook>().ConfigureAwait(false);
            await _database.CreateTableAsync<NotebookNote>().ConfigureAwait(false);
            await EnsureColumnExistsAsync("Note", "PrimarySubjectTag", "TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
            await EnsureColumnExistsAsync("Note", "SecondaryTags", "TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
            await EnsureColumnExistsAsync("Note", "SourcePostId", "TEXT NOT NULL DEFAULT ''").ConfigureAwait(false);
            await EnsureColumnExistsAsync("Note", "IsLiked", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
            await EnsureColumnExistsAsync("Note", "IsVaultSaved", "INTEGER NOT NULL DEFAULT 1").ConfigureAwait(false);
        }

        private async Task EnsureColumnExistsAsync(string tableName, string columnName, string definition)
        {
            var columns = await _database.QueryAsync<TableColumnInfo>($"PRAGMA table_info({tableName})").ConfigureAwait(false);
            if (columns.Any(column => column.name == columnName))
                return;

            await _database.ExecuteAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition}").ConfigureAwait(false);
        }

        private sealed class TableColumnInfo
        {
            public int cid { get; set; }
            public string name { get; set; } = string.Empty;
        }

        public Task<List<Note>> GetNotesAsync() =>
            _database.Table<Note>().OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<List<Note>> GetVaultNotesAsync() =>
            _database.Table<Note>()
                .Where(n => n.IsVaultSaved || n.IsLiked)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();

        public Task<List<Note>> GetMyPostedNotesAsync() =>
            _database.Table<Note>()
                .Where(n => n.IsShared)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();

        public Task<List<Note>> GetLikedNotesAsync() =>
            _database.Table<Note>()
                .Where(n => n.IsLiked)
                .OrderByDescending(n => n.UpdatedAt)
                .ToListAsync();

        public Task<List<Note>> GetPrivateNotesAsync() =>
            _database.Table<Note>().Where(n => !n.IsShared).OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<List<Note>> GetSharedNotesAsync() =>
            _database.Table<Note>().Where(n => n.IsShared).OrderByDescending(n => n.CreatedAt).ToListAsync();

        public Task<int> SaveNoteAsync(Note note) =>
            note.Id == 0 ? _database.InsertAsync(note) : _database.UpdateAsync(note);

        public async Task<Note?> GetNoteBySourcePostIdAsync(string sourcePostId)
        {
            if (string.IsNullOrWhiteSpace(sourcePostId))
                return null;

            return await _database.Table<Note>()
                .Where(note => note.SourcePostId == sourcePostId)
                .FirstOrDefaultAsync();
        }

        public async Task<int> SaveSharedPostToVaultAsync(SectionPost post)
        {
            var existing = await GetNoteBySourcePostIdAsync(post.Id);
            if (existing != null)
            {
                existing.Title = string.IsNullOrWhiteSpace(post.Title) ? post.PrimarySubjectTag : post.Title;
                existing.ExtractedText = post.Text;
                existing.UpdatedAt = DateTime.Now;
                existing.IsVaultSaved = true;
                existing.CourseCode = post.PrimarySubjectTag;
                existing.ContentType = "Notes";
                existing.PrimarySubjectTag = post.PrimarySubjectTag;
                existing.SecondaryTags = post.SecondaryTags;
                return await _database.UpdateAsync(existing);
            }

            var note = BuildLocalNoteFromPost(post);
            note.IsVaultSaved = true;
            return await _database.InsertAsync(note);
        }

        public async Task SyncLikedPostAsync(SectionPost post, bool isLiked)
        {
            var existing = await GetNoteBySourcePostIdAsync(post.Id);

            if (existing == null)
            {
                if (!isLiked)
                    return;

                var likedNote = BuildLocalNoteFromPost(post);
                likedNote.IsVaultSaved = false;
                likedNote.IsLiked = true;
                await _database.InsertAsync(likedNote);
                return;
            }

            existing.Title = string.IsNullOrWhiteSpace(post.Title) ? post.PrimarySubjectTag : post.Title;
            existing.ExtractedText = post.Text;
            existing.CourseCode = post.PrimarySubjectTag;
            existing.ContentType = "Notes";
            existing.PrimarySubjectTag = post.PrimarySubjectTag;
            existing.SecondaryTags = post.SecondaryTags;
            existing.UpdatedAt = DateTime.Now;
            existing.IsLiked = isLiked;

            if (!isLiked && !existing.IsVaultSaved && !existing.IsShared)
            {
                await RemoveNoteFromAllNotebooksAsync(existing.Id);
                await _database.DeleteAsync(existing);
                return;
            }

            await _database.UpdateAsync(existing);
        }

        public Task<List<Section>> GetSectionsAsync() =>
            _database.Table<Section>().OrderBy(s => s.Name).ToListAsync();

        public Task<List<AppNotification>> GetNotificationsAsync() =>
            _database.Table<AppNotification>()
                .OrderByDescending(notification => notification.UpdatedAt)
                .ToListAsync();

        public Task<int> GetUnreadNotificationCountAsync() =>
            _database.Table<AppNotification>()
                .Where(notification => !notification.IsRead)
                .CountAsync();

        public async Task<bool> SaveNotificationAsync(AppNotification notification)
        {
            var existing = await _database.Table<AppNotification>()
                .Where(item => item.DedupKey == notification.DedupKey)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                bool hasChanged =
                    !string.Equals(existing.Title, notification.Title, StringComparison.Ordinal) ||
                    !string.Equals(existing.Message, notification.Message, StringComparison.Ordinal);

                existing.Title = notification.Title;
                existing.Message = notification.Message;
                existing.Type = notification.Type;
                existing.UpdatedAt = notification.UpdatedAt == default ? DateTime.Now : notification.UpdatedAt;
                if (hasChanged)
                    existing.IsRead = false;

                await _database.UpdateAsync(existing);
                return hasChanged;
            }

            notification.CreatedAt = notification.CreatedAt == default ? DateTime.Now : notification.CreatedAt;
            notification.UpdatedAt = notification.UpdatedAt == default ? notification.CreatedAt : notification.UpdatedAt;
            notification.IsRead = false;
            await _database.InsertAsync(notification);
            return true;
        }

        public async Task MarkNotificationReadAsync(AppNotification notification)
        {
            if (notification == null || notification.Id == 0 || notification.IsRead)
                return;

            notification.IsRead = true;
            await _database.UpdateAsync(notification);
        }

        public Task MarkAllNotificationsReadAsync() =>
            _database.ExecuteAsync("UPDATE AppNotification SET IsRead = 1 WHERE IsRead = 0");

        public Task<List<Notebook>> GetNotebooksAsync() =>
            _database.Table<Notebook>().OrderBy(notebook => notebook.Name).ToListAsync();

        public async Task<int> SaveNotebookAsync(Notebook notebook)
        {
            var existing = await _database.Table<Notebook>()
                .Where(item => item.Name == notebook.Name)
                .FirstOrDefaultAsync();

            if (existing != null)
            {
                notebook.Id = existing.Id;
                return await _database.UpdateAsync(notebook);
            }

            return await _database.InsertAsync(notebook);
        }

        public async Task<List<Note>> GetNotesForNotebookAsync(int notebookId)
        {
            var notebookNotes = await _database.Table<NotebookNote>()
                .Where(item => item.NotebookId == notebookId)
                .ToListAsync();

            if (notebookNotes.Count == 0)
                return new List<Note>();

            var ids = notebookNotes.Select(item => item.NoteId).ToHashSet();
            var notes = await _database.Table<Note>().ToListAsync();

            return notes
                .Where(note => ids.Contains(note.Id))
                .OrderByDescending(note => note.UpdatedAt)
                .ToList();
        }

        public async Task AddNoteToNotebookAsync(int noteId, int notebookId)
        {
            var existing = await _database.Table<NotebookNote>()
                .Where(item => item.NoteId == noteId && item.NotebookId == notebookId)
                .FirstOrDefaultAsync();

            if (existing != null)
                return;

            await _database.InsertAsync(new NotebookNote
            {
                NoteId = noteId,
                NotebookId = notebookId,
                CreatedAt = DateTime.Now
            });
        }

        public async Task RemoveNoteFromNotebookAsync(int noteId, int notebookId)
        {
            var existing = await _database.Table<NotebookNote>()
                .Where(item => item.NoteId == noteId && item.NotebookId == notebookId)
                .FirstOrDefaultAsync();

            if (existing != null)
                await _database.DeleteAsync(existing);
        }

        public async Task<List<Notebook>> GetNotebooksForNoteAsync(int noteId)
        {
            var links = await _database.Table<NotebookNote>()
                .Where(item => item.NoteId == noteId)
                .ToListAsync();

            if (links.Count == 0)
                return new List<Notebook>();

            var notebookIds = links.Select(item => item.NotebookId).ToHashSet();
            var notebooks = await _database.Table<Notebook>().ToListAsync();

            return notebooks
                .Where(notebook => notebookIds.Contains(notebook.Id))
                .OrderBy(notebook => notebook.Name)
                .ToList();
        }

        public async Task<Section?> GetSectionByInviteCodeAsync(string inviteCode)
        {
            var matches = await _database.Table<Section>()
                .Where(section => section.InviteCode == inviteCode)
                .ToListAsync();

            return matches.FirstOrDefault();
        }

        public async Task<int> SaveSectionAsync(Section section)
        {
            var existing = await GetSectionByInviteCodeAsync(section.InviteCode);
            if (existing != null)
            {
                section.Id = existing.Id;
                return await _database.UpdateAsync(section);
            }

            return await _database.InsertAsync(section);
        }

        public async Task<int> DeleteSectionAsync(Section section)
        {
            var existing = await GetSectionByInviteCodeAsync(section.InviteCode);
            if (existing == null)
                return 0;

            return await _database.DeleteAsync(existing);
        }

        public Task<int> UpvoteNoteAsync(Note note)
        {
            note.Upvotes++;
            return _database.UpdateAsync(note);
        }

        public Task<int> DeleteNoteAsync(Note note) => _database.DeleteAsync(note);
        public Task<int> GetNotesCountAsync() => _database.Table<Note>().CountAsync();

        private async Task RemoveNoteFromAllNotebooksAsync(int noteId)
        {
            var links = await _database.Table<NotebookNote>()
                .Where(item => item.NoteId == noteId)
                .ToListAsync();

            foreach (var link in links)
                await _database.DeleteAsync(link);
        }

        private static Note BuildLocalNoteFromPost(SectionPost post)
        {
            return new Note
            {
                Title = string.IsNullOrWhiteSpace(post.Title) ? post.PrimarySubjectTag : post.Title,
                ExtractedText = post.Text,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                IsShared = false,
                IsAnonymous = false,
                IsLiked = false,
                IsVaultSaved = false,
                SourcePostId = post.Id,
                Upvotes = 0,
                CourseCode = post.PrimarySubjectTag,
                ContentType = "Notes",
                PrimarySubjectTag = post.PrimarySubjectTag,
                SecondaryTags = post.SecondaryTags
            };
        }
    }
}
